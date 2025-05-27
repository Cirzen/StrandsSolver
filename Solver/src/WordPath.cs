namespace Solver;

public class WordPath
{
    public string Word { get; }
    public List<(int Row, int Col)> Positions { get; }
    public List<((int Row, int Col) From, (int Row, int Col) To)> Edges { get; }

    public WordPath(string word, List<(int Row, int Col)> positions)
    {
        Word = word;
        Positions = positions;
        Edges = new List<((int, int), (int, int))>();

        for (int i = 1; i < positions.Count; i++)
        {
            var from = positions[i - 1];
            var to = positions[i];
            Edges.Add((from, to));
        }
    }

    // for debugging
    public override string ToString()
    {
        return $"{Word} [{string.Join(" -> ", Positions)}]";
    }
}