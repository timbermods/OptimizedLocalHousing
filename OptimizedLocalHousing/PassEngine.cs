using System;
using System.Collections.Generic;

namespace OptimizedLocalHousing;

public struct Move { public Guid Adult, From, To, Work; }

// Everything the engine needs from the game. Implemented by HousingService; faked in the tests.
public interface IPassWorld
{
    // Housed adult beavers, their homes and (usable) workplaces, at this instant.
    Snapshot Capture();
    // A fresh route query. False when the home cannot reach the workplace.
    bool TryRoute(Guid home, Guid work, out float cost);
    // Re-validates the moves (a cycle, or a chain whose last move takes an empty bed) against the live game and applies
    // them as a unit. False, with nothing changed, if anyone in it has moved, changed job or died since the snapshot,
    // or a home can no longer take them.
    bool ApplyCycle(Move[] cycle);
}

public sealed class Snapshot
{
    public Guid[] Adults = new Guid[0];         // ascending by ID
    public Guid[] AdultDistrict = new Guid[0];
    public int[] AdultHome = new int[0];         // index into Homes
    public int[] AdultWork = new int[0];         // index into Works, -1 when there is no usable job
    public Guid[] Homes = new Guid[0];           // ascending by ID; homes with an adult in them or an empty bed
    public Guid[] HomeDistrict = new Guid[0];
    public int[] HomeFree = new int[0];          // empty beds per home
    public int[] HomePos = new int[0];           // x, y, z per home
    public Guid[] Works = new Guid[0];           // ascending by ID
    public int[] WorkPos = new int[0];
    public int NearK;                            // candidate homes per workplace row (padding included)
    public int[] Near = new int[0];              // Works.Length * NearK home indices in the workplace's district, nearest
                                                 // first; -1 pads a row when the district has fewer homes than NearK
    public int[] Costs = new int[0];             // fixed-point route cost per Near entry, -1 until queried (padding: Unreachable)
    public int[] RecheckWork = new int[0];       // remembered costs (PassState.Learned*) this pass prices again after the
    public int[] RecheckHome = new int[0];       // rows: workplace and home indices, the home outside the workplace's row
    public int[] RecheckCosts = new int[0];      // fixed-point route cost per recheck, -1 until queried
}

