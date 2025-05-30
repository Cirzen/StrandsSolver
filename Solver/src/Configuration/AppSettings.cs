namespace Solver.Configuration;

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
}