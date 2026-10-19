# Screenctl

Control your monitor's brightness, contrast, and other supported settings from Windows using DDC/CI.

## Get started

Screenctl runs on Windows 10 version 2004 or later on x64 PCs and requires a monitor that supports DDC/CI. Enable DDC/CI in your monitor's on-screen menu before using the app.

Download `Screenctl-<version>-win-x64.zip` from [Releases](https://github.com/cnfatal/screenctl/releases/latest), extract it, and open `Screenctl.exe`. The download includes the required runtimes; no separate .NET or Windows App SDK installation is needed. Keep the entire folder together when moving or copying the app.

To update, exit Screenctl and replace the application folder with the newly extracted version. To uninstall, exit the app and delete its folder. The saved language preference remains in `HKEY_CURRENT_USER\Software\Screenctl`.

## Quick controls

![Quick controls with brightness, contrast, color, audio, and input settings](docs/screenshots/quick-controls.png)

Select a display to see the quick controls it reports: brightness, contrast, image scaling, color presets, red/green/blue gain, volume, mute, and input source. Empty groups are hidden. Input selection is available for MCCS 2.0, 2.1, and 2.2 displays that report input options. Lists contain only the options reported by the display; unrecognized options show their hexadecimal codes.

Adjust a slider, or focus it and use the arrow keys. Expand **RGB gain** to adjust the red, green, and blue channels individually. The volume level and speaker button share one row; select the speaker button to toggle mute. Choose an input from the input source list to switch connections. Screenctl reads the initial settings when you select a display, sends your adjustments to that display, and shows any errors beside the affected control. Switching inputs may disconnect this computer from the display.

Sliders use the standard value tooltip while you adjust them. Volume has a mute button beside its slider. Protocol-specific values can be entered in advanced controls. Color presets show relative warmer/cooler levels when the display does not specify an absolute color-temperature tolerance.

Expand **Display information** below the display selector to view the model, current refresh rate, reported power-on hours, and MCCS version. Values can be selected and copied. The information is read when you select or refresh the display and is available on both control pages.

If capabilities cannot be read, quick controls show the error. Advanced controls remain available for entering VCP codes manually.

Use **Refresh displays** after connecting or disconnecting a monitor. Your selected display is kept if it is still connected. Screenctl also refreshes when Windows reports a display change or resumes from sleep.

## Advanced controls

![Advanced VCP controls with display information and a brightness reading](docs/screenshots/advanced-controls.png)

Enter a VCP code in hexadecimal without `0x`, such as `10` for brightness or `12` for contrast. The value field shows `—` until you read a value or enter one yourself.

- Select **Read value** to fill the field with the monitor's current value. Known continuous controls also show their reported maximum.
- To change a setting, enter a non-negative decimal integer in the same field and select **Write value**.
- Changing the code or selected display clears the previous value. Advanced values are only read when you request them.

Available codes and values depend on your monitor. The capabilities section shows the device report with fields and nested values on indented lines. Screenctl supports individual VCP values; MCCS table operations and proprietary protocols are not supported.

In advanced controls, factory reset, input switching, and power commands that may turn off the screen ask for confirmation. These commands can interrupt the connection, and some commands only support writing.

## Settings

Open **Settings** at the bottom of the navigation pane to follow your system language or select **English** or **Simplified Chinese**. Your choice is saved and applies immediately to the interface and tray menu without reopening the window or clearing your inputs. Numbers follow your Windows regional settings.

Settings also contains information about the app.

## Tray and window behavior

Click the tray icon to open the window. Its right-click menu contains **Open** and **Exit**.

- **Minimize** keeps the window in the taskbar.
- Closing the window, including with Alt+F4, exits Screenctl.

## Troubleshooting

- **No display or controls available:** Check that DDC/CI is enabled in the monitor's menu, then refresh displays. Support depends on the monitor and connection; adapters, docks, and KVM switches may block DDC/CI.
- **A setting cannot be read or changed:** The monitor may not support that VCP code or may restrict it in the current picture mode. Check its manual and on-screen settings.
- **Communication errors:** Check the display cable and refresh displays. If another monitor-control app is running, close it and try again.
