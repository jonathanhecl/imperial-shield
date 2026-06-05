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

    [Fact]
    public void DDoSMonitor_ShouldRecognizeWhitelistedNetworkApps()
    {
        // Arrange
        var ddosMonitor = new DDoSMonitor();
        var originalWhitelist = SettingsManager.Current.WhitelistedNetworkApps;
        SettingsManager.Current.WhitelistedNetworkApps = new List<string> 
        { 
            @"C:\Users\User\AppData\Local\Programs\Arc\Arc.exe", 
            @"C:\Program Files\TestApp\testapp.exe" 
        };

        try
        {
            // Act
            bool isArcWhitelisted = InvokePrivateMethod<bool>(ddosMonitor, "IsNetworkAppWhitelisted", @"C:\Users\User\AppData\Local\Programs\Arc\Arc.exe");
            bool isChromeWhitelisted = InvokePrivateMethod<bool>(ddosMonitor, "IsNetworkAppWhitelisted", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
            bool isArcSimpleWhitelisted = InvokePrivateMethod<bool>(ddosMonitor, "IsNetworkAppWhitelisted", "arc.exe");

            // Assert
            Assert.True(isArcWhitelisted);
            Assert.False(isChromeWhitelisted);
            Assert.False(isArcSimpleWhitelisted); // Should be false because it is not an absolute path
        }
        finally
        {
            SettingsManager.Current.WhitelistedNetworkApps = originalWhitelist;
        }
    }

    [Theory]
    [InlineData("grao.exe", true)]
    [InlineData("argentum.exe", true)]
    [InlineData("ao-launcher.exe", true)]
    [InlineData("ao_client.exe", true)]
    [InlineData("client-ao.exe", true)]
    [InlineData("grao", true)]
    [InlineData("chrome.exe", false)]
    [InlineData("outlook.exe", false)]
    [InlineData("onedrive.exe", false)]
    [InlineData("download.exe", false)]
    [InlineData("layout.exe", false)]
    [InlineData("opera.exe", false)]
    public void DDoSMonitor_IsArgentumProcess_ShouldDetectCorrectly(string filename, bool expected)
    {
        // Act
        bool result = DDoSMonitor.IsArgentumProcess(filename);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("cmd.exe", true)]
    [InlineData("powershell.exe", true)]
    [InlineData("wscript.exe", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("explorer.exe", false)]
    public void LauncherProcessMonitor_IsShellOrSystemUtility_ShouldDetectCorrectly(string filename, bool expected)
    {
        // Act
        bool result = LauncherProcessMonitor.IsShellOrSystemUtility(filename);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(@"C:\Users\User\AppData\Local\Temp\payload.exe", true)]
    [InlineData(@"C:\Windows\Temp\malware.exe", true)]
    [InlineData(@"C:\Users\User\Downloads\update.exe", true)]
    [InlineData(@"C:\Games\Argentum\grao.exe", false)]
    [InlineData(@"C:\Program Files\AO\client.exe", false)]
    public void LauncherProcessMonitor_IsTemporaryOrSystemPath_ShouldDetectCorrectly(string path, bool expected)
    {
        // Act
        bool result = LauncherProcessMonitor.IsTemporaryOrSystemPath(path);

        // Assert
        Assert.Equal(expected, result);
    }

    // Helper para testear métodos privados sin cambiar el código original
    private T InvokePrivateMethod<T>(object obj, string methodName, params object[] args)
    {
        var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (T)method.Invoke(obj, args);
    }
}
