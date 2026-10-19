using System;
using System.Drawing;
using System.IO;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Message = System.Windows.Forms.Message;
using NativeWindow = System.Windows.Forms.NativeWindow;
using static Screenctl.Strings;

namespace Screenctl;

internal sealed class Tray : NativeWindow, IDisposable
{
    private readonly TaskbarIcon icon;
    private readonly Action refresh;

    public Tray(nint window, Action open, Action refresh, Action close)
    {
        this.refresh = refresh;
        AssignHandle(window);
        var openItem = MenuItem("Open", Symbol.OpenPane, open);
        var menu = new MenuFlyout
        {
            Items =
            {
                openItem,
                MenuItem("Exit", Symbol.Cancel, close)
            }
        };
        icon = new TaskbarIcon
        {
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "Screenctl.ico")),
            ContextFlyout = menu,
            ContextMenuMode = ContextMenuMode.SecondWindow,
            LeftClickCommand = openItem.Command,
            NoLeftClickDelay = true
        };
        Bind(icon, TaskbarIcon.ToolTipTextProperty, "TrayTitle");
        icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private static MenuFlyoutItem MenuItem(string key, Symbol symbol, Action action)
    {
        var command = new XamlUICommand();
        command.ExecuteRequested += (_, _) => action();
        var item = new MenuFlyoutItem { Icon = new SymbolIcon(symbol), Command = command };
        Bind(item, MenuFlyoutItem.TextProperty, key);
        return item;
    }

    protected override void WndProc(ref Message message)
    {
        // Windows broadcasts display changes and resume events to top-level windows.
        if (message.Msg == 0x007E || message.Msg == 0x0218 && message.WParam == 0x0012)
            refresh();
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        icon.Dispose();
        ReleaseHandle();
    }
}
