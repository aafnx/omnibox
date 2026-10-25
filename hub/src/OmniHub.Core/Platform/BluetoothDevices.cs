using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OmniHub.Platform;

public sealed record BluetoothRadio(string Name, ulong Address);

public sealed record BluetoothDevice(
    string Name,
    ulong Address,
    uint ClassOfDevice,
    bool Connected,
    bool Remembered,
    bool Authenticated)
{
    public string AddressText => FormatAddress(Address);

    /// <summary>Основной класс устройства (биты 8–12 Class of Device): 4 — аудио/видео, 5 — периферия.</summary>
    public int MajorClass => (int)((ClassOfDevice >> 8) & 0x1F);

    public static string FormatAddress(ulong address) =>
        string.Join(':', Enumerable.Range(0, 6).Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2")));
}

/// <summary>
/// Классический Bluetooth через Win32 (bthprops): адаптеры и запомненные устройства.
/// Без поиска новых устройств (fIssueInquiry = false), поэтому быстро и ничего не меняет.
/// </summary>
public static unsafe partial class BluetoothDevices
{
    public static List<BluetoothRadio> Radios()
    {
        var result = new List<BluetoothRadio>();
        var parameters = new FindRadioParams { Size = (uint)sizeof(FindRadioParams) };
        nint find = BluetoothFindFirstRadio(&parameters, out nint radio);
        if (find == 0)
        {
            return result;
        }
        try
        {
            do
            {
                var info = new RadioInfo { Size = (uint)sizeof(RadioInfo) };
                if (BluetoothGetRadioInfo(radio, &info) == 0)
                {
                    result.Add(new BluetoothRadio(Chars(info.Name), info.Address));
                }
                CloseHandle(radio);
            }
            while (BluetoothFindNextRadio(find, out radio) != 0);
        }
        finally
        {
            BluetoothFindRadioClose(find);
        }
        return result;
    }

    /// <summary>Сопряжённые и запомненные устройства, с отметкой «подключено сейчас».</summary>
    public static List<BluetoothDevice> Known()
    {
        var result = new List<BluetoothDevice>();
        var search = new SearchParams
        {
            Size = (uint)sizeof(SearchParams),
            ReturnAuthenticated = 1,
            ReturnRemembered = 1,
            ReturnUnknown = 0,
            ReturnConnected = 1,
            IssueInquiry = 0,
            TimeoutMultiplier = 0,
            Radio = 0,
        };
        var info = new DeviceInfo { Size = (uint)sizeof(DeviceInfo) };
        nint find = BluetoothFindFirstDevice(&search, &info);
        if (find == 0)
        {
            return result;
        }
        try
        {
            do
            {
                result.Add(new BluetoothDevice(
                    Chars(info.Name),
                    info.Address,
                    info.ClassOfDevice,
                    info.Connected != 0,
                    info.Remembered != 0,
                    info.Authenticated != 0));
                info = new DeviceInfo { Size = (uint)sizeof(DeviceInfo) };
            }
            while (BluetoothFindNextDevice(find, &info) != 0);
        }
        finally
        {
            BluetoothFindDeviceClose(find);
        }
        return result;
    }

    private static string Chars(ReadOnlySpan<char> span)
    {
        int end = span.IndexOf('\0');
        return new string(end < 0 ? span : span[..end]);
    }

    [InlineArray(248)]
    internal struct Chars248
    {
        private char _element;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    /// <summary>BLUETOOTH_DEVICE_SEARCH_PARAMS, 40 байт на x64.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SearchParams
    {
        public uint Size;
        public int ReturnAuthenticated;
        public int ReturnRemembered;
        public int ReturnUnknown;
        public int ReturnConnected;
        public int IssueInquiry;
        public byte TimeoutMultiplier;
        public nint Radio;
    }

    /// <summary>BLUETOOTH_DEVICE_INFO, 560 байт на x64.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfo
    {
        public uint Size;
        public ulong Address;
        public uint ClassOfDevice;
        public int Connected;
        public int Remembered;
        public int Authenticated;
        public SystemTime LastSeen;
        public SystemTime LastUsed;
        public Chars248 Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FindRadioParams
    {
        public uint Size;
    }

    /// <summary>BLUETOOTH_RADIO_INFO, 520 байт на x64.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RadioInfo
    {
        public uint Size;
        public ulong Address;
        public Chars248 Name;
        public uint ClassOfDevice;
        public ushort LmpSubversion;
        public ushort Manufacturer;
    }

    internal static int SizeOfSearchParams => sizeof(SearchParams);
    internal static int SizeOfDeviceInfo => sizeof(DeviceInfo);
    internal static int SizeOfRadioInfo => sizeof(RadioInfo);

    [LibraryImport("bthprops.cpl")]
    private static partial nint BluetoothFindFirstRadio(FindRadioParams* parameters, out nint radio);

    [LibraryImport("bthprops.cpl")]
    private static partial int BluetoothFindNextRadio(nint find, out nint radio);

    [LibraryImport("bthprops.cpl")]
    private static partial int BluetoothFindRadioClose(nint find);

    [LibraryImport("bthprops.cpl")]
    private static partial uint BluetoothGetRadioInfo(nint radio, RadioInfo* info);

    [LibraryImport("bthprops.cpl")]
    private static partial nint BluetoothFindFirstDevice(SearchParams* search, DeviceInfo* info);

    [LibraryImport("bthprops.cpl")]
    private static partial int BluetoothFindNextDevice(nint find, DeviceInfo* info);

    [LibraryImport("bthprops.cpl")]
    private static partial int BluetoothFindDeviceClose(nint find);

    [LibraryImport("kernel32.dll")]
    private static partial int CloseHandle(nint handle);
}
