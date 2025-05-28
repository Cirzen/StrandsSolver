// Required for StringComparison
// Required for List, HashSet, etc.
// Required for LINQ methods
// Required for CancellationToken

// Required for Task

namespace Solver;

internal class BoardSolver
{
    private const int TotalCells = 48;
    private readonly ILogger _logger;
    private HashSet<ulong> _failedStates = new(); // Ensure it's initialized

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardSolver"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic and operational information during the solving process. Cannot be null.</param>
    public BoardSolver(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Represents a solution to the board, including whether it is solved, and the words used in the solution.
    /// </summary>
    internal class BoardSolution
    {
        public bool IsSolved { get; init; } = false;
        public List<WordPath> Words { get; init; } = new();
        public HashSet<(int, int)> UsedPositions { get; set; } = new();
        public HashSet<((int, int), (int, int))> UsedEdges { get; set; } = new();
    }

    /// <summary>
    /// Clears the cache of failed states. This should be called when the conditions
    /// for solving change significantly, such as a new board or modified exclusion lists.
    /// </summary>
    public void ClearFailedStatesCache()
    {
        _failedStates.Clear();
        _logger?.Log("BoardSolver: Failed states cache cleared.");
    }

    /// <summary>
    /// Attempts to solve the board by recursively finding a valid sequence of words that covers all cells.
    /// </summary>
    /// <remarks>This method uses a recursive backtracking approach to explore potential solutions. It
    /// attempts to place words from the list of candidate words while ensuring that all board cells are covered and no
    /// invalid placements occur. If a valid solution is found, it is returned immediately. If no solution is possible
    /// from the current state, the method backtracks to try alternative paths. The method periodically reports
    /// progress through the <paramref name="progressTracker"/> and respects cancellation requests via the <paramref
    /// name="cancellationToken"/>.</remarks>
    /// <param name="allCandidatePathsOnBoard">All valid WordPaths (after user exclusions, before pre-processor).</param>
    /// <param name="currentUsedPositions">Positions used by unambiguous known words (locked in by SolverEngine).</param>
    /// <param name="currentUsedEdges">Edges used by unambiguous known words.</param>
    /// <param name="currentSolutionSoFar">WordPaths of unambiguous known words.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests, allowing the operation to be interrupted.</param>
    /// <param name="progressTracker">An object used to report progress during the solving process.</param>
    /// <param name="ambiguousKnownWordStrings">Known word STRINGS that still need to be placed and might have multiple paths.</param>
    /// <param name="allPathsForAmbiguousKnownWords">A pre-filtered list of ALL WordPath objects that correspond to ambiguousKnownWordStrings.</param>
    /// <returns>A <see cref="BoardSolution"/> object representing the result of the solving process. If the board is
    /// successfully solved, <see cref="BoardSolution.IsSolved"/> will be <see langword="true"/> and the solution will
    /// include the sequence of words used. Otherwise, <see cref="BoardSolution.IsSolved"/> will be <see
    /// langword="false"/>.</returns>
    public async Task<BoardSolution> SolveAsync(
        List<WordPath> allCandidatePathsOnBoard,
        HashSet<(int, int)> currentUsedPositions,
        HashSet<((int, int), (int, int))> currentUsedEdges,
        List<WordPath> currentSolutionSoFar,
        CancellationToken cancellationToken,
        ProgressTracker progressTracker,
        List<string> ambiguousKnownWordStrings,
        List<WordPath> allPathsForAmbiguousKnownWords)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new() { IsSolved = false };
        }

        // Base Case 1: All known words are placed, and the board is full.
        if (!ambiguousKnownWordStrings.Any() && currentUsedPositions.Count == TotalCells)
        {
            _logger?.Log($"SolveAsync: Solution found! All known words placed and board is full. Word count: {currentSolutionSoFar.Count}");
            return new()
            {
                IsSolved = true,
                Words = new(currentSolutionSoFar)
            };
        }

        // Memoization: Check if we've been in this state before (positions + remaining ambiguous known words)
        var currentStateKey = GetStateKeyBitmask(currentUsedPositions);
        if (ambiguousKnownWordStrings.Any())
        {
            currentStateKey ^= (ulong)string.Join(",", ambiguousKnownWordStrings.OrderBy(s => s)).GetHashCode();
        }

        if (_failedStates.Contains(currentStateKey))
        {
            return new() { IsSolved = false };
        }

