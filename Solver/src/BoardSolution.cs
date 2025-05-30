namespace Solver;

/// <summary>
/// Represents a solution to the board, including whether it is solved, 
/// the words used in the solution, and the board state.
/// </summary>
internal class BoardSolution
{
    public bool IsSolved { get; init; } = false;
    public List<WordPath> Words { get; init; } = new();

    // These properties might be useful if the caller needs to know the final state 
    // of positions and edges for a partial or full solution.
    // However, for the current usage, only IsSolved and Words are primarily used by SolverEngine.
    // Keeping them for potential future use or if BoardSolver needs to return this detailed state.
    public HashSet<(int, int)> UsedPositions { get; set; } = new();
    public HashSet<((int, int), (int, int))> UsedEdges { get; set; } = new();
}