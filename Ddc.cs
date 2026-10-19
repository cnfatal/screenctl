using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Screenctl;

internal sealed record Display(string Id, string Name, string Capabilities, string MccsVersion, Vcp[] Features, string? CapabilitiesError);
internal sealed record Vcp(byte Code, uint[] Values);
internal readonly record struct Feature(uint Current, uint Maximum);
internal readonly record struct VcpCommand(Display Display, byte Code, uint Value);

internal static class Ddc
{
    /// <summary>Discovers connected physical displays and their reported capabilities.</summary>
    public static Task<Display[]> DiscoverAsync() => Task.Run(() =>
    {
        var displays = new List<Display>();
        Visit((id, handle, description) =>
        {
            var capabilities = "";
            string? capabilityError = null;
            try
            {
                Check(GetCapabilitiesStringLength(handle, out var length));
                var text = new StringBuilder((int)length);
                Check(CapabilitiesRequestAndCapabilitiesReply(handle, text, length));
                capabilities = text.ToString();
            }
            catch (Win32Exception error)
            {
                capabilityError = error.Message;
            }

            var model = Group(capabilities, "model");
            var name = model.Length == 0 ? description : model;
            var features = ParseFeatures(Group(capabilities, "vcp"));
            displays.Add(new Display(id, name, capabilities, Group(capabilities, "mccs_ver"), features, capabilityError));
        });
        return displays.ToArray();
    });

    /// <summary>Reads one VCP feature.</summary>
    public static Task<Feature> ReadAsync(Display display, byte code) =>
        Task.Run(() => Use(display, handle => Read(handle, code)));

    /// <summary>Writes one VCP command without an implicit readback.</summary>
    public static Task WriteAsync(VcpCommand command) => Task.Run(() => Use(command.Display, handle =>
    {
        Check(SetVCPFeature(handle, command.Code, command.Value));
        return true;
    }));

    private static Vcp[] ParseFeatures(string text) =>
        Regex.Matches(text, @"([^\s()]+)(?:\(([^()]*)\))?")
            .Where(match => match.Groups[1].Length == 2)
            .Select(match => new Vcp(
                byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                match.Groups[2].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray()))
            .ToArray();

    private static string Group(string text, string name)
    {
        var start = text.IndexOf(name + "(", StringComparison.Ordinal);
        if (start < 0) return "";
        start += name.Length + 1;
        var depth = 1;
        var end = start;
        for (; depth != 0; end++)
        {
            if (text[end] == '(') depth++;
            if (text[end] == ')') depth--;
        }
        return text[start..(end - 1)];
    }

    private static Feature Read(nint handle, byte code)
    {
        Check(GetVCPFeatureAndVCPFeatureReply(handle, code, out _, out var current, out var maximum));
        return new Feature(current, maximum);
    }

    private static TResult Use<TResult>(Display display, Func<nint, TResult> action)
    {
        TResult result = default!;
        var found = false;
        Visit((id, handle, _) =>
        {
            if (id != display.Id) return;
            result = action(handle);
            found = true;
        });
        return found ? result : throw new Win32Exception(1167);
    }

    private static void Visit(Action<string, nint, string> action)
    {
        var monitors = new List<nint>();
        Check(EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            monitors.Add(monitor);
            return true;
        }, 0));

        foreach (var monitor in monitors)
        {
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            Check(GetMonitorInfo(monitor, ref info));
            Check(GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count));
            var physical = new PhysicalMonitor[count];
            Check(GetPhysicalMonitorsFromHMONITOR(monitor, count, physical));
            try
            {
                for (var i = 0; i < physical.Length; i++)
                    action($"{info.Device}:{i}", physical[i].Handle, physical[i].Description);
            }
            finally
            {
                DestroyPhysicalMonitors(count, physical);
            }
        }
    }

    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public nint Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    private delegate bool MonitorCallback(nint monitor, nint dc, nint rect, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(nint dc, nint rect, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, [Out] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint count, PhysicalMonitor[] physical);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetCapabilitiesStringLength(nint monitor, out uint length);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool CapabilitiesRequestAndCapabilitiesReply(nint monitor, StringBuilder capabilities, uint length);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(nint monitor, byte code, out uint type, out uint current, out uint maximum);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(nint monitor, byte code, uint value);
}
