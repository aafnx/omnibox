using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace OmniHub.Platform;

public enum AudioFlow
{
    Output = 0,
    Input = 1,
}

public sealed record AudioDevice(
    string Id,
    string Name,
    AudioFlow Flow,
    bool DefaultConsole,
    bool DefaultMultimedia,
    bool DefaultCommunications,
    int? VolumePercent,
    bool? Muted);

/// <summary>
/// Звуковые устройства через Windows Core Audio (COM, IMMDeviceEnumerator).
/// COM-обёртки генерируются при сборке (GeneratedComInterface), без рефлексии: это работает в Native AOT.
/// Только чтение.
/// </summary>
public static partial class AudioDevices
{
    private const uint DeviceStateActive = 0x1;
    private const uint ClsctxAll = 0x17;
    private const uint StgmRead = 0;
    private const ushort VtLpwstr = 31;

    private static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidMmDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IidAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly PropertyKey FriendlyNameKey = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    private static readonly StrategyBasedComWrappers Wrappers = new();

    public static List<AudioDevice> Active()
    {
        int hr = CoInitializeEx(0, 0 /* COINIT_MULTITHREADED */);
        _ = hr; // S_FALSE или RPC_E_CHANGED_MODE — COM уже инициализирован, это нормально.

        var result = new List<AudioDevice>();
        Marshal.ThrowExceptionForHR(CoCreateInstance(in ClsidMmDeviceEnumerator, 0, ClsctxAll, in IidMmDeviceEnumerator, out nint ptr));
        var enumerator = Wrap<IMMDeviceEnumerator>(ptr);

        foreach (AudioFlow flow in new[] { AudioFlow.Output, AudioFlow.Input })
        {
            string? defConsole = DefaultId(enumerator, flow, 0);
            string? defMultimedia = DefaultId(enumerator, flow, 1);
            string? defCommunications = DefaultId(enumerator, flow, 2);

            if (enumerator.EnumAudioEndpoints((int)flow, DeviceStateActive, out IMMDeviceCollection collection) != 0)
            {
                continue;
            }
            if (collection.GetCount(out uint count) != 0)
            {
                continue;
            }
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out IMMDevice device) != 0)
                {
                    continue;
                }
                string? id = DeviceId(device);
                if (id is null)
                {
                    continue;
                }
                (int? volume, bool? muted) = Volume(device);
                result.Add(new AudioDevice(
                    id,
                    FriendlyName(device) ?? id,
                    flow,
                    id == defConsole,
                    id == defMultimedia,
                    id == defCommunications,
                    volume,
                    muted));
            }
        }
        return result;
    }

    private static string? DefaultId(IMMDeviceEnumerator enumerator, AudioFlow flow, int role) =>
        enumerator.GetDefaultAudioEndpoint((int)flow, role, out IMMDevice device) == 0 ? DeviceId(device) : null;

    private static string? DeviceId(IMMDevice device)
    {
        if (device.GetId(out nint p) != 0 || p == 0)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUni(p);
        }
        finally
        {
            Marshal.FreeCoTaskMem(p);
        }
    }

    private static string? FriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(StgmRead, out IPropertyStore store) != 0)
        {
            return null;
        }
        if (store.GetValue(in FriendlyNameKey, out PropVariant value) != 0)
        {
            return null;
        }
        try
        {
            return value.Type == VtLpwstr && value.Pointer != 0 ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static (int? Volume, bool? Muted) Volume(IMMDevice device)
    {
        if (device.Activate(in IidAudioEndpointVolume, ClsctxAll, 0, out nint ptr) != 0 || ptr == 0)
        {
            return (null, null);
        }
        var volume = Wrap<IAudioEndpointVolume>(ptr);
        int? percent = volume.GetMasterVolumeLevelScalar(out float level) == 0 ? (int)Math.Round(level * 100) : null;
        bool? muted = volume.GetMute(out int mute) == 0 ? mute != 0 : null;
        return (percent, muted);
    }

    private static T Wrap<T>(nint ptr)
    {
        try
        {
            return (T)Wrappers.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint coInit);

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint clsContext, in Guid iid, out nint instance);

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(ref PropVariant value);
}
