namespace Solver;

public class WordFinder
{
    private readonly Trie _trie;
    private readonly int _rows, _cols;

    private readonly int[,] _directions = new int[,]
    {
        { -1, -1 }, { -1, 0 }, { -1, 1 },
        { 0, -1  },            {  0, 1 },
        { 1, -1  }, {  1, 0 }, {  1, 1 }
    };

    public WordFinder(Trie trie, int rows, int cols)
    {
        _trie = trie;
        _rows = rows;
        _cols = cols;
    }

    /// <summary>
    /// Performs a depth-first search on the board to find all valid word paths.
    /// </summary>
    /// <param name="board"></param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public List<WordPath> DepthFirstSearch(char[,] board)
    {
        var foundPaths = new List<WordPath>();
        bool[,] visited = new bool[_rows, _cols];

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                DepthFirstSearchRecursive(board, visited, r, c, "", new(), foundPaths);
            }
        }

        return foundPaths;
    }

    private void DepthFirstSearchRecursive(char[,] board, bool[,] visited, int r, int c, string currentWord,
        List<(int, int)> path, List<WordPath> results)
    {
        if (r < 0 || c < 0 || r >= _rows || c >= _cols || visited[r, c])
        {
            return;
        }

        currentWord += char.ToLowerInvariant(board[r, c]);
        path.Add((r, c));

        if (!_trie.StartsWith(currentWord))
        {
            path.RemoveAt(path.Count - 1);
            return;
        }

        if (currentWord.Length >= 4 && _trie.Search(currentWord))
        {
            if (!HasEdgeCrossing(path))
            {
                results.Add(new(currentWord, new List<(int, int)>(path)));
            }
        }

        visited[r, c] = true;

        for (int i = 0; i < 8; i++)
        {
            int newRow = r + _directions[i, 0];
            int newCol = c + _directions[i, 1];
            DepthFirstSearchRecursive(board, visited, newRow, newCol, currentWord, path, results);
        }

        visited[r, c] = false;
        path.RemoveAt(path.Count - 1);
    }

    /// <summary>
    /// Performs a depth-first search on the board to find all paths for a specific word.
    /// </summary>
    /// <param name="board">The game board.</param>
    /// <param name="wordToFind">The specific word to find paths for (should be lowercase).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of WordPath objects for the found word.</returns>
    public List<WordPath> DepthFirstSearchForWord(char[,] board, string wordToFind)
    {
        var foundPaths = new List<WordPath>();
        if (string.IsNullOrEmpty(wordToFind) || wordToFind.Length < 4) 
        {
            return foundPaths;
        }

        bool[,] visited = new bool[_rows, _cols];

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                DepthFirstSearchForWordRecursive(board, visited, r, c, "", new List<(int, int)>(), foundPaths, wordToFind);
            }
        }
        return foundPaths;
    }

    private void DepthFirstSearchForWordRecursive(
        char[,] board,
        bool[,] visited,
        int r, int c,
        string currentPathString,
        List<(int, int)> currentPathCoords,
        List<WordPath> results,
        string wordToFind
        )
    {
        if (r < 0 || c < 0 || r >= _rows || c >= _cols || visited[r, c])
        {
            return;
        }

        string newWordSegment = currentPathString + char.ToLowerInvariant(board[r, c]);
        currentPathCoords.Add((r, c));

        if (newWordSegment.Length > wordToFind.Length || !wordToFind.StartsWith(newWordSegment, StringComparison.Ordinal))
        {
            currentPathCoords.RemoveAt(currentPathCoords.Count - 1); // Backtrack
            return;
        }

        if (newWordSegment.Equals(wordToFind, StringComparison.Ordinal))
        {
            if (newWordSegment.Length >= 4)
            {
                if (!HasEdgeCrossing(currentPathCoords))
                {
                    results.Add(new WordPath(newWordSegment, new List<(int, int)>(currentPathCoords)));
                }
            }
            // We don't return here; there might be other valid paths that extend from this point.
        }

        visited[r, c] = true;

        if (newWordSegment.Length < wordToFind.Length)
        {
            for (int i = 0; i < 8; i++)
            {
                int newRow = r + _directions[i, 0];
                int newCol = c + _directions[i, 1];
                DepthFirstSearchForWordRecursive(board, visited, newRow, newCol, newWordSegment, currentPathCoords, results, wordToFind);
            }
        }
        
        // Backtrack
        visited[r, c] = false;
        currentPathCoords.RemoveAt(currentPathCoords.Count - 1);
    }

    public static List<WordPath> GetWordPathsFor(string word, List<WordPath> allPaths)
    {
        return allPaths
            .Where(wp => wp.Word.Equals(word, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    internal bool HasEdgeCrossing(List<(int, int)> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            var edge1Start = path[i - 1];
            var edge1End = path[i];

            for (int j = i + 1; j < path.Count; j++)
            {
                var edge2Start = path[j - 1];
                var edge2End = path[j];

                if (EdgesCross(edge1Start, edge1End, edge2Start, edge2End))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal bool EdgesCross((int, int) a1, (int, int) a2, (int, int) b1, (int, int) b2)
    {
        return EdgeUtils.EdgesCross(a1, a2, b1, b2);
    }
}