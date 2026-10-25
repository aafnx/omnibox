using System.Net;
using System.Runtime.InteropServices;

namespace OmniHub.Platform;

public sealed record PortListener(IPAddress Address, int Port, int Pid)
{
    /// <summary>Слушает только этот компьютер (127.0.0.1 или ::1).</summary>
    public bool IsLoopback => IPAddress.IsLoopback(Address);
}

/// <summary>Кто слушает TCP-порты: адрес, порт и PID владельца (GetExtendedTcpTable).</summary>
public static partial class TcpListeners
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const int TcpTableOwnerPidListener = 3;
    private const uint ErrorInsufficientBuffer = 122;

    public static List<PortListener> All()
    {
        var result = new List<PortListener>();
        Read(AfInet, result);
        Read(AfInet6, result);
        return result;
    }

    /// <summary>Порт в строке таблицы хранится в сетевом порядке байт в младших 16 битах.</summary>
    public static int PortFromNetworkOrder(uint value) => (int)(((value & 0xFF) << 8) | ((value >> 8) & 0xFF));

    private static void Read(int family, List<PortListener> result)
    {
        int size = 0;
        uint rc = GetExtendedTcpTable(0, ref size, 0, family, TcpTableOwnerPidListener, 0);
        if (rc != ErrorInsufficientBuffer || size <= 0)
        {
            return;
        }
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            rc = GetExtendedTcpTable(buffer, ref size, 0, family, TcpTableOwnerPidListener, 0);
            if (rc != 0)
            {
                return;
            }
            int count = Marshal.ReadInt32(buffer);
            if (family == AfInet)
            {
                // MIB_TCPROW_OWNER_PID: state, localAddr, localPort, remoteAddr, remotePort, owningPid — по 4 байта.
                const int rowSize = 24;
                for (int i = 0; i < count; i++)
                {
                    nint row = buffer + 4 + i * rowSize;
                    uint addr = (uint)Marshal.ReadInt32(row, 4);
                    uint port = (uint)Marshal.ReadInt32(row, 8);
                    int pid = Marshal.ReadInt32(row, 20);
                    result.Add(new PortListener(new IPAddress(addr), PortFromNetworkOrder(port), pid));
                }
            }
            else
            {
                // MIB_TCP6ROW_OWNER_PID: localAddr[16], localScopeId, localPort, remoteAddr[16], remoteScopeId, remotePort, state, owningPid.
                const int rowSize = 56;
                var bytes = new byte[16];
                for (int i = 0; i < count; i++)
                {
                    nint row = buffer + 4 + i * rowSize;
                    Marshal.Copy(row, bytes, 0, 16);
                    uint port = (uint)Marshal.ReadInt32(row, 20);
                    int pid = Marshal.ReadInt32(row, 52);
                    result.Add(new PortListener(new IPAddress(bytes), PortFromNetworkOrder(port), pid));
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetExtendedTcpTable(nint table, ref int size, int order, int addressFamily, int tableClass, uint reserved);
}
