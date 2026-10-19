using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Screenctl.Strings;
using static Screenctl.Ui;

namespace Screenctl;

internal static class Views
{
    private static readonly (string Group, byte Code, string Key)[] controls =
    [
        ("Picture", 0x10, "Brightness"), ("Picture", 0x12, "Contrast"), ("Picture", 0x86, "Scaling"),
        ("Color", 0x14, "ColorPreset"), ("Color", 0x16, "RedGain"), ("Color", 0x18, "GreenGain"), ("Color", 0x1A, "BlueGain"),
        ("Audio", 0x62, "Volume"), ("Audio", 0x8D, "Mute"), ("Input", 0x60, "InputSource")
    ];

    private static bool IsContinuous(byte code, string version) =>
        code is 0x10 or 0x12 or 0x16 or 0x18 or 0x1A || code == 0x62 && version is "2.0" or "2.1";

    public static StackPanel Basic(Display display)
    {
        var panel = new StackPanel { Spacing = 24 };
        if (display.CapabilitiesError is string capabilityError)
        {
            panel.Children.Add(Label("CapabilitiesUnavailable", args: [capabilityError]));
            return panel;
        }
        foreach (var group in controls.Where(item => display.Features.Any(feature => feature.Code == item.Code)).GroupBy(item => item.Group))
        {
            var section = new StackPanel { Spacing = 4 };
            var rgb = new SettingsExpander { Name = "RgbGain" };
            Bind(rgb, SettingsExpander.HeaderProperty, "RgbGain");
            if (group.Key == "Audio") section.Children.Add(CreateAudio());
            else foreach (var item in group)
            {
                var feature = display.Features.First(feature => feature.Code == item.Code);
                if (item.Code is 0x16 or 0x18 or 0x1A) rgb.Items.Add(CreateSlider(item.Key, item.Code));
                else if (IsContinuous(item.Code, display.MccsVersion)) section.Children.Add(CreateSlider(item.Key, item.Code));
                else if (feature.Values.Length > 0 && (item.Code != 0x60 || display.MccsVersion is "2.0" or "2.1" or "2.2"))
                    section.Children.Add(CreateSelector(item.Key, feature));
            }
            if (rgb.Items.Count > 0) section.Children.Add(rgb);
            if (section.Children.Count == 0) continue;
            section.Children.Insert(0, Label(group.Key, "BodyStrongTextBlockStyle"));
            panel.Children.Add(section);
        }
        if (panel.Children.Count == 0) panel.Children.Add(Label("NoBasicControls"));
        return panel;

        SettingsCard CreateSlider(string key, byte code)
        {
            var slider = new Slider { Name = key, Width = 240, IsEnabled = false, StepFrequency = 1 };
            Bind(slider, AutomationProperties.NameProperty, key);
            var error = Text("");
            error.Visibility = Visibility.Collapsed;
            _ = Load();
            return ControlRow(key, slider, error);

            async Task Load()
            {
                try
                {
                    var feature = await Ddc.ReadAsync(display, code);
                    slider.Maximum = feature.Maximum;
                    slider.Value = feature.Current;
                    slider.IsEnabled = true;
                    slider.ValueChanged += async (_, args) =>
                    {
                        var command = new VcpCommand(display, code, (uint)args.NewValue);
                        error.Visibility = Visibility.Collapsed;
                        await Write(command, error);
                    };
                }
                catch (Win32Exception failure)
                {
                    Bind(error, TextBlock.TextProperty, "FeatureUnavailable", failure.Message);
                    error.Visibility = Visibility.Visible;
                }
            }
        }

        SettingsCard CreateSelector(string key, Vcp feature)
        {
            var selector = new ComboBox { Name = key, MaxWidth = 260, PlaceholderText = "—", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
            var caption = Text("");
            caption.Visibility = Visibility.Collapsed;
            Bind(selector, AutomationProperties.NameProperty, key);
            _ = Load();
            return ControlRow(key, selector, caption);

            async Task Load()
            {
                try
                {
                    var reading = await Ddc.ReadAsync(display, feature.Code);
                    foreach (var option in feature.Values)
                    {
                        var item = new ComboBoxItem { Tag = option };
                        SetOptionName(item, feature.Code, option, display.MccsVersion, reading);
                        selector.Items.Add(item);
                    }
                    selector.SelectedItem = selector.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (uint)item.Tag == (reading.Current & 0xFF));
                    selector.IsEnabled = true;
                    selector.SelectionChanged += async (_, _) =>
                    {
                        var selected = (uint)((ComboBoxItem)selector.SelectedItem!).Tag;
                        // MCCS 2.2 packs screen blanking into the high byte of Audio Mute.
                        var target = feature.Code == 0x8D ? (reading.Current & 0xFF00) | selected : selected;
                        caption.Visibility = Visibility.Collapsed;
                        await Write(new VcpCommand(display, feature.Code, target), caption);
                    };
                }
                catch (Win32Exception error)
                {
                    Bind(caption, TextBlock.TextProperty, "FeatureUnavailable", error.Message);
                    caption.Visibility = Visibility.Visible;
                }
            }
        }

        SettingsCard CreateAudio()
        {
            var inputs = new Grid
            {
                Width = 280,
                ColumnSpacing = 8,
                ColumnDefinitions = { new() { Width = GridLength.Auto }, new() }
            };
            var error = Text("");
            error.Visibility = Visibility.Collapsed;
            var volumeFeature = display.Features.FirstOrDefault(feature => feature.Code == 0x62);
            if (volumeFeature is not null)
            {
                var volume = new Slider { Name = "Volume", IsEnabled = false, StepFrequency = 1 };
                Bind(volume, AutomationProperties.NameProperty, "Volume");
                Grid.SetColumn(volume, 1);
                inputs.Children.Add(volume);
                _ = LoadVolume();

                async Task LoadVolume()
                {
                    try
                    {
                        var reading = await Ddc.ReadAsync(display, 0x62);
                        volume.Maximum = reading.Maximum;
                        volume.Value = reading.Current;
                        volume.IsEnabled = true;
                        volume.ValueChanged += async (_, args) =>
                        {
                            var command = new VcpCommand(display, 0x62, (uint)args.NewValue);
                            error.Visibility = Visibility.Collapsed;
                            await Write(command, error);
                        };
                    }
                    catch (Win32Exception failure) { ShowError(failure); }
                }
            }
            if (display.Features.FirstOrDefault(feature => feature.Code == 0x8D) is { } muteFeature &&
                muteFeature.Values.Contains(1u) && muteFeature.Values.Contains(2u))
            {
                var icon = new SymbolIcon(Symbol.Volume);
                var mute = new Button
                {
                    Name = "Mute",
                    Content = icon,
                    IsEnabled = false,
                    Style = (Style)Application.Current.Resources["SubtleButtonStyle"]
                };
                inputs.Children.Add(mute);
                _ = LoadMute();

                async Task LoadMute()
                {
                    try
                    {
                        var reading = await Ddc.ReadAsync(display, 0x8D);
                        var current = reading.Current;
                        Render();
                        mute.IsEnabled = true;
                        mute.Click += async (_, _) =>
                        {
                            var target = (current & 0xFF00) | ((current & 0xFF) == 1 ? 2u : 1u);
                            var command = new VcpCommand(display, 0x8D, target);
                            current = target;
                            Render();
                            error.Visibility = Visibility.Collapsed;
                            await Write(command, error);
                        };

                        void Render()
                        {
                            var muted = (current & 0xFF) == 1;
                            icon.Symbol = muted ? Symbol.Mute : Symbol.Volume;
                            var key = muted ? "Unmute" : "Mute";
                            Bind(mute, AutomationProperties.NameProperty, key);
                            ToolTipService.SetToolTip(mute, Label(key));
                        }
                    }
                    catch (Win32Exception failure) { ShowError(failure); }
                }
            }
            return ControlRow(volumeFeature is null ? "Mute" : "Volume", inputs, error);

            void ShowError(Win32Exception failure)
            {
                Bind(error, TextBlock.TextProperty, "FeatureUnavailable", failure.Message);
                error.Visibility = Visibility.Visible;
            }
        }

        async Task Write(VcpCommand command, TextBlock caption)
        {
            try { await Ddc.WriteAsync(command); }
            catch (Win32Exception error)
            {
                Bind(caption, TextBlock.TextProperty, "AdjustmentFailed", error.Message);
                caption.Visibility = Visibility.Visible;
            }
        }
    }