public sealed class SnapshotBuilder
{
    private readonly List<(Guid Id, Guid Home, Guid Work, Guid District)> _adults = new List<(Guid, Guid, Guid, Guid)>();
    private readonly HashSet<Guid> _seen = new HashSet<Guid>();
    private readonly Dictionary<Guid, (Guid District, int X, int Y, int Z, int Free)> _homes = new Dictionary<Guid, (Guid, int, int, int, int)>();
    private readonly Dictionary<Guid, (int X, int Y, int Z)> _works = new Dictionary<Guid, (int, int, int)>();
    // free: the home's empty beds. A home is kept when an adult lives in it, or it has an empty bed in a district with adults.
    public void AddHome(Guid id, Guid district, int x, int y, int z, int free = 0) => _homes[id] = (district, x, y, z, Math.Max(free, 0));
    public void AddWork(Guid id, int x, int y, int z) => _works[id] = (x, y, z);
    public void AddAdult(Guid id, Guid home, Guid work, Guid district) { if (_seen.Add(id)) _adults.Add((id, home, work, district)); }
    public Snapshot Build()
    {
        var adults = new List<(Guid Id, Guid Home, Guid Work, Guid District)>();
        foreach (var a in _adults)
            if (_homes.TryGetValue(a.Home, out var home) && home.District == a.District) adults.Add(a);
        adults.Sort((x, y) => x.Id.CompareTo(y.Id));
        var homeIds = new SortedSet<Guid>(); var workIds = new SortedSet<Guid>();
        var districts = new HashSet<Guid>();   // looked up only, never enumerated
        foreach (var a in adults) { homeIds.Add(a.Home); districts.Add(a.District); if (a.Work != Guid.Empty && _works.ContainsKey(a.Work)) workIds.Add(a.Work); }
        foreach (var h in _homes) if (h.Value.Free > 0 && districts.Contains(h.Value.District)) homeIds.Add(h.Key);
        var s = new Snapshot { Homes = new Guid[homeIds.Count], Works = new Guid[workIds.Count] };
        var homeIndex = new Dictionary<Guid, int>(); var workIndex = new Dictionary<Guid, int>();
        s.HomeDistrict = new Guid[s.Homes.Length]; s.HomePos = new int[s.Homes.Length * 3]; s.HomeFree = new int[s.Homes.Length];
        int n = 0;
        foreach (var id in homeIds)
        {
            var h = _homes[id]; s.Homes[n] = id; s.HomeDistrict[n] = h.District; s.HomeFree[n] = h.Free;
            s.HomePos[n * 3] = h.X; s.HomePos[n * 3 + 1] = h.Y; s.HomePos[n * 3 + 2] = h.Z; homeIndex[id] = n++;
        }
        s.WorkPos = new int[s.Works.Length * 3]; n = 0;
        foreach (var id in workIds)
        {
            var w = _works[id]; s.Works[n] = id;
            s.WorkPos[n * 3] = w.X; s.WorkPos[n * 3 + 1] = w.Y; s.WorkPos[n * 3 + 2] = w.Z; workIndex[id] = n++;
        }
        s.Adults = new Guid[adults.Count]; s.AdultDistrict = new Guid[adults.Count];
        s.AdultHome = new int[adults.Count]; s.AdultWork = new int[adults.Count];
        for (int i = 0; i < adults.Count; i++)
        {
            s.Adults[i] = adults[i].Id; s.AdultDistrict[i] = adults[i].District; s.AdultHome[i] = homeIndex[adults[i].Home];
            s.AdultWork[i] = adults[i].Work != Guid.Empty && workIndex.TryGetValue(adults[i].Work, out var wi) ? wi : -1;
        }
        return s;
    }
}

public sealed class PassState
{
    public const int CurrentVersion = 6;         // 2: Near rows hold only the workplace's district, padded with -1
                                                 // 3: route costs verified outside the Near rows are kept (Learned*)
                                                 // 4: each district is solved on its own (District, Beds)
                                                 // 5: per-tick budgets are set for each pass (QueryBudget, SolveBudget)
                                                 // 6: empty beds are solved for too (Snapshot.HomeFree; Beds holds homes)
    public int Version = CurrentVersion;
    public bool Requested = true;                // start a pass at the next opportunity
    public int Stage;                            // 0 idle, 1 route costs, 2 assignment, 3 verification
    public Snapshot Snap;
    public int QueryBudget;                      // stages 1 and 3: route queries per tick, set from the snapshot
    public long SolveBudget;                     // stage 2: solver operations per tick, set from the snapshot
    public int Cursor;                           // stage 1: next cost entry; stage 3: next move to verify
    public int District;                         // stage 2: the district being solved, counted in ID order
    public int Row = 1;                          // stage 2: next row of that district's assignment
    public long[] U, V; public int[] P;          // stage 2 solver state of that district
    public int[] Beds;                           // stages 2-3: per adult, the home it gets (its own until solved)
    public int[] VerifyCurrent, VerifyTarget;    // stage 3: fresh route costs per move
    public long Queries, Ticks;                  // of the running pass
    public long Passes, MovedAdults, AppliedCycles, RejectedCycles, StaleCycles;   // lifetime
    // Fresh route costs from earlier passes' route checks, for homes outside the workplace's Near row, which pricing
    // only estimates. Sorted by workplace, then home; age = passes finished since the cost was checked.
    public Guid[] LearnedWork = new Guid[0], LearnedHome = new Guid[0];
    public int[] LearnedCost = new int[0], LearnedAge = new int[0];
}

public sealed class PassReport
{
    public int Adults, Homes, Works, Moves, Applied, Rejected, Stale, Recovered;
    public long Queries, Ticks;
    public double RouteCostSaved;
}

