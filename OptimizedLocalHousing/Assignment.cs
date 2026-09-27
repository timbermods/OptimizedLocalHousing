using System;
using System.Collections.Generic;

namespace OptimizedLocalHousing;

// Fixed-point costs keep every decision bit-identical on all machines (no floating point in the solver).
public static class Cost
{
    public const int Scale = 16;                       // fixed-point units per route-cost unit
    public const int Unreachable = 10_000_000;         // no usable route (larger than any real route)
    public const int MaxReal = 5_000_000;
    public const int StayBonus = 1 * Scale;            // an adult prefers its current home unless a move saves more
    public const int MinimumGain = Scale / 2;         // a cycle of moves must save at least this in total
    public const int FarMargin = 10 * Scale;           // un-queried homes are estimated this far beyond the worst reachable queried one

    public static int Fixed(float route)
    {
        if (float.IsNaN(route) || float.IsInfinity(route) || route < 0) return Unreachable;
        double scaled = Math.Round((double)route * Scale);
        return scaled >= MaxReal ? MaxReal : (int)scaled;
    }
}

public static class Geometry
{
    // Block-coordinate distance between position i of a and position j of b (x, y, z triples; z counts double).
    public static int Distance(int[] a, int i, int[] b, int j) =>
        Math.Abs(a[i * 3] - b[j * 3]) + Math.Abs(a[i * 3 + 1] - b[j * 3 + 1]) + 2 * Math.Abs(a[i * 3 + 2] - b[j * 3 + 2]);
}

// Minimum-cost assignment of n rows to distinct columns among m >= n (Hungarian algorithm, O(n^2 m)).
// The whole solver state is u, v, p and the next row, so it can be paused between rows and saved.
public static class Hungarian
{
    // Processes rows until the operation budget is spent (a started row is always finished).
    // prepareRow(i) must fill row i of cost (0-based) and returns the work it spent. Returns true once every row is assigned.
    // After completion p[j] (1-based) is the row assigned to column j-1.
    public static bool Step(long[,] cost, int n, long[] u, long[] v, int[] p, ref int row, long budget, Func<int, long> prepareRow)
    {
        long ops = 0; return Step(cost, n, n, u, v, p, ref row, ref ops, budget, prepareRow);
    }
    // The same, counting on from the work already spent (ops), so that one budget can span several assignments.
    public static bool Step(long[,] cost, int n, long[] u, long[] v, int[] p, ref int row, ref long ops, long budget, Func<int, long> prepareRow) =>
        Step(cost, n, n, u, v, p, ref row, ref ops, budget, prepareRow);
    // The same for n rows and m columns (u has n + 1 entries, v and p m + 1; a column no row takes keeps p[j] = 0),
    // also counting on from ops.
    public static bool Step(long[,] cost, int n, int m, long[] u, long[] v, int[] p, ref int row, ref long ops, long budget, Func<int, long> prepareRow)
    {
        var way = new int[m + 1]; var minv = new long[m + 1]; var used = new bool[m + 1];
        while (row <= n && ops < budget)
        {
            ops += prepareRow(row - 1);
            p[0] = row; int j0 = 0;
            for (int j = 0; j <= m; j++) { minv[j] = long.MaxValue; used[j] = false; }
            do
            {
                used[j0] = true; int i0 = p[j0], j1 = 0; long delta = long.MaxValue;
                for (int j = 1; j <= m; j++)
                {
                    if (used[j]) continue;
                    long cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                    if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                }
                for (int j = 0; j <= m; j++)
                    if (used[j]) { u[p[j]] += delta; v[j] -= delta; } else minv[j] -= delta;
                j0 = j1; ops += m;
            } while (p[j0] != 0);
            do { int j1 = way[j0]; p[j0] = p[j1]; j0 = j1; } while (j0 != 0);
            row++;
        }
        return row > n;
    }
}

