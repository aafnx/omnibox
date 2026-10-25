using System.Globalization;
using Microsoft.Win32;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class GameBarCollector
{
    public static void Collect(StatusSection s)
    {
        int? nexus = Win.ReadInt(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled");
        bool opens = nexus != 0;
        s.Add("guide", StatusTexts.GameBarGuide, opens ? StatusTexts.GameBarGuideOn : StatusTexts.GameBarGuideOff)
            .With("opensGameBar", opens)
            .With("registryValue", nexus);
    }
}

internal static class AudioCollector
{
    public static void Collect(StatusSection s)
    {
        List<AudioDevice> devices = AudioDevices.Active();
        Add(s, "outputs", StatusTexts.AudioOutputs, devices.Where(d => d.Flow == AudioFlow.Output));
        Add(s, "inputs", StatusTexts.AudioInputs, devices.Where(d => d.Flow == AudioFlow.Input));
    }

    private static void Add(StatusSection s, string id, string label, IEnumerable<AudioDevice> devices)
    {
        List<AudioDevice> list = devices.OrderByDescending(d => d.DefaultMultimedia).ThenBy(d => d.Name, StringComparer.CurrentCulture).ToList();
        if (list.Count == 0)
        {
            s.Add(id, label, StatusTexts.AudioNone, Level.Missing).With("count", 0);
            return;
        }
        AudioDevice? def = list.FirstOrDefault(d => d.DefaultMultimedia);
        var item = new StatusItem(id, label, def?.Name ?? StatusTexts.NotFound, def is null ? Level.Warn : Level.Ok)
            .With("count", list.Count)
            .With("default", def?.Id);
        foreach (AudioDevice d in list)
        {
            var parts = new List<string>();
            if (d.DefaultMultimedia)
            {
                parts.Add(StatusTexts.AudioDefault);
            }
            else if (d.DefaultCommunications)
            {
                parts.Add(StatusTexts.AudioDefaultComms);
            }
            if (d.VolumePercent is int v)
            {
                parts.Add($"{v}%");
            }
            if (d.Muted == true)
            {
                parts.Add(StatusTexts.AudioMuted);
            }
            item.Add(new StatusItem("device", d.Name, string.Join(" · ", parts), d.DefaultMultimedia ? Level.Ok : Level.Info)
                .With("id", d.Id)
                .With("name", d.Name)
                .With("defaultConsole", d.DefaultConsole)
                .With("defaultMultimedia", d.DefaultMultimedia)
                .With("defaultCommunications", d.DefaultCommunications)
                .With("volume", d.VolumePercent)
                .With("muted", d.Muted));
        }
        s.Add(item);
    }
}

internal static class DisplayCollector
{
    public static void Collect(StatusSection s, StatusContext ctx)
    {
        List<DisplayInfo> displays = Displays.Active();
        bool noDesktop = Win.SessionId() == 0 || Win.InSsh();
        if (displays.Count == 0 && noDesktop)
        {
            s.Add("count", StatusTexts.DisplaysCount, StatusTexts.DisplaysNotFromSsh, Level.Info)
                .With("count", 0)
                .With("visible", false);
        }
        else
        {
            s.Add("count", StatusTexts.DisplaysCount, StatusTexts.Count(displays.Count), displays.Count == 0 ? Level.Missing : Level.Info)
                .With("count", displays.Count)
                .With("visible", true);
        }

        int n = 0;
        foreach (DisplayInfo d in displays.OrderByDescending(d => d.Primary))
        {
            n++;
            string name = string.IsNullOrWhiteSpace(d.FriendlyName) ? $"{StatusTexts.DisplayUnnamed} {n}" : d.FriendlyName;
            var parts = new List<string>
            {
                $"{d.Width}×{d.Height}",
                $"{d.RefreshHz.ToString("0.##", ctx.Russian)} {StatusTexts.Hz}",
                d.Connector,
                d.HdrSupported ? (d.HdrEnabled ? StatusTexts.HdrOn : StatusTexts.HdrOff) : StatusTexts.HdrUnsupported,
            };
            if (d.BitsPerColor > 0)
            {
                parts.Add($"{d.BitsPerColor} {StatusTexts.Bits}");
            }
            if (d.Primary)
            {
                parts.Add(StatusTexts.DisplayPrimary);
            }
            s.Add("display", name, string.Join(" · ", parts), Level.Ok)
                .With("key", d.Key)
                .With("name", d.FriendlyName)
                .With("gdiName", d.GdiName)
                .With("connector", d.Connector)
                .With("manufacturer", d.EdidManufacturer)
                .With("product", d.EdidProduct)
                .With("width", d.Width)
                .With("height", d.Height)
                .With("refreshHz", d.RefreshHz)
                .With("primary", d.Primary)
                .With("hdrSupported", d.HdrSupported)
                .With("hdrEnabled", d.HdrEnabled)
                .With("bitsPerColor", d.BitsPerColor);
        }

        string? gpuPrefs = Win.ReadString(RegistryHive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings");
        bool? vrr = Vrr.Parse(gpuPrefs);
        s.Add("vrr", StatusTexts.Vrr, vrr switch { true => StatusTexts.VrrOn, false => StatusTexts.VrrOff, null => StatusTexts.VrrDefault })
            .With("enabled", vrr);
    }
}

public static class Vrr
{
    /// <summary>DirectXUserGlobalSettings: "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;". Null — не задано.</summary>
    public static bool? Parse(string? settings)
    {
        if (string.IsNullOrEmpty(settings))
        {
            return null;
        }
        foreach (string part in settings.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && kv[0].Equals("VRROptimizeEnable", StringComparison.OrdinalIgnoreCase))
            {
                return kv[1] == "1";
            }
        }
        return null;
    }
}

internal static class BluetoothCollector
{
    public static void Collect(StatusSection s)
    {
        List<BluetoothRadio> radios;
        try
        {
            radios = BluetoothDevices.Radios();
        }
        catch (DllNotFoundException)
        {
            s.Add("radio", StatusTexts.BluetoothRadio, StatusTexts.BluetoothUnavailable, Level.Missing).With("available", false);
            return;
        }
        if (radios.Count == 0)
        {
            s.Add("radio", StatusTexts.BluetoothRadio, StatusTexts.BluetoothNoRadio, Level.Missing).With("available", false);
            return;
        }
        foreach (BluetoothRadio r in radios)
        {
            s.Add("radio", StatusTexts.BluetoothRadio, r.Name, Level.Ok)
                .With("available", true)
                .With("name", r.Name)
                .With("address", BluetoothDevice.FormatAddress(r.Address));
        }

        List<BluetoothDevice> devices = BluetoothDevices.Known()
            .OrderByDescending(d => d.Connected)
            .ThenBy(d => d.Name, StringComparer.CurrentCulture)
            .ToList();
        int connected = devices.Count(d => d.Connected);
        var item = new StatusItem("devices", StatusTexts.BluetoothDevices,
                $"{StatusTexts.Count(devices.Count)}, {StatusTexts.BluetoothConnected}: {StatusTexts.Count(connected)}")
            .With("count", devices.Count)
            .With("connected", connected);
        foreach (BluetoothDevice d in devices)
        {
            string state = d.Connected ? StatusTexts.BluetoothConnected
                : d.Authenticated ? StatusTexts.BluetoothPaired
                : StatusTexts.BluetoothRemembered;
            string name = string.IsNullOrWhiteSpace(d.Name) ? d.AddressText : d.Name;
            item.Add(new StatusItem("device", name, $"{state} · {StatusTexts.BluetoothClass(d.MajorClass)}",
                    d.Connected ? Level.Ok : Level.Info)
                .With("name", d.Name)
                .With("address", d.AddressText)
                .With("classOfDevice", d.ClassOfDevice.ToString("X6", CultureInfo.InvariantCulture))
                .With("majorClass", d.MajorClass)
                .With("connected", d.Connected)
                .With("paired", d.Authenticated)
                .With("remembered", d.Remembered));
        }
        s.Add(item);
    }
}