// One pass = capture the colony, price each workplace's nearest homes in its district, solve each district's optimal
// assignment to its occupied and empty beds, re-check every proposed move against fresh routes, then apply whole cycles
// and chains of moves. The re-checked costs of homes beyond the nearest are kept for the next passes, in place of
// estimates, and priced again when they come due while still needed. Each stage is spread over ticks with operation budgets set from the snapshot when the pass starts,
// and the entire state is serializable, so a pass resumes identically after a save/load and on every multiplayer peer.
public sealed class PassEngine
{
    // QueriesPerTick and SolveOpsPerTick are the least budgets per tick. A pass is asked for when the day starts, and a
    // day has 512 daytime ticks (of 768): a large colony's pass takes larger budgets, set from its snapshot, so that
    // pricing still ends within about PriceTicks, the solve within SolveTicks and the route checks within VerifyTicks
    // (448 ticks in all). They never exceed the Max* budgets: a still larger colony's pass takes longer instead.
    public const int NearHomes = 32, QueriesPerTick = 32;
    public const long SolveOpsPerTick = 250_000;
    public const int PriceTicks = 192, SolveTicks = 192, VerifyTicks = 64, MaxQueriesPerTick = 128;
    public const long MaxSolveOpsPerTick = 1_000_000;
    // A checked cost is used by this many later passes; the last of them prices it again, and so keeps it, while one of
    // the workplace's workers lives in the home or while no route led there. At most LearnedLimit costs are kept.
    public const int LearnedPasses = 7, LearnedLimit = 1024;
    private readonly IPassWorld _world;
    public PassState State { get; private set; }
    public PassReport LastReport { get; private set; }
    public event Action<PassReport> Reported;
    public event Action<Exception> Faulted;
    private long[,] _matrix; private int _built;   // of the district being solved; rebuilt after a load, never saved
    private int[][] _members;                       // adult indices per district, both in ID order; from the snapshot
    private List<int[]> _moves;                     // [adult index, from home, to home]

    public PassEngine(IPassWorld world, PassState state = null)
    {
        _world = world; State = state ?? new PassState();
        bool budgeted = State.Stage == 0 || State.QueryBudget >= QueriesPerTick && State.SolveBudget >= SolveOpsPerTick;
        if (State.Version != PassState.CurrentVersion || !budgeted) State = Restart(State);
    }
    // A state saved by another version, or a running pass without its budgets, is not trusted: a pass it was running
    // starts over. The lifetime counters carry over, and an idle state keeps its schedule. This depends on the save alone,
    // so every peer does the same.
    private static PassState Restart(PassState old)
    {
        var state = new PassState
        {
            Requested = old.Stage != 0 || old.Requested,
            Passes = old.Passes, MovedAdults = old.MovedAdults,
            AppliedCycles = old.AppliedCycles, RejectedCycles = old.RejectedCycles, StaleCycles = old.StaleCycles,
        };
        // Versions 3 to 5 kept the remembered costs just as this one does (only the solve stage, the budgets and the
        // snapshot's empty beds changed since), so they carry over and a cycle a route check turned down stays known. A
        // pass changes them only when it finishes.
        int count = old.LearnedWork?.Length ?? -1;
        if (old.Version >= 3 && old.Version <= PassState.CurrentVersion && count >= 0 && old.LearnedHome?.Length == count && old.LearnedCost?.Length == count && old.LearnedAge?.Length == count)
        {
            state.LearnedWork = old.LearnedWork; state.LearnedHome = old.LearnedHome;
            state.LearnedCost = old.LearnedCost; state.LearnedAge = old.LearnedAge;
        }
        return state;
    }
    public bool Busy => State.Stage != 0;
    public void RequestPass() => State.Requested = true;

    public void Tick()
    {
        try { TickCore(); }
        catch (Exception exception)
        {
            State = new PassState { Requested = false, Passes = State.Passes }; Forget();
            Faulted?.Invoke(exception);
        }
    }
    private void TickCore()
    {
        switch (State.Stage)
        {
            case 0: if (State.Requested) Begin(); break;
            case 1: State.Ticks++; PriceStep(); break;
            case 2: State.Ticks++; SolveStep(); break;
            case 3: State.Ticks++; VerifyStep(); break;
            default: throw new InvalidOperationException("Unknown pass stage " + State.Stage);
        }
    }
    private void Forget() { _matrix = null; _built = 0; _members = null; _columns = null; _moves = null; _known = null; _far = null; }

