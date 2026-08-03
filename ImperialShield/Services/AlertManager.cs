using System;
using System.Collections.Generic;
using System.Windows;

namespace ImperialShield.Services;

public enum AlertType
{
    DDoS,
    Defender,
    Hosts,
    Exclusion,
    Privacy,
    Browser,
    Startup,
    NewTask,
    Argentum,
    RogueIFEO,
    BlockedExecution
}

public static class AlertManager
{
    private static readonly object _lock = new();
    private static readonly Queue<AlertRequest> _alertQueue = new();
    private static readonly HashSet<AlertType> _activeOrQueuedTypes = new();
    private static Window? _currentAlertWindow;

    private class AlertRequest
    {
        public AlertType Type { get; }
        public Func<Window> WindowCreator { get; }

        public AlertRequest(AlertType type, Func<Window> windowCreator)
        {
            Type = type;
            WindowCreator = windowCreator;
        }
    }

    /// <summary>
    /// Intenta encolar y mostrar una alerta.
    /// Si ya hay una alerta activa o encolada del mismo tipo, la nueva solicitud se ignora (deduplicación por chequeo).
    /// Si es de un tipo distinto y hay una alerta mostrándose, se encola para mostrarse secuencialmente al cerrar la actual.
    /// </summary>
    public static void ShowAlert(AlertType type, Func<Window> windowCreator)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                if (_activeOrQueuedTypes.Contains(type))
                {
                    Logger.Log($"[AlertManager] Alerta de tipo {type} ignorada por ser duplicada durante el chequeo activo.");
                    return;
                }

                _activeOrQueuedTypes.Add(type);
                _alertQueue.Enqueue(new AlertRequest(type, windowCreator));

                if (_currentAlertWindow == null)
                {
                    ProcessNextAlertLocked();
                }
            }
        });
    }

    private static void ProcessNextAlertLocked()
    {
        if (_alertQueue.Count == 0)
        {
            _currentAlertWindow = null;
            return;
        }

        var request = _alertQueue.Dequeue();

        try
        {
            var win = request.WindowCreator();
            _currentAlertWindow = win;
            win.Topmost = true;

            win.Closed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_lock)
                    {
                        _activeOrQueuedTypes.Remove(request.Type);
                        _currentAlertWindow = null;
                        ProcessNextAlertLocked();
                    }
                });
            };

            win.Show();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, $"AlertManager.ProcessNextAlertLocked ({request.Type})");
            _activeOrQueuedTypes.Remove(request.Type);
            _currentAlertWindow = null;
            ProcessNextAlertLocked();
        }
    }
}
