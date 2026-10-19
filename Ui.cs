using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using static Screenctl.Strings;

namespace Screenctl;

internal static class Ui
{
    public static TextBlock Text(string text, string style = "BodyTextBlockStyle") => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        Style = (Style)Application.Current.Resources[style]
    };

    public static TextBlock Label(string key, string style = "BodyTextBlockStyle", params object[] args)
    {
        var text = Text("", style);
        Bind(text, TextBlock.TextProperty, key, args);
        return text;
    }

    public static Border Card(UIElement content) => new()
    {
        Child = content,
        Padding = new Thickness(16),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
    };

    public static Button ActionButton(string key, RoutedEventHandler action)
    {
        var button = new Button { MinWidth = 120, VerticalAlignment = VerticalAlignment.Center };
        Bind(button, ContentControl.ContentProperty, key);
        button.Click += action;
        return button;
    }

    public static Image Icon(double size) => new()
    {
        Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Screenctl.png"))),
        Width = size,
        Height = size,
        HorizontalAlignment = HorizontalAlignment.Left
    };
}
