using System.IO;
using System.Linq; // Added for Enumerable.Any

namespace Solver;

internal class SolverEngine
{
    private static readonly Trie Trie = new();
    private static CancellationTokenSource? _ctSource;
    private readonly ILogger _logger;

    private string? _cachedBoardStringForPrePruning; 
    private List<WordPath>? _cachedPrePrunedWordPaths; 

    private string? _cachedBoardStringForProblematicFilter; 
    private List<WordPath>? _cachedPathsAfterProblematicFilter; 

    private record BoardSolverInputParameters(
        List<WordPath> FinalGeneralCandidatePool,
        HashSet<(int, int)> UsedPositions,
        HashSet<((int, int), (int, int))> UsedEdges,
        List<WordPath> CurrentSolutionWithUnambiguousKnowns,
        List<string> AmbiguousKnownWordStrings,
        List<WordPath> AllPathsForAmbiguousResolution
    );

    public SolverEngine(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the board-solving process asynchronously, identifying valid word paths on the board and resolving
    /// ambiguities based on known words and user exclusions.
    /// </summary>
    /// <remarks>This method processes the board by scanning for potential word paths, applying filters for
    /// problematic words and user exclusions, and resolving ambiguities using known words. The solving process is
    /// asynchronous and supports progress tracking via the <paramref name="progressTracker"/> parameter.</remarks>
    /// <param name="board">A 2D character array representing the board to solve.</param>
    /// <param name="knownWordsInput">A collection of known words to prioritize during the solving process. Can be <see langword="null"/>.</param>
    /// <param name="progressTracker">An object to track and report progress during the solving process. Cannot be <see langword="null"/>.</param>
    /// <param name="wordsToExcludeInput">A collection of words to exclude from the solution. Can be <see langword="null"/>.</param>
    /// <returns>A <see cref="BoardSolution"/> object containing the final solution, including resolved word paths
    /// and any associated metadata.</returns>
    public async Task<BoardSolution> ExecuteAsync(char[,] board, IEnumerable<string> knownWordsInput, ProgressTracker progressTracker, IEnumerable<string> wordsToExcludeInput)
    {
        EnsureTrieInitialized();

        var knownWords = knownWordsInput ?? Enumerable.Empty<string>();
        var wordsToExclude = wordsToExcludeInput ?? Enumerable.Empty<string>();

        PrepareInitialState(knownWords, board); 

        string currentBoardString = Utils.ConvertBoardToString(board);

        List<WordPath> rawPathsFromBoardScan = GetRawBoardPaths(board, currentBoardString);
        List<WordPath> pathsAfterProblematicFilter = ApplyProblematicWordsFilter(rawPathsFromBoardScan, board, currentBoardString);
        List<WordPath> generalCandidatePool = ApplyUserExclusions(pathsAfterProblematicFilter, wordsToExclude);

        BoardSolverInputParameters solverInputs = PrepareBoardSolverInputs(generalCandidatePool, knownWords);

        var solver = new BoardSolver(_logger);
        BoardSolution solution = await solver.SolveAsync(
            solverInputs.FinalGeneralCandidatePool,
            solverInputs.UsedPositions,
            solverInputs.UsedEdges,
            solverInputs.CurrentSolutionWithUnambiguousKnowns,
            _ctSource.Token,
            progressTracker,
            solverInputs.AmbiguousKnownWordStrings,
            solverInputs.AllPathsForAmbiguousResolution
        );

        await ProcessSolutionAsync(solution, progressTracker, _ctSource.Token);
        return solution;
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
            ClearPrePruningCache(); 
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

    private void EnsureTrieInitialized()
    {
        if (!Trie.IsEmpty)
        {
            return;
        }

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

    private void PrepareInitialState(IEnumerable<string> knownWords, char[,] board)
    {
        _ctSource = new();
        var newlyAddedWords = new List<string>();

        foreach (var word in knownWords.Where(w => !string.IsNullOrWhiteSpace(w)))
        {
            string lowerWord = word.ToLowerInvariant(); 
            if (!Trie.Search(lowerWord))
            {
                Trie.Insert(lowerWord);
                _logger?.Log($"Temporarily added known word '{lowerWord}' to Trie for this solve attempt.");
                newlyAddedWords.Add(lowerWord);
            }
        }

        if (newlyAddedWords.Any())
        {
            _logger?.Log($"Trie was modified with {newlyAddedWords.Count} new known word(s): {string.Join(", ", newlyAddedWords)}.");

            if (_cachedPrePrunedWordPaths != null && Utils.ConvertBoardToString(board) == _cachedBoardStringForPrePruning)
            {
                _logger?.Log("Attempting to augment existing raw path cache with newly added words.");
                var finder = new WordFinder(Trie, board.GetLength(0), board.GetLength(1));
                
                foreach (string newWord in newlyAddedWords)
                {
                    var pathsForNewWord = finder.DepthFirstSearchForWord(board, newWord);

                    if (pathsForNewWord != null && pathsForNewWord.Any())
                    {
                        _cachedPrePrunedWordPaths.AddRange(pathsForNewWord);
                        _logger?.Log($"Added {pathsForNewWord.Count} path(s) for new word '{newWord}' to raw path cache. Total raw paths: {_cachedPrePrunedWordPaths.Count}.");
                    }
                    else
                    {
                        _logger?.Log($"No paths found on board for new word '{newWord}'.");
                    }
                }
                _cachedBoardStringForProblematicFilter = null;
                _cachedPathsAfterProblematicFilter = null;
                _logger?.Log("Invalidated problematic starter words cache due to augmentation of raw paths.");
            }
            else
            {
                ClearPrePruningCache();
                _logger?.Log("Cleared all pre-pruning caches as no valid raw path cache existed, board changed, or augmentation is not implemented this way.");
            }
        }
    }

    private List<WordPath> GetRawBoardPaths(char[,] board, string currentBoardString)
    {
        if (currentBoardString == _cachedBoardStringForPrePruning && _cachedPrePrunedWordPaths != null)
        {
            _logger?.Log("Using cached raw word paths for the board.");
            return new List<WordPath>(_cachedPrePrunedWordPaths);
        }

        _logger?.Log("Board has changed or no raw path cache. Performing board scan with WordFinder (DFS).");
        var finder = new WordFinder(Trie, board.GetLength(0), board.GetLength(1));
        var rawPaths = finder.DepthFirstSearch(board);

        _cachedPrePrunedWordPaths = new List<WordPath>(rawPaths);
        _cachedBoardStringForPrePruning = currentBoardString;
        _logger?.Log($"Board scan complete. Found {rawPaths.Count} raw paths. Cached.");

        _cachedBoardStringForProblematicFilter = null;
        _cachedPathsAfterProblematicFilter = null;
        _logger?.Log("Invalidated problematic starter words cache due to new raw path calculation.");
        return rawPaths;
    }

    private List<WordPath> ApplyProblematicWordsFilter(List<WordPath> rawPaths, char[,] board, string currentBoardString)
    {
        if (currentBoardString == _cachedBoardStringForProblematicFilter && _cachedPathsAfterProblematicFilter != null)
        {
            _logger?.Log("Using cached paths after problematic starter words filter.");
            return new List<WordPath>(_cachedPathsAfterProblematicFilter);
        }

        _logger?.Log("Applying FilterProblematicStarterWords to raw paths.");
        var sortedRawPaths = rawPaths
            .OrderByDescending(w => w.Word.Length)
            .ThenBy(w => w.Word)
            .ToList();

        var filteredPaths = SolverPreProcessor.FilterProblematicStarterWords(
            sortedRawPaths,
            board.GetLength(0),
            board.GetLength(1),
            _logger
        );

        _cachedPathsAfterProblematicFilter = new List<WordPath>(filteredPaths);
        _cachedBoardStringForProblematicFilter = currentBoardString;
        _logger?.Log($"Problematic starter words filter applied. Count: {filteredPaths.Count}. Cached.");
        return filteredPaths;
    }

    private List<WordPath> ApplyUserExclusions(List<WordPath> pathsToFilter, IEnumerable<string> wordsToExclude)
    {
        if (!wordsToExclude.Any())
        {
            _logger?.Log("No user exclusions to apply.");
            return new List<WordPath>(pathsToFilter);
        }

        var exclusionSet = new HashSet<string>(wordsToExclude.Select(w => w.ToLowerInvariant()));
        int originalCount = pathsToFilter.Count;
        var generalCandidatePool = pathsToFilter.Where(wp => !exclusionSet.Contains(wp.Word.ToLowerInvariant())).ToList();
        _logger?.Log($"Filtered out {originalCount - generalCandidatePool.Count} user-excluded words. Final candidate pool count: {generalCandidatePool.Count}");
        return generalCandidatePool;
    }

    private BoardSolverInputParameters PrepareBoardSolverInputs(List<WordPath> generalCandidatePool, IEnumerable<string> knownWords)
    {
        var pathsConsideredForKnownWords = Utils.SelectKnownPaths(knownWords, generalCandidatePool, _logger);

        var usedPositions = new HashSet<(int, int)>();
        var usedEdges = new HashSet<((int, int), (int, int))>();
        var currentSolutionWithUnambiguousKnowns = new List<WordPath>();
        var ambiguousKnownWordStrings = new List<string>();
        var pathsGroupedByWord = pathsConsideredForKnownWords.GroupBy(p => p.Word, StringComparer.OrdinalIgnoreCase);

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
        var finalGeneralCandidatePoolForSolver = generalCandidatePool
            .Where(p => !wordsInUnambiguousSolution.Contains(p.Word) || ambiguousKnownWordStrings.Contains(p.Word, StringComparer.OrdinalIgnoreCase))
            .ToList();
        _logger?.Log($"Final general candidate pool size for BoardSolver (after knowns processing): {finalGeneralCandidatePoolForSolver.Count}");

        return new BoardSolverInputParameters(
            finalGeneralCandidatePoolForSolver,
            usedPositions,
            usedEdges,
            currentSolutionWithUnambiguousKnowns,
            ambiguousKnownWordStrings,
            allPathsForAmbiguousResolution
        );
    }

    private async Task ProcessSolutionAsync(BoardSolution solution, ProgressTracker progressTracker, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger?.Log("Solution processing skipped as operation was cancelled.");
            return;
        }

        if (solution.IsSolved)
        {
            _logger?.Log("Solution found!");
            TimeSpan totalElapsedTime = progressTracker.GetElapsedTimeThisSolve();
            long totalWordsAttempted = progressTracker.GetTotalWordsAttemptedThisSolve();
            double overallWps = (totalElapsedTime.TotalSeconds > 0) ? (totalWordsAttempted / totalElapsedTime.TotalSeconds) : 0;
            if (double.IsNaN(overallWps) || double.IsInfinity(overallWps)) overallWps = 0;

            if (solution.Words != null)
            {
                await progressTracker.ReportProgress(new List<WordPath>(solution.Words), (long)overallWps, progressTracker.CurrentHeatMap);
            }
            else
            {
                await progressTracker.ReportProgress(new List<WordPath>(), (long)overallWps, progressTracker.CurrentHeatMap);
                _logger?.LogError("Solution reported as solved, but solution.Words was null.");
            }
        }
        else
        {
            _logger?.LogError("No solution found.");
            await progressTracker.ReportProgress(new List<WordPath>(), 0, new Dictionary<(int, int), int>());
        }
    }

    public void Abort()
    {
        _logger?.Log("Solver aborted by user.");
        _ctSource?.Cancel();
    }
}