    private void Begin()
    {
        Forget();
        State.Requested = false; State.Queries = 0; State.Ticks = 0;
        var snap = _world.Capture();
        if (snap.Adults.Length == 0 || snap.Works.Length == 0) { Finish(snap, new List<int[]>(), null, null); return; }
        // The game's road routes never leave a district, so a workplace only ranks homes in its own district: the one
        // its workers live in (a job in another district does not count), taken from the lowest-ID worker.
        var workDistrict = new Guid[snap.Works.Length]; var seen = new bool[snap.Works.Length];
        for (int i = 0; i < snap.Adults.Length; i++)
        {
            int w = snap.AdultWork[i];
            if (w >= 0 && !seen[w]) { workDistrict[w] = snap.AdultDistrict[i]; seen[w] = true; }
        }
        int k = Math.Min(NearHomes, snap.Homes.Length);
        snap.NearK = k; snap.Near = new int[snap.Works.Length * k]; snap.Costs = new int[snap.Works.Length * k];
        var order = new List<int>(snap.Homes.Length); var distance = new int[snap.Homes.Length];
        for (int w = 0; w < snap.Works.Length; w++)
        {
            order.Clear();
            for (int h = 0; h < snap.Homes.Length; h++)
                if (snap.HomeDistrict[h] == workDistrict[w]) { order.Add(h); distance[h] = Geometry.Distance(snap.HomePos, h, snap.WorkPos, w); }
            order.Sort((a, b) => distance[a] != distance[b] ? distance[a].CompareTo(distance[b]) : a.CompareTo(b));
            // Rows keep a fixed stride of k; a district with fewer homes pads its row with -1, never queried.
            for (int i = 0; i < k; i++)
            {
                bool real = i < order.Count;
                snap.Near[w * k + i] = real ? order[i] : -1; snap.Costs[w * k + i] = real ? -1 : Cost.Unreachable;
            }
        }
        Recheck(snap);
        State.Snap = snap; Budget(snap); State.Stage = 1; State.Cursor = 0;
    }

    // The pass's budgets per tick, set from its snapshot alone and saved with it, so every peer, reloaded or not, does the
    // same work on every tick. Pricing asks one route per real candidate and per recheck; the route checks ask two per
    // move, and at most every adult moves. Solving a district of n adults and n beds took about n^3 / 9 operations on the
    // first pass over a randomly housed test colony (never more than about n^3 / 2), and far fewer once a colony is
    // settled; with m beds, occupied and empty, n^2 m / 8 is taken.
    private void Budget(Snapshot s)
    {
        long queries = s.RecheckCosts.Length;
        foreach (int home in s.Near) if (home >= 0) queries++;
        long perTick = Math.Max((queries + PriceTicks - 1) / PriceTicks, (2L * s.Adults.Length + VerifyTicks - 1) / VerifyTicks);
        State.QueryBudget = (int)Math.Min(Math.Max(perTick, QueriesPerTick), MaxQueriesPerTick);
        long ops = 0;
        for (int d = 0; d < Districts().Length; d++) { long n = Districts()[d].Length, m = Columns(d).Length; ops += n * n * m / 8; }
        State.SolveBudget = Math.Min(Math.Max((ops + SolveTicks - 1) / SolveTicks, SolveOpsPerTick), MaxSolveOpsPerTick);
    }

