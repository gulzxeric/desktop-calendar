using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopTodo;

public static class LayoutRules
{
    public const double MinimumWidth = 560;
    public const double MinimumHeight = 420;
    public const double CompactWidth = 820;
    public const double MaximumWidth = 2400;
    public const double MaximumHeight = 1600;
}

public sealed class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string Notes { get; set; } = "";
    public bool Done { get; set; }
    public int Color { get; set; }
    public string Time { get; set; } = "";
    public bool Remind { get; set; }
    public bool Notified { get; set; }
}

public sealed class Preferences
{
    public double Left { get; set; } = -99999;
    public double Top { get; set; } = -99999;
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 700;
    public double Opacity { get; set; } = 0.92;
    public bool Light { get; set; }
    public int ThemeIndex { get; set; }
    public bool Locked { get; set; }
    public bool Desktop { get; set; } = true;
    public string View { get; set; } = "月历";
}

public sealed class CalendarData
{
    public int Version { get; set; } = 1;
    public List<TodoItem> Items { get; set; } = new();
    public Preferences Settings { get; set; } = new();
}

public sealed class Store
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "calendar.json");
    public CalendarData Data { get; private set; } = new();
    public string? RecoveryMessage { get; private set; }
    private readonly Stack<string> undo = new();
    public bool CanUndo => undo.Count > 0;

    public Store(string path)
    {
        DirectoryPath = path;
        Directory.CreateDirectory(path);
        if (!File.Exists(FilePath)) return;
        try { Data = Read(FilePath); }
        catch (Exception ex) when (ex is JsonException || ex is InvalidDataException)
        {
            string copy = Path.Combine(path, "calendar-damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.Copy(FilePath, copy, false);
            if (!File.Exists(FilePath + ".bak"))
                throw new InvalidDataException("日历数据无法读取，原文件已保留：" + copy, ex);
            Data = Read(FilePath + ".bak");
            RecoveryMessage = "已从上一次备份恢复。损坏的原文件已保留在数据文件夹。";
            // Preserve the known-good backup on the first subsequent save.
            File.Delete(FilePath);
        }
    }

    public static CalendarData Read(string path)
    {
        var data = JsonSerializer.Deserialize<CalendarData>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("备份内容为空。");
        if (data.Version != 1 || data.Items == null || data.Settings == null)
            throw new InvalidDataException("不是受支持的拾日备份文件。");
        if (data.Items.Any(i => i == null || string.IsNullOrWhiteSpace(i.Id) || string.IsNullOrWhiteSpace(i.Title)
            || i.Title.Length > 300 || i.Notes == null || i.Notes.Length > 10000
            || i.Date.Year < 1901 || i.Date.Year > 2100 || i.Color < 0 || i.Color > 4
            || i.Time == null || (i.Time != "" && !TimeSpan.TryParseExact(i.Time, @"hh\:mm", CultureInfo.InvariantCulture, out _)))
            || data.Items.Select(i => i.Id).Distinct().Count() != data.Items.Count)
            throw new InvalidDataException("备份中包含无效或重复的任务。");
        data.Settings.Width = Clamp(data.Settings.Width, LayoutRules.MinimumWidth, LayoutRules.MaximumWidth, 1100);
        data.Settings.Height = Clamp(data.Settings.Height, LayoutRules.MinimumHeight, LayoutRules.MaximumHeight, 700);
        data.Settings.Opacity = Clamp(data.Settings.Opacity, 0.45, 1, 0.92);
        // 旧备份只有“浅色主题”开关；若曾启用浅色且未显式选择新主题，迁移到对应浅色主题。
        data.Settings.ThemeIndex = Theme.ClampIndex(data.Settings.ThemeIndex);
        if (data.Settings.Light && data.Settings.ThemeIndex == 0)
            data.Settings.ThemeIndex = 1;
        data.Settings.Light = Theme.Themes[data.Settings.ThemeIndex].Light;
        if (!double.IsFinite(data.Settings.Left)) data.Settings.Left = -99999;
        if (!double.IsFinite(data.Settings.Top)) data.Settings.Top = -99999;
        if (!new[] { "月历", "周历", "清单" }.Contains(data.Settings.View)) data.Settings.View = "月历";
        foreach (var item in data.Items) item.Date = item.Date.Date;
        return data;
    }
    private static double Clamp(double n, double min, double max, double fallback) => double.IsFinite(n) ? Math.Clamp(n, min, max) : fallback;
    public void Save()
    {
        string temp = FilePath + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, Data, Json);
            stream.Flush(true);
        }
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
    }
    public void Change(Action<CalendarData> mutation)
    {
        string before = JsonSerializer.Serialize(Data, Json);
        try { mutation(Data); Save(); undo.Push(before); }
        catch { Data = JsonSerializer.Deserialize<CalendarData>(before, Json)!; throw; }
    }
    public void Undo()
    {
        if (undo.Count == 0) return;
        var current = Data;
        var previous = JsonSerializer.Deserialize<CalendarData>(undo.Peek(), Json)!;
        previous.Settings = current.Settings;
        Data = previous;
        try { Save(); undo.Pop(); }
        catch { Data = current; throw; }
    }
    public int Import(string path)
    {
        var incoming = Read(path);
        // Merge only unknown IDs: importing a backup never overwrites existing edits.
        var existing = Data.Items.Select(i => i.Id).ToHashSet();
        var additions = incoming.Items.Where(i => !existing.Contains(i.Id)).ToList();
        Change(d => d.Items.AddRange(additions));
        return additions.Count;
    }
    public void Export(string path)
    {
        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath + ".bak"), StringComparison.OrdinalIgnoreCase))
            throw new IOException("请另选一个位置导出备份。");
        File.WriteAllText(path, JsonSerializer.Serialize(Data, Json));
    }
}

public static class CalendarMath
{
    public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    public static IEnumerable<DateTime> MonthDays(DateTime date) => Enumerable.Range(0, 42).Select(i => WeekStart(new DateTime(date.Year, date.Month, 1)).AddDays(i));
    public static string Lunar(DateTime day)
    {
        try
        {
            var lunar = new ChineseLunisolarCalendar();
            int d = lunar.GetDayOfMonth(day), m = lunar.GetMonth(day), leap = lunar.GetLeapMonth(lunar.GetYear(day));
            bool isLeap = leap == m;
            if (leap > 0 && m >= leap) m--;
            string[] months = { "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月" };
            if (d == 1) return (isLeap ? "闰" : "") + months[m - 1];
            string[] digits = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (d <= 10) return "初" + digits[d - 1];
            if (d < 20) return "十" + digits[d - 11];
            if (d == 20) return "二十";
            if (d == 30) return "三十";
            return "廿" + digits[d - 21];
        }
        catch (ArgumentOutOfRangeException) { return ""; }
    }
    public static IEnumerable<TodoItem> OnDate(CalendarData data, DateTime date) => data.Items.Where(i => i.Date.Date == date.Date).OrderBy(i => i.Done).ThenBy(i => i.Time == "" ? "99:99" : i.Time);
}
