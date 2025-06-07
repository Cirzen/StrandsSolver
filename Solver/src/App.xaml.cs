using System.Windows;
using Solver.Configuration;

namespace Solver;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static ConfigurationService ConfigService { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigService = new();
        ApplyTheme(ConfigService.Settings.SelectedTheme);
    }

    public void ApplyTheme(AppTheme theme)
    {
        // Clear existing theme dictionaries
        var existingThemeDictionaries = Resources.MergedDictionaries
            .Where(rd => rd.Source != null && 
                  (rd.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) || 
                   rd.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))).ToList();

        foreach (var rd in existingThemeDictionaries)
        {
            Resources.MergedDictionaries.Remove(rd);
        }

        // Load new theme dictionary using pack URIs
        // Adjusted to lowercase "resources" as per the error message for Release build
        string themeUriString = theme == AppTheme.Dark 
            ? "pack://application:,,,/src/Resources/DarkTheme.xaml" 
            : "pack://application:,,,/src/Resources/LightTheme.xaml";
        
        var newThemeDictionary = new ResourceDictionary { Source = new Uri(themeUriString, UriKind.Absolute) };
        Resources.MergedDictionaries.Add(newThemeDictionary);

        ConfigService.Settings.SelectedTheme = theme; // Ensure the setting is updated if called externally
    }
}