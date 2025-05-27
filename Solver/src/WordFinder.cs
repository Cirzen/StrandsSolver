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

        currentWord += board[r, c];
        path.Add((r, c));

        if (!_trie.StartsWith(currentWord))
        {
            path.RemoveAt(path.Count - 1);
            return;
        }

        if (currentWord.Length >= 4 && _trie.Search(currentWord))
        {
            // Check for edge crossings within the word
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

                // Check if the two edges cross
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
        // Check if two line segments (a1-a2 and b1-b2) intersect
        return EdgeUtils.EdgesCross(a1, a2, b1, b2);
    }

}