    private static SettingsCard ControlRow(string key, FrameworkElement control, TextBlock error)
    {
        var card = new SettingsCard { Content = control, Description = error };
        Bind(card, SettingsCard.HeaderProperty, key);
        return card;
    }

    public static SettingsExpander Information(Display display)
    {
        var properties = new Grid
        {
            Margin = new Thickness(16),
            ColumnSpacing = 24,
            RowSpacing = 12,
            ColumnDefinitions = { new() { Width = GridLength.Auto }, new() }
        };
        Add("Model", display.Name);
        foreach (var feature in display.Features.Where(feature => feature.Code is 0xAE or 0xC0))
        {
            var value = Add(feature.Code == 0xC0 ? "UsageTime" : "RefreshRate", "—");
            _ = Load();

            async Task Load()
            {
                try
                {
                    var reading = await Ddc.ReadAsync(display, feature.Code);
                    Bind(value, TextBlock.TextProperty, feature.Code == 0xC0 ? "HoursValue" : "HertzValue",
                        feature.Code == 0xC0 ? reading.Current : reading.Current / 100.0);
                }
                catch (Win32Exception error)
                {
                    Bind(value, TextBlock.TextProperty, "FeatureUnavailable", error.Message);
                }
            }
        }
        if (display.MccsVersion.Length > 0) Add("MccsVersion", display.MccsVersion);
        var panel = new SettingsExpander
        {
            Name = "DisplayInformation",
            HeaderIcon = new FontIcon { Glyph = "\uE7F4" },
            ItemsHeader = properties
        };
        Bind(panel, SettingsExpander.HeaderProperty, "DisplayInformation");
        return panel;

        TextBlock Add(string key, string text)
        {
            var label = Label(key);
            var value = Text(text);
            value.Name = key + "Value";
            value.IsTextSelectionEnabled = true;
            Grid.SetRow(label, properties.RowDefinitions.Count);
            Grid.SetRow(value, properties.RowDefinitions.Count);
            Grid.SetColumn(value, 1);
            properties.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            properties.Children.Add(label);
            properties.Children.Add(value);
            return value;
        }
    }

