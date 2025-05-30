using Solver;
namespace Solver.Tests;

public class MockLogger : ILogger
{
    public List<string> LoggedMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();

    public void Log(string message)
    {
        LoggedMessages.Add(message);
    }

    public void LogError(string message)
    {
        ErrorMessages.Add(message);
    }
}