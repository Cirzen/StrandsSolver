using System.Configuration;
using System.Data;
using System.IO;
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
        ConfigService = new ConfigurationService();
    }
}