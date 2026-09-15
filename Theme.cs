using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopTodo;

public sealed class ThemeSpec
{
    public string Name;
    public bool Light;
    public string Bg, Panel, Cell, Ink, Muted, Line, Accent;
    public ThemeSpec(string name, bool light, string bg, string panel, string cell, string ink, string muted, string line, string accent)
    {
        Name = name; Light = light; Bg = bg; Panel = panel; Cell = cell; Ink = ink; Muted = muted; Line = line; Accent = accent;
    }
}

public static class Theme
{
    public static Brush Bg = Brush("#202A37"), Panel = Brush("#283544"), Cell = Brush("#2D3B4C"), Ink = Brush("#F2F6FA"), Muted = Brush("#AFBED0"), Line = Brush("#415065"), Accent = Brush("#A8D9EF");

    // 每套主题一种风格：深蓝夜色 / 晨雾浅蓝 / 樱粉 / 墨绿森林 / 暖砂 / 暮紫
    public static readonly ThemeSpec[] Themes =
    {
        new ThemeSpec("深蓝夜色", false, "#202A37", "#283544", "#2D3B4C", "#F2F6FA", "#AFBED0", "#415065", "#A8D9EF"),
        new ThemeSpec("晨雾浅蓝", true,  "#EEF3F7", "#E1EAF1", "#F8FAFC", "#263647", "#54677D", "#BECCD8", "#27698A"),
        new ThemeSpec("樱粉",     true,  "#FBF0F3", "#F7E3EA", "#FFF7FA", "#4A2B3A", "#9B7285", "#E8C9D6", "#C75B8A"),
        new ThemeSpec("墨绿森林", false, "#1A2B22", "#22372C", "#294136", "#E9F5EC", "#9BB8A4", "#3B5A49", "#8FD3A0"),
        new ThemeSpec("暖砂",     true,  "#F7F1E5", "#F0E7D6", "#FDF9F0", "#4A3B28", "#8A7A5F", "#D9CCB2", "#C08A3E"),
        new ThemeSpec("暮紫",     false, "#1E1730", "#291F3D", "#322647", "#F3EEFB", "#A79BC8", "#4A3D66", "#C39BEF"),
    };

    public static ThemeSpec Current = Themes[0];
    public static bool IsLight => Current.Light;

    public static readonly string[] Colors = { "#A8D9EF", "#B8D7AD", "#EEC681", "#D7B4E8", "#ECAFA9" };
    public static readonly string[] ColorNames = { "日常", "学习", "工作", "生活", "重要" };
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    public static int ClampIndex(int index) => index < 0 ? 0 : Math.Min(index, Themes.Length - 1);
    public static void Set(int index)
    {
        Current = Themes[ClampIndex(index)];
        Bg = Brush(Current.Bg); Panel = Brush(Current.Panel); Cell = Brush(Current.Cell);
        Ink = Brush(Current.Ink); Muted = Brush(Current.Muted); Line = Brush(Current.Line);
        Accent = Brush(Current.Accent);
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