    private static bool InRow(Snapshot s, int w, int home)
    {
        for (int e = 0; e < s.NearK; e++) if (s.Near[w * s.NearK + e] == home) return true;
        return false;
    }
    // Remembered costs that this pass uses for the last time are priced again with the rows, and kept for LearnedPasses
    // more passes, while they matter: when one of the workplace's workers lives in the home, so their own commute is not
    // left to an estimate that makes a move out look better than it is (a cycle the route check then turns down), and
    // when no route led there, so a road that comes back is noticed. The rest lapse to the estimate.
    private void Recheck(Snapshot s)
    {
        var lives = new HashSet<(int Work, int Home)>();   // looked up only, never enumerated
        for (int i = 0; i < s.Adults.Length; i++) if (s.AdultWork[i] >= 0) lives.Add((s.AdultWork[i], s.AdultHome[i]));
        var work = new List<int>(); var home = new List<int>();
        for (int e = 0; e < State.LearnedCost.Length; e++)
        {
            if (State.LearnedAge[e] != LearnedPasses - 1) continue;
            int w = Array.BinarySearch(s.Works, State.LearnedWork[e]), h = Array.BinarySearch(s.Homes, State.LearnedHome[e]);
            if (w < 0 || h < 0 || InRow(s, w, h)) continue;
            if (State.LearnedCost[e] >= Cost.Unreachable || lives.Contains((w, h))) { work.Add(w); home.Add(h); }
        }
        s.RecheckWork = work.ToArray(); s.RecheckHome = home.ToArray(); s.RecheckCosts = new int[work.Count];
        for (int r = 0; r < s.RecheckCosts.Length; r++) s.RecheckCosts[r] = -1;
    }

    private void PriceStep()
    {
        // The cursor runs over the rows' entries, then over the rechecks.
        var s = State.Snap; int k = s.NearK, end = s.Costs.Length + s.RecheckCosts.Length;
        for (int q = 0; q < State.QueryBudget && State.Cursor < end; State.Cursor++)
        {
            int at = State.Cursor;
            if (at >= s.Costs.Length)
            {
                int r = at - s.Costs.Length;
                s.RecheckCosts[r] = _world.TryRoute(s.Homes[s.RecheckHome[r]], s.Works[s.RecheckWork[r]], out var again) ? Cost.Fixed(again) : Cost.Unreachable;
            }
            else if (s.Near[at] < 0) continue;   // padding: already Unreachable, and costs no query
            else s.Costs[at] = _world.TryRoute(s.Homes[s.Near[at]], s.Works[at / k], out var route) ? Cost.Fixed(route) : Cost.Unreachable;
            State.Queries++; q++;
        }
        if (State.Cursor < end) return;
        State.Beds = (int[])s.AdultHome.Clone();
        State.District = 0; StartDistrict(); State.Stage = 2;
    }

    // Nobody crosses districts, so the colony's assignment is one per district: each is solved on its own, in ID order,
    // over its own adults, the beds they live in and its empty beds. The optimum is the same as for the whole colony at
    // once, which scanned every district's beds for every adult; this takes fewer ticks and a far smaller matrix.
    private int[] Members(int district) => Districts()[district];
    // A district's beds, as the home each column of its matrix stands for: first the beds its adults live in, in the
    // adults' order, then its homes' empty beds in home order (at most one per adult in each home: no more can be used).
    private int[][] _columns;
    private int[] Columns(int district)
    {
        if (_columns == null)
        {
            var s = State.Snap; var all = Districts(); _columns = new int[all.Length][];
            var districts = new List<Guid>(new SortedSet<Guid>(s.AdultDistrict));
            for (int d = 0; d < all.Length; d++)
            {
                var columns = new List<int>();
                foreach (int i in all[d]) columns.Add(s.AdultHome[i]);
                for (int h = 0; h < s.Homes.Length; h++)
                    if (s.HomeDistrict[h] == districts[d]) for (int b = Math.Min(s.HomeFree[h], all[d].Length); b > 0; b--) columns.Add(h);
                _columns[d] = columns.ToArray();
            }
        }
        return _columns[district];
    }
    private int[][] Districts()
    {
        if (_members == null)
        {
            var s = State.Snap; var districts = new List<Guid>(new SortedSet<Guid>(s.AdultDistrict));
            var members = new List<int>[districts.Count];
            for (int d = 0; d < members.Length; d++) members[d] = new List<int>();
            for (int i = 0; i < s.Adults.Length; i++) members[districts.BinarySearch(s.AdultDistrict[i])].Add(i);
            _members = new int[members.Length][];
            for (int d = 0; d < members.Length; d++) _members[d] = members[d].ToArray();
        }
        return _members;
    }
    private void StartDistrict()
    {
        int n = Members(State.District).Length, m = Columns(State.District).Length;
        State.U = new long[n + 1]; State.V = new long[m + 1]; State.P = new int[m + 1]; State.Row = 1;
    }

