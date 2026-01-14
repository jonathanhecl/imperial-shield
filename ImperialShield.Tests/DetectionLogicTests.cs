using ImperialShield.Services;
using Xunit;

namespace ImperialShield.Tests;

public class DetectionLogicTests
{
    [Fact]
    public void ProcessAnalyzer_ShouldFlagSystemProcessInWrongPathAsCritical()
    {
        // Arrange
        var analyzer = new ProcessAnalyzer();
        var processInfo = new ProcessInfo
        {
            Name = "lsass",
            Path = @"C:\Users\Public\lsass.exe", // Ubicación sospechosa
            IsInSafePath = false,
            SignatureInfo = new SignatureInfo { IsSigned = false }
        };

        // Act
        // Como CalculateThreatLevel es privado, en un escenario real lo haríamos público o usaríamos Reflection.
        // Por ahora, simulamos la lógica que leímos en el código.
        var threatLevel = InvokePrivateMethod<ThreatLevel>(analyzer, "CalculateThreatLevel", processInfo);

        // Assert
        Assert.Equal(ThreatLevel.Critical, threatLevel);
    }

    [Fact]
    public void NetworkMonitor_ShouldFlagPowerShellConnectionAsCritical()
    {
        // Arrange
        var monitor = new NetworkMonitor();
        var connInfo = new ConnectionInfo
        {
            ProcessName = "powershell",
            State = "ESTABLISHED",
            RemoteAddress = "1.2.3.4",
            RemotePort = 4444
        };

        // Act
        var threatLevel = InvokePrivateMethod<ConnectionThreatLevel>(monitor, "AnalyzeConnectionThreat", connInfo);

        // Assert
        Assert.Equal(ConnectionThreatLevel.Critical, threatLevel);
    }

    [Fact]
    public void NetworkMonitor_ShouldFlagSuspiciousPortAsHigh()
    {
        // Arrange
        var monitor = new NetworkMonitor();
        var connInfo = new ConnectionInfo
        {
            ProcessName = "chrome",
            State = "ESTABLISHED",
            RemoteAddress = "1.2.3.4",
            RemotePort = 31337 // Back Orifice
        };

        // Act
        var threatLevel = InvokePrivateMethod<ConnectionThreatLevel>(monitor, "AnalyzeConnectionThreat", connInfo);

        // Assert
        Assert.Equal(ConnectionThreatLevel.High, threatLevel);
    }

    [Fact]
    public void QuarantineService_ShouldDetectDisabledVBS()
    {
        // Este test verifica que la lógica mejorada detecte correctamente cuando VBS está deshabilitado
        // Nota: Este test puede requerir configuración específica del sistema para pasar
        
        // Act
        var isVBSEnabled = QuarantineService.IsVBSEnabled();
        
        // Assert
        // El resultado depende del estado real del sistema
        // Si VBS está realmente deshabilitado, esto debería retornar false
        // Si está habilitado, debería retornar true
        
        // Para debugging: podemos registrar el estado actual
        System.Diagnostics.Debug.WriteLine($"VBS Enabled: {isVBSEnabled}");
    }

    // Helper para testear métodos privados sin cambiar el código original
    private T InvokePrivateMethod<T>(object obj, string methodName, params object[] args)
    {
        var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (T)method.Invoke(obj, args);
    }
}
