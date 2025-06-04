using Xunit;
using System.Linq; // Required for LINQ methods like Any() and Count()

namespace Solver.Tests;

public class WordFinderTests
{
    /// <summary>
    /// Asserts that HasEdgeCrossing returns true for a path with crossing edges.
    /// </summary>
    [Fact]
    public void HasEdgeCrossing_ShouldReturnTrue_WhenEdgesCross()
    {
        // Arrange
        var trie = new Trie(); // Trie not strictly needed but WordFinder constructor requires it
        var wordFinder = new WordFinder(trie, 0, 0);
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
        var trie = new Trie();
        var wordFinder = new WordFinder(trie, 0, 0);
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
        var trie = new Trie();
        var wordFinder = new WordFinder(trie, 0, 0);
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
        var trie = new Trie();
        var wordFinder = new WordFinder(trie, 0, 0);
        var edge1Start = (0, 0); // A
        var edge1End = (0, 1); // B
        var edge2Start = (1, 0); // D
        var edge2End = (1, 1); // E

        // Act
        var result = wordFinder.EdgesCross(edge1Start, edge1End, edge2Start, edge2End);

        // Assert
        Assert.False(result, "Expected EdgesCross to return false for non-intersecting edges.");
    }

    // --- Tests for DepthFirstSearch(char[,] board) ---

