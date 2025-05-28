using System.IO;

namespace Solver;

internal class SolverEngine
{
    private static readonly Trie Trie = new();
    private static CancellationTokenSource? _ctSource;
    private readonly ILogger _logger;

    private string? _cachedBoardStringForPrePruning; // For WordFinder raw paths
    private List<WordPath>? _cachedPrePrunedWordPaths; // For WordFinder raw paths

    private string? _cachedBoardStringForProblematicFilter; // For FilterProblematicStarterWords output
    private List<WordPath>? _cachedPathsAfterProblematicFilter; // For FilterProblematicStarterWords output

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
            ClearPrePruningCache(); // Clear all board-specific caches when Trie changes
        }
        else
        {
            _logger?.LogError($"Dictionary file not found: {filePath}");
            throw new FileNotFoundException($"Dictionary file not found: {filePath}.");
        }
    }

    public void ClearPrePruningCache()
    {
        _cachedBoardStringForPrePruning = null;
        _cachedPrePrunedWordPaths = null;
        _cachedBoardStringForProblematicFilter = null;
        _cachedPathsAfterProblematicFilter = null;
        _logger?.Log("All board-specific caches cleared (raw paths and problematic filter).");
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
        wordsToExclude ??= Enumerable.Empty<string>(); 
        _ctSource = new();

        foreach (var word in knownWords.Where(word => !Trie.Search(word)))
        {
            Trie.Insert(word);
        }

        string currentBoardString = Utils.ConvertBoardToString(board);
        List<WordPath> rawPathsFromBoardScan;

        // 1. Get raw paths for the board: from cache or by new scan.
        if (currentBoardString == _cachedBoardStringForPrePruning && _cachedPrePrunedWordPaths != null)
        {
            _logger?.Log("Using cached raw word paths for the board.");
            rawPathsFromBoardScan = new List<WordPath>(_cachedPrePrunedWordPaths);
        }
        else
        {
            _logger?.Log("Board has changed or no raw path cache. Performing board scan with WordFinder (DFS).");
            var finder = new WordFinder(Trie, 8, 6); 
            rawPathsFromBoardScan = finder.DepthFirstSearch(board);

            _cachedPrePrunedWordPaths = new List<WordPath>(rawPathsFromBoardScan); 
            _cachedBoardStringForPrePruning = currentBoardString; 
            _logger?.Log($"Board scan complete. Found {rawPathsFromBoardScan.Count} raw paths. Cached.");
            
            // If raw paths are re-calculated, the problematic filter cache is also invalid.
            _cachedBoardStringForProblematicFilter = null; 
            _cachedPathsAfterProblematicFilter = null;
            _logger?.Log("Invalidated problematic starter words cache due to new raw path calculation.");
        }

        // 2. Apply FilterProblematicStarterWords (cached based on board string).
        List<WordPath> pathsAfterProblematicFilter;
        if (currentBoardString == _cachedBoardStringForProblematicFilter && _cachedPathsAfterProblematicFilter != null)
        {
            _logger?.Log("Using cached paths after problematic starter words filter.");
            pathsAfterProblematicFilter = new List<WordPath>(_cachedPathsAfterProblematicFilter);
        }
        else
        {
            _logger?.Log("Board has changed or no cache for problematic filter. Applying FilterProblematicStarterWords to raw paths.");
            var sortedRawPaths = rawPathsFromBoardScan // Use the up-to-date rawPathsFromBoardScan
                .OrderByDescending(w => w.Word.Length)
                .ThenBy(w => w.Word)
                .ToList();
            
            pathsAfterProblematicFilter = SolverPreProcessor.FilterProblematicStarterWords(
                sortedRawPaths,
                board.GetLength(0),
                board.GetLength(1),
                _logger
            );
            _cachedPathsAfterProblematicFilter = new List<WordPath>(pathsAfterProblematicFilter);
            _cachedBoardStringForProblematicFilter = currentBoardString;
            _logger?.Log($"Problematic starter words filter applied. Count: {pathsAfterProblematicFilter.Count}. Cached.");
        }

        // 3. Apply current user exclusions to the pathsAfterProblematicFilter.
        List<WordPath> generalCandidatePool; // This will be the final pool for the solver
        if (wordsToExclude.Any())
        {
            var exclusionSet = new HashSet<string>(wordsToExclude.Select(w => w.ToLowerInvariant()));
            int originalCount = pathsAfterProblematicFilter.Count;
            generalCandidatePool = pathsAfterProblematicFilter.Where(wp => !exclusionSet.Contains(wp.Word.ToLowerInvariant())).ToList();
            _logger?.Log($"Filtered out {originalCount - generalCandidatePool.Count} user-excluded words from problematic-filtered list. Final candidate pool count: {generalCandidatePool.Count}");
        }
        else
        {
            generalCandidatePool = new List<WordPath>(pathsAfterProblematicFilter); // No exclusions, use all
            _logger?.Log($"No user exclusions. Final candidate pool count: {generalCandidatePool.Count}");
        }
        
        // 4. Prepare inputs for BoardSolver (largely unchanged from here)
        var pathsConsideredForKnownWords = Utils.SelectKnownPaths(knownWords, generalCandidatePool, _logger);

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

        var allPathsForAmbiguousResolution = generalCandidatePool
            .Where(p => ambiguousKnownWordStrings.Contains(p.Word, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var wordsInUnambiguousSolution = new HashSet<string>(currentSolutionWithUnambiguousKnowns.Select(p => p.Word), StringComparer.OrdinalIgnoreCase);

        // finalGeneralCandidatePool is already what we called generalCandidatePool
        // We need to ensure it doesn't re-include unambiguous knowns unless they are also ambiguous.
        var finalGeneralCandidatePoolForSolver = generalCandidatePool
            .Where(p => !wordsInUnambiguousSolution.Contains(p.Word) || ambiguousKnownWordStrings.Contains(p.Word, StringComparer.OrdinalIgnoreCase))
            .ToList();
        _logger?.Log($"Final general candidate pool size for BoardSolver (after knowns processing): {finalGeneralCandidatePoolForSolver.Count}");

        var solver = new BoardSolver(_logger); 

        BoardSolver.BoardSolution solution = await solver.SolveAsync(
            finalGeneralCandidatePoolForSolver, // Use this refined list
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