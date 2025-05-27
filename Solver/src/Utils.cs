namespace Solver;

public class Utils
{
    public static char[,] CreateBoardFromString(string rawBoard)
    {
        if (rawBoard.Length != 48)
        {
            throw new ArgumentException("Board string must be exactly 48 characters long (8x6).");
        }

        var board = new char[8, 6];
        int index = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                board[row, col] = char.ToLower(rawBoard[index++]);
            }
        }

        return board;
    }
    
    public static List<WordPath> SelectKnownPathsFromConsole(IEnumerable<string> knownWords, List<WordPath> allPaths)
    {
        var selectedPaths = new List<WordPath>();

        foreach (var word in knownWords)
        {
            var matches = allPaths
                .Where(wp => wp.Word.Equals(word, StringComparison.OrdinalIgnoreCase))
                .ToList();

            switch (matches.Count)
            {
                case 0:
                    Console.WriteLine($"No paths found for word: {word}");
                    continue;
                case > 1:
                {
                    Console.WriteLine($"\nPaths for \"{word}\":");
                    for (int i = 0; i < matches.Count; i++)
                    {
                        Console.WriteLine($"[{i}] {matches[i]}");
                    }

                    Console.Write("Select index: ");
                    if (int.TryParse(Console.ReadLine(), out int index) && index >= 0 && index < matches.Count)
                    {
                        selectedPaths.Add(matches[index]);
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Skipping.");
                    }

                    break;
                }
                default:
                    selectedPaths.Add(matches[0]);
                    break;
            }
        }

        return selectedPaths;
    }

    /// <summary>
    /// Finds all WordPath objects from a list of all available paths that match the given known word strings.
    /// </summary>
    /// <param name="knownWords">A collection of word strings that are known to be part of the solution.</param>
    /// <param name="allPaths">A list of all WordPath objects found on the board.</param>
    /// <param name="logger">Optional logger for providing feedback.</param>
    /// <returns>A list containing all WordPath objects that match any of the known word strings. 
    /// This list can contain multiple paths for the same word if that word can be formed in multiple ways on the board.</returns>
    public static List<WordPath> SelectKnownPaths(IEnumerable<string> knownWords, List<WordPath> allPaths, ILogger logger = null)
    {
        var pathsMatchingKnownWords = new List<WordPath>();
        if (knownWords == null || !knownWords.Any() || allPaths == null || !allPaths.Any())
        {
            logger?.Log("SelectKnownPaths: No known words or no board paths provided, returning empty list.");
            return pathsMatchingKnownWords;
        }

        // Process known words: trim, lowercase, distinct, and filter out empty strings
        var distinctLowerCaseKnownWords = knownWords
                                          .Select(k => k?.Trim().ToLowerInvariant())
                                          .Where(k => !string.IsNullOrWhiteSpace(k))
                                          .Distinct()
                                          .ToList();

        if (!distinctLowerCaseKnownWords.Any())
        {
            logger?.Log("SelectKnownPaths: Known words list is empty after processing, returning empty list.");
            return pathsMatchingKnownWords;
        }

        logger?.Log($"SelectKnownPaths: Attempting to find paths for known words: {string.Join(", ", distinctLowerCaseKnownWords)}");

        foreach (var knownWordStr in distinctLowerCaseKnownWords)
        {
            var matches = allPaths
                .Where(wp => wp.Word.Equals(knownWordStr, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Any())
            {
                pathsMatchingKnownWords.AddRange(matches); // Add all found paths for this known word
                if (matches.Count > 1)
                {
                    logger?.Log($"SelectKnownPaths: Found {matches.Count} paths for known word '{knownWordStr}'. All added as potential options.");
                }
                else
                {
                    logger?.Log($"SelectKnownPaths: Found 1 path for known word '{knownWordStr}'. Added as a potential option.");
                }
            }
            else
            {
                // This is a significant situation: a "known word" (which implies it should be in the solution)
                // does not have any corresponding path on the current board.
                logger?.LogError($"SelectKnownPaths: CRITICAL - No paths found on board for known word: '{knownWordStr}'. This word cannot be part of any solution with the current board's paths.");
                // Depending on desired behavior, the SolverEngine might need to halt or inform the user prominently.
                // For now, this method will simply not add any paths for this word.
                // If the calling code expects all knownWords to be resolvable to paths, it will need to check.
            }
        }
        logger?.Log($"SelectKnownPaths: Returning {pathsMatchingKnownWords.Count} total paths corresponding to known words.");
        return pathsMatchingKnownWords;
    }
}