    [Fact]
    public void DepthFirstSearch_WordExists_SimplePath_IsFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("best");
        char[,] board = {
            { 'B', 'E' },
            { 'S', 'T' }
        };

        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        var expectedPath = new List<(int, int)> { (0,0), (0,1), (1,0), (1,1) };

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Single(results);
        AssertPathExists(results, "best", expectedPath);
    }

    [Fact]
    public void DepthFirstSearch_MultipleWords_DistinctPaths_AreFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("test");
        trie.Insert("comb");
        char[,] board = {
            { 'T', 'E', 'S', 'T' },
            { 'C', 'O', 'M', 'B' } 
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        var expectedPathTest = new List<(int, int)> { (0,0), (0,1), (0,2), (0,3) };
        var expectedPathCode = new List<(int, int)> { (1, 0), (1, 1), (1, 2), (1, 3) };

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Equal(2, results.Count);
        AssertPathExists(results, "test", expectedPathTest);
        AssertPathExists(results, "comb", expectedPathCode);
    }

    [Fact]
    public void DepthFirstSearch_Word_WithMultipleValidPaths_FindsAll()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("noon");
        char[,] board = {
            { 'N', 'O' },
            { 'O', 'N' }
        };

        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        var expectedPath1 = new List<(int, int)> { (0,0), (0,1), (1,0), (1,1) };
        var expectedPath2 = new List<(int, int)> { (0,0), (1,0), (0,1), (1,1) };
        var expectedPath3 = new List<(int, int)> { (1, 1), (0, 1), (1, 0), (0, 0) };
        var expectedPath4 = new List<(int, int)> { (1, 1), (1, 0), (0, 1), (0, 0) };


        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Equal(4, results.Count(wp => wp.Word == "noon"));
        AssertPathExists(results, "noon", expectedPath1);
        AssertPathExists(results, "noon", expectedPath2);
        AssertPathExists(results, "noon", expectedPath3);
        AssertPathExists(results, "noon", expectedPath4);
    }

    [Fact]
    public void DepthFirstSearch_WordOnBoard_NotInTrie_IsNotFound()
    {
        // Arrange
        var trie = new Trie(); // Does not contain "xyzp"
        char[,] board = {
            { 'X', 'Y' },
            { 'Z', 'P' }
        };

        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearch_WordInTrie_NotOnBoard_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("apple");
        char[,] board = {
            { 'X', 'Y' },
            { 'Z', 'P' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearch_WordShorterThanMinLength_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("ace");
        char[,] board = Utils.CreateBoardFromString("acexyz", 2, 3); // Example board: { { 'A', 'C', 'E' }, { 'X', 'Y', 'Z' } }
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearch_Word_WithEdgeCrossing_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("cross"); // Word that requires crossing
        char[,] board = {
            { 'C', 'S', 'X' },
            { 'S', 'R', 'O' },
            { 'X', 'W', 'D' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.DoesNotContain(results, wp => wp.Word == "cross");
    }

    [Fact]
    public void DepthFirstSearch_CaseInsensitivity_BoardMixedCase_WordFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("desk");
        char[,] board = {
            { 'D', 'e' },
            { 'S', 'k' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        var expectedPath = new List<(int, int)> { (0,0), (0,1), (1,0), (1,1) };

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Single(results);
        AssertPathExists(results, "desk", expectedPath);
    }

    [Fact]
    public void DepthFirstSearch_PrefixIsWord_And_LongerWordIsWord_BothFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("care");
        trie.Insert("careful");
        char[,] board = {
            { 'C', 'A', 'R', 'E' },
            { 'L', 'U', 'F', 'X' } 
        };
        
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        var expectedPathCare = new List<(int, int)> { (0,0), (0,1), (0,2), (0,3) };
        var expectedPathCareful = new List<(int, int)> { (0,0), (0,1), (0,2), (0,3), (1,2), (1,1), (1,0) };

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Equal(2, results.Count);
        AssertPathExists(results, "care", expectedPathCare);
        AssertPathExists(results, "careful", expectedPathCareful);
    }

    [Fact]
    public void DepthFirstSearch_EmptyBoard_ReturnsEmptyList()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("test");
        char[,] board = new char[0,0];
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));

        // Act
        var results = wordFinder.DepthFirstSearch(board);

        // Assert
        Assert.Empty(results);

        // Test with a non-zero dimension empty board
        char[,] board2 = new char[2,2]; // Contains null chars '\0'
        var wordFinder2 = new WordFinder(trie, board2.GetLength(0), board2.GetLength(1));
        var results2 = wordFinder2.DepthFirstSearch(board2);
        Assert.Empty(results2);
    }

    // --- Tests for DepthFirstSearchForWord(char[,] board, string wordToFind) ---

    [Fact]
    public void DepthFirstSearchForWord_SpecificWordExists_SimplePath_IsFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("target");
        char[,] board = {
            { 'T', 'A', 'R' },
            { 'X', 'G', 'E' },
            { 'X', 'X', 'T' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "target";
        var expectedPath = new List<(int, int)> { (0,0), (0,1), (0,2), (1,1), (1,2), (2,2) }; // Adjust path as needed

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Single(results);
        AssertPathExists(results, "target", expectedPath);
    }

    [Fact]
    public void DepthFirstSearchForWord_SpecificWord_WithMultipleValidPaths_FindsAll()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("noon");
        char[,] board = {
            { 'N', 'O' },
            { 'N', 'O' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "noon";
        var expectedPath1 = new List<(int, int)> { (0,0), (0,1), (1,1), (1,0) };
        var expectedPath2 = new List<(int, int)> { (1,0), (1,1), (0,1), (0,0) };

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Equal(2, results.Count); // Or however many paths you expect
        AssertPathExists(results, "noon", expectedPath1);
        AssertPathExists(results, "noon", expectedPath2);
    }

    [Fact]
    public void DepthFirstSearchForWord_SpecificWord_NotOnBoard_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        char[,] board = {
            { 'X', 'Y' },
            { 'Z', 'P' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "apple";

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearchForWord_SpecificWord_ShorterThanMinLength_ReturnsEmptyList()
    {
        // Arrange
        var trie = new Trie();
        char[,] board = {
            { 'A', 'C', 'E' },
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "ace"; // Length 3

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearchForWord_SpecificWord_WithEdgeCrossing_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        char[,] board = {
            { 'C', 'S', 'X' },
            { 'S', 'R', 'O' },
            { 'X', 'W', 'D' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "cross";

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearchForWord_CaseInsensitivity_BoardMixedCase_TargetLowercase_WordFound()
    {
        // Arrange
        var trie = new Trie();
        char[,] board = {
            { 'T', 'a' },
            { 'R', 'g' },
            { 'E', 't' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "target"; // lowercase
        var expectedPath = new List<(int, int)> { (0,0), (0,1), (1,0), (1,1), (2,0), (2,1) };

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Single(results);
        AssertPathExists(results, "target", expectedPath);
    }

    [Fact]
    public void DepthFirstSearchForWord_SearchForNonTargetWord_ThatExists_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        trie.Insert("other");
        char[,] board = {
            { 'T', 'A', 'R' },
            { 'X', 'E', 'G' }, // Includes valid paths for both OTHER
            { 'H', 'T', 'O' }  // and TARGET
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "target";
        var expectedTargetPath = new List<(int, int)> { (0,0), (0,1), (0,2), (1,2), (1,1), (2,1) };

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Single(results); // Only "target" should be found
        AssertPathExists(results, "target", expectedTargetPath);
        Assert.False(results.Any(wp => wp.Word == "other"), "Should not find 'other' when searching for 'target'.");
    }

    [Fact]
    public void DepthFirstSearchForWord_SearchForWord_WhenOnlyItsPrefixExistsOnBoard_IsNotFound()
    {
        // Arrange
        var trie = new Trie();
        char[,] board = {
            { 'C', 'A', 'R', 'E' },
            { 'F', 'U', 'L', 'X' }
        };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "careful";

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void DepthFirstSearchForWord_SearchFor_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var trie = new Trie();
        char[,] board = { { 'A' } };
        var wordFinder = new WordFinder(trie, board.GetLength(0), board.GetLength(1));
        string wordToFind = "";

        // Act
        var results = wordFinder.DepthFirstSearchForWord(board, wordToFind);

        // Assert
        Assert.Empty(results);
    }

    // Helper method to assert a specific path exists in the results
    private void AssertPathExists(List<WordPath> results, string expectedWord, List<(int, int)> expectedPositions)
    {
        bool pathFound = results.Any(wp =>
            wp.Word.Equals(expectedWord, StringComparison.OrdinalIgnoreCase) &&
            wp.Positions.SequenceEqual(expectedPositions)
        );
        Assert.True(pathFound, $"Expected to find word '{expectedWord}' with path [{string.Join(", ", expectedPositions)}]");
    }
}