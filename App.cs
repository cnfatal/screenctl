using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;
using Microsoft.Win32;

namespace Screenctl;

public sealed class App : Application, IXamlMetadataProvider
{
    private readonly XamlControlsXamlMetaDataProvider metadata = new();
    private readonly CommunityToolkit.WinUI.Controls.SettingsControlsRns.CommunityToolkit_WinUI_Controls_SettingsControls_XamlTypeInfo.XamlMetaDataProvider settingsMetadata = new();
    private MainWindow window = null!;
    private static readonly CultureInfo systemLanguage = CultureInfo.CurrentUICulture;
    internal static string Language { get; private set; } = (string?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Screenctl", "Language", "") ?? "";

    [STAThread]
    public static void Main()
    {
        ApplyLanguage();
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }

    public IXamlType GetXamlType(Type type) => metadata.GetXamlType(type) ?? settingsMetadata.GetXamlType(type);
    public IXamlType GetXamlType(string name) => metadata.GetXamlType(name) ?? settingsMetadata.GetXamlType(name);
    public XmlnsDefinition[] GetXmlnsDefinitions() => metadata.GetXmlnsDefinitions().Concat(settingsMetadata.GetXmlnsDefinitions()).ToArray();

    private static void ApplyLanguage()
    {
        CultureInfo.CurrentUICulture = Language == "" ? systemLanguage : CultureInfo.GetCultureInfo(Language);
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;
        Strings.SetCulture(CultureInfo.CurrentUICulture);
    }

    internal void ChangeLanguage(string language)
    {
        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Screenctl", "Language", language);
        Language = language;
        ApplyLanguage();
        ((FrameworkElement)window.Content).Language = CultureInfo.CurrentUICulture.Name;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Resources.MergedDictionaries.Add(new XamlControlsResources());
        window = new MainWindow();
        window.Activate();
    }
}