        // --- Progress Reporting ---
        int updateIntervalMilliseconds = App.ConfigService.Settings.ProgressUpdateIntervalMilliseconds;
        if ((DateTime.Now - progressTracker.LastProgressUpdate).TotalMilliseconds >= updateIntervalMilliseconds)
        {
            long wordsInInterval = progressTracker.GetAndResetWordsAttemptedSinceLastReport();
            double wpsInterval = wordsInInterval / (updateIntervalMilliseconds / 1000.0);
            if (double.IsNaN(wpsInterval) || double.IsInfinity(wpsInterval))
            {
                wpsInterval = 0;
            }

            await progressTracker.ReportProgress(new(currentSolutionSoFar), (long)wpsInterval, progressTracker.CurrentHeatMap);
            progressTracker.LastProgressUpdate = DateTime.Now;
        }

        // Step 1: Prioritize placing remaining ambiguous known words
        if (ambiguousKnownWordStrings.Any())
        {
            string wordToPlaceNowStr = ambiguousKnownWordStrings.First();
            var remainingAmbiguousStrings = ambiguousKnownWordStrings.Skip(1).ToList();

            var pathsToTryForThisWord = allPathsForAmbiguousKnownWords
                .Where(p => p.Word.Equals(wordToPlaceNowStr, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!pathsToTryForThisWord.Any())
            {
                _logger?.LogError($"SolveAsync: CRITICAL - No paths found in allPathsForAmbiguousKnownWords for ambiguous known word '{wordToPlaceNowStr}'.");
                return new() { IsSolved = false };
            }

            foreach (var pathOption in pathsToTryForThisWord)
            {
                progressTracker.IncrementWordsAttempted();

                if (cancellationToken.IsCancellationRequested)
                {
                    return new() { IsSolved = false };
                }

                if (!IsValidWordPlacement(pathOption, currentUsedPositions, currentUsedEdges))
                {
                    continue;
                }

                currentSolutionSoFar.Add(pathOption);
                foreach (var pos in pathOption.Positions)
                {
                    currentUsedPositions.Add(pos);
                }

                foreach (var edge in pathOption.Edges)
                {
                    currentUsedEdges.Add(edge);
                }

                var result = await SolveAsync(
                    allCandidatePathsOnBoard,
                    currentUsedPositions,
                    currentUsedEdges,
                    currentSolutionSoFar,
                    cancellationToken,
                    progressTracker,
                    remainingAmbiguousStrings,
                    allPathsForAmbiguousKnownWords);

                if (result.IsSolved)
                {
                    return result;
                }

                foreach (var edge in pathOption.Edges)
                {
                    currentUsedEdges.Remove(edge);
                }

                foreach (var pos in pathOption.Positions)
                {
                    currentUsedPositions.Remove(pos);
                }

                currentSolutionSoFar.RemoveAt(currentSolutionSoFar.Count - 1);
            }
            return new() { IsSolved = false };
        }
        else
        {
            if (currentUsedPositions.Count == TotalCells)
            {
                return new() { IsSolved = true, Words = new(currentSolutionSoFar) };
            }

            var wordsInCurrentSolutionSet = currentSolutionSoFar.Select(wp => wp.Word.ToLowerInvariant()).ToHashSet();

            var rankedGeneralCandidates = GetRankedCandidateWordsForNextStep(
                allCandidatePathsOnBoard,
                currentUsedPositions,
                currentUsedEdges,
                progressTracker,
                wordsInCurrentSolutionSet);

            if (!rankedGeneralCandidates.Any() && currentUsedPositions.Count != TotalCells)
            {
                _failedStates.Add(currentStateKey);
                return new() { IsSolved = false };
            }

            foreach (var wordToTry in rankedGeneralCandidates)
            {
                progressTracker.IncrementWordsAttempted();

                if (cancellationToken.IsCancellationRequested)
                {
                    return new() { IsSolved = false };
                }

                currentSolutionSoFar.Add(wordToTry);
                foreach (var pos in wordToTry.Positions)
                {
                    currentUsedPositions.Add(pos);
                }

                foreach (var edge in wordToTry.Edges)
                {
                    currentUsedEdges.Add(edge);
                }

                var result = await SolveAsync(
                    allCandidatePathsOnBoard,
                    currentUsedPositions,
                    currentUsedEdges,
                    currentSolutionSoFar,
                    cancellationToken,
                    progressTracker,
                    new(),
                    allPathsForAmbiguousKnownWords);

                if (result.IsSolved)
                {
                    return result;
                }

                foreach (var edge in wordToTry.Edges)
                {
                    currentUsedEdges.Remove(edge);
                }

                foreach (var pos in wordToTry.Positions)
                {
                    currentUsedPositions.Remove(pos);
                }

                currentSolutionSoFar.RemoveAt(currentSolutionSoFar.Count - 1);
            }
        }

        _failedStates.Add(currentStateKey);
        return new() { IsSolved = false };
    }

