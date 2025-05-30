using System.Text;

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
                logger?.Log(matches.Count > 1
                    ? $"SelectKnownPaths: Found {matches.Count} paths for known word '{knownWordStr}'. All added as potential options."
                    : $"SelectKnownPaths: Found 1 path for known word '{knownWordStr}'. Added as a potential option.");
            }
            else
            {
                // This is a significant situation: a "known word" (which implies it should be in the solution)
                // does not have any corresponding path on the current board.
                logger?.LogError($"SelectKnownPaths: CRITICAL - No paths found on board for known word: '{knownWordStr}'. This word cannot be part of any solution with the current board's paths.");
            }
        }
        logger?.Log($"SelectKnownPaths: Returning {pathsMatchingKnownWords.Count} total paths corresponding to known words.");
        return pathsMatchingKnownWords;
    }

    /// <summary>
    /// Converts a board represented as a 2D char array into a single string.
    /// </summary>
    /// <param name="board">The board to convert</param>
    /// <returns>A string representation of the board</returns>
    internal static string ConvertBoardToString(char[,] board)
    {
        var dimensions = (board.GetLength(0), board.GetLength(1));
        var builder = new StringBuilder(dimensions.Item1 * dimensions.Item2);
        for (int row = 0; row < dimensions.Item1; row++)
        {
            for (int col = 0; col < dimensions.Item2; col++)
            {
                builder.Append(board[row, col]);
            }
        }
        return builder.ToString();
    }
}