    private static void SetOptionName(ComboBoxItem item, byte code, uint option, string version, Feature reading)
    {
        var key = (code, option) switch
        {
            (0x14, 1) => "sRGB",
            (0x14, 2) => "NativeColor",
            (0x86, 1) => "NoScaling",
            (0x86, 2) => "FitImage",
            (0x86, 3) => "FitHeight",
            (0x86, 4) => "FitWidth",
            (0x86, 5) => "StretchHeight",
            (0x86, 6) => "StretchWidth",
            (0x86, 7) => "ExpandHorizontal",
            (0x86, 8) => "ExpandBoth",
            (0x86, 9) => "SqueezeImage",
            (0x86, 10) => "NonlinearScaling",
            (0x8D, 1) => "Muted",
            (0x8D, 2) => "Unmuted",
            _ => null
        };
        if (key is not null) Bind(item, ContentControl.ContentProperty, key);
        else if (code == 0x14 && option is >= 3 and <= 10)
        {
            if (version is "2.0" or "2.1" || (reading.Maximum & 0xFF00) != 0)
                item.Content = $"{new[] { 4000, 5000, 6500, 7500, 8200, 9300, 10000, 11500 }[option - 3]} K";
            else Bind(item, ContentControl.ContentProperty, option <= 6 ? "WarmerValue" : "CoolerValue", option <= 6 ? 7 - option : option - 6);
        }
        else if (code == 0x14 && option is >= 11 and <= 13) Bind(item, ContentControl.ContentProperty, "UserColor", option - 10);
        else item.Content = (code, option) switch
        {
            (0x60, 0x01 or 0x02) => $"VGA {option}",
            (0x60, 0x03 or 0x04) => $"DVI {option - 2}",
            (0x60, 0x0F or 0x10) => $"DisplayPort {option - 14}",
            (0x60, 0x11 or 0x12) => $"HDMI {option - 16}",
            _ => $"0x{option:X2}"
        };
    }

