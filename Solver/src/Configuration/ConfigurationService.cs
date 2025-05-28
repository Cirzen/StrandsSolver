using System.IO;
using System.Text.Json;
using System.Windows;

namespace Solver.Configuration;

public class ConfigurationService
{
    private static readonly string AppName = "StrandsSolver";
    private static readonly string ConfigFileName = "settings.json";
    private string ConfigFilePath { get; }

    public AppSettings Settings { get; private set; }

    public ConfigurationService()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolderPath = Path.Combine(appDataPath, AppName);
        Directory.CreateDirectory(appFolderPath); // Ensure the directory exists

        ConfigFilePath = Path.Combine(appFolderPath, ConfigFileName);
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(ConfigFilePath))
        {
            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefaultSettings();
            }
            catch (Exception /* ex */)
            {
                // Optionally log the exception ex
                Settings = CreateDefaultSettings(); // Fallback to defaults on error
            }
        }
        else
        {
            Settings = CreateDefaultSettings();
            SaveSettings(); // Create and save default settings if no file exists
        }
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private AppSettings CreateDefaultSettings()
    {
        return new()
        {
            WordListPath = "sowpods.txt",
            ProgressUpdateIntervalMilliseconds = 500,
            DoubleClickBehavior = SolutionDoubleClickAction.AddToExcluded,
            SelectedTheme = AppTheme.Light,
            PathOpacityNormal = 180 // 0-255
        };
    }
}