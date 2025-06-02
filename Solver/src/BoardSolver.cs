namespace Solver;

/// <summary>
/// Provides functionality to solve a board by finding a valid sequence of words that covers all cells.
/// </summary>
internal class BoardSolver
{
    private const int TotalCells = 48;
    private readonly ILogger _logger;
    private HashSet<ulong> _failedStates = new();

    public BoardSolver(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ClearFailedStatesCache()
    {
        _failedStates.Clear();
        _logger?.Log("BoardSolver: Failed states cache cleared.");
    }

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
            _logger?.Log($"SolveAsync: Base case solution found! All known words placed and board is full. Word count: {currentSolutionSoFar.Count}");
            return new() { IsSolved = true, Words = new(currentSolutionSoFar) };
        }

        var currentStateKey = GetStateKeyBitmask(currentUsedPositions, ambiguousKnownWordStrings);
        if (_failedStates.Contains(currentStateKey))
        {
            return new() { IsSolved = false }; // Memoization hit
        }

        await HandleProgressReportingAsync(progressTracker, currentSolutionSoFar, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return new() { IsSolved = false };
        }

        BoardSolution solution;
        if (ambiguousKnownWordStrings.Any())
        {
            solution = await SolveForAmbiguousWordsAsync(
                allCandidatePathsOnBoard, currentUsedPositions, currentUsedEdges, currentSolutionSoFar,
                cancellationToken, progressTracker, ambiguousKnownWordStrings, allPathsForAmbiguousKnownWords);
        }
        else
        {
            // If no ambiguous words and board is full, it's a solution (already covered by base case 1, but good check)
            if (currentUsedPositions.Count == TotalCells) 
            {
                 _logger?.Log($"SolveAsync: General words path found solution. Word count: {currentSolutionSoFar.Count}");
                return new() { IsSolved = true, Words = new(currentSolutionSoFar) };
            }
            solution = await SolveForGeneralWordsAsync(
                allCandidatePathsOnBoard, currentUsedPositions, currentUsedEdges, currentSolutionSoFar,
                cancellationToken, progressTracker, allPathsForAmbiguousKnownWords);
        }