public static class Cycles
{
    // Splits a balanced multigraph (every node has as many incoming as outgoing edges) into simple cycles.
    // Returns edge-id lists in a deterministic order. Each cycle can be applied on its own without changing
    // how many adults live in any home.
    public static List<int[]> Split(int nodes, int[] from, int[] to)
    {
        int edges = from.Length;
        var start = new int[nodes + 1];
        for (int e = 0; e < edges; e++) start[from[e] + 1]++;
        for (int i = 0; i < nodes; i++) start[i + 1] += start[i];
        var order = new int[edges]; var fill = (int[])start.Clone();
        for (int e = 0; e < edges; e++) order[fill[from[e]]++] = e;
        var pos = new int[nodes]; var onPath = new int[nodes];
        for (int i = 0; i < nodes; i++) onPath[i] = -1;
        var result = new List<int[]>();
        for (int s = 0; s < nodes; s++)
        {
            while (start[s] + pos[s] < start[s + 1])
            {
                var pathEdges = new List<int>(); var pathNodes = new List<int> { s };
                onPath[s] = 0; int cur = s;
                while (true)
                {
                    if (start[cur] + pos[cur] >= start[cur + 1]) throw new InvalidOperationException("Moves are not balanced.");
                    int e = order[start[cur] + pos[cur]++];
                    pathEdges.Add(e); int next = to[e];
                    if (onPath[next] >= 0)
                    {
                        int at = onPath[next];
                        result.Add(pathEdges.GetRange(at, pathEdges.Count - at).ToArray());
                        for (int k = at + 1; k < pathNodes.Count; k++) onPath[pathNodes[k]] = -1;
                        pathNodes.RemoveRange(at + 1, pathNodes.Count - at - 1);
                        pathEdges.RemoveRange(at, pathEdges.Count - at);
                        cur = next;
                        if (pathEdges.Count == 0) { onPath[s] = -1; break; }
                    }
                    else { onPath[next] = pathNodes.Count; pathNodes.Add(next); cur = next; }
                }
            }
        }
        return result;
    }

    // Splits moves that may end in empty beds into units that can each be applied on its own: chains (A leaves home X
    // for Y, Y's B leaves for Z, ..., the last one takes an empty bed) and cycles. A chain leaves one more bed free where
    // it starts and one fewer where it ends; every other home keeps its head count. Chains start in homes the moves take
    // more adults from than they bring, and end in homes they bring more to. Returns edge-id lists in a deterministic order.
    public static List<int[]> SplitChains(int nodes, int[] from, int[] to)
    {
        // A virtual node closes every chain into a cycle: it feeds each home that loses more adults than it gains and
        // drains each home that gains more than it loses. Split returns simple cycles, so each passes it at most once.
        int edges = from.Length; var net = new int[nodes];
        for (int e = 0; e < edges; e++) { net[from[e]]--; net[to[e]]++; }
        var f = new List<int>(from); var t = new List<int>(to);
        for (int h = 0; h < nodes; h++)
        {
            for (int k = 0; k < -net[h]; k++) { f.Add(nodes); t.Add(h); }
            for (int k = 0; k < net[h]; k++) { f.Add(h); t.Add(nodes); }
        }
        var result = new List<int[]>();
        foreach (var cycle in Split(nodes + 1, f.ToArray(), t.ToArray()))
        {
            var real = new List<int>(cycle.Length);
            foreach (int e in cycle) if (e < edges) real.Add(e);
            // A cycle through the virtual node is a chain; rotate it so the moves run from its start to its end.
            int cut = Array.FindIndex(cycle, e => e >= edges && f[e] == nodes);
            if (cut >= 0)
            {
                real.Clear();
                for (int k = 1; k < cycle.Length; k++) { int e = cycle[(cut + k) % cycle.Length]; if (e < edges) real.Add(e); }
            }
            if (real.Count > 0) result.Add(real.ToArray());
        }
        return result;
    }
}
