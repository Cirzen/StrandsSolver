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
        var existingThemeDictionaries = Resources.MergedDictionaries.Where(rd => rd.Source != null && rd.Source.OriginalString.Contains("Theme.xaml")).ToList();
        foreach (var rd in existingThemeDictionaries)
        {
            Resources.MergedDictionaries.Remove(rd);
        }

        // Load new theme dictionary
        string themeFile = theme == AppTheme.Dark ? "src/Resources/DarkTheme.xaml" : "src/Resources/LightTheme.xaml";
        var newThemeDictionary = new ResourceDictionary { Source = new Uri(themeFile, UriKind.Relative) };
        Resources.MergedDictionaries.Add(newThemeDictionary);

        ConfigService.Settings.SelectedTheme = theme; // Ensure the setting is updated if called externally
    }
}