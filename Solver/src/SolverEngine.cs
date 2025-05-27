using System.IO;

namespace Solver;

internal class SolverEngine
{
    private static readonly Trie Trie = new();
    private static CancellationTokenSource? _ctSource;
    private readonly ILogger _logger;

    public SolverEngine(ILogger logger)
    {
        _logger = logger;
    }

    public void InitializeTrie()
    {
        string filePath = App.ConfigService.Settings.WordListPath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger?.LogError("Word list path is not configured.");
            throw new InvalidOperationException("Word list path is not configured.");
        }
        _logger?.Log($"Attempting to load dictionary from: {filePath}");
        if (File.Exists(filePath))
        {
            Trie.Clear();
            int wordCount = 0;
            foreach (string line in File.ReadLines(filePath))
            {
                string word = line.Trim().ToLowerInvariant();
                if (word.Length >= 4)
                {
                    Trie.Insert(word);
                    wordCount++;
                }
            }
            _logger?.Log($"Dictionary loaded into Trie from {filePath}. {wordCount} words added.");
        }
        else
        {
            _logger?.LogError($"Dictionary file not found: {filePath}");
            throw new FileNotFoundException($"Dictionary file not found: {filePath}.");
        }
    }

    public async Task<BoardSolver.BoardSolution> ExecuteAsync(char[,] board, IEnumerable<string> knownWords, ProgressTracker progressTracker, IEnumerable<string> wordsToExclude)
    {
        if (Trie.IsEmpty)
        {
            _logger?.Log("Trie is empty, attempting to initialize before execution.");
            try
            {
                InitializeTrie();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to initialize Trie during ExecuteAsync: {ex.Message}");
                throw;
            }
        }

        knownWords ??= Enumerable.Empty<string>();
        _ctSource = new();

        foreach (var word in knownWords.Where(word => !Trie.Search(word)))
        {
            Trie.Insert(word);
        }

        var finder = new WordFinder(Trie, 8, 6);
        var allWordPathsFromFinder = finder.DepthFirstSearch(board);

        if (wordsToExclude is not null && wordsToExclude.Any())
        {
            var exclusionSet = new HashSet<string>(wordsToExclude.Select(w => w.ToLowerInvariant()));
            int originalCount = allWordPathsFromFinder.Count;
            allWordPathsFromFinder = allWordPathsFromFinder.Where(wp => !exclusionSet.Contains(wp.Word.ToLowerInvariant())).ToList();
            _logger?.Log($"Filtered out {originalCount - allWordPathsFromFinder.Count} user-excluded words. Word path count now: {allWordPathsFromFinder.Count}");
        }

        var sortedAllWordPaths = allWordPathsFromFinder
            .OrderByDescending(w => w.Word.Length)
            .ThenBy(w => w.Word)
            .ToList();
        _logger?.Log($"Total usable word paths after DFS and exclusion: {sortedAllWordPaths.Count}");

        List<WordPath> generalCandidatePool = SolverPreProcessor.FilterProblematicStarterWords(
            sortedAllWordPaths,
            board.GetLength(0),
            board.GetLength(1),
            _logger
        );
        _logger?.Log($"Pruned general candidate pool count: {generalCandidatePool.Count}");

        var pathsConsideredForKnownWords = Utils.SelectKnownPaths(knownWords, sortedAllWordPaths, _logger);

        var usedPositions = new HashSet<(int, int)>();
        var usedEdges = new HashSet<((int, int), (int, int))>();
        var currentSolutionWithUnambiguousKnowns = new List<WordPath>();
        var ambiguousKnownWordStrings = new List<string>();

        var pathsGroupedByWord = pathsConsideredForKnownWords
            .GroupBy(p => p.Word, StringComparer.OrdinalIgnoreCase);

        foreach (var group in pathsGroupedByWord)
        {
            var pathsInGroup = group.ToList();
            if (pathsInGroup.Count == 1)
            {
                var unambiguousPath = pathsInGroup.Single();
                bool positionConflict = unambiguousPath.Positions.Any(p => usedPositions.Contains(p));
                bool edgeConflict = unambiguousPath.Edges.Any(e => usedEdges.Contains(e) || usedEdges.Contains((e.To, e.From)));
                if (positionConflict || edgeConflict)
                {
                    _logger?.LogError($"Conflict for unambiguous path '{unambiguousPath.Word}'. Treating as ambiguous.");
                    if (knownWords.Any(k => k.Equals(group.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        ambiguousKnownWordStrings.Add(group.Key);
                    }
                }
                else
                {
                    foreach (var pos in unambiguousPath.Positions) usedPositions.Add(pos);
                    foreach (var edge in unambiguousPath.Edges) usedEdges.Add(edge);
                    currentSolutionWithUnambiguousKnowns.Add(unambiguousPath);
                    _logger?.Log($"Locked in unambiguous path for known word: '{unambiguousPath.Word}'.");
                }
            }
            else if (pathsInGroup.Count > 1)
            {
                if (knownWords.Any(k => k.Equals(group.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    ambiguousKnownWordStrings.Add(group.Key);
                }
                _logger?.Log($"Known word '{group.Key}' has {pathsInGroup.Count} paths. To be resolved by BoardSolver.");
            }
        }

        var allPathsForAmbiguousResolution = sortedAllWordPaths
            .Where(p => ambiguousKnownWordStrings.Contains(p.Word, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var wordsInUnambiguousSolution = new HashSet<string>(currentSolutionWithUnambiguousKnowns.Select(p => p.Word), StringComparer.OrdinalIgnoreCase);

        var finalGeneralCandidatePool = generalCandidatePool
            .Where(p => !wordsInUnambiguousSolution.Contains(p.Word) || ambiguousKnownWordStrings.Contains(p.Word, StringComparer.OrdinalIgnoreCase))
            .ToList();
        _logger?.Log($"Final general candidate pool size for BoardSolver: {finalGeneralCandidatePool.Count}");

        var solver = new BoardSolver(_logger);

        BoardSolver.BoardSolution solution = await solver.SolveAsync(
            finalGeneralCandidatePool,
            usedPositions,
            usedEdges,
            currentSolutionWithUnambiguousKnowns,
            _ctSource.Token,
            progressTracker,
            ambiguousKnownWordStrings,
            allPathsForAmbiguousResolution
        );

        if (solution.IsSolved)
        {
            _logger?.Log("Solution found!");
            TimeSpan totalElapsedTime = progressTracker.GetElapsedTimeThisSolve();
            long totalWordsAttempted = progressTracker.GetTotalWordsAttemptedThisSolve();
            double overallWps = (totalElapsedTime.TotalSeconds > 0) ? (totalWordsAttempted / totalElapsedTime.TotalSeconds) : 0;
            if (double.IsNaN(overallWps) || double.IsInfinity(overallWps))
            {
                overallWps = 0;
            }
            if (solution.Words != null)
            {
                await progressTracker.ReportProgress(new(solution.Words), (long)overallWps, progressTracker.CurrentHeatMap);
            }
            else
            {
                await progressTracker.ReportProgress(new(), (long)overallWps, progressTracker.CurrentHeatMap);
                _logger?.LogError("Solution reported as solved, but solution.Words was null.");
            }
        }
        else
        {
            _logger?.LogError("No solution found.");
        }

        return solution;
    }

    public void Abort()
    {
        _logger?.Log("Solver aborted.");
        _ctSource?.Cancel();
    }
}