    // Operations the latest solve tick charged, summed apart from the budget check. Only the tests read it; never saved.
    internal long SolveOps;
    private void SolveStep()
    {
        // A tick's budget carries on from a district that is done into the next one.
        long ops = 0; SolveOps = 0;
        while (true)
        {
            var members = Members(State.District); var columns = Columns(State.District); int n = members.Length, m = columns.Length;
            if (_matrix == null) { _matrix = new long[n, m]; _built = 0; }
            // After a load the district's rows solved so far are rebuilt first; they are needed by the rows still to
            // come. That work is not charged to this tick's budget: how many rows a tick solves must depend on the saved
            // state alone, so a peer that loaded a save taken mid-pass keeps step, tick for tick, with a peer that did not.
            while (_built < State.Row - 1) BuildRow(members, columns, _built++);
            long before = ops;
            bool solved = Hungarian.Step(_matrix, n, m, State.U, State.V, State.P, ref State.Row, ref ops, State.SolveBudget, row => { BuildRow(members, columns, row); _built = row + 1; return m; });
            SolveOps += ops - before;
            if (!solved) return;
            for (int j = 1; j <= m; j++) if (State.P[j] > 0) State.Beds[members[State.P[j] - 1]] = columns[j - 1];
            _matrix = null;
            if (++State.District == _members.Length) break;
            StartDistrict();
            if (ops >= State.SolveBudget) return;
        }
        State.U = null; State.V = null; State.P = null;
        _moves = null; EnsureMoves();
        State.VerifyCurrent = new int[_moves.Count]; State.VerifyTarget = new int[_moves.Count];
        State.Cursor = 0; State.Stage = 3;
    }

    // A row of the district's cost matrix: what each of the district's beds, occupied or empty, would cost that row's adult.
    private Dictionary<int, int>[] _known; private long[] _far;
    private void BuildRow(int[] members, int[] columns, int row)
    {
        var s = State.Snap; int m = columns.Length, k = s.NearK, i = members[row];
        if (_known == null)
        {
            _known = new Dictionary<int, int>[s.Works.Length]; _far = new long[s.Works.Length];
            for (int w = 0; w < s.Works.Length; w++)
            {
                // Unpriced homes are estimated beyond the worst reachable priced one. If none could reach the workplace,
                // it is likely cut off, so unpriced homes are assumed no better than unreachable.
                var d = new Dictionary<int, int>(k); int worst = -1;
                for (int e = 0; e < k; e++)
                {
                    int home = s.Near[w * k + e], c = s.Costs[w * k + e];
                    if (home < 0) continue;
                    d[home] = c; if (c < Cost.Unreachable && c > worst) worst = c;
                }
                _known[w] = d; _far[w] = (long)(worst < 0 ? Cost.Unreachable : worst) + Cost.FarMargin;
            }
            // A home outside the row whose route this pass priced again, or an earlier pass checked, is taken at that cost
            // instead of the estimate (any move is re-checked all the same). This reads the saved state only, so a
            // rebuild after a load matches.
            for (int r = 0; r < s.RecheckCosts.Length; r++) _known[s.RecheckWork[r]][s.RecheckHome[r]] = s.RecheckCosts[r];
            for (int e = 0; e < State.LearnedCost.Length; e++)
            {
                int w = Array.BinarySearch(s.Works, State.LearnedWork[e]), home = Array.BinarySearch(s.Homes, State.LearnedHome[e]);
                if (w >= 0 && home >= 0 && !_known[w].ContainsKey(home)) _known[w][home] = State.LearnedCost[e];
            }
        }
        int own = s.AdultHome[i], work = s.AdultWork[i];
        for (int j = 0; j < m; j++)
        {
            int bed = columns[j];   // beds in one home are alike
            long c = 0;
            if (work >= 0)
                c = _known[work].TryGetValue(bed, out var known) ? known
                    : _far[work] + (long)Cost.Scale * Geometry.Distance(s.HomePos, bed, s.WorkPos, work);
            if (bed == own) c -= Cost.StayBonus;
            _matrix[row, j] = c;
        }
    }

