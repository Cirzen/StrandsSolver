using Xunit;

namespace Solver.Tests;

public class WordFinderTests
{
    private readonly Trie _trie = new();

    /// <summary>
    /// Asserts that HasEdgeCrossing returns true for a path with crossing edges.
    /// </summary>
    [Fact]
    public void HasEdgeCrossing_ShouldReturnTrue_WhenEdgesCross()
    {
        // Arrange
        var wordFinder = new WordFinder(_trie, 0, 0);
        var path = new List<(int, int)>
        {
            (0, 0), // A
            (1, 1), // E
            (0, 1), // B
            (1, 0) // D
        };

        // Act
        var result = wordFinder.HasEdgeCrossing(path);

        // Assert
        Assert.True(result, "Expected HasEdgeCrossing to return true for a path with crossing edges.");
    }

    /// <summary>
    /// Asserts that HasEdgeCrossing returns false for a path without crossing edges.
    /// </summary>
    [Fact]
    public void HasEdgeCrossing_ShouldReturnFalse_WhenEdgesDoNotCross()
    {
        // Arrange
        var wordFinder = new WordFinder(_trie, 0, 0); // Trie is not needed for this test
        var path = new List<(int, int)>
        {
            (0, 0), // A
            (0, 1), // B
            (1, 1), // E
            (1, 0) // D
        };

        // Act
        var result = wordFinder.HasEdgeCrossing(path);

        // Assert
        Assert.False(result, "Expected HasEdgeCrossing to return false for a path without crossing edges.");
    }

    /// <summary>
    /// Asserts that EdgesCross returns true for intersecting edges.
    /// </summary>
    [Fact]
    public void EdgesCross_ShouldReturnTrue_WhenEdgesIntersect()
    {
        // Arrange
        var wordFinder = new WordFinder(_trie, 0, 0);
        var edge1Start = (0, 0); // A
        var edge1End = (1, 1); // E
        var edge2Start = (0, 1); // B
        var edge2End = (1, 0); // D

        // Act
        var result = wordFinder.EdgesCross(edge1Start, edge1End, edge2Start, edge2End);

        // Assert
        Assert.True(result, "Expected EdgesCross to return true for intersecting edges.");
    }

    /// <summary>
    /// Asserts that EdgesCross returns false for non-intersecting edges.
    /// </summary>
    [Fact]
    public void EdgesCross_ShouldReturnFalse_WhenEdgesDoNotIntersect()
    {
        // Arrange
        var wordFinder = new WordFinder(_trie, 0, 0);
        var edge1Start = (0, 0); // A
        var edge1End = (0, 1); // B
        var edge2Start = (1, 0); // D
        var edge2End = (1, 1); // E

        // Act
        var result = wordFinder.EdgesCross(edge1Start, edge1End, edge2Start, edge2End);

        // Assert
        Assert.False(result, "Expected EdgesCross to return false for non-intersecting edges.");
    }
}