    public static ContentControl Advanced(Display display)
    {
        var panel = new ContentControl { Name = "AdvancedControls", HorizontalContentAlignment = HorizontalAlignment.Stretch };
        TextBox code = new() { Name = "Code", Text = "10", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        NumberBox value = new() { Name = "Value", PlaceholderText = "—", Minimum = 0, Maximum = uint.MaxValue, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        TextBlock error = new() { Name = "InputError", TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        TextBlock result = new() { Name = "FeatureResult", TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

        Bind(code, TextBox.HeaderProperty, "VcpCode");
        Bind(code, AutomationProperties.NameProperty, "VcpCode");
        Bind(value, NumberBox.HeaderProperty, "Value");
        Bind(value, AutomationProperties.NameProperty, "Value");
        error.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
        code.TextChanged += (_, _) => Clear();
        var write = ActionButton("Write", Write);
        write.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        var inputs = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new(), new() },
            Children = { code, value }
        };
        Grid.SetColumn(value, 1);
        var capabilities = new TextBox
        {
            Name = "Capabilities",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        if (display.CapabilitiesError is string capabilityError)
            Bind(capabilities, TextBox.TextProperty, "CapabilitiesUnavailable", capabilityError);
        else capabilities.Text = CapabilitiesText.Format(display.Capabilities);
        var expander = new Expander
        {
            Name = "CapabilitiesExpander",
            Content = capabilities,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        Bind(expander, Expander.HeaderProperty, "Capabilities");
        panel.Content = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                Card(new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        inputs,
                        new StackPanel { Spacing = 12, Orientation = Orientation.Horizontal, Children = { ActionButton("Read", Read), write } },
                        error,
                        result
                    }
                }),
                expander
            }
        };
        return panel;

        void Clear()
        {
            result.Visibility = error.Visibility = Visibility.Collapsed;
            value.Description = null;
            value.Value = double.NaN;
        }

        bool TryCode(out byte feature)
        {
            result.Visibility = error.Visibility = Visibility.Collapsed;
            if (byte.TryParse(code.Text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out feature)) return true;
            Bind(error, TextBlock.TextProperty, "InvalidCode");
            error.Visibility = Visibility.Visible;
            code.Focus(FocusState.Programmatic);
            return false;
        }

        async void Read(object sender, RoutedEventArgs args)
        {
            if (!TryCode(out var feature)) return;
            Clear();
            await Run(async () =>
            {
                var reading = await Ddc.ReadAsync(display, feature);
                if (IsContinuous(feature, display.MccsVersion))
                    value.Description = Label("ReportedMaximum", "CaptionTextBlockStyle", reading.Maximum);
                value.Value = reading.Current;
            });
        }

        async void Write(object sender, RoutedEventArgs args)
        {
            if (!TryCode(out var feature)) return;
            if (!uint.TryParse(value.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var target))
            {
                Bind(error, TextBlock.TextProperty, "InvalidValue");
                error.Visibility = Visibility.Visible;
                value.Focus(FocusState.Programmatic);
                return;
            }
            var command = new VcpCommand(display, feature, target);
            await Run(async () =>
            {
                var consequence = feature switch
                {
                    0x04 => T("ResetConsequence"),
                    0x60 => T("InputConsequence"),
                    0xD6 when target != 1 => T("PowerConsequence"),
                    _ => null
                };
                if (consequence is not null)
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = panel.XamlRoot,
                        Title = T("ConfirmWrite"),
                        Content = T("WriteConsequence", display.Name, feature, target, consequence),
                        PrimaryButtonText = T("Write"),
                        CloseButtonText = T("Cancel"),
                        DefaultButton = ContentDialogButton.Close
                    };
                    if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                }
                await Ddc.WriteAsync(command);
                Bind(result, TextBlock.TextProperty, "FeatureWritten", feature, target);
                result.Visibility = Visibility.Visible;
            });
        }

        async Task Run(Func<Task> operation)
        {
            panel.IsEnabled = false;
            try { await operation(); }
            catch (Win32Exception failure)
            {
                error.Text = failure.Message;
                error.Visibility = Visibility.Visible;
            }
            finally { panel.IsEnabled = true; }
        }
    }

    public static StackPanel Settings()
    {
        var language = new ComboBox { Name = "Language", MinWidth = 240 };
        Bind(language, AutomationProperties.NameProperty, "Language");
        var system = new ComboBoxItem { Tag = "" };
        Bind(system, ContentControl.ContentProperty, "SystemLanguage");
        language.Items.Add(system);
        language.Items.Add(new ComboBoxItem { Content = "简体中文", Tag = "zh-CN" });
        language.Items.Add(new ComboBoxItem { Content = "English", Tag = "en-US" });
        language.SelectedIndex = App.Language == "" ? 0 : App.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        language.SelectionChanged += (_, _) => ((App)Application.Current).ChangeLanguage((string)((ComboBoxItem)language.SelectedItem).Tag);
        return new StackPanel
        {
            Name = "SettingsPage",
            Spacing = 24,
            Children =
            {
                new SettingsCard { Header = Label("Language"), Content = language },
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Label("About", "BodyStrongTextBlockStyle"),
                        new SettingsCard
                        {
                            Header = "Screenctl", Description = Label("AboutDescription"),
                            HeaderIcon = new BitmapIcon { UriSource = new Uri("ms-appx:///Assets/Screenctl.png"), ShowAsMonochrome = false },
                            Content = Label("Version", "CaptionTextBlockStyle", typeof(App).Assembly.GetName().Version!.ToString(3))
                        }
                    }
                }
            }
        };
    }

    public static (FrameworkElement View, Func<Task> Reload) Displays(Action<Display?> select)
    {
        ComboBox Displays = new() { Name = "Displays", DisplayMemberPath = "Name", HorizontalAlignment = HorizontalAlignment.Stretch };
        InfoBar Notice = new() { Name = "Notice" };
        ProgressRing Progress = new() { Width = 20, Height = 20, IsActive = false, Opacity = 0 };
        var refresh = new Button { Name = "RefreshButton", MinWidth = 120, VerticalAlignment = VerticalAlignment.Center };

        Bind(Displays, ComboBox.PlaceholderTextProperty, "SelectDisplay");
        Bind(Displays, AutomationProperties.NameProperty, "Display");
        Bind(Progress, AutomationProperties.NameProperty, "Communicating");
        Displays.SelectionChanged += Selected;
        Bind(refresh, ContentControl.ContentProperty, "Refresh");
        refresh.Click += async (_, _) => await Reload();
        var selector = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new(), new() { Width = GridLength.Auto }, new() { Width = GridLength.Auto } },
            Children = { Displays, Progress, refresh }
        };
        Grid.SetColumn(Progress, 1);
        Grid.SetColumn(refresh, 2);
        return (new StackPanel { Children = { selector, Notice } }, Reload);

        async Task Reload()
        {
            refresh.IsEnabled = false;
            Progress.IsActive = true;
            Progress.Opacity = 1;
            Notice.IsOpen = false;
            try
            {
                var selected = ((Display?)Displays.SelectedItem)?.Id;
                var displays = await Ddc.DiscoverAsync();
                Displays.SelectionChanged -= Selected;
                Displays.ItemsSource = displays;
                Displays.SelectedItem = displays.FirstOrDefault(display => display.Id == selected) ?? displays.FirstOrDefault();
                Displays.SelectionChanged += Selected;
                select((Display?)Displays.SelectedItem);
                if (displays.Length == 0)
                {
                    Notice.Title = "";
                    Bind(Notice, InfoBar.MessageProperty, "NoDisplays");
                    Notice.Severity = InfoBarSeverity.Informational;
                    Notice.IsOpen = true;
                }
            }
            catch (Win32Exception error)
            {
                Bind(Notice, InfoBar.TitleProperty, "CommunicationError");
                Notice.Message = error.Message;
                Notice.Severity = InfoBarSeverity.Error;
                Notice.IsOpen = true;
            }
            finally
            {
                refresh.IsEnabled = true;
                Progress.IsActive = false;
                Progress.Opacity = 0;
            }
        }

        void Selected(object sender, SelectionChangedEventArgs args) => select((Display?)Displays.SelectedItem);
    }
}
