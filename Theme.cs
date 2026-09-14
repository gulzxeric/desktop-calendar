using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopTodo;

public static class Theme
{
    public static Brush Bg = Brush("#202A37"), Panel = Brush("#283544"), Cell = Brush("#2D3B4C"), Ink = Brush("#F2F6FA"), Muted = Brush("#AFBED0"), Line = Brush("#415065"), Accent = Brush("#A8D9EF");
    public static readonly string[] Colors = { "#A8D9EF", "#B8D7AD", "#EEC681", "#D7B4E8", "#ECAFA9" };
    public static readonly string[] ColorNames = { "日常", "学习", "工作", "生活", "重要" };
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    public static void Set(bool light)
    {
        Bg = Brush(light ? "#EEF3F7" : "#202A37"); Panel = Brush(light ? "#E1EAF1" : "#283544");
        Cell = Brush(light ? "#F8FAFC" : "#2D3B4C"); Ink = Brush(light ? "#263647" : "#F2F6FA");
        Muted = Brush(light ? "#54677D" : "#AFBED0"); Line = Brush(light ? "#BECCD8" : "#415065");
        Accent = Brush(light ? "#27698A" : "#A8D9EF");
    }
    public static TextBlock Text(string text, double size = 14, Brush? color = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Ink,
        FontFamily = new FontFamily("Microsoft YaHei UI"), VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    public static Button Button(string text, RoutedEventHandler click, bool primary = false)
    {
        var b = new Button { Content = text, FontSize = 13, FontFamily = new FontFamily("Microsoft YaHei UI"),
            Foreground = primary ? Brush("#203344") : Ink, Background = primary ? Brush("#A8D9EF") : Cell,
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
    public static Window Dialog(string title, double width, double height) => new()
    {
        Title = title, Width = width, Height = height, Background = Bg, Foreground = Ink,
        FontFamily = new FontFamily("Microsoft YaHei UI"), WindowStartupLocation = WindowStartupLocation.CenterScreen,
        ResizeMode = ResizeMode.NoResize, ShowInTaskbar = true
    };
}
