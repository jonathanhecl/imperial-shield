using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ImperialShield.Services;

/// <summary>
/// Monitor del archivo HOSTS usando FileSystemWatcher
/// Detecta cambios no autorizados en tiempo real
/// </summary>
public class HostsFileMonitor : IDisposable
{
    private FileSystemWatcher? _watcher;
    private string? _lastKnownHash;
    private string? _backupContent;
    private readonly string _hostsPath;
    private readonly string _backupPath;
    private bool _isDisposed;

    public event EventHandler<HostsFileChangedEventArgs>? HostsFileChanged;

    public HostsFileMonitor()
    {
        _hostsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"drivers\etc\hosts");
        
        _backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImperialShield", "hosts_backup.txt");
    }

    public void Start()
    {
        // Crear directorio de backup si no existe
        var backupDir = Path.GetDirectoryName(_backupPath);
        if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        // Guardar el estado inicial del archivo hosts
        SaveInitialState();

        // Configurar el FileSystemWatcher
        var directory = Path.GetDirectoryName(_hostsPath);
        if (string.IsNullOrEmpty(directory)) return;

        _watcher = new FileSystemWatcher(directory)
        {
            Filter = "hosts",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnHostsFileChanged;
        _watcher.Deleted += OnHostsFileDeleted;
    }

    private void SaveInitialState()
    {
        try
        {
            if (File.Exists(_hostsPath))
            {
                var content = File.ReadAllText(_hostsPath);
                _lastKnownHash = ComputeHash(content);
                
                // Si no existe backup o está vacío, crear uno
                if (!File.Exists(_backupPath) || new FileInfo(_backupPath).Length == 0)
                {
                    _backupContent = content;
                    File.WriteAllText(_backupPath, content);
                }
                else
                {
                    _backupContent = File.ReadAllText(_backupPath);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar estado inicial: {ex.Message}");
        }
    }

    private void OnHostsFileChanged(object sender, FileSystemEventArgs e)
    {
        // Pequeño delay para asegurar que el archivo no está bloqueado
        Thread.Sleep(100);

        try
        {
            var content = ReadFileWithRetry(_hostsPath);
            if (content == null) return;

            var newHash = ComputeHash(content);
            
            if (newHash != _lastKnownHash)
            {
                var changes = DetectChanges(_backupContent ?? "", content);
                _lastKnownHash = newHash;

                HostsFileChanged?.Invoke(this, new HostsFileChangedEventArgs
                {
                    ChangeDescription = changes,
                    NewContent = content,
                    OriginalContent = _backupContent ?? "",
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al procesar cambio en hosts: {ex.Message}");
        }
    }

    private void OnHostsFileDeleted(object sender, FileSystemEventArgs e)
    {
        HostsFileChanged?.Invoke(this, new HostsFileChangedEventArgs
        {
            ChangeDescription = "⚠️ El archivo HOSTS ha sido ELIMINADO. Esto es muy sospechoso.",
            NewContent = "",
            OriginalContent = _backupContent ?? "",
            Timestamp = DateTime.Now
        });
    }

    private string? ReadFileWithRetry(string path, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
        return null;
    }

    private string DetectChanges(string original, string modified)
    {
        var originalLines = original.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#")).ToHashSet();
        var modifiedLines = modified.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#")).ToHashSet();

        var added = modifiedLines.Except(originalLines).ToList();
        var removed = originalLines.Except(modifiedLines).ToList();

        var sb = new StringBuilder();
        
        if (added.Count > 0)
        {
            sb.AppendLine($"➕ {added.Count} línea(s) añadida(s):");
            foreach (var line in added.Take(5))
            {
                sb.AppendLine($"  {line}");
            }
            if (added.Count > 5) sb.AppendLine($"  ... y {added.Count - 5} más");
        }

        if (removed.Count > 0)
        {
            sb.AppendLine($"➖ {removed.Count} línea(s) eliminada(s):");
            foreach (var line in removed.Take(5))
            {
                sb.AppendLine($"  {line}");
            }
            if (removed.Count > 5) sb.AppendLine($"  ... y {removed.Count - 5} más");
        }

        return sb.Length > 0 ? sb.ToString() : "Cambios detectados en el archivo.";
    }

    public bool RestoreBackup()
    {
        try
        {
            if (!string.IsNullOrEmpty(_backupContent))
            {
                File.WriteAllText(_hostsPath, _backupContent);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void UpdateBackup()
    {
        try
        {
            if (File.Exists(_hostsPath))
            {
                _backupContent = File.ReadAllText(_hostsPath);
                File.WriteAllText(_backupPath, _backupContent);
                _lastKnownHash = ComputeHash(_backupContent);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al actualizar backup: {ex.Message}");
        }
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

public class HostsFileChangedEventArgs : EventArgs
{
    public required string ChangeDescription { get; init; }
    public required string NewContent { get; init; }
    public required string OriginalContent { get; init; }
    public required DateTime Timestamp { get; init; }
}
