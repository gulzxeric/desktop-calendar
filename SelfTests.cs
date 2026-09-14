using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopTodo;

public static class SelfTests
{
    public static int RunWindowContract()
    {
        string root = Path.Combine(Path.GetTempPath(), "ShiriWindowTests-" + Guid.NewGuid().ToString("N"));
        string report = Program.Option("--report") ?? Path.Combine(AppContext.BaseDirectory, "window-contract-test-results.txt");
        Directory.CreateDirectory(root);
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var store = new Store(root);
            store.Data.Settings.Desktop = true;
            var window = new MainWindow(store, false);
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var hwnd = new WindowInteropHelper(window).Handle;
            long exStyle = DesktopHost.GetWindowLongPtr(hwnd, DesktopHost.GwlExStyle).ToInt64();
            long style = DesktopHost.GetWindowLongPtr(hwnd, DesktopHost.GwlStyle).ToInt64();
            var desktop = DesktopHost.FindDesktop();
            bool hiddenFromTaskbar = (exStyle & DesktopHost.WsExAppWindow) == 0
                && (exStyle & DesktopHost.WsExToolWindow) != 0;
            bool stableDesktopLayer = (style & DesktopHost.WsChild) == 0
                && (style & DesktopHost.WsPopup) != 0
                && DesktopHost.GetParent(hwnd) == IntPtr.Zero
                && DesktopHost.GetWindow(hwnd, DesktopHost.GwHwndNext) == desktop;
            bool passed = hiddenFromTaskbar && stableDesktopLayer;
            File.WriteAllText(report,
                $"{(passed ? "PASS" : "FAIL")} taskbarSuppressed={hiddenFromTaskbar}; managedShowInTaskbar={window.ShowInTaskbar}; parent={DesktopHost.GetParent(hwnd)}; desktop={desktop}; style=0x{style:X}; exStyle=0x{exStyle:X}; next={DesktopHost.GetWindow(hwnd, DesktopHost.GwHwndNext)}");
            window.Close();
            app.Shutdown();
            return passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, "FAIL " + ex);
            return 1;
        }
        finally { Directory.Delete(root, true); }
    }

    public static int RunResize()
    {
        string root = Path.Combine(Path.GetTempPath(), "ShiriResizeTests-" + Guid.NewGuid().ToString("N"));
        string report = Program.Option("--report") ?? Path.Combine(AppContext.BaseDirectory, "resize-test-results.txt");
        Directory.CreateDirectory(root);
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var store = new Store(root);
            store.Data.Settings.Desktop = false;
            store.Data.Settings.Width = 900;
            store.Data.Settings.Height = 580;
            var window = new MainWindow(store, true);
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var grip = FindVisual<Thumb>(window, t => t.Name == "ResizeCorner")
                ?? throw new Exception("Resize grip was not found");
            grip.RaiseEvent(new DragDeltaEventArgs(-340, -160) { RoutedEvent = Thumb.DragDeltaEvent });
            grip.RaiseEvent(new DragCompletedEventArgs(-340, -160, false) { RoutedEvent = Thumb.DragCompletedEvent });
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.SavePlacement();
            var reloaded = new Store(root);
            bool shrank = window.Width <= 600 && window.Height <= 430 && window.IsCompactLayout
                && reloaded.Data.Settings.Width <= 600 && reloaded.Data.Settings.Height <= 430;
            double smallWidth = window.Width, smallHeight = window.Height;
            var growGrip = FindVisual<Thumb>(window, t => t.Name == "ResizeCorner")
                ?? throw new Exception("Resize grip disappeared in compact layout");
            growGrip.RaiseEvent(new DragDeltaEventArgs(340, 200) { RoutedEvent = Thumb.DragDeltaEvent });
            growGrip.RaiseEvent(new DragCompletedEventArgs(340, 200, false) { RoutedEvent = Thumb.DragCompletedEvent });
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            bool grew = window.Width >= 880 && window.Height >= 600 && !window.IsCompactLayout;
            bool passed = shrank && grew;
            string result = $"{(passed ? "PASS" : "FAIL")} shrink 900x580 -> {smallWidth:0}x{smallHeight:0}; persisted {reloaded.Data.Settings.Width:0}x{reloaded.Data.Settings.Height:0}; grow -> {window.Width:0}x{window.Height:0}";
            File.WriteAllText(report, result);
            window.Close();
            app.Shutdown();
            return passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, "FAIL " + ex);
            return 1;
        }
        finally { Directory.Delete(root, true); }
    }
    private static T? FindVisual<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
    {
        if (root is T match && predicate(match)) return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindVisual(VisualTreeHelper.GetChild(root, i), predicate);
            if (found != null) return found;
        }
        return null;
    }

    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "ShiriTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var log = new System.Collections.Generic.List<string>();
        void Check(bool result, string label) { if (!result) throw new Exception(label); log.Add("PASS " + label); }
        try
        {
            var store = new Store(Path.Combine(root, "data"));
            var item = new TodoItem { Title = "学习前端30分钟", Date = new DateTime(2026, 9, 14), Notes = "中文备注\n第二行", Time = "09:30" };
            store.Change(d => d.Items.Add(item));
            var reloaded = new Store(store.DirectoryPath);
            Check(reloaded.Data.Items.Single().Title == item.Title && reloaded.Data.Items[0].Notes == item.Notes, "Chinese task survives restart");
            store.Change(_ => { item.Done = true; item.Date = new DateTime(2026, 9, 15); });
            reloaded = new Store(store.DirectoryPath);
            Check(reloaded.Data.Items.Single().Done && reloaded.Data.Items.Single().Date.Day == 15, "Completion and reschedule persisted");
            store.Undo();
            Check(store.Data.Items.Single().Date.Day == 14 && !store.Data.Items.Single().Done, "Undo restores task and date");
            store.Change(d => d.Items.Clear()); store.Undo();
            Check(new Store(store.DirectoryPath).Data.Items.Count == 1, "Delete then undo persists restoration");
            string export = Path.Combine(root, "export.json"); store.Export(export);
            Check(store.Import(export) == 0 && store.Data.Items.Count == 1, "Import is idempotent");
            var other = new Store(Path.Combine(root, "other"));
            Check(other.Import(export) == 1 && other.Data.Items.Single().Title == item.Title, "Backup imports into empty installation");
            File.WriteAllText(Path.Combine(root, "bad.json"), "{broken}");
            try { store.Import(Path.Combine(root, "bad.json")); throw new Exception("Malformed import accepted"); } catch (System.Text.Json.JsonException) { }
            Check(store.Data.Items.Count == 1, "Invalid import preserves existing tasks");
            store.Save();
            File.WriteAllText(store.FilePath, "broken");
            var recovered = new Store(store.DirectoryPath);
            Check(recovered.Data.Items.Count == 1 && recovered.RecoveryMessage != null, "Corrupt data recovers previous backup");
            Check(Directory.GetFiles(store.DirectoryPath, "calendar-damaged-*.json").Length == 1, "Corrupt original retained");
            recovered.Save(); Check(new Store(store.DirectoryPath).Data.Items.Count == 1, "Recovered data remains valid after save");
            var feb = CalendarMath.MonthDays(new DateTime(2024, 2, 15)).ToArray();
            Check(feb.Length == 42 && feb[0].DayOfWeek == DayOfWeek.Monday && feb.Contains(new DateTime(2024, 2, 29)), "Leap-month grid is complete and Monday-first");
            Check(CalendarMath.WeekStart(new DateTime(2026, 9, 20)) == new DateTime(2026, 9, 14), "Sunday maps to correct week");
            Check(CalendarMath.WeekStart(new DateTime(2026, 9, 14)) == new DateTime(2026, 9, 14), "Monday maps to same day");
            Check(CalendarMath.Lunar(new DateTime(2024, 2, 10)) == "正月", "Lunar new year conversion");
            var tasks = new CalendarData(); tasks.Items.Add(new TodoItem { Title = "全天" }); tasks.Items.Add(new TodoItem { Title = "早晨", Time = "08:00" }); tasks.Items.Add(new TodoItem { Title = "完成", Done = true, Time = "07:00" });
            Check(string.Join(",", CalendarMath.OnDate(tasks, DateTime.Today).Select(t => t.Title)) == "早晨,全天,完成", "Agenda sorts time and completion");
            log.Add("ALL 15 TESTS PASSED");
            File.WriteAllLines(Program.Option("--report") ?? Path.Combine(AppContext.BaseDirectory, "test-results.txt"), log);
            return 0;
        }
        catch (Exception ex)
        {
            log.Add("FAIL " + ex);
            File.WriteAllLines(Program.Option("--report") ?? Path.Combine(AppContext.BaseDirectory, "test-results.txt"), log);
            return 1;
        }
        finally { Directory.Delete(root, true); }
    }
}
