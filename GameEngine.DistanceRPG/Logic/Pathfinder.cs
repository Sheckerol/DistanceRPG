namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// A* grid pathfinding over the map grid (0 = walkable, 1 = wall), ported from
/// the Phaser prototype. Diagonal steps cost 1.414 and require both adjacent
/// cardinal tiles to be open (no corner cutting). The binary min-heap mirrors
/// the JS implementation so tie-breaking — and therefore the chosen path —
/// matches the original.
/// </summary>
public static class Pathfinder
{
    private struct HeapNode
    {
        public int R;
        public int C;
        public double F;
    }

    private sealed class MinHeap
    {
        private readonly List<HeapNode> _data = new();

        public int Count => _data.Count;

        public void Push(HeapNode node)
        {
            _data.Add(node);
            BubbleUp(_data.Count - 1);
        }

        public HeapNode Pop()
        {
            var top = _data[0];
            var last = _data[^1];
            _data.RemoveAt(_data.Count - 1);
            if (_data.Count > 0)
            {
                _data[0] = last;
                SinkDown(0);
            }
            return top;
        }

        private void BubbleUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_data[i].F >= _data[parent].F) break;
                (_data[i], _data[parent]) = (_data[parent], _data[i]);
                i = parent;
            }
        }

        private void SinkDown(int i)
        {
            int n = _data.Count;
            while (true)
            {
                int smallest = i;
                int l = 2 * i + 1;
                int r = 2 * i + 2;
                if (l < n && _data[l].F < _data[smallest].F) smallest = l;
                if (r < n && _data[r].F < _data[smallest].F) smallest = r;
                if (smallest == i) break;
                (_data[i], _data[smallest]) = (_data[smallest], _data[i]);
                i = smallest;
            }
        }
    }

    private static readonly (int Dr, int Dc)[] Dirs =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1),       // cardinal
        (-1, -1), (-1, 1), (1, -1), (1, 1),      // diagonal
    };

    /// <summary>
    /// Find a path from (startR, startC) to (goalR, goalC).
    /// Returns tile positions excluding the start, or null if no path exists.
    /// </summary>
    /// <param name="maxRange">Stop when within this many tiles (Chebyshev) of the goal; 0 = reach it exactly.</param>
    public static List<(int R, int C)>? FindPath(
        int[,] grid, int startR, int startC, int goalR, int goalC, int maxRange = 0)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        if (startR == goalR && startC == goalC) return new List<(int, int)>();

        int Key(int r, int c) => r * cols + c;

        var gScore = new Dictionary<int, double>();
        var cameFrom = new Dictionary<int, int>();
        var open = new MinHeap();

        int startKey = Key(startR, startC);
        gScore[startKey] = 0;
        open.Push(new HeapNode { R = startR, C = startC, F = Heuristic(startR, startC, goalR, goalC) });

        while (open.Count > 0)
        {
            var cur = open.Pop();
            int curKey = Key(cur.R, cur.C);
            double curG = gScore[curKey];

            // If within desired range of goal, reconstruct path
            if (maxRange > 0)
            {
                int dr = Math.Abs(cur.R - goalR);
                int dc = Math.Abs(cur.C - goalC);
                if (Math.Max(dr, dc) <= maxRange && !(cur.R == startR && cur.C == startC))
                    return Reconstruct(cameFrom, curKey, startKey, cols);
            }
            else if (cur.R == goalR && cur.C == goalC)
            {
                return Reconstruct(cameFrom, curKey, startKey, cols);
            }

            foreach (var (dr, dc) in Dirs)
            {
                int nr = cur.R + dr;
                int nc = cur.C + dc;
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (grid[nr, nc] != 0) continue;

                // For diagonal moves, both adjacent cardinal tiles must be walkable
                if (dr != 0 && dc != 0)
                {
                    if (grid[cur.R + dr, cur.C] != 0 || grid[cur.R, cur.C + dc] != 0) continue;
                }

                double moveCost = (dr != 0 && dc != 0) ? 1.414 : 1;
                double tentG = curG + moveCost;
                int nKey = Key(nr, nc);

                if (!gScore.TryGetValue(nKey, out double existing) || tentG < existing)
                {
                    gScore[nKey] = tentG;
                    cameFrom[nKey] = curKey;
                    double h = Heuristic(nr, nc, goalR, goalC);
                    open.Push(new HeapNode { R = nr, C = nc, F = tentG + h });
                }
            }
        }

        return null; // no path found
    }

    private static double Heuristic(int r1, int c1, int r2, int c2)
    {
        // Octile distance — consistent with diagonal movement cost
        int dr = Math.Abs(r1 - r2);
        int dc = Math.Abs(c1 - c2);
        return Math.Max(dr, dc) + 0.414 * Math.Min(dr, dc);
    }

    private static List<(int R, int C)> Reconstruct(
        Dictionary<int, int> cameFrom, int goalKey, int startKey, int cols)
    {
        var path = new List<(int R, int C)>();
        int current = goalKey;
        while (current != startKey)
        {
            path.Add((current / cols, current % cols));
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }
}
