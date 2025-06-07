using Xunit;

namespace Solver.Tests;

public class EdgeUtilsTests
{
    // Helper to create an edge tuple easily
    private ((int R, int C) From, (int R, int C) To) Edge((int R, int C) from, (int R, int C) to)
        => (from, to);

    // --- Tests for EdgesCross ---
    [Fact]
    public void EdgesCross_SimpleXCrossing_ReturnsTrue()
    {
        var edge1 = Edge((0, 0), (2, 2)); // Diagonal \
        var edge2 = Edge((0, 2), (2, 0)); // Diagonal /
        Assert.True(EdgeUtils.EdgesCross(edge1, edge2));
        Assert.True(EdgeUtils.EdgesCross(edge2, edge1)); // Commutative
    }

    [Fact]
    public void EdgesCross_NonCrossingParallel_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (2, 0)); // Horizontal ---
        var edge2 = Edge((0, 1), (2, 1)); // Horizontal --- (offset)
        Assert.False(EdgeUtils.EdgesCross(edge1, edge2));
    }

    [Fact]
    public void EdgesCross_NonCrossingPerpendicularTouchAtNonEndpoint_ReturnsTrue()
    {
        //   |
        // --+-- (T-junction, but segments cross)
        //   |
        var edge1 = Edge((1, 0), (1, 2)); // Vertical |
        var edge2 = Edge((0, 1), (2, 1)); // Horizontal --
        Assert.True(EdgeUtils.EdgesCross(edge1, edge2));
    }
    
    [Fact]
    public void EdgesCross_SegmentsSharingEndpoint_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (1, 1));
        var edge2 = Edge((1, 1), (2, 0)); // Share (1,1)
        Assert.False(EdgeUtils.EdgesCross(edge1, edge2));
    }

    [Fact]
    public void EdgesCross_CollinearNoOverlap_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (1, 0));
        var edge2 = Edge((2, 0), (3, 0));
        Assert.False(EdgeUtils.EdgesCross(edge1, edge2));
    }

    [Fact]
    public void EdgesCross_CollinearOverlap_ReturnsFalse()
    {
        // EdgesCross is for X-style. Overlap is for EdgesOverlap.
        var edge1 = Edge((0, 0), (2, 0));
        var edge2 = Edge((1, 0), (3, 0));
        Assert.False(EdgeUtils.EdgesCross(edge1, edge2));
    }

    [Fact]
    public void EdgesCross_LShortSegmentsNotCrossing_ReturnsFalse()
    {
        var edge1 = Edge((0,0), (0,1)); // Vertical
        var edge2 = Edge((1,1), (1,0)); // Vertical, offset and reversed, not crossing
        Assert.False(EdgeUtils.EdgesCross(edge1, edge2));
    }


    // --- Tests for EdgesOverlap ---
    [Fact]
    public void EdgesOverlap_IdenticalEdges_ReturnsTrue()
    {
        var edge1 = Edge((0, 0), (2, 0));
        var edge2 = Edge((0, 0), (2, 0));
        Assert.True(EdgeUtils.EdgesOverlap(edge1, edge2));
    }

    [Fact]
    public void EdgesOverlap_IdenticalEdgesReversed_ReturnsTrue()
    {
        var edge1 = Edge((0, 0), (2, 0));
        var edge2 = Edge((2, 0), (0, 0));
        Assert.True(EdgeUtils.EdgesOverlap(edge1, edge2));
    }

    [Fact]
    public void EdgesOverlap_CollinearPartialOverlap_ReturnsTrue()
    {
        var edge1 = Edge((0, 0), (3, 0)); // 0---3
        var edge2 = Edge((1, 0), (2, 0)); //  1-2 (subset)
        Assert.True(EdgeUtils.EdgesOverlap(edge1, edge2));
        Assert.True(EdgeUtils.EdgesOverlap(edge2, edge1)); // Commutative
    }
    
    [Fact]
    public void EdgesOverlap_CollinearPartialOverlapAtEnd_ReturnsTrue()
    {
        var edge1 = Edge((0, 0), (2, 0)); // 0-2
        var edge2 = Edge((1, 0), (3, 0)); //  1---3
        Assert.True(EdgeUtils.EdgesOverlap(edge1, edge2));
    }

    [Fact]
    public void EdgesOverlap_CollinearTouchingAtEndpointNoInternalOverlap_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (1, 0)); // 0-1
        var edge2 = Edge((1, 0), (2, 0)); //   1-2
        Assert.False(EdgeUtils.EdgesOverlap(edge1, edge2));
    }

    [Fact]
    public void EdgesOverlap_CollinearNoOverlap_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (1, 0));
        var edge2 = Edge((2, 0), (3, 0));
        Assert.False(EdgeUtils.EdgesOverlap(edge1, edge2));
    }

    [Fact]
    public void EdgesOverlap_NonCollinear_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (2, 0));
        var edge2 = Edge((0, 1), (2, 1));
        Assert.False(EdgeUtils.EdgesOverlap(edge1, edge2));
    }

    [Fact]
    public void EdgesOverlap_CrossingSegments_ReturnsFalse()
    {
        var edge1 = Edge((0, 0), (2, 2));
        var edge2 = Edge((0, 2), (2, 0));
        Assert.False(EdgeUtils.EdgesOverlap(edge1, edge2)); // Overlap is for collinear sharing
    }

    // --- Tests for PathSelfIntersects ---
    private WordPath CreateWordPath(string word, params (int, int)[] positions)
    {
        return new(word, positions.ToList());
    }

    [Fact]
    public void PathSelfIntersects_NoIntersection_ReturnsFalse()
    {
        var path = CreateWordPath("LINE", (0, 0), (0, 1), (0, 2), (0, 3));
        Assert.False(EdgeUtils.PathSelfIntersects(path));
    }

    [Fact]
    public void PathSelfIntersects_ReusedPosition_ReturnsTrue()
    {
        var path = CreateWordPath("LOOP", (0, 0), (0, 1), (1, 1), (0, 1), (0,2)); // (0,1) reused
        Assert.True(EdgeUtils.PathSelfIntersects(path));
    }

    [Fact]
    public void PathSelfIntersects_NonConsecutiveEdgesCross_ReturnsTrue()
    {
        // Path: (0,0) -> (1,2) -> (2,0) -> (0,1) -> (2,1)
        // Edges: e0: (0,0)-(1,2), e1: (1,2)-(2,0), e2: (2,0)-(0,1), e3: (0,1)-(2,1)
        // e0 ((0,0)-(1,2)) vs e2 ((2,0)-(0,1)) should cross.
        var pathCross = CreateWordPath("CROSSPATH", (0,0), (1,2), (2,0), (0,1), (2,1));
        Assert.True(EdgeUtils.PathSelfIntersects(pathCross));
    }
    
    [Fact]
    public void PathSelfIntersects_SimpleSquarePathNoPositionReuse_ReturnsFalse()
    {
        var path = CreateWordPath("OPENBOX", (0,0), (1,0), (1,1), (0,1));
        Assert.False(EdgeUtils.PathSelfIntersects(path));
    }

    [Fact]
    public void PathSelfIntersects_PathTooShortForNonConsecutiveEdgeCross_ReturnsFalse()
    {
        var path0 = CreateWordPath("A", (0,0));
        var path1 = CreateWordPath("AB", (0,0), (0,1)); 
        var path2 = CreateWordPath("ABC", (0,0), (0,1), (0,2)); 
        Assert.False(EdgeUtils.PathSelfIntersects(path0));
        Assert.False(EdgeUtils.PathSelfIntersects(path1));
        Assert.False(EdgeUtils.PathSelfIntersects(path2));
    }
}