    /// <summary>
    /// Identifies and ranks candidate words that can be placed on the board in the next step, based on the current
    /// state of the board and the available words.
    /// </summary>
    /// <remarks>This method evaluates the unused regions of the board and identifies words that can fit
    /// entirely within the smallest region. It ensures that all positions in the region can be covered by at least one
    /// candidate word. The candidates are then scored based on the frequency of their positions within the region, with
    /// less frequent positions being prioritized. If no unused regions are found or no valid candidates exist for the
    /// smallest region, the method returns an empty list, signaling that the current path may be a dead end.</remarks>
    /// <param name="allPossibleWordsOnBoard">A list of all possible words that can be placed on the board, represented as <see cref="WordPath"/> objects.
    /// Each word includes its positions on the board.</param>
    /// <param name="currentUsedPositions">A set of positions on the board that are already occupied by previously placed words.</param>
    /// <param name="currentUsedEdges">A set of edges on the board that are already in use.</param>
    /// <param name="progressTracker">An object used to track progress and store heatmap data for the current step.</param>
    /// <param name="wordsInCurrentSolution">A set of words that are already part of the current solution.</param>
    /// <returns>A ranked list of <see cref="WordPath"/> objects representing candidate words for the next step. The list is
    /// ordered by a scoring heuristic that prioritizes words based on their fit within the smallest unused region and
    /// their contribution to covering the region efficiently. Returns an empty list if no valid candidates are found.</returns>
    internal List<WordPath> GetRankedCandidateWordsForNextStep(
        List<WordPath> allPossibleWordsOnBoard,
        HashSet<(int, int)> currentUsedPositions,
        HashSet<((int, int), (int, int))> currentUsedEdges,
        ProgressTracker progressTracker,
        HashSet<string> wordsInCurrentSolution)
    {
        var regions = FindUnusedRegions(currentUsedPositions, 8, 6)
            .OrderBy(r => r.Count)
            .ToList();

        progressTracker.CurrentHeatMap.Clear();

        if (regions.Count > 0)
        {
            var smallestRegion = regions[0];

            var candidateRegionWords = allPossibleWordsOnBoard
                .Where(w =>
                    !wordsInCurrentSolution.Contains(w.Word.ToLowerInvariant()) && // Not already in solution
                    w.Positions.All(p => smallestRegion.Contains(p)) && // Fits in smallest region
                    w.Positions.All(p => !currentUsedPositions.Contains(p)) && // Does not overlap existing positions
                    w.Edges.All(newEdge => !currentUsedEdges.Any(existingEdge => EdgeUtils.EdgesCross(newEdge, existingEdge))) && // Does not cross existing edges
                    !EdgeUtils.PathSelfIntersects(w))
                .ToList();

            if (candidateRegionWords.Count == 0)
            {
                return new();
            }

            if (!smallestRegion.All(p => candidateRegionWords.SelectMany(wp => wp.Positions).Distinct().Contains(p)))
            {
                return new();
            }

            var positionFrequencyInRegionCandidates = candidateRegionWords
                .SelectMany(w => w.Positions)
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => g.Count());

            progressTracker.CurrentHeatMap = new(positionFrequencyInRegionCandidates);

            var scoredAndRankedWords = candidateRegionWords
                .Select(w => new
                {
                    WordPath = w,
                    Score = w.Positions.Sum(p => positionFrequencyInRegionCandidates.TryGetValue(p, out int freq) ? 1.0 / freq : 0.0) +
                            (w.Word.Length * 0.01) // Tie-breaker: longer words get a slight bonus
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.WordPath)
                .ToList();

            return scoredAndRankedWords;
        }
        // No unused regions found, but board not full
        if (currentUsedPositions.Count != TotalCells)
        {
            _logger?.Log("No unused regions found, but board not full. This is unexpected or a dead-end path.");
        }
        return new();
    }

