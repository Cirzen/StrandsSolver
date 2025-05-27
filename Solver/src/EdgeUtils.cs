namespace Solver;

public static class EdgeUtils
{
    public static bool EdgesCross(((int Row, int Col) From, (int Row, int Col) To) edge1,
        ((int Row, int Col) From, (int Row, int Col) To) edge2)
    {
        var (a1, a2) = edge1;
        var (b1, b2) = edge2;

        return EdgesCross(a1, a2, b1, b2);
    }

    public static bool EdgesCross((int, int) a1, (int, int) a2, (int, int) b1, (int, int) b2)
    {
        // Exclude cases where the segments share an endpoint
        if (a1 == b1 || a1 == b2 || a2 == b1 || a2 == b2)
        {
            return false;
        }

        return DoSegmentsIntersect(a1, a2, b1, b2);
    }

    private static bool DoSegmentsIntersect((int, int) p1, (int, int) p2, (int, int) q1, (int, int) q2)
    {
        int o1 = Orientation(p1, p2, q1);
        int o2 = Orientation(p1, p2, q2);
        int o3 = Orientation(q1, q2, p1);
        int o4 = Orientation(q1, q2, p2);

        // General case of intersection (segments cross each other)
        if (o1 != 0 && o2 != 0 && o3 != 0 && o4 != 0 && o1 != o2 && o3 != o4)
        {
            return true;
        }
        
        // Collinear cases are not considered "crossing" in the typical sense for this method,
        // but are handled by EdgesOverlap if needed.
        // The original EdgesCross was designed to find X-intersections.

        return false;
    }

    // Returns 0 if P->Q->R is linear, 1 if clockwise, 2 if counterclockwise
    private static int Orientation((int, int) p, (int, int) q, (int, int) r)
    {
        long val = (long)(q.Item2 - p.Item2) * (r.Item1 - q.Item1) -
                   (long)(q.Item1 - p.Item1) * (r.Item2 - q.Item2); // Use long to prevent overflow
        if (val == 0)
        {
            return 0; // Collinear
        }

        return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
    }

    /// <summary>
    /// Given three collinear points p, q, r, the function checks if
    /// point q lies on line segment 'pr'.
    /// </summary>
    private static bool OnSegment((int Row, int Col) p, (int Row, int Col) q, (int Row, int Col) r)
    {
        return (q.Col <= Math.Max(p.Col, r.Col) && q.Col >= Math.Min(p.Col, r.Col) &&
                q.Row <= Math.Max(p.Row, r.Row) && q.Row >= Math.Min(p.Row, r.Row));
    }

    /// <summary>
    /// Checks if two edges overlap. Overlap includes being identical,
    /// or collinear and sharing a segment of non-zero length.
    /// It does not consider merely touching at endpoints as overlap unless they are collinear and extend over each other.
    /// </summary>
    public static bool EdgesOverlap(((int Row, int Col) From, (int Row, int Col) To) edge1,
                                   ((int Row, int Col) From, (int Row, int Col) To) edge2)
    {
        var (p1, q1) = edge1; // p1-q1
        var (p2, q2) = edge2; // p2-q2

        // Check for identical edges (including reversed)
        if ((p1 == p2 && q1 == q2) || (p1 == q2 && q1 == p2))
        {
            return true;
        }

        // Check for collinearity of all four points.
        // If p1, q1, p2 are collinear AND p1, q1, q2 are collinear, then all four are.
        if (Orientation(p1, q1, p2) == 0 && Orientation(p1, q1, q2) == 0)
        {
            // All four points are collinear. Now check for overlap.
            // Check if the bounding boxes of the collinear segments overlap.
            // This means the segments must share some common interval on the line they define.
            bool xOverlap = Math.Max(Math.Min(p1.Col, q1.Col), Math.Min(p2.Col, q2.Col)) <= 
                            Math.Min(Math.Max(p1.Col, q1.Col), Math.Max(p2.Col, q2.Col));
            bool yOverlap = Math.Max(Math.Min(p1.Row, q1.Row), Math.Min(p2.Row, q2.Row)) <= 
                            Math.Min(Math.Max(p1.Row, q1.Row), Math.Max(p2.Row, q2.Row));

            if (xOverlap && yOverlap)
            {
                // To be a true overlap beyond a single shared endpoint,
                // at least one endpoint of one segment must lie strictly between the endpoints of the other,
                // or they must be identical (already checked).
                // Or, more simply, if their combined length is less than the distance between the furthest two points.
                // This is complex. A simpler check for problematic overlap:
                // Do they share more than one point?
                // If p2 lies on p1-q1 (and p2 is not p1 and p2 is not q1), it's an overlap.
                // If q2 lies on p1-q1 (and q2 is not p1 and q2 is not q1), it's an overlap.
                // And vice-versa.

                // If p2 is on segment p1-q1 AND p2 is not an endpoint of p1-q1
                if (OnSegment(p1, p2, q1) && p2 != p1 && p2 != q1)
                {
                    return true;
                }

                // If q2 is on segment p1-q1 AND q2 is not an endpoint of p1-q1
                if (OnSegment(p1, q2, q1) && q2 != p1 && q2 != q1)
                {
                    return true;
                }

                // If p1 is on segment p2-q2 AND p1 is not an endpoint of p2-q2
                if (OnSegment(p2, p1, q2) && p1 != p2 && p1 != q2)
                {
                    return true;
                }

                // If q1 is on segment p2-q2 AND q1 is not an endpoint of p2-q2
                if (OnSegment(p2, q1, q2) && q1 != p2 && q1 != q2)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a path self-intersects.
    /// A path self-intersects if it reuses any position or if any of its non-consecutive edges cross.
    /// </summary>
    /// <param name="path">The WordPath to check.</param>
    /// <returns>True if the path self-intersects, false otherwise.</returns>
    public static bool PathSelfIntersects(WordPath path)
    {
        if (path == null || path.Positions == null || path.Edges == null)
        {
            return false; // Or throw ArgumentNullException, depending on desired behavior
        }

        // 1. Check for reused positions
        // The first position is the start, subsequent positions form the path.
        // A simple Boggle rule is "each letter cell can be used only once".
        var positionSet = new HashSet<(int Row, int Col)>();
        foreach (var position in path.Positions)
        {
            if (!positionSet.Add(position))
            {
                return true; // Duplicate position found
            }
        }

        // 2. Check for non-consecutive edges crossing
        // An edge is defined by two consecutive positions in the path.
        // path.Edges should already represent these.
        for (int i = 0; i < path.Edges.Count; i++)
        {
            // Compare edge 'i' with subsequent non-adjacent edges 'j'
            // j starts from i + 2 because edge i+1 shares an endpoint with edge i and cannot "cross" it in an X.
            for (int j = i + 2; j < path.Edges.Count; j++)
            {
                // Ensure the edges are not sharing an endpoint, which EdgesCross already handles,
                // but this loop structure (j = i + 2) ensures they are non-consecutive.
                if (EdgesCross(path.Edges[i], path.Edges[j]))
                {
                    return true; // Non-consecutive edges cross
                }
            }
        }
        return false;
    }
}