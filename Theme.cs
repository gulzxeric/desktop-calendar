using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopTodo;

public sealed class ThemeSpec
{
    public string Name;
    public bool Light;
    public string Bg, BgAlt, Panel, Cell, Ink, Muted, Line, Accent;
    public ThemeSpec(string name, bool light, string bg, string bgAlt, string panel, string cell, string ink, string muted, string line, string accent)
    {
        Name = name; Light = light; Bg = bg; BgAlt = bgAlt; Panel = panel; Cell = cell; Ink = ink; Muted = muted; Line = line; Accent = accent;
    }
}

public static class Theme
{
    public static Brush Bg = Brush("#202A37"), Panel = Brush("#283544"), Cell = Brush("#2D3B4C"), Ink = Brush("#F2F6FA"), Muted = Brush("#AFBED0"), Line = Brush("#415065"), Accent = Brush("#A8D9EF"), Edge = Brushes.Transparent;

    // 6 套主题，风格借鉴 Apple：低饱和柔和配色 + 微渐变
    public static readonly ThemeSpec[] Themes =
    {
        new ThemeSpec("静夜",   false, "#1E2836", "#191F2B", "#283242", "#333E52",
                       "#E8ECF4", "#8C9BB4", "#3A465C", "#5B9FD6"),
        new ThemeSpec("晨雾",   true,  "#F0F4F8", "#E6ECF2", "#E8EEF4", "#F8FAFC",
                       "#2E3948", "#7A8A9E", "#C5D0DC", "#3A7CA5"),
        new ThemeSpec("樱",     true,  "#FAF3F5", "#F5E8EC", "#F2E2E8", "#FFF8FA",
                       "#5C3648", "#A67D92", "#E0C4D2", "#C45D8A"),
        new ThemeSpec("松林",   false, "#1B2B21", "#162318", "#253829", "#2E4535",
                       "#DCEEE2", "#8AAE92", "#3A5A45", "#7BC7A0"),
        new ThemeSpec("米砂",   true,  "#F8F3EB", "#F0E9DB", "#F2EBDD", "#FFFCF4",
                       "#5A4A32", "#9E8C6C", "#D6C8AC", "#B8864A"),
        new ThemeSpec("暮",     false, "#1D1730", "#161228", "#2A2244", "#342850",
                       "#EEE8FA", "#9E8FC0", "#453A68", "#B08AE8"),
    };

    public static ThemeSpec Current = Themes[0];
    public static bool IsLight => Current.Light;

    public static readonly string[] Colors = { "#5B9FD6", "#7BC7A0", "#D4B85C", "#B08AE8", "#D08A8A" };
    public static readonly string[] ColorNames = { "日常", "学习", "工作", "生活", "重要" };
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    public static int ClampIndex(int index) => index < 0 ? 0 : Math.Min(index, Themes.Length - 1);
    public static void Set(int index)
    {
        index = ClampIndex(index);
        Current = Themes[index];
        Apply(Current);
    }
    private static void Apply(ThemeSpec s)
    {
        // 背景用线性渐变 Bg→BgAlt（上→下），Apple 风格柔和过渡
        var c1 = (Color)ColorConverter.ConvertFromString(s.Bg);
        var c2 = (Color)ColorConverter.ConvertFromString(s.BgAlt);
        var g = new LinearGradientBrush(
            new GradientStopCollection {
                new GradientStop(c1, 0.0),
                new GradientStop(c2, 1.0)
            },
            new Point(0, 0), new Point(0, 1));
        g.Freeze();
        Bg = g;
        // 边缘入光：窗口顶部 12% 高度内，一道由白（亮色主题）/浅（暗色主题）到透明的柔和渐变
        var edge = s.Light ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(46, 235, 240, 255);
        var eg = new LinearGradientBrush(
            new GradientStopCollection {
                new GradientStop(edge, 0.0),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.12)
            },
            new Point(0, 0), new Point(0, 1));
        eg.Freeze();
        Edge = eg;
        Panel = Brush(s.Panel);
        Cell = Brush(s.Cell);
        Ink = Brush(s.Ink);
        Muted = Brush(s.Muted);
        Line = Brush(s.Line);
        Accent = Brush(s.Accent);
    }
    // 兼容旧调用：仅按明暗切换。
    public static void Set(bool light) => Set(light ? 1 : 0);

    public static TextBlock Text(string text, double size = 14, Brush? color = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Ink,
        FontFamily = new FontFamily("Microsoft YaHei UI"), VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    public static Button Button(string text, RoutedEventHandler click, bool primary = false)
    {
        var b = new Button { Content = text, FontSize = 13, FontFamily = new FontFamily("Microsoft YaHei UI"),
            Foreground = primary ? Brush("#203344") : Ink, Background = primary ? Accent : Cell,
            BorderThickness = new Thickness(0), Padding = new Thickness(13, 8, 13, 8),
            Margin = new Thickness(3), Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        b.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        b.MouseEnter += (_, _) => b.Opacity = .78; b.MouseLeave += (_, _) => b.Opacity = 1;
        b.Click += click;
        return b;
    }
    public static TextBox Input(string value = "", int max = 300) => new()
    {
        Text = value, MaxLength = max, FontSize = 14, FontFamily = new FontFamily("Microsoft YaHei UI"),
        Background = Cell, Foreground = Ink, CaretBrush = Ink, BorderBrush = Line,
        BorderThickness = new Thickness(1), Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 12)
    };
    public static Window Dialog(string title, double width, double height)
    {
        var dialog = new Window
        {
            Title = title, Width = width, Height = height, Background = Bg, Foreground = Ink,
            FontFamily = new FontFamily("Microsoft YaHei UI"), WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false
        };
        dialog.SourceInitialized += (_, _) => DesktopHost.HideFromTaskbar(dialog);
        return dialog;
    }
}
