namespace Solver;

internal class ProgressTracker
{
    public DateTime LastProgressUpdate { get; set; } = DateTime.Now;
    public Func<List<WordPath>, long, Dictionary<(int, int), int>, Task> ReportProgress { get; }

    private long _wordsAttemptedSinceLastReport = 0;
    private DateTime _startTimeOfCurrentSolveAttempt;
    private long _totalWordsAttemptedThisSolve = 0;

    // Property to store the latest heatmap
    // Key: (row, col), Value: frequency or a normalized heat value
    public Dictionary<(int, int), int> CurrentHeatMap { get; set; } = new();

    public ProgressTracker(Func<List<WordPath>, long, Dictionary<(int, int), int>, Task> reportProgress) // Updated signature
    {
        ReportProgress = reportProgress ?? throw new ArgumentNullException(nameof(reportProgress));
        _startTimeOfCurrentSolveAttempt = DateTime.Now;
    }

    public void IncrementWordsAttempted()
    {
        Interlocked.Increment(ref _wordsAttemptedSinceLastReport);
        Interlocked.Increment(ref _totalWordsAttemptedThisSolve);
    }

    public long GetAndResetWordsAttemptedSinceLastReport()
    {
        return Interlocked.Exchange(ref _wordsAttemptedSinceLastReport, 0);
    }

    public long GetTotalWordsAttemptedThisSolve()
    {
        return Interlocked.Read(ref _totalWordsAttemptedThisSolve);
    }

    public TimeSpan GetElapsedTimeThisSolve()
    {
        return DateTime.Now - _startTimeOfCurrentSolveAttempt;
    }

    public void ResetForNewSolveAttempt()
    {
        _startTimeOfCurrentSolveAttempt = DateTime.Now;
        _totalWordsAttemptedThisSolve = 0;
        _wordsAttemptedSinceLastReport = 0;
        LastProgressUpdate = DateTime.Now;
        CurrentHeatMap.Clear(); // Clear heatmap for new solve
    }
}
