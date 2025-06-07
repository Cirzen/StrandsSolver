using System.Diagnostics;

namespace Solver;

internal class ProgressTracker
{
    public delegate Task ReportProgressDelegate(List<WordPath> solutionForDisplay, long wps);

    private readonly ReportProgressDelegate _reportProgress;
    private readonly Stopwatch _stopwatch = new();
    private long _attemptedWordsThisSolve;

    private long _wordsAttemptedSinceLastReport = 0;

    public ProgressTracker(ReportProgressDelegate reportProgress)
    {
        _reportProgress = reportProgress;
    }

    /// <summary>
    /// Reports the current solution progress to the UI.
    /// </summary>
    /// <param name="solution">The current solution.</param>
    /// <param name="wps">Words per second metric.</param>
    public async Task ReportProgress(List<WordPath> solution, long wps)
    {
        await _reportProgress(solution, wps);
        _stopwatch.Restart();
    }

    public void IncrementWordsAttempted()
    {
        Interlocked.Increment(ref _wordsAttemptedSinceLastReport);
        Interlocked.Increment(ref _attemptedWordsThisSolve);
    }

    public long GetAndResetWordsAttemptedSinceLastReport()
    {
        return Interlocked.Exchange(ref _wordsAttemptedSinceLastReport, 0);
    }

    public long GetTotalWordsAttemptedThisSolve()
    {
        return Interlocked.Read(ref _attemptedWordsThisSolve);
    }

    /// <summary>
    /// Gets the total elapsed time for the current solve attempt.
    /// </summary>
    public TimeSpan GetElapsedTimeThisSolve()
    {
        return _stopwatch.Elapsed;
    }

    /// <summary>
    /// Resets the internal state for a new solve attempt.
    /// </summary>
    public void ResetForNewSolveAttempt()
    {
        _stopwatch.Restart();
        Interlocked.Exchange(ref _attemptedWordsThisSolve, 0);
        Interlocked.Exchange(ref _wordsAttemptedSinceLastReport,  0);
    }
}
