using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solver;

public interface ILogger
{
    void Log(string message);
    void LogError(string message);
}

internal class StatusBarLogger : ILogger
{
    private readonly Action<string, bool> updateStatusBar;

    public StatusBarLogger(Action<string, bool> updateStatusBarAction)
    {
        updateStatusBar = updateStatusBarAction;
    }

    public void Log(string message)
    {
        // Implement logging to the status bar
        updateStatusBar($"{message}", false);
    }

    public void LogError(string message)
    {
        // Implement error logging to the status bar
        updateStatusBar($"[ERROR] {message}", true);
    }
}
