using System.IO;

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

internal class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly Object _lock = new object();
    public FileLogger(string filePath)
    {
        _filePath = filePath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating directory for log file {_filePath}: {ex.Message}");
        }
        // Clear the log file on each app run
        if (File.Exists(filePath))
        {
            File.WriteAllLines(filePath, Enumerable.Empty<string>());
        }
    }
    public void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_filePath, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing to log file {_filePath}: {ex.Message}");
        }
    }
    public void LogError(string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_filePath, $"{DateTime.Now} [ERROR]: {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing error to log file {_filePath}: {ex.Message}");
        }
    }
}

internal class Logger : ILogger
{
    private readonly List<ILogger> _loggers = new();

    public void AddLogger(ILogger logger)
    {
        if (logger != null && !_loggers.Contains(logger))
        {
            _loggers.Add(logger);
        }
    }

    public void Log(string message)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(message);
        }
    }

    public void LogError(string message)
    {
        foreach (var logger in _loggers)
        {
            logger.LogError(message);
        }
    }
}