    // Moves implied by the assignment: [adult, from home, to home], in adult order.
    private void EnsureMoves()
    {
        if (_moves != null) return;
        var s = State.Snap; int n = s.Adults.Length;
        _moves = new List<int[]>();
        for (int i = 0; i < n; i++)
        {
            int to = State.Beds[i];
            if (to != s.AdultHome[i]) _moves.Add(new[] { i, s.AdultHome[i], to });
        }
    }

    private void VerifyStep()
    {
        EnsureMoves();
        var s = State.Snap;
        for (int q = 0; q < State.QueryBudget / 2 && State.Cursor < _moves.Count; q++, State.Cursor++)
        {
            var m = _moves[State.Cursor]; int work = s.AdultWork[m[0]];
            if (work < 0) { State.VerifyCurrent[State.Cursor] = 0; State.VerifyTarget[State.Cursor] = 0; continue; }
            State.VerifyCurrent[State.Cursor] = Price(s.Homes[m[1]], s.Works[work]);
            State.VerifyTarget[State.Cursor] = Price(s.Homes[m[2]], s.Works[work]);
        }
        if (State.Cursor < _moves.Count) return;
        var edgesFrom = new int[_moves.Count]; var edgesTo = new int[_moves.Count];
        for (int e = 0; e < _moves.Count; e++) { edgesFrom[e] = _moves[e][1]; edgesTo[e] = _moves[e][2]; }
        Finish(s, Cycles.SplitChains(s.Homes.Length, edgesFrom, edgesTo), State.VerifyCurrent, State.VerifyTarget);
    }
    private int Price(Guid home, Guid work) { State.Queries++; return _world.TryRoute(home, work, out var route) ? Cost.Fixed(route) : Cost.Unreachable; }

    private void Finish(Snapshot s, List<int[]> cycles, int[] current, int[] target)
    {
        var report = new PassReport { Adults = s.Adults.Length, Homes = s.Homes.Length, Works = s.Works.Length,
            Moves = _moves?.Count ?? 0, Queries = State.Queries, Ticks = State.Ticks };
        var accepted = new List<(int[] Edges, long Gain)>();
        foreach (var cycle in cycles)
        {
            long gain = 0; bool ok = true;
            foreach (int e in cycle)
            {
                // A mover must not end up unreachable unless it already was.
                if (target[e] >= Cost.Unreachable && current[e] < Cost.Unreachable) ok = false;
                gain += (long)current[e] - target[e];
            }
            if (ok && gain >= Cost.MinimumGain) accepted.Add((cycle, gain)); else report.Rejected++;
        }
        accepted.Sort((x, y) => x.Gain != y.Gain ? y.Gain.CompareTo(x.Gain) : x.Edges[0].CompareTo(y.Edges[0]));
        foreach (var (edges, gain) in accepted)
        {
            var moves = new Move[edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                var m = _moves[edges[i]]; int work = s.AdultWork[m[0]];
                moves[i] = new Move { Adult = s.Adults[m[0]], From = s.Homes[m[1]], To = s.Homes[m[2]], Work = work < 0 ? Guid.Empty : s.Works[work] };
            }
            if (_world.ApplyCycle(moves))
            {
                report.Applied++;
                foreach (int e in edges)
                {
                    // Repaired only if the new home reaches work: a cut-off beaver can be moved along and stay cut off.
                    if (current[e] >= Cost.Unreachable) { if (target[e] < Cost.Unreachable) report.Recovered++; }
                    else report.RouteCostSaved += (double)((long)current[e] - target[e]) / Cost.Scale;
                    State.MovedAdults++;
                }
            }
            else report.Stale++;
        }
        State.AppliedCycles += report.Applied; State.RejectedCycles += report.Rejected; State.StaleCycles += report.Stale;
        State.Passes++; Learn(s, current, target);
        State.Snap = null; State.U = null; State.V = null; State.P = null; State.Beds = null; State.VerifyCurrent = null; State.VerifyTarget = null;
        State.QueryBudget = 0; State.SolveBudget = 0; State.Stage = 0; State.Cursor = 0; State.District = 0; State.Row = 1; Forget();
        LastReport = report; Reported?.Invoke(report);
    }

