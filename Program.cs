using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopTodo;

public sealed class Program : Application
{
    private Forms.NotifyIcon? tray;
    private MainWindow? calendar;
    private Store store = null!;
    private DispatcherTimer? timer;
    private EventWaitHandle? showEvent;
    private bool quitting, windowed;
    private int ticks;
    public static string[] Arguments = Array.Empty<string>();

    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        if (args.Contains("--window-contract-self-test")) return SelfTests.RunWindowContract();
        if (args.Contains("--resize-self-test")) return SelfTests.RunResize();
        if (args.Contains("--self-test")) return SelfTests.Run();
        // Layered WPF windows reparented into Explorer need a software surface:
        // GPU composition can keep a stale surface after the parent changes.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        string folder = Option("--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShiriCalendar");
        string identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(folder).ToLowerInvariant())))[..20];
        using var mutex = new Mutex(true, @"Local\ShiriCalendar-" + identity, out bool first);
        if (!first)
        {
            try { using var ev = EventWaitHandle.OpenExisting(@"Local\ShiriCalendarShow-" + identity); ev.Set(); } catch { }
            return 0;
        }
        var app = new Program();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.windowed = args.Contains("--windowed");
        app.showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ShiriCalendarShow-" + identity);
        app.DispatcherUnhandledException += (_, e) =>
        {
            try { Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "error.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); } catch { }
            MessageBox.Show("操作未完成，已保留原数据。\n" + e.Exception.Message, "拾日 · 桌面日历");
            e.Handled = true;
        };
        app.Startup += (_, _) =>
        {
            try
            {
                app.store = new Store(folder);
                app.CreateTray(); app.OpenCalendar();
                app.timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                app.timer.Tick += (_, _) => app.Tick(); app.timer.Start();
                app.LogDiagnostics("startup-complete");
                if (app.store.RecoveryMessage != null) MessageBox.Show(app.store.RecoveryMessage, "数据恢复");
            }
            catch (Exception ex) { MessageBox.Show("无法启动拾日。\n" + ex.Message, "拾日 · 桌面日历"); app.Shutdown(1); }
        };
        return app.Run();
    }
    public static string? Option(string name)
    {
        int i = Array.IndexOf(Arguments, name);
        return i >= 0 && i + 1 < Arguments.Length ? Arguments[i + 1] : null;
    }
    private void CreateTray()
    {
        tray = new Forms.NotifyIcon { Text = "拾日 · 桌面日历", Visible = true,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Reveal);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("确保日历在桌面层", null, (_, _) => Dispatcher.Invoke(Reveal));
        menu.Items.Add("新增今日待办", null, (_, _) => Dispatcher.Invoke(() => { OpenCalendar(); calendar!.Edit(null, DateTime.Today); }));
        menu.Items.Add("设置与备份", null, (_, _) => Dispatcher.Invoke(() => { OpenCalendar(); calendar!.SettingsDialog(); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出拾日", null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu;
    }
    private void OpenCalendar()
    {
        if (calendar != null) return;
        CreateCalendar(windowed);
    }
    private void CreateCalendar(bool asWindow, DateTime? selected = null)
    {
        var created = new MainWindow(store, asWindow);
        calendar = created; MainWindow = created;
        if (selected.HasValue) created.FocusDate(selected.Value);
        created.Closed += (_, _) => { if (calendar == created) calendar = null; };
        created.Show();
    }
    public void Reveal()
    {
        OpenCalendar();
        calendar!.EnsureDesktopVisible();
    }
    public void Quit()
    {
        quitting = true; timer?.Stop(); calendar?.SavePlacement(); tray?.Dispose(); showEvent?.Dispose();
        Shutdown();
    }
    private void Tick()
    {
        LogDiagnostics("timer-tick");
        if (quitting) return;
        // Explorer can replace or reorder its desktop window while restarting.
        OpenCalendar();
        if (showEvent?.WaitOne(0) == true) Reveal();
        calendar?.CheckDesktop();
        if (++ticks % 15 != 0) return;
        calendar?.CheckDate();
        var due = store.Data.Items.Where(i => i.Remind && !i.Notified && !i.Done && i.Time != ""
            && i.Date.Date.Add(TimeSpan.Parse(i.Time)) <= DateTime.Now).ToList();
        if (due.Count == 0) return;
        tray!.ShowBalloonTip(8000, "拾日 · 待办提醒", string.Join("\n", due.Take(4).Select(i => i.Title)) + (due.Count > 4 ? "\n还有 " + (due.Count - 4) + " 项" : ""), Forms.ToolTipIcon.Info);
        store.Change(_ => due.ForEach(i => i.Notified = true));
    }
    private void LogDiagnostics(string stage)
    {
        if (Arguments.Contains("--diagnostics")) File.WriteAllText(Path.Combine(store.DirectoryPath, "lifecycle.txt"), DateTime.Now.ToString("O") + " " + stage);
    }
}
