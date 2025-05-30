using Xunit;

namespace Solver.Tests;

// A simple mock ILogger for testing purposes

public class BoardSolverTests
{
    private readonly BoardSolver _solver;
    private readonly MockLogger _mockLogger;

    private readonly ProgressTracker _dummyProgressTracker = new(
        (words, wordsAttempted, heatMap) => Task.CompletedTask
    );

    public BoardSolverTests()
    {
        _mockLogger = new();
        _solver = new(_mockLogger);
    }

    // Helper to create a WordPath for tests
    private WordPath CreateWordPath(string word, params (int, int)[] positions)
    {
        return new(word, positions.ToList());
    }

    // --- Tests for IsValidWordPlacement ---

    [Fact]
    public void IsValidWordPlacement_ValidPlacement_ReturnsTrue()
    {
        var wordToPlace = CreateWordPath("TEST", (0, 0), (0, 1), (0, 2));
        var existingUsedPositions = new HashSet<(int, int)>();
        var existingUsedEdges = new HashSet<((int, int), (int, int))>();
        Assert.True(_solver.IsValidWordPlacement(wordToPlace, existingUsedPositions, existingUsedEdges));
    }

    [Fact]
    public void IsValidWordPlacement_PositionOverlap_ReturnsFalse()
    {
        var wordToPlace = CreateWordPath("TEST", (0, 0), (0, 1), (0, 2));
        var existingUsedPositions = new HashSet<(int, int)> { (0, 1) };
        var existingUsedEdges = new HashSet<((int, int), (int, int))>();
        Assert.False(_solver.IsValidWordPlacement(wordToPlace, existingUsedPositions, existingUsedEdges));
    }

    [Fact]
    public void IsValidWordPlacement_EdgeCrossingXStyle_ReturnsFalse()
    {
        var wordToPlace = CreateWordPath("DIAG1", (0, 0), (1, 1));
        var existingUsedPositions = new HashSet<(int, int)>();
        var existingEdge = (From: (0, 1), To: (1, 0));
        var existingUsedEdges = new HashSet<((int, int), (int, int))> { existingEdge };
        // IsValidWordPlacement checks: EdgeUtils.EdgesCross || EdgeUtils.EdgesOverlap
        // For this X-style, EdgesCross should be true.
        Assert.False(_solver.IsValidWordPlacement(wordToPlace, existingUsedPositions, existingUsedEdges));
    }

    [Fact]
    public void IsValidWordPlacement_EdgeOverlapCollinear_ReturnsFalse()
    {
        // Word to place has edge ((0,0)-(1,0))
        var wordToPlace = CreateWordPath("SUB", (0, 0), (1, 0));
        var existingUsedPositions = new HashSet<(int, int)>();
        // Existing edge ((0,0)-(2,0)) overlaps with wordToPlace's edge.
        var existingEdge = (From: (0, 0), To: (2, 0));
        var existingUsedEdges = new HashSet<((int, int), (int, int))> { existingEdge };
        // IsValidWordPlacement checks: EdgeUtils.EdgesCross || EdgeUtils.EdgesOverlap
        // For this, EdgesOverlap should be true.
        Assert.False(_solver.IsValidWordPlacement(wordToPlace, existingUsedPositions, existingUsedEdges));
    }

    [Fact]
    public void IsValidWordPlacement_NoEdgeConflict_ReturnsTrue()
    {
        var wordToPlace = CreateWordPath("LINE", (0, 0), (0, 1));
        var existingUsedPositions = new HashSet<(int, int)>();
        var existingEdge = (From: (1, 0), To: (1, 1));
        var existingUsedEdges = new HashSet<((int, int), (int, int))> { existingEdge };
        Assert.True(_solver.IsValidWordPlacement(wordToPlace, existingUsedPositions, existingUsedEdges));
    }

    // --- Tests for GetRankedCandidateWordsForNextStep ---

