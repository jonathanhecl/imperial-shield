using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;

namespace ImperialShield.Services;

/// <summary>
/// Monitor de conexiones de red usando GetExtendedTcpTable
/// Mapea conexiones TCP a procesos y detecta comportamientos sospechosos
/// </summary>
public class NetworkMonitor
{
    // Procesos que normalmente NO deberían hacer conexiones de red
    private static readonly HashSet<string> SuspiciousNetworkProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell",
        "pwsh",
        "cmd",
        "wscript",
        "cscript",
        "mshta",
        "regsvr32",
        "rundll32",
        "msiexec",
        "certutil"
    };

    // Puertos comúnmente usados por malware para C&C
    private static readonly HashSet<int> SuspiciousPorts = new()
    {
        4444,  // Metasploit default
        5555,  // Android ADB
        6666,  // IRC bots
        6667,  // IRC
        8080,  // HTTP alternativo
        8443,  // HTTPS alternativo
        31337, // Back Orifice
        12345, // NetBus
        20000, // Millennium
        65535  // Puerto máximo (usado por algunos RATs)
    };

    #region P/Invoke Declarations

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        TCP_TABLE_CLASS tblClass,
        uint reserved = 0);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        UDP_TABLE_CLASS tblClass,
        uint reserved = 0);

    private enum TCP_TABLE_CLASS
    {
        TCP_TABLE_BASIC_LISTENER,
        TCP_TABLE_BASIC_CONNECTIONS,
        TCP_TABLE_BASIC_ALL,
        TCP_TABLE_OWNER_PID_LISTENER,
        TCP_TABLE_OWNER_PID_CONNECTIONS,
        TCP_TABLE_OWNER_PID_ALL,
        TCP_TABLE_OWNER_MODULE_LISTENER,
        TCP_TABLE_OWNER_MODULE_CONNECTIONS,
        TCP_TABLE_OWNER_MODULE_ALL
    }

    private enum UDP_TABLE_CLASS
    {
        UDP_TABLE_BASIC,
        UDP_TABLE_OWNER_PID,
        UDP_TABLE_OWNER_MODULE
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
        // Seguido de un array de MIB_TCPROW_OWNER_PID
    }

    private const int AF_INET = 2; // IPv4
    private const int NO_ERROR = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    #endregion

    /// <summary>
    /// Obtiene todas las conexiones TCP activas con información del proceso propietario
    /// </summary>
    public List<ConnectionInfo> GetTcpConnections()
    {
        var connections = new List<ConnectionInfo>();
        int bufferSize = 0;

        // Primera llamada para obtener el tamaño del buffer necesario
        uint result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, 
            TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);

        if (result != ERROR_INSUFFICIENT_BUFFER && result != NO_ERROR)
            return connections;

        IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);

        try
        {
            result = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, 
                TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);

            if (result != NO_ERROR)
                return connections;

            // Leer el número de entradas
            var table = Marshal.PtrToStructure<MIB_TCPTABLE_OWNER_PID>(tcpTablePtr);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            IntPtr rowPtr = tcpTablePtr + Marshal.SizeOf<uint>(); // Saltar dwNumEntries

            for (int i = 0; i < table.dwNumEntries; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                
                var connInfo = new ConnectionInfo
                {
                    Protocol = "TCP",
                    LocalAddress = new IPAddress(row.localAddr).ToString(),
                    LocalPort = (int)NetworkToHostOrder((ushort)row.localPort),
                    RemoteAddress = new IPAddress(row.remoteAddr).ToString(),
                    RemotePort = (int)NetworkToHostOrder((ushort)row.remotePort),
                    State = GetTcpStateString(row.state),
                    ProcessId = (int)row.owningPid
                };

                // Obtener información del proceso
                try
                {
                    var process = Process.GetProcessById(connInfo.ProcessId);
                    connInfo.ProcessName = process.ProcessName;
                    
                    try
                    {
                        connInfo.ProcessPath = process.MainModule?.FileName ?? "";
                    }
                    catch
                    {
                        connInfo.ProcessPath = "[Acceso Denegado]";
                    }
                }
                catch
                {
                    connInfo.ProcessName = "[Proceso Terminado]";
                }

                // Analizar nivel de sospecha
                connInfo.ThreatLevel = AnalyzeConnectionThreat(connInfo);
                connInfo.ThreatDescription = GetConnectionThreatDescription(connInfo);

                connections.Add(connInfo);
                rowPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }

        return connections.OrderByDescending(c => c.ThreatLevel).ThenBy(c => c.ProcessName).ToList();
    }

    /// <summary>
    /// Obtiene solo las conexiones establecidas (no listening)
    /// </summary>
    public List<ConnectionInfo> GetEstablishedConnections()
    {
        return GetTcpConnections()
            .Where(c => c.State == "ESTABLISHED" && c.RemoteAddress != "0.0.0.0")
            .ToList();
    }

    /// <summary>
    /// Detecta conexiones sospechosas (posibles reverse shells)
    /// </summary>
    public List<ConnectionInfo> GetSuspiciousConnections()
    {
        return GetTcpConnections()
            .Where(c => c.ThreatLevel >= ConnectionThreatLevel.High)
            .ToList();
    }

    private ConnectionThreatLevel AnalyzeConnectionThreat(ConnectionInfo conn)
    {
        // Ignorar conexiones locales
        if (conn.RemoteAddress == "0.0.0.0" || conn.RemoteAddress == "127.0.0.1")
            return ConnectionThreatLevel.Safe;

        // Conexión ESTABLISHED desde proceso sospechoso = CRÍTICO
        if (conn.State == "ESTABLISHED" && 
            SuspiciousNetworkProcesses.Contains(conn.ProcessName.ToLowerInvariant()))
        {
            return ConnectionThreatLevel.Critical;
        }

        // Puerto remoto sospechoso = ALTO
        if (SuspiciousPorts.Contains(conn.RemotePort))
            return ConnectionThreatLevel.High;

        // cmd/powershell escuchando = ALTO
        if (conn.State == "LISTEN" && 
            SuspiciousNetworkProcesses.Contains(conn.ProcessName.ToLowerInvariant()))
        {
            return ConnectionThreatLevel.High;
        }

        // Puerto local alto siendo escuchado por proceso desconocido = MEDIO
        if (conn.State == "LISTEN" && conn.LocalPort > 49152)
            return ConnectionThreatLevel.Medium;

        return ConnectionThreatLevel.Safe;
    }

    private string GetConnectionThreatDescription(ConnectionInfo conn)
    {
        return conn.ThreatLevel switch
        {
            ConnectionThreatLevel.Critical => 
                $"🚨 CRÍTICO: {conn.ProcessName} tiene una conexión activa. Posible Reverse Shell!",
            ConnectionThreatLevel.High => 
                $"🔴 ALTO: Conexión a puerto sospechoso ({conn.RemotePort}) o proceso de riesgo",
            ConnectionThreatLevel.Medium => 
                $"🟠 MEDIO: Puerto inusual o comportamiento atípico",
            ConnectionThreatLevel.Low => 
                $"🟡 BAJO: Revisar si es esperado",
            ConnectionThreatLevel.Safe => 
                "🟢 Normal",
            _ => ""
        };
    }

    private static ushort NetworkToHostOrder(ushort value)
    {
        return (ushort)((value << 8) | (value >> 8));
    }

    private static string GetTcpStateString(uint state)
    {
        return state switch
        {
            1 => "CLOSED",
            2 => "LISTEN",
            3 => "SYN_SENT",
            4 => "SYN_RCVD",
            5 => "ESTABLISHED",
            6 => "FIN_WAIT1",
            7 => "FIN_WAIT2",
            8 => "CLOSE_WAIT",
            9 => "CLOSING",
            10 => "LAST_ACK",
            11 => "TIME_WAIT",
            12 => "DELETE_TCB",
            _ => "UNKNOWN"
        };
    }

    /// <summary>
    /// Resuelve el hostname de una IP (con caché)
    /// </summary>
    private readonly Dictionary<string, string> _hostnameCache = new();

    public string ResolveHostname(string ipAddress)
    {
        if (_hostnameCache.TryGetValue(ipAddress, out var cached))
            return cached;

        try
        {
            var entry = Dns.GetHostEntry(ipAddress);
            _hostnameCache[ipAddress] = entry.HostName;
            return entry.HostName;
        }
        catch
        {
            _hostnameCache[ipAddress] = ipAddress;
            return ipAddress;
        }
    }

    /// <summary>
    /// Obtiene información de geolocalización básica de una IP (requiere servicio externo)
    /// Por ahora retorna un placeholder
    /// </summary>
    public string GetIpLocation(string ipAddress)
    {
        // Rangos privados
        if (ipAddress.StartsWith("10.") || 
            ipAddress.StartsWith("192.168.") || 
            ipAddress.StartsWith("172.16.") ||
            ipAddress.StartsWith("127.") ||
            ipAddress == "0.0.0.0")
        {
            return "Red Local";
        }

        // TODO: Implementar lookup de GeoIP si se desea
        return "IP Externa";
    }
}

public class ConnectionInfo
{
    public string Protocol { get; set; } = "";
    public string LocalAddress { get; set; } = "";
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = "";
    public int RemotePort { get; set; }
    public string State { get; set; } = "";
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";
    public ConnectionThreatLevel ThreatLevel { get; set; }
    public string ThreatDescription { get; set; } = "";
}

public enum ConnectionThreatLevel
{
    Safe = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
