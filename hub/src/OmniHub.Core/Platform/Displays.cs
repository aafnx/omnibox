using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OmniHub.Platform;

public sealed record DisplayInfo(
    string Key,
    string FriendlyName,
    string GdiName,
    string Connector,
    string? EdidManufacturer,
    int EdidProduct,
    int Width,
    int Height,
    double RefreshHz,
    bool Primary,
    bool HdrSupported,
    bool HdrEnabled,
    int BitsPerColor);

/// <summary>Активные экраны через Windows DisplayConfig: имя, разъём, разрешение, частота, HDR. Только чтение.</summary>
public static unsafe partial class Displays
{
    private const uint QdcOnlyActivePaths = 0x2;
    private const uint ModeInfoTypeSource = 1;
    private const uint InvalidModeIndex = 0xFFFFFFFF;
    private const uint GetSourceName = 1;
    private const uint GetTargetName = 2;
    private const uint GetAdvancedColorInfo = 9;
    private const uint GetAdvancedColorInfo2 = 15;

    public static List<DisplayInfo> Active()
    {
        var result = new List<DisplayInfo>();
        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount) != 0 || pathCount == 0)
        {
            return result;
        }
        var paths = new PathInfo[pathCount];
        var modes = new ModeInfo[modeCount];
        fixed (PathInfo* p = paths)
        fixed (ModeInfo* m = modes)
        {
            if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, p, ref modeCount, m, 0) != 0)
            {
                return result;
            }
        }

        for (int i = 0; i < pathCount; i++)
        {
            PathInfo path = paths[i];
            int width = 0, height = 0;
            bool primary = false;
            uint sourceIndex = path.Source.ModeInfoIdx;
            if (sourceIndex != InvalidModeIndex && sourceIndex < modeCount && modes[sourceIndex].InfoType == ModeInfoTypeSource)
            {
                ModeInfo mode = modes[sourceIndex];
                width = (int)mode.SourceWidth;
                height = (int)mode.SourceHeight;
                primary = mode.PositionX == 0 && mode.PositionY == 0;
            }

            var target = new TargetDeviceName();
            target.Header = Header(GetTargetName, (uint)sizeof(TargetDeviceName), path.Target.AdapterId, path.Target.Id);
            string friendly = string.Empty;
            string? manufacturer = null;
            int product = 0;
            uint technology = path.Target.OutputTechnology;
            if (DisplayConfigGetDeviceInfo(&target) == 0)
            {
                friendly = Chars(target.MonitorFriendlyDeviceName);
                technology = target.OutputTechnology;
                if ((target.Flags & 0x2) != 0)
                {
                    // flags.edidIdsValid
                    manufacturer = DecodeEdidManufacturer(target.EdidManufactureId);
                    product = target.EdidProductCodeId;
                }
            }

            var source = new SourceDeviceName();
            source.Header = Header(GetSourceName, (uint)sizeof(SourceDeviceName), path.Source.AdapterId, path.Source.Id);
            string gdi = DisplayConfigGetDeviceInfo(&source) == 0 ? Chars(source.ViewGdiDeviceName) : string.Empty;

            (bool hdrSupported, bool hdrEnabled, int bits) = AdvancedColor(path.Target.AdapterId, path.Target.Id);

            double hz = path.Target.RefreshRate.Denominator == 0
                ? 0
                : (double)path.Target.RefreshRate.Numerator / path.Target.RefreshRate.Denominator;

            result.Add(new DisplayInfo(
                Key: $"{path.Target.AdapterId.HighPart:X}-{path.Target.AdapterId.LowPart:X}-{path.Target.Id}",
                FriendlyName: friendly,
                GdiName: gdi,
                Connector: ConnectorName(technology),
                EdidManufacturer: manufacturer,
                EdidProduct: product,
                Width: width,
                Height: height,
                RefreshHz: Math.Round(hz, 2),
                Primary: primary,
                HdrSupported: hdrSupported,
                HdrEnabled: hdrEnabled,
                BitsPerColor: bits));
        }
        return result;
    }

    private static (bool Supported, bool Enabled, int Bits) AdvancedColor(Luid adapter, uint id)
    {
        // Windows 11 24H2+: новый запрос, в нём HDR отделён от управления цветом (ACM).
        var info2 = new AdvancedColorInfo2();
        info2.Header = Header(GetAdvancedColorInfo2, (uint)sizeof(AdvancedColorInfo2), adapter, id);
        if (DisplayConfigGetDeviceInfo(&info2) == 0)
        {
            return ((info2.Value & 0x10) != 0, info2.ActiveColorMode == 2, (int)info2.BitsPerColorChannel);
        }

        var info = new AdvancedColorInfo();
        info.Header = Header(GetAdvancedColorInfo, (uint)sizeof(AdvancedColorInfo), adapter, id);
        if (DisplayConfigGetDeviceInfo(&info) == 0)
        {
            return ((info.Value & 0x1) != 0, (info.Value & 0x2) != 0, (int)info.BitsPerColorChannel);
        }
        return (false, false, 0);
    }

    /// <summary>Код производителя из EDID: три буквы по 5 бит, байты в обратном порядке (LG = «GSM»).</summary>
    public static string DecodeEdidManufacturer(ushort raw)
    {
        int id = ((raw & 0xFF) << 8) | (raw >> 8);
        char a = (char)('@' + ((id >> 10) & 0x1F));
        char b = (char)('@' + ((id >> 5) & 0x1F));
        char c = (char)('@' + (id & 0x1F));
        return new string([a, b, c]);
    }

    public static string ConnectorName(uint technology) => technology switch
    {
        0 => "VGA",
        4 => "DVI",
        5 => "HDMI",
        6 => "LVDS",
        10 => "DisplayPort",
        11 => "DisplayPort (встроенный)",
        15 => "Miracast",
        16 or 17 => "виртуальный",
        18 => "DisplayPort через USB",
        0x80000000 => "встроенный",
        _ => "другой",
    };

    private static DeviceInfoHeader Header(uint type, uint size, Luid adapter, uint id) =>
        new() { Type = type, Size = size, AdapterId = adapter, Id = id };

    private static string Chars(ReadOnlySpan<char> span)
    {
        int end = span.IndexOf('\0');
        return new string(end < 0 ? span : span[..end]);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public Rational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathInfo
    {
        public PathSourceInfo Source;
        public PathTargetInfo Target;
        public uint Flags;
    }

    /// <summary>DISPLAYCONFIG_MODE_INFO, 64 байта; из объединения нужен только режим источника.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModeInfo
    {
        [FieldOffset(0)] public uint InfoType;
        [FieldOffset(4)] public uint Id;
        [FieldOffset(8)] public Luid AdapterId;
        [FieldOffset(16)] public uint SourceWidth;
        [FieldOffset(20)] public uint SourceHeight;
        [FieldOffset(24)] public uint SourcePixelFormat;
        [FieldOffset(28)] public int PositionX;
        [FieldOffset(32)] public int PositionY;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [InlineArray(32)]
    internal struct Chars32
    {
        private char _element;
    }

    [InlineArray(64)]
    internal struct Chars64
    {
        private char _element;
    }

    [InlineArray(128)]
    internal struct Chars128
    {
        private char _element;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        public Chars64 MonitorFriendlyDeviceName;
        public Chars128 MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        public Chars32 ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvancedColorInfo
    {
        public DeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvancedColorInfo2
    {
        public DeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;
        public uint ActiveColorMode;
    }

    internal static int SizeOfPathInfo => sizeof(PathInfo);
    internal static int SizeOfModeInfo => sizeof(ModeInfo);
    internal static int SizeOfTargetDeviceName => sizeof(TargetDeviceName);
    internal static int SizeOfSourceDeviceName => sizeof(SourceDeviceName);

    [LibraryImport("user32.dll")]
    private static partial int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [LibraryImport("user32.dll")]
    private static partial int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, PathInfo* pathArray,
        ref uint numModeInfoArrayElements, ModeInfo* modeInfoArray, nint currentTopologyId);

    [LibraryImport("user32.dll")]
    private static partial int DisplayConfigGetDeviceInfo(void* requestPacket);
}
