using System;
using System.Globalization;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using static Screenctl.Strings;
using static Screenctl.Ui;

namespace Screenctl;

internal sealed class MainWindow : Window
{
    private readonly Tray tray;
    private readonly TextBlock heading = Label("Quick", "TitleTextBlockStyle");
    private readonly ContentControl basic = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl information = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl advanced = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
    private readonly FrameworkElement settings = Views.Settings();
    private readonly StackPanel monitor = new() { Spacing = 16 };

    public MainWindow()
    {
        Title = "Screenctl";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Screenctl.ico"));
        SystemBackdrop = new MicaBackdrop();
        var displays = Views.Displays(SelectDisplay);
        settings.Visibility = Visibility.Collapsed;
        monitor.Children.Add(displays.View);
        monitor.Children.Add(information);
        monitor.Children.Add(new Grid { Children = { basic, advanced } });
        var navigation = new NavigationView
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            OpenPaneLength = 192,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = true,
            IsPaneToggleButtonVisible = false,
            MenuItems =
            {
                new NavigationViewItem { Tag = "Quick", Icon = new FontIcon { Glyph = "\uE706" } },
                new NavigationViewItem { Tag = "Advanced", Icon = new FontIcon { Glyph = "\uE713" } }
            },
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Padding = new Thickness(32, 24, 32, 32),
                    Spacing = 24,
                    MaxWidth = 1040,
                    Children = { heading, monitor, settings }
                }
            }
        };
        foreach (NavigationViewItem item in navigation.MenuItems) Bind(item, ContentControl.ContentProperty, (string)item.Tag);
        navigation.SelectionChanged += Navigate;
        navigation.SelectedItem = navigation.MenuItems[0];
        navigation.Loaded += (_, _) =>
        {
            Bind((NavigationViewItem)navigation.SettingsItem, ContentControl.ContentProperty, "Settings");
            Bind((NavigationViewItem)navigation.SettingsItem, AutomationProperties.NameProperty, "Settings");
        };
        var titleBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(20, 0, 140, 0),
            Children = { Icon(20), Text("Screenctl", "BodyStrongTextBlockStyle") }
        };
        Content = new Grid
        {
            Language = CultureInfo.CurrentUICulture.Name,
            RowDefinitions = { new() { Height = new GridLength(48) }, new() },
            Children = { titleBar, navigation }
        };
        Grid.SetRow(navigation, 1);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);
        AppWindow.Resize(new SizeInt32(1280, 900));
        tray = new Tray(WinRT.Interop.WindowNative.GetWindowHandle(this), () =>
        {
            AppWindow.Show();
            ((OverlappedPresenter)AppWindow.Presenter).Restore();
            Activate();
        }, () => DispatcherQueue.TryEnqueue(() => _ = displays.Reload()), Close);
        Closed += (_, _) => tray.Dispose();
        _ = displays.Reload();
    }

    private void SelectDisplay(Display? display)
    {
        information.Content = display is null ? null : Views.Information(display);
        basic.Content = display is null ? null : Views.Basic(display);
        advanced.Content = display is null ? null : Views.Advanced(display);
    }

    private void Navigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var page = args.IsSettingsSelected ? "Settings" : (string)((NavigationViewItem)args.SelectedItem).Tag;
        monitor.Visibility = page == "Settings" ? Visibility.Collapsed : Visibility.Visible;
        settings.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        basic.Visibility = page == "Quick" ? Visibility.Visible : Visibility.Collapsed;
        advanced.Visibility = page == "Advanced" ? Visibility.Visible : Visibility.Collapsed;
        Bind(heading, TextBlock.TextProperty, page);
    }
}
