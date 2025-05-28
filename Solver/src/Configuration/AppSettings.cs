namespace Solver.Configuration;

public enum AppTheme
{
    Light,
    Dark
}

public class AppSettings
{
    /// <summary>
    /// Gets or sets the file path to the word list used by the application.
    /// </summary>
    public string WordListPath { get; set; } = System.IO.Path.Combine(System.AppContext.BaseDirectory, "sowpods.txt");
    /// <summary>
    /// Default interval in milliseconds
    /// </summary>
    public int ProgressUpdateIntervalMilliseconds { get; set; } = 1000;
    /// <summary>
    /// Behavior for double-clicking a solution word.
    /// </summary>
    public SolutionDoubleClickAction DoubleClickBehavior { get; set; } = SolutionDoubleClickAction.AddToExcluded;
    /// <summary>
    /// Gets or sets the selected application theme.
    /// </summary>
    public AppTheme SelectedTheme { get; set; } = AppTheme.Light;
    /// <summary>
    /// Gets or sets the opacity for normal (non-highlighted) solution path lines.
    /// Value should be between 0 (transparent) and 255 (opaque).
    /// </summary>
    public byte PathOpacityNormal { get; set; } = 180; // Default to ~70% opacity
}