    [Fact]
    public void GetRankedCandidateWordsForNextStep_NoUnusedRegionsAndBoardNotFull_ReturnsEmptyList()
    {
        var allPossibleWords = new List<WordPath> { CreateWordPath("A", (0, 0)) };
        var currentUsedPositions = new HashSet<(int, int)>();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 6; c++)
            {
                if (!(r == 7 && c == 5)) // Leave one cell (7,5) open
                {
                    currentUsedPositions.Add((r, c));
                }
            }
        }
        // Smallest region is (7,5). Word "A" at (0,0) doesn't fit.
        var candidates = _solver.GetRankedCandidateWordsForNextStep(allPossibleWords, currentUsedPositions, new(), _dummyProgressTracker, new());
        Assert.Empty(candidates);
    }

    [Fact]
    public void GetRankedCandidateWordsForNextStep_SmallestRegionNoFittingWords_ReturnsEmptyList()
    {
        var allPossibleWords = new List<WordPath> { CreateWordPath("FAR", (2, 0), (2, 1), (2, 2)) };
        var currentUsedPositions = new HashSet<(int, int)>();
        for (int r = 0; r < 8; r++) // Block columns 2-5, leaving 0-1 open
        {
            for (int c = 2; c < 6; c++)
                currentUsedPositions.Add((r, c));
        }
        var candidates = _solver.GetRankedCandidateWordsForNextStep(allPossibleWords, currentUsedPositions, new(), _dummyProgressTracker, new());
        Assert.Empty(candidates);
    }

    [Fact]
    public void GetRankedCandidateWordsForNextStep_SmallestRegionCellsNotCoverable_ReturnsEmptyList()
    {
        // Smallest region will be (0,0), (0,1), (0,2)
        var allPossibleWords = new List<WordPath> { CreateWordPath("AB", (0, 0), (0, 1)) }; // Covers (0,0), (0,1) but not (0,2)
        var currentUsedPositions = new HashSet<(int, int)>();
        for (int r = 1; r < 8; ++r) { for (int c = 0; c < 6; ++c) currentUsedPositions.Add((r, c)); } // Block rows 1-7
        for (int c = 3; c < 6; ++c) currentUsedPositions.Add((0, c)); // Block (0,3), (0,4), (0,5)

        var candidates = _solver.GetRankedCandidateWordsForNextStep(allPossibleWords, currentUsedPositions, new(), _dummyProgressTracker, new());
        Assert.Empty(candidates);
    }

    [Fact]
    public void GetRankedCandidateWordsForNextStep_FittingWords_ReturnsRankedList()
    {
        // Smallest region: (0,0), (0,1), (0,2)
        var currentUsedPositions = new HashSet<(int, int)>();
        for (int r = 1; r < 8; ++r) { for (int c = 0; c < 6; ++c) currentUsedPositions.Add((r, c)); }
        for (int c = 3; c < 6; ++c) currentUsedPositions.Add((0, c));

        var word1 = CreateWordPath("ACE", (0, 0), (0, 1), (0, 2));
        var word2 = CreateWordPath("AC", (0, 0), (0, 1));
        var word3 = CreateWordPath("CE", (0, 1), (0, 2));
        var allPossibleWords = new List<WordPath> { word1, word2, word3 };

        var candidates = _solver.GetRankedCandidateWordsForNextStep(allPossibleWords, currentUsedPositions, new(), _dummyProgressTracker, new());

        Assert.Equal(3, candidates.Count);
        Assert.Equal("ACE", candidates[0].Word); // Highest score due to length tie-breaker and full coverage
        var nextWords = candidates.Skip(1).Select(c => c.Word).ToList();
        Assert.Contains("AC", nextWords);
        Assert.Contains("CE", nextWords);
    }

    [Fact]
    public void GetRankedCandidateWordsForNextStep_FiltersXCrossingWordsOnly()
    {
        // Arrange
        // Smallest region: (0,0), (0,1), (1,0), (1,1) - a 2x2 square
        var currentUsedPositions = new HashSet<(int, int)>();
        for (int r = 2; r < 8; ++r) { for (int col = 0; col < 6; ++col) currentUsedPositions.Add((r, col)); }
        for (int col = 2; col < 6; ++col) { for (int r = 0; r < 2; ++r) currentUsedPositions.Add((r, col)); }

        var existingEdgeToCross = (From: (0, 1), To: (1, 0)); // Diagonal B-C in the 2x2
        var currentUsedEdges = new HashSet<((int, int), (int, int))> { existingEdgeToCross };
        var wordsInSolution = new HashSet<string>();

        var wordXCrossing = CreateWordPath("XWORD", (0, 0), (1, 1)); // Edge A-D, crosses B-C

        // Safe words that together cover the 2x2 region and do not X-cross existingEdgeToCross
        var wordSafe1 = CreateWordPath("SAFE1", (0, 0), (0, 1)); // Edge A-B (top horizontal)
        var wordSafe2 = CreateWordPath("SAFE2", (1, 0), (1, 1)); // Edge C-D (bottom horizontal)
                                                                 // These cover (0,0),(0,1),(1,0),(1,1)

        var allPossibleWords = new List<WordPath> { wordXCrossing, wordSafe1, wordSafe2 };

        // Act
        var candidates = _solver.GetRankedCandidateWordsForNextStep(
            allPossibleWords,
            currentUsedPositions,
            currentUsedEdges,
            _dummyProgressTracker,
            wordsInSolution
        );

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.DoesNotContain(candidates, c => c.Word == "XWORD");
        Assert.Contains(candidates, c => c.Word == "SAFE1");
        Assert.Contains(candidates, c => c.Word == "SAFE2");
    }

    // --- Tests for FindUnusedRegions ---
    private void AssertRegionsEqual(IEnumerable<List<(int, int)>> expectedRegions, IEnumerable<List<(int, int)>> actualRegions)
    {
        var expectedRegionSets = expectedRegions.Select(r => r.ToHashSet()).ToList();
        var actualRegionSets = actualRegions.Select(r => r.ToHashSet()).ToList();
        Assert.Equal(expectedRegionSets.Count, actualRegionSets.Count);
        foreach (var expectedSet in expectedRegionSets)
        {
            Assert.Contains(actualRegionSets, actualSet => actualSet.SetEquals(expectedSet));
        }
    }

    [Fact]
    public void FindUnusedRegions_EmptyBoard_ReturnsSingleRegionCoveringAllCells()
    {
        var usedPositions = new HashSet<(int, int)>();
        int rows = 3, cols = 3;
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Single(regions);
        Assert.Equal(rows * cols, regions[0].Count);
        var expectedAllCells = new List<(int, int)>();
        for (int r = 0; r < rows; r++) { for (int c = 0; c < cols; c++) expectedAllCells.Add((r, c)); }
        AssertRegionsEqual(new List<List<(int, int)>> { expectedAllCells }, regions);
    }

    [Fact]
    public void FindUnusedRegions_FullyUsedBoard_ReturnsEmptyList()
    {
        var usedPositions = new HashSet<(int, int)>();
        int rows = 3, cols = 3;
        for (int r = 0; r < rows; r++) { for (int c = 0; c < cols; c++) usedPositions.Add((r, c)); }
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Empty(regions);
    }

    [Fact]
    public void FindUnusedRegions_SingleContiguousUnusedRegion_ReturnsCorrectRegion()
    {
        var usedPositions = new HashSet<(int, int)> { (0, 0), (0, 1), (0, 2), (1, 0), (1, 2), (2, 0), (2, 1), (2, 2) };
        int rows = 3, cols = 3;
        var expectedRegion = new List<(int, int)> { (1, 1) };
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Single(regions);
        AssertRegionsEqual(new List<List<(int, int)>> { expectedRegion }, regions);
    }

    [Fact]
    public void FindUnusedRegions_SingleLargerContiguousUnusedRegion_ReturnsCorrectRegion()
    {
        var usedPositions = new HashSet<(int, int)> { (0, 0), (0, 2), (1, 0), (1, 2), (2, 0), (2, 2) };
        int rows = 3, cols = 3;
        var expectedRegion = new List<(int, int)> { (0, 1), (1, 1), (2, 1) }; // Corrected expectation
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Single(regions);
        AssertRegionsEqual(new List<List<(int, int)>> { expectedRegion }, regions);
    }

    [Fact]
    public void FindUnusedRegions_MultipleDisconnectedUnusedRegions_ReturnsCorrectRegions()
    {
        var usedPositions = new HashSet<(int, int)> { (0, 1), (1, 0), (1, 1), (1, 2), (2, 1) };
        int rows = 3, cols = 3;
        var expectedRegion1 = new List<(int, int)> { (0, 0) };
        var expectedRegion2 = new List<(int, int)> { (0, 2) };
        var expectedRegion3 = new List<(int, int)> { (2, 0) };
        var expectedRegion4 = new List<(int, int)> { (2, 2) };
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Equal(4, regions.Count);
        AssertRegionsEqual(new List<List<(int, int)>> { expectedRegion1, expectedRegion2, expectedRegion3, expectedRegion4 }, regions);
    }

    [Fact]
    public void FindUnusedRegions_ComplexDisconnectedRegions_ReturnsCorrectRegions()
    {
        var usedPositions = new HashSet<(int, int)> { (0, 2), (1, 0), (1, 1), (1, 2), (2, 2) };
        int rows = 3, cols = 3;
        var expectedRegion1 = new List<(int, int)> { (0, 0), (0, 1) };
        var expectedRegion2 = new List<(int, int)> { (2, 0), (2, 1) };
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Equal(2, regions.Count);
        AssertRegionsEqual(new List<List<(int, int)>> { expectedRegion1, expectedRegion2 }, regions);
    }

    [Fact]
    public void FindUnusedRegions_BoardWithNoUnusedCells_ReturnsEmptyList()
    {
        var usedPositions = new HashSet<(int, int)>();
        int rows = 2, cols = 2;
        for (int r = 0; r < rows; ++r) { for (int c = 0; c < cols; ++c) usedPositions.Add((r, c)); }
        var regions = BoardSolver.FindUnusedRegions(usedPositions, rows, cols).ToList();
        Assert.Empty(regions);
    }
}