        if (!solution.IsSolved)
        {
            _failedStates.Add(currentStateKey);
        }
        return solution;
    }

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
                    !wordsInCurrentSolution.Contains(w.Word.ToLowerInvariant()) &&
                    w.Positions.All(p => smallestRegion.Contains(p)) &&
                    IsValidWordPlacement(w, currentUsedPositions, currentUsedEdges)) // Use IsValidWordPlacement
                .ToList();

            if (candidateRegionWords.Count == 0) return new();
            if (!smallestRegion.All(p => candidateRegionWords.SelectMany(wp => wp.Positions)
                    .Distinct()
                    .Contains(p)))
                return new();

            var positionFrequencyInRegionCandidates = candidateRegionWords
                .SelectMany(w => w.Positions)
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => g.Count());
            progressTracker.CurrentHeatMap = new(positionFrequencyInRegionCandidates);

            return candidateRegionWords
                .Select(w => new
                {
                    WordPath = w,
                    Score = w.Positions.Sum(p => positionFrequencyInRegionCandidates.TryGetValue(p, out int freq) ? 1.0 / freq : 0.0) + (w.Word.Length * 0.01)
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.WordPath)
                .ToList();
        }
        if (currentUsedPositions.Count != TotalCells) _logger?.Log("No unused regions found, but board not full.");
        return new();
    }

    internal bool IsValidWordPlacement(WordPath wordToPlace, HashSet<(int, int)> existingUsedPositions, HashSet<((int, int), (int, int))> existingUsedEdges)
    {
        if (wordToPlace.Positions.Any(p => existingUsedPositions.Contains(p))) return false;
        if (wordToPlace.Edges.Any(newEdge => existingUsedEdges.Any(existingEdge => EdgeUtils.EdgesCross(newEdge, existingEdge) || EdgeUtils.EdgesOverlap(newEdge, existingEdge)))) return false;
        return !EdgeUtils.PathSelfIntersects(wordToPlace);
    }

    internal static IEnumerable<List<(int, int)>> FindUnusedRegions(HashSet<(int, int)> usedPositions, int totalRows, int totalColumns)
    {
        var regions = new List<List<(int, int)>>();
        var visited = new bool[totalRows, totalColumns];
        for (int r = 0; r < totalRows; r++)
        {
            for (int c = 0; c < totalColumns; c++)
            {
                if (!usedPositions.Contains((r, c)) && !visited[r, c])
                {
                    var region = new List<(int, int)>();
                    var q = new Queue<(int, int)>();
                    q.Enqueue((r, c));
                    visited[r, c] = true;
                    while (q.Count > 0)
                    {
                        var (currR, currC) = q.Dequeue();
                        region.Add((currR, currC));
                        for (int ro = -1; ro <= 1; ro++) for (int co = -1; co <= 1; co++)
                        {
                            if (ro == 0 && co == 0) continue;
                            int nr = currR + ro, nc = currC + co;
                            if (nr >= 0 && nr < totalRows && nc >= 0 && nc < totalColumns &&
                                !usedPositions.Contains((nr, nc)) && !visited[nr, nc])
                            {
                                q.Enqueue((nr, nc));
                                visited[nr, nc] = true;
                            }
                        }
                    }
                    if (region.Any()) regions.Add(region);
                }
            }
        }
        return regions;
    }

    private ulong GetStateKeyBitmask(HashSet<(int, int)> usedPositions, List<string> ambiguousKnownWordStrings)
    {
        ulong key = 0;
        foreach (var (row, col) in usedPositions.OrderBy(p => p.Item1).ThenBy(p => p.Item2))
        {
            int idx = row * 6 + col; // Assuming 6 columns
            if (idx < 64) key |= 1UL << idx;
        }
        if (ambiguousKnownWordStrings.Any())
        {
            key ^= (ulong)string.Join(",", ambiguousKnownWordStrings.OrderBy(s => s)).GetHashCode();
        }
        return key;
    }

    private async Task HandleProgressReportingAsync(ProgressTracker progressTracker, List<WordPath> currentSolutionSoFar, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;

        int updateIntervalMilliseconds = App.ConfigService.Settings.ProgressUpdateIntervalMilliseconds;
        if ((DateTime.Now - progressTracker.LastProgressUpdate).TotalMilliseconds >= updateIntervalMilliseconds)
        {
            long wordsInInterval = progressTracker.GetAndResetWordsAttemptedSinceLastReport();
            double wpsInterval = (updateIntervalMilliseconds > 0) ? (wordsInInterval / (updateIntervalMilliseconds / 1000.0)) : 0;
            if (double.IsNaN(wpsInterval) || double.IsInfinity(wpsInterval)) wpsInterval = 0;

            await progressTracker.ReportProgress(new List<WordPath>(currentSolutionSoFar), (long)wpsInterval, progressTracker.CurrentHeatMap);
            progressTracker.LastProgressUpdate = DateTime.Now;
        }
    }

    private async Task<BoardSolution> SolveForAmbiguousWordsAsync(
        List<WordPath> allCandidatePathsOnBoard, HashSet<(int, int)> currentUsedPositions,
        HashSet<((int, int), (int, int))> currentUsedEdges, List<WordPath> currentSolutionSoFar,
        CancellationToken cancellationToken, ProgressTracker progressTracker,
        List<string> ambiguousKnownWordStrings, List<WordPath> allPathsForAmbiguousKnownWords)
    {
        if (cancellationToken.IsCancellationRequested) return new() { IsSolved = false };

        string wordToPlaceNowStr = ambiguousKnownWordStrings.First();
        var remainingAmbiguousStrings = ambiguousKnownWordStrings.Skip(1).ToList();
        var pathsToTryForThisWord = allPathsForAmbiguousKnownWords
            .Where(p => p.Word.Equals(wordToPlaceNowStr, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!pathsToTryForThisWord.Any())
        {
            _logger?.LogError($"SolveForAmbiguousWordsAsync: No paths for ambiguous known word '{wordToPlaceNowStr}'.");
            return new() { IsSolved = false };
        }

        foreach (var pathOption in pathsToTryForThisWord)
        {
            progressTracker.IncrementWordsAttempted();
            if (cancellationToken.IsCancellationRequested) return new() { IsSolved = false };
            if (!IsValidWordPlacement(pathOption, currentUsedPositions, currentUsedEdges)) continue;

            currentSolutionSoFar.Add(pathOption);
            foreach (var pos in pathOption.Positions)
            {
                currentUsedPositions.Add(pos);
            }

            foreach (var edge in pathOption.Edges)
            {
                currentUsedEdges.Add(edge);
            }

            var result = await SolveAsync(allCandidatePathsOnBoard, currentUsedPositions, currentUsedEdges,
                                          currentSolutionSoFar, cancellationToken, progressTracker,
                                          remainingAmbiguousStrings, allPathsForAmbiguousKnownWords);
            if (result.IsSolved) return result;

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

    private async Task<BoardSolution> SolveForGeneralWordsAsync(
        List<WordPath> allCandidatePathsOnBoard, HashSet<(int, int)> currentUsedPositions,
        HashSet<((int, int), (int, int))> currentUsedEdges, List<WordPath> currentSolutionSoFar,
        CancellationToken cancellationToken, ProgressTracker progressTracker,
        List<WordPath> allPathsForAmbiguousKnownWords) // allPathsForAmbiguousKnownWords might not be needed here if only passed for ambiguous
    {
        if (cancellationToken.IsCancellationRequested) return new() { IsSolved = false };
        
        // This check is technically redundant if the main SolveAsync's base case is hit,
        // but good for clarity within this specific path.
        if (currentUsedPositions.Count == TotalCells) 
        {
            _logger?.Log($"SolveForGeneralWordsAsync: Board full. Word count: {currentSolutionSoFar.Count}");
            return new() { IsSolved = true, Words = new(currentSolutionSoFar) };
        }

        var wordsInCurrentSolutionSet = currentSolutionSoFar.Select(wp => wp.Word.ToLowerInvariant()).ToHashSet();
        // Order the words for the next step, prioritizing those that fit best in the current unused regions.
        var rankedGeneralCandidates = GetRankedCandidateWordsForNextStep(
            allCandidatePathsOnBoard, currentUsedPositions, currentUsedEdges, progressTracker, wordsInCurrentSolutionSet);

        if (!rankedGeneralCandidates.Any() && currentUsedPositions.Count != TotalCells)
        {
            // No candidates and board not full, this path fails.
            // The main SolveAsync will add to _failedStates based on this method's return.
            return new() { IsSolved = false };
        }

        foreach (var wordToTry in rankedGeneralCandidates)
        {
            progressTracker.IncrementWordsAttempted();
            if (cancellationToken.IsCancellationRequested) return new() { IsSolved = false };

            currentSolutionSoFar.Add(wordToTry);
            foreach (var pos in wordToTry.Positions)
            {
                currentUsedPositions.Add(pos);
            }

            foreach (var edge in wordToTry.Edges)
            {
                currentUsedEdges.Add(edge);
            }

            var result = await SolveAsync(allCandidatePathsOnBoard, currentUsedPositions, currentUsedEdges,
                                          currentSolutionSoFar, cancellationToken, progressTracker,
                                          new List<string>(), // No ambiguous words left to process down this path
                                          allPathsForAmbiguousKnownWords); 
            if (result.IsSolved) return result;

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
        return new() { IsSolved = false };
    }
}