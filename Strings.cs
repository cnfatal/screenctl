using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Screenctl;

public sealed class Strings : INotifyPropertyChanged, IValueConverter
{
    private static readonly ResourceManager resources = new("Screenctl.Strings", typeof(Strings).Assembly);
    private static readonly Strings instance = new();
    private CultureInfo culture = CultureInfo.CurrentUICulture;

    private Strings() { }

    public string this[string key] => resources.GetString(key, culture)!;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string T(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, instance[key], args);

    public static void SetCulture(CultureInfo culture)
    {
        instance.culture = culture;
        instance.PropertyChanged?.Invoke(instance, new("Item[]"));
    }

    public static void Bind(FrameworkElement control, DependencyProperty property, string key, params object[] args) =>
        control.SetBinding(property, new Binding
        {
            Source = instance,
            Path = new PropertyPath($"[{key}]"),
            Mode = BindingMode.OneWay,
            Converter = instance,
            ConverterParameter = args
        });

    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.Format(CultureInfo.CurrentCulture, (string)value, (object[])parameter);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