    // Keeps this pass's fresh route costs for homes outside the workplace's Near row, which the next pass would only
    // estimate again: without them a move the check turned down comes back every day. A newer cost replaces an older one
    // for the same pair (a move's check wins over a recheck); the rest age by a pass and go after LearnedPasses, or once
    // pricing covered their pair. The list lives in the saved state, sorted, so every peer, reloaded or not, starts the
    // next pass from the same costs.
    private void Learn(Snapshot s, int[] current, int[] target)
    {
        var all = new List<(Guid Work, Guid Home, int Cost, int Age, int Order)>();
        for (int e = 0; _moves != null && e < _moves.Count; e++)
        {
            var m = _moves[e]; int work = s.AdultWork[m[0]];
            if (work < 0) continue;
            if (!InRow(s, work, m[1])) all.Add((s.Works[work], s.Homes[m[1]], current[e], 0, all.Count));
            if (!InRow(s, work, m[2])) all.Add((s.Works[work], s.Homes[m[2]], target[e], 0, all.Count));
        }
        for (int r = 0; r < s.RecheckCosts.Length; r++) all.Add((s.Works[s.RecheckWork[r]], s.Homes[s.RecheckHome[r]], s.RecheckCosts[r], 0, all.Count));
        for (int e = 0; e < State.LearnedCost.Length; e++)
        {
            if (State.LearnedAge[e] + 1 >= LearnedPasses) continue;
            int w = Array.BinarySearch(s.Works, State.LearnedWork[e]), home = Array.BinarySearch(s.Homes, State.LearnedHome[e]);
            if (w >= 0 && home >= 0 && InRow(s, w, home)) continue;
            all.Add((State.LearnedWork[e], State.LearnedHome[e], State.LearnedCost[e], State.LearnedAge[e] + 1, all.Count));
        }
        int ByPair((Guid Work, Guid Home, int Cost, int Age, int Order) x, (Guid Work, Guid Home, int Cost, int Age, int Order) y) =>
            x.Work != y.Work ? x.Work.CompareTo(y.Work) : x.Home != y.Home ? x.Home.CompareTo(y.Home) : x.Age != y.Age ? x.Age.CompareTo(y.Age) : x.Order.CompareTo(y.Order);
        all.Sort(ByPair);
        var kept = new List<(Guid Work, Guid Home, int Cost, int Age, int Order)>(all.Count);
        foreach (var x in all) if (kept.Count == 0 || kept[kept.Count - 1].Work != x.Work || kept[kept.Count - 1].Home != x.Home) kept.Add(x);
        if (kept.Count > LearnedLimit)
        {
            kept.Sort((x, y) => x.Age != y.Age ? x.Age.CompareTo(y.Age) : ByPair(x, y));   // the youngest stay
            kept.RemoveRange(LearnedLimit, kept.Count - LearnedLimit); kept.Sort(ByPair);
        }
        State.LearnedWork = new Guid[kept.Count]; State.LearnedHome = new Guid[kept.Count];
        State.LearnedCost = new int[kept.Count]; State.LearnedAge = new int[kept.Count];
        for (int i = 0; i < kept.Count; i++)
        { State.LearnedWork[i] = kept[i].Work; State.LearnedHome[i] = kept[i].Home; State.LearnedCost[i] = kept[i].Cost; State.LearnedAge[i] = kept[i].Age; }
    }
}
