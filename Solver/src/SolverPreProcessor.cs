using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Solver;

public class SolverPreProcessor
{
    private readonly Trie _trie;
    private readonly ILogger _logger;

    public SolverPreProcessor(Trie trie, ILogger logger)
    {
        _trie = trie;
        _logger = logger;
    }

    /// <summary>
    /// Filters out problematic starter words from a list of potential words on the board.
    /// </summary>
    /// <remarks>A starter word is considered problematic if placing it on the board results in: <list
    /// type="bullet"> <item> <description>A region of unused cells that is smaller than <paramref
    /// name="minWordLength"/>.</description> </item> <item> <description>A region of unused cells where no other word
    /// from the original list can fit without overlapping the starter word.</description> </item> <item> <description>A
    /// region of unused cells that cannot be fully covered by other words.</description> </item> </list> The method
    /// logs details about any problematic words it removes using the provided <paramref name="logger"/>.</remarks>
    /// <param name="allWordsOnBoard">A list of all potential words on the board, represented as <see cref="WordPath"/> objects.</param>
    /// <param name="boardRows">The number of rows on the board.</param>
    /// <param name="boardCols">The number of columns on the board.</param>
    /// <param name="logger">An <see cref="ILogger"/> instance used to log details about problematic words.</param>
    /// <param name="minWordLength">The minimum length a word must have to be considered valid. Defaults to 4.</param>
    /// <returns>A filtered list of <see cref="WordPath"/> objects, excluding words that create problematic board configurations.</returns>
    internal static List<WordPath> FilterProblematicStarterWords(
        List<WordPath> allWordsOnBoard,
        int boardRows,
        int boardCols,
        ILogger logger,
        int minWordLength = 4)
    {
        var prunedWordList = new List<WordPath>();
        logger.Log("Performing initial word pruning...");
        foreach (var potentialStarterWord in allWordsOnBoard)
        {
            // Simulate placing this word
            var usedPositionsAfterStarter = new HashSet<(int, int)>(potentialStarterWord.Positions);

            // Find the smallest region *after* placing this word
            var regions = BoardSolver.FindUnusedRegions(usedPositionsAfterStarter, boardRows, boardCols)
                .OrderBy(r => r.Count)
                .ToList();

            bool isStarterWordProblematic = false;

            if (regions.Any())
            {
                var smallestRegion = regions[0];

                // Check 1: Is the smallest region too small for any word?
                if (smallestRegion.Count < minWordLength)
                {
                    isStarterWordProblematic = true;
                }
                else
                {
                    // Check 2: Can any *other* word from the original list fit into this smallest region
                    // without overlapping the starter word?
                    var candidateWordsForSmallestRegion = allWordsOnBoard
                        .Where(w => w != potentialStarterWord && // Must be a different word
                                    w.Positions.All(p => smallestRegion.Contains(p)) && // Fits in region
                                    !w.Positions.Any(p => usedPositionsAfterStarter.Contains(p))) // Doesn't overlap starter
                        .ToList();

                    if (!candidateWordsForSmallestRegion.Any())
                    {
                        // No other word can fit into the smallest region created by this starter word
                        isStarterWordProblematic = true;
                    }
                    else
                    {
                        // Optional Check 3: Can this smallest region actually be fully covered?
                        // This is similar to the check in GetRankedCandidateWordsForNextStep
                        if (!smallestRegion.All(p => candidateWordsForSmallestRegion.SelectMany(wp => wp.Positions).Distinct().Contains(p)))
                        {
                            isStarterWordProblematic = true;
                        }
                    }
                }
            }
            // If regions.Any() is false, it means placing potentialStarterWord filled the whole board.
            // This is a valid single-word solution if TotalCells == potentialStarterWord.Positions.Count.
            // Or, if it didn't fill the board but left no contiguous regions (e.g. checkerboard of used/unused),
            // FindUnusedRegions would return multiple 1-cell regions, which would likely fail the minWordLength check.

            if (!isStarterWordProblematic)
            {
                prunedWordList.Add(potentialStarterWord);
            }
        }
        return prunedWordList;
    }

    /// <summary>
    /// Pre-processes the board to prepare for solving.
    /// </summary>
    /// <param name="board">The board represented as a 2D character array.</param>
    /// <param name="token">A cancellation token to handle task cancellation.</param>
    /// <returns>A list of <see cref="WordPath"/> objects representing potential words on the board.</returns>
    public List<WordPath> PreProcessBoard(char[,] board, CancellationToken token)
    {
        _logger.Log("Starting board pre-processing (SolverPreProcessor).");

        // Example: If WordFinder was a separate step called from here.
        // var wordFinder = new WordFinder(board, _trie);
        // List<WordPath> allPaths = wordFinder.FindAllWords(token);
        // _logger.Log($"Found {allPaths.Count} initial paths during pre-processing.");

        // Perform any additional filtering or scoring if necessary.
        // For this caching change, the main responsibility of generating the initial
        // cachable list is shifted to be directly called by SolverEngine,
        // or SolverEngine uses a component like WordFinder that does this.

        // This class might be more about refining a list of paths if needed,
        // but the caching logic is now primarily in SolverEngine.
        // If this class was responsible for generating the initial list of WordPaths,
        // SolverEngine would call a method here.

        // For the current structure, SolverEngine uses WordFinder directly for the initial set of paths.
        // This class could be used for more advanced pruning if WordFinder returns too many results
        // or if there are board-specific heuristics to apply before the main BoardSolver.

        // Return an empty list or throw NotImplementedException if this method is not meant to be called directly
        // in the new flow. For now, let's assume it's not the primary source of the cachable list.
        return new List<WordPath>();
    }

    /// <summary>
    /// Filters a list of paths based on user-excluded words.
    /// </summary>
    /// <param name="paths">The list of <see cref="WordPath"/> objects to filter.</param>
    /// <param name="userExcludedWords">A list of words to exclude.</param>
    /// <param name="token">A cancellation token to handle task cancellation.</param>
    /// <returns>A filtered list of <see cref="WordPath"/> objects.</returns>
    public List<WordPath> FilterPaths(List<WordPath> paths, List<string> userExcludedWords, CancellationToken token)
    {
        if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();

        var filtered = paths
            .Where(p => !userExcludedWords.Contains(p.Word, System.StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.Log($"Filtered paths from {paths.Count} to {filtered.Count} based on user exclusions.");
        return filtered;
    }
}