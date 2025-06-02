namespace Solver;

/// <summary>
/// Represents a solution to the board, including whether it is solved, 
/// the words used in the solution, and the board state.
/// </summary>
internal class BoardSolution
{
    public bool IsSolved { get; init; } = false;
    public List<WordPath> Words { get; init; } = new();

    public HashSet<(int, int)> UsedPositions { get; set; } = new();
    public HashSet<((int, int), (int, int))> UsedEdges { get; set; } = new();
}