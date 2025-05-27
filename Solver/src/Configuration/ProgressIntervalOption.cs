namespace Solver.Configuration;

/// <summary>
/// Represents an option for configuring a progress interval, including a display name and the interval duration in
/// milliseconds.
/// </summary>
/// <remarks>This class is commonly used to define selectable progress interval options in user interfaces, such
/// as dropdowns or combo boxes. The <see cref="DisplayName"/> property provides a human-readable label for the option,
/// while the <see cref="Milliseconds"/> property specifies the interval duration.</remarks>
public class ProgressIntervalOption
{
    public string DisplayName { get; set; }
    public int Milliseconds { get; set; }

    public ProgressIntervalOption(string displayName, int milliseconds)
    {
        DisplayName = displayName;
        Milliseconds = milliseconds;
    }

    // Override ToString for ComboBox display if not using DisplayMemberPath
    public override string ToString() => DisplayName;
}