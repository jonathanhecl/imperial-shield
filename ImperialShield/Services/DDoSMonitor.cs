using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ImperialShield.Services;

public class DDoSEventArgs : EventArgs
{
    public string ProcessName { get; }
    public string RemoteIP { get; }
    public int ConnectionCount { get; }
    public string WarningMessage { get; }

    public DDoSEventArgs(string processName, string ip, int count, string warning)
    {
        ProcessName = processName;
        RemoteIP = ip;
        ConnectionCount = count;
        WarningMessage = warning;
    }
}

public class DDoSMonitor : IDisposable
{
    private readonly NetworkMonitor _networkMonitor;
    private Timer? _timer;
    private bool _isDisposed = false;

    // Configuración de umbrales
    private const int THRESHOLD_CONNECTIONS_PER_IP = 40;  // Más de 40 conexiones a una sola IP
    private const int THRESHOLD_TOTAL_CONNECTIONS = 150;  // Más de 150 conexiones totales del mismo proceso
    
    // Para evitar spam de alertas
    private readonly HashSet<string> _reportedIncidents = new();
    private DateTime _lastCacheClear = DateTime.Now;

    public event EventHandler<DDoSEventArgs>? DDoSAttackDetected;

    public DDoSMonitor()
    {
        _networkMonitor = new NetworkMonitor();
    }

    public void Start(int intervalMs = 3000)
    {
        _timer = new Timer(AnalyzeTraffic, null, 0, intervalMs);
        Logger.Log("DDoS Watchdog started");
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, 0);
    }

    private void AnalyzeTraffic(object? state)
    {
        if (_isDisposed) return;

        try
        {
            // Limpiar caché de incidentes cada 5 minutos para permitir re-alertar
            if ((DateTime.Now - _lastCacheClear).TotalMinutes > 5)
            {
                _reportedIncidents.Clear();
                _lastCacheClear = DateTime.Now;
            }

            // Obtener solo conexiones establecidas (ignoramos LISTEN y TIME_WAIT para este análisis)
            // Queremos ver quién está enviando datos activamente.
            var connections = _networkMonitor.GetTcpConnections()
                .Where(c => c.State == "ESTABLISHED" || c.State == "SYN_SENT" || c.State == "FIN_WAIT1")
                .Where(c => c.RemoteAddress != "127.0.0.1" && c.RemoteAddress != "0.0.0.0") // Ignorar loopback
                .ToList();

            // 1. Agrupar por Proceso
            var connectionsByProcess = connections
                .GroupBy(c => c.ProcessId)
                .ToList();

            foreach (var processGroup in connectionsByProcess)
            {
                var processName = processGroup.First().ProcessName;
                int totalCount = processGroup.Count();

                // 2. Análisis Volumen Total (High Output Flood)
                if (totalCount > THRESHOLD_TOTAL_CONNECTIONS)
                {
                    string incidentKey = $"{processName}:TOTAL";
                    if (!_reportedIncidents.Contains(incidentKey))
                    {
                        // Excluir navegadores legítimos si se desea, pero 150 cxns sigue siendo mucho incluso para Chrome
                        if (!IsWhitelistedBrowser(processName, totalCount)) 
                        {
                            ReportDDoS(processName, "Múltiples destinos", totalCount, 
                                $"El proceso '{processName}' tiene {totalCount} conexiones activas excesivas.");
                            _reportedIncidents.Add(incidentKey);
                        }
                    }
                }

                // 3. Análisis por IP Destino (Targeted Flood)
                var connectionsByIP = processGroup
                    .GroupBy(c => c.RemoteAddress)
                    .Where(g => g.Count() > THRESHOLD_CONNECTIONS_PER_IP)
                    .ToList();

                foreach (var ipGroup in connectionsByIP)
                {
                    string ip = ipGroup.Key;
                    int count = ipGroup.Count();

                    string incidentKey = $"{processName}:{ip}";
                    if (!_reportedIncidents.Contains(incidentKey))
                    {
                        // Excepción para servidores de P2P/Torrent si el usuario lo permite
                        // Pero por defecto, avisamos.
                        if (!IsWhitelistedBrowser(processName, count))
                        {
                            ReportDDoS(processName, ip, count, 
                                $"ATAQUE SALIENTE: '{processName}' está atacando a {ip} con {count} conexiones simultáneas.");
                            _reportedIncidents.Add(incidentKey);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "DDoSMonitor Analysis");
        }
    }

    private void ReportDDoS(string process, string ip, int count, string msg)
    {
        Logger.Log($"[DDoS ALERT] {msg}");
        DDoSAttackDetected?.Invoke(this, new DDoSEventArgs(process, ip, count, msg));
    }

    private bool IsWhitelistedBrowser(string processName, int count)
    {
        // Los navegadores modernos pueden abrir muchas conexiones legítimas
        // pero 50+ a la *misma IP* sigue siendo sospechoso.
        // Solo relajamos el chequeo de "Conexiones Totales".
        
        string name = processName.ToLowerInvariant();
        bool isBrowser = name == "chrome" || name == "firefox" || name == "msedge" || name == "opera";

        // Si es navegador, permitimos más conexiones totales, pero no infinitas.
        if (isBrowser && count < 300) return true;

        return false;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _timer?.Dispose();
    }
}
