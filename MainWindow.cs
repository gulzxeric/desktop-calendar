using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopTodo;

public sealed class MainWindow : Window
{
    private readonly Store store;
    private DateTime selected = DateTime.Today, displayed = DateTime.Today, lastToday = DateTime.Today;
    private Grid root = null!, body = null!;
    private TextBlock status = null!;
    private bool attached, dragging, initialRendered, compact;
    private DesktopHost.POINT dragStart;
    private DesktopHost.RECT startRect;
    private readonly bool testWindow;
    private string filter = "";
    private Preferences Pref => store.Data.Settings;
    private string view;
    public bool IsOnDesktop => attached;
    public bool IsCompactLayout => compact;
    public DateTime SelectedDate => selected;
    public void FocusDate(DateTime date) { selected = displayed = date; Build(); }
    public MainWindow(Store data, bool windowed)
    {
        store = data; testWindow = windowed; view = Pref.View;
        Title = "拾日 · 桌面日历";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = windowed || !Pref.Desktop;
        MinWidth = LayoutRules.MinimumWidth; MinHeight = LayoutRules.MinimumHeight;
        Width = Pref.Width; Height = Pref.Height; Opacity = Pref.Opacity;
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width); Height = Math.Min(Height, area.Height);
        Left = Pref.Left == -99999 ? area.Right - Width - 28 : Pref.Left;
        Top = Pref.Top == -99999 ? area.Top + 60 : Pref.Top;
        if (!System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new System.Drawing.Rectangle((int)Left, (int)Top, 140, 100))))
        { Left = area.Right - Width - 20; Top = area.Top + 40; }
        Build();
        SourceInitialized += (_, _) =>
        {
            // Establish the parent before the first WPF composition surface.
            if (Pref.Desktop && !testWindow)
            {
                attached = DesktopHost.Attach(this);
                if (!attached) ShowInTaskbar = true;
            }
        };
        ContentRendered += (_, _) =>
        {
            if (initialRendered) return;
            initialRendered = true;
            Build();
            WriteDiagnostics();
            string? screenshot = Program.Option("--capture");
            if (screenshot != null)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                timer.Tick += (_, _) => { timer.Stop(); Capture(screenshot); }; timer.Start();
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N) { Edit(null, selected); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo(); e.Handled = true; }
            if (e.Key == Key.Escape && !testWindow) SetDesktop(true);
        };
        Closed += (_, _) => SavePlacement();
    }
    private void Build()
    {
        compact = (ActualWidth > 0 ? ActualWidth : Width) < LayoutRules.CompactWidth;
        Theme.Set(Pref.Light);
        var frame = new Border { Background = Theme.Bg, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), ClipToBounds = true };
        root = new Grid(); frame.Child = root; Content = frame;
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        Header(); Toolbar();
        body = new Grid { Margin = new Thickness(18, 3, 18, 0) };
        Grid.SetRow(body, 2); root.Children.Add(body); RenderBody();
        var footer = new DockPanel { Margin = new Thickness(22, 0, 16, 0), LastChildFill = true };
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        var undo = Theme.Button("撤销", (_, _) => Undo()); undo.Padding = new Thickness(9, 2, 9, 2); undo.ToolTip = "撤销本次运行中的任务修改 · Ctrl+Z";
        DockPanel.SetDock(undo, Dock.Right); footer.Children.Add(undo);
        status = Theme.Text(attached ? "桌面层  ·  双击日期添加  ·  拖动任务改期" : "本地保存  ·  双击日期添加  ·  拖动任务改期", 11, Theme.Muted);
        footer.Children.Add(status);
        AddResizeHandles();
    }
    private void AddResizeHandles()
    {
        Thumb Handle(double width, double height, HorizontalAlignment horizontal, VerticalAlignment vertical, Cursor cursor, double xFactor, double yFactor, int z)
        {
            var thumb = new Thumb
            {
                Width = width, Height = height, HorizontalAlignment = horizontal, VerticalAlignment = vertical,
                Cursor = cursor, ToolTip = "拖动调整大小", Background = Brushes.Transparent
            };
            var hitArea = new FrameworkElementFactory(typeof(Border));
            hitArea.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = hitArea };
            thumb.DragDelta += (_, e) =>
            {
                if (Pref.Locked) { SetStatus("位置和大小已锁定，可在设置中解除"); return; }
                if (xFactor != 0) Width = Math.Clamp(Width + e.HorizontalChange * xFactor, MinWidth, LayoutRules.MaximumWidth);
                if (yFactor != 0) Height = Math.Clamp(Height + e.VerticalChange * yFactor, MinHeight, LayoutRules.MaximumHeight);
            };
            thumb.DragCompleted += (_, _) => { SavePlacement(); Build(); };
            Grid.SetRowSpan(thumb, 4); Panel.SetZIndex(thumb, z); root.Children.Add(thumb);
            return thumb;
        }
        var right = Handle(12, double.NaN, HorizontalAlignment.Right, VerticalAlignment.Stretch, Cursors.SizeWE, 1, 0, 40);
        right.ToolTip = "拖动右边缘调整宽度";
        var bottom = Handle(double.NaN, 12, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Cursors.SizeNS, 0, 1, 40);
        bottom.ToolTip = "拖动下边缘调整高度";
        var corner = Handle(38, 38, HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE, 1, 1, 50);
        corner.Name = "ResizeCorner";
        var marker = Theme.Text("◢", 16, Theme.Muted);
        marker.HorizontalAlignment = HorizontalAlignment.Right; marker.VerticalAlignment = VerticalAlignment.Bottom;
        marker.Margin = new Thickness(0, 0, 3, 1); marker.IsHitTestVisible = false;
        Grid.SetRowSpan(marker, 4); Panel.SetZIndex(marker, 60); root.Children.Add(marker);
    }
    private void Header()
    {
        var bar = new Grid { Margin = new Thickness(22, 12, 17, 4), Background = Brushes.Transparent };
        bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Add(bar);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Cursor = Pref.Locked ? Cursors.Arrow : Cursors.SizeAll };
        brand.Children.Add(Theme.Text("拾日", 26));
        if (!compact)
        {
            var label = Theme.Text("桌面日历", 12, Theme.Muted); label.Margin = new Thickness(14, 5, 0, 0); brand.Children.Add(label);
        }
        bar.Children.Add(brand);
        brand.MouseLeftButtonDown += (_, e) =>
        {
            if (Pref.Locked) return;
            dragging = true; DesktopHost.GetCursorPos(out dragStart); DesktopHost.GetWindowRect(new WindowInteropHelper(this).Handle, out startRect); brand.CaptureMouse(); e.Handled = true;
        };
        brand.MouseMove += (_, e) =>
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
            DesktopHost.GetCursorPos(out var current);
            DesktopHost.MoveScreen(new WindowInteropHelper(this).Handle, startRect.Left + current.X - dragStart.X, startRect.Top + current.Y - dragStart.Y);
        };
        brand.MouseLeftButtonUp += (_, _) => { dragging = false; brand.ReleaseMouseCapture(); SavePlacement(); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(buttons, 1); bar.Children.Add(buttons);
        var mode = Theme.Button(attached ? "弹出窗口" : "放回桌面", (_, _) => SetDesktop(!attached));
        mode.ToolTip = attached ? "暂时变成普通窗口，方便移动和缩放" : "将日历放回桌面图标层"; buttons.Children.Add(mode);
        buttons.Children.Add(Theme.Button("设置", (_, _) => SettingsDialog()));
        buttons.Children.Add(Theme.Button("＋ 新建", (_, _) => Edit(null, selected), true));
    }
    private void Toolbar()
    {
        var bar = new DockPanel { Margin = new Thickness(18, 0, 18, 4), LastChildFill = true };
        Grid.SetRow(bar, 1); root.Children.Add(bar);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string label in new[] { "月历", "周历", "清单" })
        {
            var b = Theme.Button(label, (_, _) => { view = label; Pref.View = label; store.Save(); Build(); }, label == view); right.Children.Add(b);
        }
        DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right);
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        nav.Children.Add(Theme.Button("‹", (_, _) => Navigate(-1)));
        var month = Theme.Button(displayed.ToString("yyyy 年 M 月"), (_, _) => JumpDate()); month.FontSize = 18; month.Background = Brushes.Transparent; month.ToolTip = "跳转日期"; nav.Children.Add(month);
        nav.Children.Add(Theme.Button("›", (_, _) => Navigate(1)));
        nav.Children.Add(Theme.Button("今天", (_, _) => { selected = displayed = DateTime.Today; Build(); }));
        bar.Children.Add(nav);
    }
    private void Navigate(int direction)
    {
        DateTime next = view == "周历" ? displayed.AddDays(direction * 7) : displayed.AddMonths(direction);
        if (next.Year < 1901 || next.Year > 2100) return;
        displayed = next; selected = next; Build();
    }
    private void RenderBody()
    {
        body.Children.Clear(); body.ColumnDefinitions.Clear();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = compact ? new GridLength(0) : new GridLength(268) });
        if (view == "清单") RenderList(); else RenderCalendar();
        if (!compact) RenderDay();
    }
    private void RenderCalendar()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 14, 0) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
        int rows = view == "周历" ? 1 : 6;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition());
        for (int c = 0; c < 7; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        string[] days = { "一", "二", "三", "四", "五", "六", "日" };
        for (int c = 0; c < 7; c++)
        {
            var text = Theme.Text(days[c], 12, c >= 5 ? Theme.Accent : Theme.Muted); text.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(text, c); grid.Children.Add(text);
        }
        var dates = view == "周历" ? Enumerable.Range(0, 7).Select(i => CalendarMath.WeekStart(displayed).AddDays(i)) : CalendarMath.MonthDays(displayed);
        int index = 0;
        foreach (var date in dates)
        {
            var cell = new Border { Background = date == selected.Date ? Theme.Panel : Theme.Cell,
                BorderBrush = date == selected.Date ? Theme.Accent : Theme.Line,
                BorderThickness = new Thickness(date == selected.Date ? 1.5 : .5), CornerRadius = new CornerRadius(5),
                Margin = new Thickness(2), Padding = compact ? new Thickness(4, 3, 3, 3) : new Thickness(7, 5, 5, 4), ClipToBounds = true, AllowDrop = true,
                ToolTip = date.ToString("yyyy年M月d日 dddd") + " · 双击添加待办", Tag = date };
            Grid.SetRow(cell, index / 7 + 1); Grid.SetColumn(cell, index % 7); grid.Children.Add(cell); index++;
            var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); layout.RowDefinitions.Add(new RowDefinition()); cell.Child = layout;
            var top = new DockPanel(); layout.Children.Add(top);
            var lunar = Theme.Text(CalendarMath.Lunar(date), 9, Theme.Muted); lunar.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(lunar, Dock.Right); top.Children.Add(lunar);
            var day = Theme.Text(date.Day.ToString(), compact ? 15 : 17, date == DateTime.Today ? Theme.Accent : Theme.Ink);
            day.FontFamily = new FontFamily("Bahnschrift"); day.FontWeight = date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal;
            if (date.Month != displayed.Month && view == "月历") day.Opacity = .4;
            top.Children.Add(day);
            if (date == DateTime.Today) day.Text += " ·";
            var stack = new StackPanel();
            var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
            foreach (var item in CalendarMath.OnDate(store.Data, date)) stack.Children.Add(TaskChip(item, view == "周历"));
            cell.MouseLeftButtonDown += (_, e) =>
            {
                if (date.Year < 1901 || date.Year > 2100) return;
                if (e.ClickCount == 2) { selected = date; Edit(null, date); }
            };
            cell.MouseLeftButtonUp += (_, e) =>
            {
                if (date.Year < 1901 || date.Year > 2100) return;
                if (selected != date) SelectDate(date);
            };
            cell.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("ShiriTask") ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            cell.Drop += (_, e) =>
            {
                if (date.Year < 1901 || date.Year > 2100 || !e.Data.GetDataPresent("ShiriTask")) return;
                var id = e.Data.GetData("ShiriTask") as string;
                var item = store.Data.Items.FirstOrDefault(i => i.Id == id);
                if (item == null) return;
                store.Change(_ => { item.Date = date; item.Notified = false; }); selected = date; RenderBody(); SetStatus("已移至 " + date.ToString("M月d日") + " · 可撤销");
                e.Handled = true;
            };
        }
        body.Children.Add(grid);
    }
    private void SelectDate(DateTime date)
    {
        selected = date;
        // Keep the clicked cell alive, so WPF can recognize its second click.
        if (body.Children.OfType<Grid>().FirstOrDefault() is Grid calendarGrid)
            foreach (var cell in calendarGrid.Children.OfType<Border>())
                if (cell.Tag is DateTime d)
                {
                    cell.Background = d == selected ? Theme.Panel : Theme.Cell;
                    cell.BorderBrush = d == selected ? Theme.Accent : Theme.Line;
                    cell.BorderThickness = new Thickness(d == selected ? 1.5 : .5);
                }
        if (!compact)
        {
            var sidebar = body.Children.OfType<Border>().FirstOrDefault(b => Grid.GetColumn(b) == 1);
            if (sidebar != null) body.Children.Remove(sidebar);
            RenderDay();
        }
    }
    private UIElement TaskChip(TodoItem item, bool wrap)
    {
        var row = new Border { Margin = new Thickness(0, 2, 0, 1), Padding = new Thickness(4, 3, 2, 3),
            BorderBrush = Theme.Brush(Theme.Colors[item.Color]), BorderThickness = new Thickness(2, 0, 0, 0), Cursor = Cursors.Hand,
            Background = Brushes.Transparent, ToolTip = (item.Time != "" ? item.Time + "  " : "") + item.Title + (item.Notes != "" ? "\n" + item.Notes : "") };
        var text = Theme.Text((item.Done ? "✓ " : "") + (item.Time != "" ? item.Time + " " : "") + item.Title, 11, item.Done ? Theme.Muted : Theme.Ink);
        if (wrap) { text.TextWrapping = TextWrapping.Wrap; text.TextTrimming = TextTrimming.None; }
        if (item.Done) text.TextDecorations = TextDecorations.Strikethrough;
        row.Child = text;
        Point? start = null;
        row.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { start = null; row.ReleaseMouseCapture(); Edit(item, item.Date); }
            else { start = e.GetPosition(row); row.CaptureMouse(); }
            e.Handled = true;
        };
        row.MouseMove += (_, e) =>
        {
            if (start == null || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(row);
            if (Math.Abs(p.X - start.Value.X) + Math.Abs(p.Y - start.Value.Y) < 8) return;
            start = null; row.ReleaseMouseCapture(); DragDrop.DoDragDrop(row, new DataObject("ShiriTask", item.Id), DragDropEffects.Move);
        };
        row.MouseLeftButtonUp += (_, _) => { start = null; row.ReleaseMouseCapture(); };
        var menu = new ContextMenu();
        AddMenu(menu, item.Done ? "标记未完成" : "完成待办", () => Toggle(item));
        AddMenu(menu, "编辑", () => Edit(item, item.Date));
        AddMenu(menu, "删除（可撤销）", () => Delete(item)); row.ContextMenu = menu;
        return row;
    }
    private static void AddMenu(ContextMenu menu, string label, Action action)
    {
        var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item);
    }
    private void RenderDay()
    {
        var panel = new Border { Background = Theme.Panel, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 17, 16, 12) };
        Grid.SetColumn(panel, 1); body.Children.Add(panel);
        var layout = new Grid(); panel.Child = layout;
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel(); layout.Children.Add(heading);
        heading.Children.Add(Theme.Text(selected == DateTime.Today ? "今天" : selected.ToString("dddd"), 12, Theme.Accent));
        var date = Theme.Text(selected.ToString("M 月 d 日"), 25); date.Margin = new Thickness(0, 5, 0, 5); heading.Children.Add(date);
        var items = CalendarMath.OnDate(store.Data, selected).ToList();
        var count = Theme.Text(items.Count == 0 ? "给这一天留一点计划" : $"{items.Count(i => !i.Done)} 项待办 · {items.Count(i => i.Done)} 项完成", 12, Theme.Muted);
        count.Margin = new Thickness(0, 0, 0, 14); heading.Children.Add(count);
        var tasks = new StackPanel(); var scroll = new ScrollViewer { Content = tasks, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        if (items.Count == 0)
        {
            var empty = Theme.Text("还没有安排\n\n双击左侧日期，\n或在下方添加一件事。", 13, Theme.Muted); empty.TextWrapping = TextWrapping.Wrap; empty.Margin = new Thickness(0, 30, 0, 0); tasks.Children.Add(empty);
        }
        foreach (var item in items) tasks.Children.Add(TaskRow(item));
        var add = Theme.Button("＋ 添加待办", (_, _) => Edit(null, selected), true); add.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(add, 2); layout.Children.Add(add);
    }
    private UIElement TaskRow(TodoItem item)
    {
        var frame = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, .5), Padding = new Thickness(0, 11, 0, 11) };
        var row = new Grid(); frame.Child = row;
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        var check = new CheckBox { IsChecked = item.Done, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0), ToolTip = "完成：" + item.Title };
        check.Click += (_, _) => Toggle(item); row.Children.Add(check);
        var words = new StackPanel { Cursor = Cursors.Hand };
        var title = Theme.Text(item.Title, 13, item.Done ? Theme.Muted : Theme.Ink); title.TextWrapping = TextWrapping.Wrap;
        if (item.Done) title.TextDecorations = TextDecorations.Strikethrough;
        words.Children.Add(title);
        var meta = Theme.Text((item.Time != "" ? item.Time + " · " : "") + Theme.ColorNames[item.Color] + (item.Remind ? " · 提醒" : ""), 10, Theme.Muted); meta.Margin = new Thickness(0, 5, 0, 0); words.Children.Add(meta);
        words.MouseLeftButtonUp += (_, _) => Edit(item, item.Date); Grid.SetColumn(words, 1); row.Children.Add(words);
        var more = Theme.Button("⋯", (_, _) => { }); more.Padding = new Thickness(3); more.Background = Brushes.Transparent; more.VerticalAlignment = VerticalAlignment.Top;
        var menu = new ContextMenu(); AddMenu(menu, "编辑", () => Edit(item, item.Date)); AddMenu(menu, "删除（可撤销）", () => Delete(item));
        more.Click += (_, _) => { menu.PlacementTarget = more; menu.IsOpen = true; }; Grid.SetColumn(more, 2); row.Children.Add(more);
        return frame;
    }
    private void RenderList()
    {
        var layout = new Grid { Margin = new Thickness(2, 0, 16, 0) }; layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition());
        var search = Theme.Input(filter); search.ToolTip = "搜索所有日期的任务标题和备注";
        search.Margin = new Thickness(0, 0, 0, 8);
        var list = new StackPanel();
        void Fill()
        {
            list.Children.Clear();
            var matches = store.Data.Items.Where(i => filter == "" || i.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || i.Notes.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Date).ThenBy(i => i.Done).ThenBy(i => i.Time).GroupBy(i => i.Date.Date).ToList();
            if (matches.Count == 0) list.Children.Add(Theme.Text(filter == "" ? "还没有待办。点击右上角「新建」开始。" : "没有找到匹配的待办。", 14, Theme.Muted));
            foreach (var group in matches)
            {
                var label = Theme.Button(group.Key.ToString("yyyy年 M月d日  dddd"), (_, _) => { selected = displayed = group.Key; RenderBody(); });
                label.HorizontalAlignment = HorizontalAlignment.Left; label.Background = Brushes.Transparent; label.Foreground = Theme.Accent; label.Margin = new Thickness(0, 12, 0, 4); list.Children.Add(label);
                foreach (var item in group) list.Children.Add(TaskRow(item));
            }
        }
        search.TextChanged += (_, _) => { filter = search.Text.Trim(); Fill(); }; layout.Children.Add(search);
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); layout.Children.Add(scroll); Fill(); body.Children.Add(layout);
    }
    private void Toggle(TodoItem item) { store.Change(_ => item.Done = !item.Done); RenderBody(); SetStatus(item.Done ? "已完成 · 可撤销" : "已恢复为待办 · 可撤销"); }
    private void Delete(TodoItem item) { store.Change(d => d.Items.Remove(item)); RenderBody(); SetStatus("已删除 · 点击右下角撤销可恢复"); }
    private void Undo() { store.Undo(); RenderBody(); SetStatus("已撤销上一次任务修改"); }
    private void SetStatus(string text) { if (status != null) status.Text = text; }

    public void Edit(TodoItem? item, DateTime date)
    {
        var dialog = Theme.Dialog(item == null ? "新建待办 · 拾日" : "编辑待办 · 拾日", 490, 570);
        var grid = new Grid { Margin = new Thickness(26, 20, 26, 20) };
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); dialog.Content = grid;
        var form = new StackPanel(); grid.Children.Add(form);
        form.Children.Add(Theme.Text("要做什么", 13, Theme.Muted));
        var title = Theme.Input(item?.Title ?? ""); title.ToolTip = "待办标题"; form.Children.Add(title);
        var schedule = new Grid(); schedule.ColumnDefinitions.Add(new ColumnDefinition()); schedule.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        var datePanel = new StackPanel(); datePanel.Children.Add(Theme.Text("日期", 12, Theme.Muted));
        var picker = new DatePicker { SelectedDate = date, DisplayDateStart = new DateTime(1901, 1, 1), DisplayDateEnd = new DateTime(2100, 12, 31), Margin = new Thickness(0, 6, 14, 12), FontSize = 14, Height = 34 };
        datePanel.Children.Add(picker); schedule.Children.Add(datePanel);
        var timePanel = new StackPanel(); timePanel.Children.Add(Theme.Text("时间（可不填）", 12, Theme.Muted));
        var time = Theme.Input(item?.Time ?? "", 5); time.ToolTip = "24 小时制，如 09:30"; timePanel.Children.Add(time); Grid.SetColumn(timePanel, 1); schedule.Children.Add(timePanel); form.Children.Add(schedule);
        form.Children.Add(Theme.Text("分类颜色", 12, Theme.Muted));
        var colors = new WrapPanel { Margin = new Thickness(0, 6, 0, 13) }; int color = item?.Color ?? 0;
        for (int i = 0; i < Theme.Colors.Length; i++)
        {
            int n = i;
            var radio = new RadioButton { Content = Theme.ColorNames[i], Foreground = Theme.Brush(Pref.Light ? "#263647" : Theme.Colors[i]), GroupName = "colors", IsChecked = n == color, Margin = new Thickness(0, 6, 15, 4), FontSize = 13 };
            radio.Checked += (_, _) => color = n; colors.Children.Add(radio);
        }
        form.Children.Add(colors); form.Children.Add(Theme.Text("备注", 12, Theme.Muted));
        var notes = Theme.Input(item?.Notes ?? "", 10000); notes.Height = 98; notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; form.Children.Add(notes);
        var reminder = new CheckBox { Content = "到时提醒（需保持拾日运行）", Foreground = Theme.Ink, IsChecked = item?.Remind ?? false, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) }; form.Children.Add(reminder);
        var error = Theme.Text("", 12, Theme.Brush("#ECAFA9")); error.TextWrapping = TextWrapping.Wrap; form.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetRow(buttons, 1); grid.Children.Add(buttons);
        var cancel = Theme.Button("取消", (_, _) => dialog.Close()); cancel.IsCancel = true; buttons.Children.Add(cancel);
        var save = Theme.Button("保存待办", (_, _) =>
        {
            string name = title.Text.Trim(), clock = time.Text.Trim();
            if (name == "") { error.Text = "请填写待办内容。"; title.Focus(); return; }
            if (picker.SelectedDate == null) { error.Text = "请选择日期。"; return; }
            if (clock != "" && !TimeSpan.TryParseExact(clock, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out _)) { error.Text = "时间格式为 09:30，小时范围 00–23。"; return; }
            if (reminder.IsChecked == true && clock == "") { error.Text = "开启提醒需要填写时间。"; return; }
            store.Change(d =>
            {
                var target = item ?? new TodoItem();
                if (target.Date.Date != picker.SelectedDate.Value.Date || target.Time != clock || target.Remind != (reminder.IsChecked == true)) target.Notified = false;
                target.Title = name; target.Date = picker.SelectedDate.Value.Date; target.Time = clock;
                target.Notes = notes.Text.Trim(); target.Color = color; target.Remind = reminder.IsChecked == true;
                if (item == null) d.Items.Add(target);
            });
            selected = displayed = picker.SelectedDate.Value.Date; dialog.Close(); Build(); SetStatus("已保存到本机 · " + DateTime.Now.ToString("HH:mm"));
        }, true);
        buttons.Children.Add(save);
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
        dialog.Loaded += (_, _) => title.Focus(); dialog.ShowDialog();
    }
    private void JumpDate()
    {
        var dialog = Theme.Dialog("跳转日期", 330, 210); var panel = new StackPanel { Margin = new Thickness(24) }; dialog.Content = panel;
        var date = new DatePicker { SelectedDate = selected, DisplayDateStart = new DateTime(1901, 1, 1), DisplayDateEnd = new DateTime(2100, 12, 31), FontSize = 16, Margin = new Thickness(0, 0, 0, 18) }; panel.Children.Add(date);
        panel.Children.Add(Theme.Button("跳转", (_, _) => { if (date.SelectedDate == null) return; selected = displayed = date.SelectedDate.Value; dialog.Close(); Build(); }, true)); dialog.ShowDialog();
    }

    public void SetDesktop(bool enable, bool persist = true)
    {
        if (enable == attached) return;
        ((Program)Application.Current).ChangeMode(enable, persist);
    }
    public void CheckDesktop()
    {
        if (testWindow || !attached) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!DesktopHost.IsWindow(hwnd)) return;
        if (DesktopHost.GetParent(hwnd) != DesktopHost.FindDesktop())
            ((Program)Application.Current).ChangeMode(true, false);
    }
    public void CheckDate() { if (lastToday != DateTime.Today) { lastToday = DateTime.Today; Build(); } }
    public void SavePlacement()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !DesktopHost.IsWindow(hwnd)) return;
        DesktopHost.GetWindowRect(hwnd, out var rect);
        var dpi = VisualTreeHelper.GetDpi(this);
        Pref.Left = rect.Left / dpi.DpiScaleX; Pref.Top = rect.Top / dpi.DpiScaleY;
        Pref.Width = Width; Pref.Height = Height; store.Save();
    }
    private void WriteDiagnostics()
    {
        if (!Program.Arguments.Contains("--diagnostics")) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        File.WriteAllText(Path.Combine(store.DirectoryPath, "window-diagnostics.json"), System.Text.Json.JsonSerializer.Serialize(new
        { hwnd = hwnd.ToInt64(), parent = DesktopHost.GetParent(hwnd).ToInt64(), desktop = DesktopHost.FindDesktop().ToInt64(), visible = DesktopHost.IsWindowVisible(hwnd), attached, width = ActualWidth, height = ActualHeight, style = DesktopHost.GetWindowLongPtr(hwnd, -16).ToInt64() }, Store.Json));
    }
    private void Capture(string path)
    {
        root.UpdateLayout(); var image = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render((Visual)Content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
    public void SettingsDialog()
    {
        var dialog = Theme.Dialog("设置与备份 · 拾日", 510, 630);
        var panel = new StackPanel { Margin = new Thickness(26, 20, 26, 20) }; dialog.Content = panel;
        panel.Children.Add(Theme.Text("把日历放成你喜欢的样子", 19));
        var help = Theme.Text("所有待办保存在本机，无需账号。", 12, Theme.Muted); help.Margin = new Thickness(0, 8, 0, 20); panel.Children.Add(help);
        var light = new CheckBox { Content = "浅色主题", Foreground = Theme.Ink, IsChecked = Pref.Light, Margin = new Thickness(0, 0, 0, 16) }; panel.Children.Add(light);
        var locked = new CheckBox { Content = "锁定日历位置和大小", Foreground = Theme.Ink, IsChecked = Pref.Locked, Margin = new Thickness(0, 0, 0, 16) }; panel.Children.Add(locked);
        string startupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "拾日桌面日历.lnk");
        var startup = new CheckBox { Content = "登录 Windows 时自动启动", Foreground = Theme.Ink, IsChecked = File.Exists(startupPath), Margin = new Thickness(0, 0, 0, 20) }; panel.Children.Add(startup);
        panel.Children.Add(Theme.Text("不透明度（数值越低越透）", 12, Theme.Muted));
        var opacityLabel = Theme.Text(((int)(Pref.Opacity * 100)) + "%", 12, Theme.Muted); panel.Children.Add(opacityLabel);
        var opacity = new Slider { Minimum = 45, Maximum = 100, Value = Pref.Opacity * 100, Margin = new Thickness(0, 8, 0, 20), TickFrequency = 5, IsSnapToTickEnabled = true };
        opacity.ValueChanged += (_, _) => opacityLabel.Text = ((int)opacity.Value) + "%"; panel.Children.Add(opacity);
        panel.Children.Add(Theme.Text("数据与备份", 15));
        var backup = new WrapPanel { Margin = new Thickness(0, 8, 0, 6) }; panel.Children.Add(backup);
        backup.Children.Add(Theme.Button("导出备份", (_, _) =>
        {
            var file = new SaveFileDialog { Filter = "拾日备份 (*.json)|*.json", FileName = "拾日备份-" + DateTime.Now.ToString("yyyyMMdd") + ".json" };
            if (file.ShowDialog(dialog) == true) { store.Export(file.FileName); MessageBox.Show(dialog, "备份已导出。", "拾日"); }
        }));
        backup.Children.Add(Theme.Button("导入备份", (_, _) =>
        {
            var file = new OpenFileDialog { Filter = "拾日备份 (*.json)|*.json" };
            if (file.ShowDialog(dialog) != true) return;
            try { int count = store.Import(file.FileName); RenderBody(); MessageBox.Show(dialog, "已合并 " + count + " 项新任务。已有任务未覆盖。", "拾日"); }
            catch (Exception ex) { MessageBox.Show(dialog, "导入失败：" + ex.Message, "拾日"); }
        }));
        backup.Children.Add(Theme.Button("数据文件夹", (_, _) => Process.Start(new ProcessStartInfo { FileName = store.DirectoryPath, UseShellExecute = true })));
        var info = Theme.Text("每次保存保留上一版备份。导入按任务编号合并。\n删除或改期后，可用右下角「撤销」恢复。\n提醒依赖程序运行和 Windows 通知设置。", 12, Theme.Muted); info.TextWrapping = TextWrapping.Wrap; info.Margin = new Thickness(0, 5, 0, 20); panel.Children.Add(info);
        var end = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(end);
        end.Children.Add(Theme.Button("退出拾日", (_, _) => { dialog.Close(); ((Program)Application.Current).Quit(); }));
        end.Children.Add(Theme.Button("保存设置", (_, _) =>
        {
            try
            {
                bool wantStartup = startup.IsChecked == true;
                if (wantStartup && !File.Exists(startupPath))
                {
                    var type = Type.GetTypeFromProgID("WScript.Shell")!;
                    dynamic shell = Activator.CreateInstance(type)!;
                    try
                    {
                        dynamic shortcut = shell.CreateShortcut(startupPath);
                        try { shortcut.TargetPath = Environment.ProcessPath!; shortcut.WorkingDirectory = AppContext.BaseDirectory; shortcut.Description = "拾日 · 本地桌面日历"; shortcut.Save(); }
                        finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); }
                    }
                    finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
                }
                else if (!wantStartup && File.Exists(startupPath)) File.Delete(startupPath);
                Pref.Light = light.IsChecked == true; Pref.Locked = locked.IsChecked == true; Pref.Opacity = opacity.Value / 100; Opacity = Pref.Opacity; store.Save(); dialog.Close(); Build();
            }
            catch (Exception ex) { MessageBox.Show(dialog, "设置未能保存：" + ex.Message, "拾日"); }
        }, true));
        dialog.ShowDialog();
    }
}
