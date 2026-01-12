using System;
using System.IO;
using System.Text.Json;

namespace ImperialShield.Services;

public class AppSettings
{
    public int PollingIntervalMs { get; set; } = 60000; // Default 1 min
}

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImperialShield", "settings.json");

    public static AppSettings Current { get; private set; } = new();

    static SettingsManager()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "SettingsManager.Load");
        }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            
            var json = JsonSerializer.Serialize(Current);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "SettingsManager.Save");
        }
    }
}
