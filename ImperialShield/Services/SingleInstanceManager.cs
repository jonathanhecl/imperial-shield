using System.Threading;

namespace ImperialShield.Services;

/// <summary>
/// Gestor para asegurar que solo una instancia de la aplicación esté corriendo
/// </summary>
public static class SingleInstanceManager
{
    private static Mutex? _mutex;
    private const string MutexName = "ImperialShield_SingleInstance_Mutex";

    public static bool TryAcquireLock()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        
        return true;
    }

    public static void ReleaseLock()
    {
        if (_mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
