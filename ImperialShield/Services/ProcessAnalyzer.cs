using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.ComponentModel;

namespace ImperialShield.Services;

/// <summary>
/// Analizador de procesos con verificación de firma digital
/// Identifica procesos sospechosos basándose en ubicación y firma
/// </summary>
public class ProcessAnalyzer
{
    // Rutas seguras donde deberían estar los ejecutables legítimos
    private static readonly HashSet<string> SafePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Windows",
        @"C:\Windows\System32",
        @"C:\Windows\SysWOW64",
        @"C:\Program Files",
        @"C:\Program Files (x86)"
    };

    // Nombres de procesos del sistema que son comúnmente suplantados
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost.exe",
        "csrss.exe",
        "services.exe",
        "lsass.exe",
        "winlogon.exe",
        "smss.exe",
        "explorer.exe",
        "conhost.exe",
        "RuntimeBroker.exe",
        "dwm.exe",
        "taskhostw.exe"
    };

    // Emisores de certificados confiables
    private static readonly HashSet<string> TrustedIssuers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft",
        "Microsoft Corporation",
        "Microsoft Windows",
        "Google LLC",
        "Google Inc",
        "Mozilla Corporation",
        "Apple Inc.",
        "Adobe Inc.",
        "Adobe Systems",
        "NVIDIA Corporation",
        "Intel Corporation",
        "AMD"
    };

    /// <summary>
    /// Obtiene información de todos los procesos en ejecución
    /// </summary>
    public List<ProcessInfo> GetAllProcesses()
    {
        var processes = new List<ProcessInfo>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var info = AnalyzeProcess(process);
                if (info != null)
                {
                    processes.Add(info);
                }
            }
            catch
            {
                // Ignorar procesos que no podemos acceder
            }
        }

        return processes.OrderByDescending(p => p.ThreatLevel).ThenBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Analiza un proceso específico
    /// </summary>
    public ProcessInfo? AnalyzeProcess(Process process)
    {
        try
        {
            var info = new ProcessInfo
            {
                Pid = process.Id,
                Name = process.ProcessName,
                SessionId = process.SessionId
            };

            // Intentar obtener la ruta del ejecutable
            try
            {
                info.Path = process.MainModule?.FileName ?? "";
            }
            catch (Win32Exception)
            {
                info.Path = GetProcessPath(process.Id) ?? "[Acceso Denegado]";
            }

            // Analizar la ruta
            if (!string.IsNullOrEmpty(info.Path) && info.Path != "[Acceso Denegado]")
            {
                info.IsInSafePath = IsInSafePath(info.Path);
                info.SignatureInfo = VerifyDigitalSignature(info.Path);
            }

            // Determinar nivel de amenaza
            info.ThreatLevel = CalculateThreatLevel(info);
            info.ThreatDescription = GetThreatDescription(info);

            // Obtener uso de memoria
            try
            {
                info.MemoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
            }
            catch { }

            // Obtener tiempo de CPU
            try
            {
                info.CpuTime = process.TotalProcessorTime;
            }
            catch { }

            // Obtener hora de inicio
            try
            {
                info.StartTime = process.StartTime;
            }
            catch { }

            return info;
        }
        catch
        {
            return null;
        }
    }

    private bool IsInSafePath(string path)
    {
        foreach (var safePath in SafePaths)
        {
            if (path.StartsWith(safePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private SignatureInfo VerifyDigitalSignature(string filePath)
    {
        var info = new SignatureInfo();

        try
        {
            if (!File.Exists(filePath))
                return info;

            using var cert = X509Certificate.CreateFromSignedFile(filePath);
            var cert2 = new X509Certificate2(cert);

            info.IsSigned = true;
            info.Subject = cert2.Subject;
            info.Issuer = cert2.Issuer;
            info.ValidFrom = cert2.NotBefore;
            info.ValidTo = cert2.NotAfter;
            info.IsValid = DateTime.Now >= cert2.NotBefore && DateTime.Now <= cert2.NotAfter;

            // Verificar si el emisor es de confianza
            info.IsTrustedIssuer = TrustedIssuers.Any(issuer => 
                cert2.Issuer.Contains(issuer, StringComparison.OrdinalIgnoreCase) ||
                cert2.Subject.Contains(issuer, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            info.IsSigned = false;
        }

        return info;
    }

    private ThreatLevel CalculateThreatLevel(ProcessInfo info)
    {
        // Proceso sin ruta = sospechoso
        if (string.IsNullOrEmpty(info.Path) || info.Path == "[Acceso Denegado]")
            return ThreatLevel.Unknown;

        bool isSystemName = SystemProcessNames.Contains(info.Name + ".exe");
        bool isInSafePath = info.IsInSafePath;
        bool isSigned = info.SignatureInfo?.IsSigned ?? false;
        bool isTrusted = info.SignatureInfo?.IsTrustedIssuer ?? false;

        // Proceso del sistema en ubicación incorrecta = CRÍTICO
        if (isSystemName && !isInSafePath)
            return ThreatLevel.Critical;

        // Proceso sin firma desde ubicación de usuario = ALTO
        if (!isSigned && !isInSafePath)
            return ThreatLevel.High;

        // Proceso sin firma pero en ubicación segura = MEDIO
        if (!isSigned && isInSafePath)
            return ThreatLevel.Medium;

        // Proceso firmado pero no por emisor de confianza = BAJO
        if (isSigned && !isTrusted)
            return ThreatLevel.Low;

        // Proceso firmado por emisor de confianza = SEGURO
        return ThreatLevel.Safe;
    }

    private string GetThreatDescription(ProcessInfo info)
    {
        return info.ThreatLevel switch
        {
            ThreatLevel.Critical => $"⚠️ CRÍTICO: Proceso del sistema '{info.Name}' ejecutándose desde ubicación no autorizada",
            ThreatLevel.High => $"🔴 ALTO: Proceso sin firma digital ejecutándose desde ubicación de usuario",
            ThreatLevel.Medium => $"🟠 MEDIO: Proceso sin firma digital",
            ThreatLevel.Low => $"🟡 BAJO: Firmado por emisor desconocido",
            ThreatLevel.Safe => "🟢 Seguro: Firmado por emisor de confianza",
            ThreatLevel.Unknown => "❓ No se pudo verificar",
            _ => ""
        };
    }

    /// <summary>
    /// Obtiene la ruta de un proceso usando WMI cuando el acceso directo falla
    /// </summary>
    private string? GetProcessPath(int processId)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                return obj["ExecutablePath"]?.ToString();
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Termina un proceso por su PID
    /// </summary>
    public bool KillProcess(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public int SessionId { get; set; }
    public bool IsInSafePath { get; set; }
    public SignatureInfo? SignatureInfo { get; set; }
    public ThreatLevel ThreatLevel { get; set; }
    public string ThreatDescription { get; set; } = "";
    public double MemoryMB { get; set; }
    public TimeSpan CpuTime { get; set; }
    public DateTime? StartTime { get; set; }
}

public class SignatureInfo
{
    public bool IsSigned { get; set; }
    public bool IsValid { get; set; }
    public bool IsTrustedIssuer { get; set; }
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public enum ThreatLevel
{
    Safe = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
    Unknown = 5
}
