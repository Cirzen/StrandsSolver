namespace Solver;

public interface ILogger
{
    void Log(string message);
    void LogError(string message);
}

internal class StatusBarLogger : ILogger
{
    private readonly Action<string, bool> _updateStatusBar;

    public StatusBarLogger(Action<string, bool> updateStatusBarAction)
    {
        _updateStatusBar = updateStatusBarAction;
    }

    public void Log(string message)
    {
        _updateStatusBar($"{message}", false);
    }

    public void LogError(string message)
    {
        _updateStatusBar($"[ERROR] {message}", true);
    }
}