    /// <summary>
    /// Determines whether the specified word can be placed on the board without overlapping existing positions or
    /// crossing existing edges.
    /// </summary>
    /// <param name="wordToPlace">The word to be placed, represented by its positions and edges.</param>
    /// <param name="existingUsedPositions">A set of positions on the board that are already occupied.</param>
    /// <param name="existingUsedEdges">A set of edges on the board that are already in use.</param>
    /// <returns><see langword="true"/> if the word can be placed without conflicts; otherwise, <see langword="false"/>.</returns>
    internal bool IsValidWordPlacement(WordPath wordToPlace, HashSet<(int, int)> existingUsedPositions, HashSet<((int, int), (int, int))> existingUsedEdges)
    {
        if (wordToPlace.Positions.Any(p => existingUsedPositions.Contains(p)))
        {
            return false;
        }
        if (wordToPlace.Edges.Any(newEdge => existingUsedEdges.Any(existingEdge => EdgeUtils.EdgesCross(newEdge,
                    existingEdge) ||
                EdgeUtils.EdgesOverlap(newEdge,
                    existingEdge))))
        {
            return false;
        }
        return !EdgeUtils.PathSelfIntersects(wordToPlace);
    }

    /// <summary>
    /// Identifies and returns all contiguous regions of unused positions within a grid.
    /// </summary>
    /// <remarks>This method performs a breadth-first search (BFS) to identify contiguous regions of unused
    /// positions. A position is considered "unused" if it is not present in <paramref name="usedPositions"/>. The grid
    /// is traversed row by row, and adjacent unused positions (horizontally or vertically) are grouped into the same
    /// region.</remarks>
    /// <param name="usedPositions">A set of grid positions that are considered "used". Each position is represented as a tuple of row and column
    /// indices.</param>
    /// <param name="rows">The total number of rows in the grid.</param>
    /// <param name="cols">The total number of columns in the grid.</param>
    /// <returns>A list of regions, where each region is represented as a list of tuples. Each tuple contains the row and column
    /// indices of an unused position within that region. If no unused regions are found, the method returns an empty
    /// list.</returns>
    internal static IEnumerable<List<(int, int)>> FindUnusedRegions(HashSet<(int, int)> usedPositions, int totalRows, int totalColumns)
    {
        var regions = new List<List<(int, int)>>();
        var visited = new bool[totalRows, totalColumns];
    
        for (int row = 0; row < totalRows; row++)
        {
            for (int column = 0; column < totalColumns; column++)
            {
                if (!usedPositions.Contains((row, column)) && !visited[row, column])
                {
                    var region = new List<(int, int)>();
                    var queue = new Queue<(int, int)>();
                    queue.Enqueue((row, column));
                    visited[row, column] = true;
    
                    while (queue.Count > 0)
                    {
                        var (currentRow, currentColumn) = queue.Dequeue();
                        region.Add((currentRow, currentColumn));
                        for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
                        {
                            for (int columnOffset = -1; columnOffset <= 1; columnOffset++)
                            {
                                if (rowOffset == 0 && columnOffset == 0)
                                {
                                    continue;
                                }
    
                                int neighborRow = currentRow + rowOffset, neighborColumn = currentColumn + columnOffset;
                                if (neighborRow >= 0 && neighborRow < totalRows && neighborColumn >= 0 && neighborColumn < totalColumns &&
                                    !usedPositions.Contains((neighborRow, neighborColumn)) && !visited[neighborRow, neighborColumn])
                                {
                                    queue.Enqueue((neighborRow, neighborColumn));
                                    visited[neighborRow, neighborColumn] = true;
                                }
                            }
                        }
                    }
                    if (region.Any())
                    {
                        regions.Add(region);
                    }
                }
            }
        }
        return regions;
    }

    // TODO: might need to incorporate the state of ambiguousKnownWordStrings (e.g., a hash of the sorted list of remaining strings)
    // if we want more precise memoization when ambiguous words are still pending
    private ulong GetStateKeyBitmask(HashSet<(int, int)> usedPositions)
    {
        ulong key = 0;
        foreach (var (row, col) in usedPositions.OrderBy(p => p.Item1).ThenBy(p => p.Item2))
        {
            int idx = row * 6 + col;
            if (idx < 64) // overkill?
            {
                key |= 1UL << idx;
            }
        }
        return key;
    }
}