using Newtonsoft.Json;
using OptimizedLocalHousing;

static class Program
{
    static int passed;
    static Guid G(int n) => new Guid(n, 0, 0, new byte[8]);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }

    // ---- a fake game --------------------------------------------------------------------------------------------
    sealed class Person { public Guid Id, Home, Work, District; public bool Adult = true; }
    sealed class Home { public int Capacity, X, Y, Z; public Guid District = G(9000); public bool Usable = true; }
    sealed class Fake : IPassWorld
    {
        public Dictionary<Guid, Person> People = new(); public Dictionary<Guid, Home> Homes = new();
        public Dictionary<Guid, (int X, int Y, int Z)> Works = new(); public HashSet<(Guid, Guid)> Blocked = new();
        public Dictionary<Guid, Guid> WorkDistrict = new();   // a workplace not listed here is in G(9000), like a home
        public bool Reverse; public int Calls, CrossCalls; public List<string> Applied = new(); public bool ThrowOnApply;
        public Snapshot Capture()
        {
            var b = new SnapshotBuilder(); var people = Reverse ? People.Values.Reverse() : People.Values;
            foreach (var p in people)
            {
                if (!p.Adult || !Homes.TryGetValue(p.Home, out var h) || !h.Usable) continue;
                b.AddHome(p.Home, h.District, h.X, h.Y, h.Z, h.Capacity, AdultsIn(p.Home), ChildrenIn(p.Home));
                var work = Job(p); if (work != Guid.Empty) b.AddWork(work, Works[work].X, Works[work].Y, Works[work].Z);
                b.AddAdult(p.Id, p.Home, work, p.District);
            }
            // Like the game, every usable home, lived in or not.
            foreach (var h in Reverse ? Homes.Reverse() : Homes) if (h.Value.Usable) b.AddHome(h.Key, h.Value.District, h.Value.X, h.Value.Y, h.Value.Z, h.Value.Capacity, AdultsIn(h.Key), ChildrenIn(h.Key));
            return b.Build();
        }
        // Like the game (Dwelling): a third of the beds, rounded down, are for children, the rest for adults.
        public int AdultsIn(Guid home) => People.Values.Count(p => p.Adult && p.Home == home);
        public int ChildrenIn(Guid home) => People.Values.Count(p => !p.Adult && p.Home == home);
        public int AdultBeds(Guid home) => Homes[home].Capacity - Homes[home].Capacity / 3;
        // Like the Folktails' ProcreationHouse: two adults, a free bed, and fewer children than the child beds and than
        // half the adults.
        public bool CanBreed(Guid home)
        {
            int adults = AdultsIn(home), children = ChildrenIn(home), beds = Homes[home].Capacity;
            return adults >= 2 && adults + children < beds && children < beds / 3 && children < adults / 2;
        }
        public int BreedingHomes() => Homes.Keys.Count(CanBreed);
        public Guid DistrictOf(Guid work) => WorkDistrict.TryGetValue(work, out var d) ? d : G(9000);
        // Like the game, a workplace in another district is no job at all.
        public Guid Job(Person p) => p.Work != Guid.Empty && Works.ContainsKey(p.Work) && DistrictOf(p.Work) == p.District ? p.Work : Guid.Empty;
        public float Route(Guid home, Guid work)
        { var h = Homes[home]; var w = Works[work]; return Math.Abs(h.X - w.X) + Math.Abs(h.Y - w.Y) + 2 * Math.Abs(h.Z - w.Z) + 5; }
        public bool TryRoute(Guid home, Guid work, out float cost)
        {
            Calls++; cost = 0;
            if (!Homes.ContainsKey(home) || !Works.ContainsKey(work) || Blocked.Contains((home, work))) return false;
            // The game's road pathfinding never leaves the start's district, so there is no route to another district.
            if (Homes[home].District != DistrictOf(work)) { CrossCalls++; return false; }
            cost = Route(home, work); return true;
        }
        public bool ApplyCycle(Move[] cycle)
        {
            if (ThrowOnApply) throw new InvalidOperationException("boom");
            var delta = new Dictionary<Guid, int>();
            foreach (var m in cycle)
            {
                if (!People.TryGetValue(m.Adult, out var p) || !p.Adult || p.Home != m.From || Job(p) != m.Work || !Homes[m.To].Usable) return false;
                delta[m.From] = delta.GetValueOrDefault(m.From) - 1; delta[m.To] = delta.GetValueOrDefault(m.To) + 1;
            }
            foreach (var d in delta) if (People.Values.Count(p => p.Home == d.Key) + d.Value > Homes[d.Key].Capacity) return false;
            // The game moves an adult out of a home with more adults than adult beds, so the adapter never makes one.
            foreach (var d in delta) if (d.Value > 0 && AdultsIn(d.Key) + d.Value > AdultBeds(d.Key)) return false;
            foreach (var m in cycle) People[m.Adult].Home = m.To;
            Applied.Add(string.Join(",", cycle.Select(m => $"{m.Adult.ToString()[..4]}>{m.To.ToString()[..4]}")));
            return true;
        }
        public double Total() => People.Values.Where(p => p.Adult && Job(p) != Guid.Empty).Sum(p => Route(p.Home, p.Work));
        public double Objective() => People.Values.Where(p => p.Adult).Sum(p => Job(p) == Guid.Empty ? 0 : Cost.Fixed(Route(p.Home, p.Work))); // fixed-point units
        public Dictionary<Guid, int> AdultCounts() => People.Values.Where(p => p.Adult).GroupBy(p => p.Home).ToDictionary(g => g.Key, g => g.Count());
        public string Fingerprint() => string.Join(";", People.Values.OrderBy(p => p.Id).Select(p => p.Id.ToString()[..8] + ">" + p.Home.ToString()[..8]));
        public Fake Copy()
        {
            var f = new Fake { Reverse = Reverse };
            foreach (var p in People) f.People[p.Key] = new Person { Id = p.Value.Id, Home = p.Value.Home, Work = p.Value.Work, District = p.Value.District, Adult = p.Value.Adult };
            foreach (var h in Homes) f.Homes[h.Key] = new Home { Capacity = h.Value.Capacity, X = h.Value.X, Y = h.Value.Y, Z = h.Value.Z, District = h.Value.District, Usable = h.Value.Usable };
            foreach (var w in Works) f.Works[w.Key] = w.Value; f.Blocked = new HashSet<(Guid, Guid)>(Blocked);
            f.WorkDistrict = new Dictionary<Guid, Guid>(WorkDistrict); return f;
        }
        // One district on its own: its adults, homes and workplaces.
        public Fake Only(Guid district)
        {
            var f = Copy();
            foreach (var p in People.Values) if (p.District != district) f.People.Remove(p.Id);
            foreach (var h in Homes) if (h.Value.District != district) f.Homes.Remove(h.Key);
            foreach (var w in Works.Keys) if (DistrictOf(w) != district) f.Works.Remove(w);
            return f;
        }
    }

    // Random colony: every home is filled by adults (plus a few children who never move); some adults are unemployed.
    // Districts lie side by side along x, 60 blocks wide each unless given another width.
    static Fake Colony(int seed, int homes, int adults, int works, int children = 0, int districts = 1, int width = 60)
    {
        var rng = new Random(seed); var f = new Fake();
        for (int h = 0; h < homes; h++) f.Homes[G(100 + h)] = new Home { X = h % districts * width + rng.Next(0, width), Y = rng.Next(0, 60), Z = rng.Next(0, 3), District = G(9000 + h % districts) };
        for (int w = 0; w < works; w++) { f.Works[G(5000 + w)] = (w % districts * width + rng.Next(0, width), rng.Next(0, 60), rng.Next(0, 3)); f.WorkDistrict[G(5000 + w)] = G(9000 + w % districts); }
        var keys = f.Homes.Keys.OrderBy(k => k).ToList();
        Populate(f, rng, adults);
        for (int c = 0; c < children; c++) { var home = keys[rng.Next(keys.Count)]; f.People[G(3000 + c)] = new Person { Id = G(3000 + c), Home = home, District = f.Homes[home].District, Adult = false }; }
        foreach (var h in keys) f.Homes[h].Capacity = f.People.Values.Count(p => p.Home == h);
        return f;
    }
    // Adds empty beds: a spare bed in about one home in four, and a few empty homes in each district (laid out as Colony does).
    static Fake Spare(Fake f, int seed, int emptyHomes = 3, int width = 60)
    {
        var rng = new Random(seed); var districts = f.Homes.Values.Select(h => h.District).Distinct().OrderBy(d => d).ToList();
        foreach (var h in f.Homes.OrderBy(h => h.Key)) if (rng.Next(0, 4) == 0) h.Value.Capacity++;
        for (int d = 0; d < districts.Count; d++)
            for (int k = 0; k < emptyHomes; k++)
                f.Homes[G(7000 + 10 * d + k)] = new Home { Capacity = rng.Next(1, 4), X = (BitConverter.ToInt32(districts[d].ToByteArray(), 0) - 9000) * width + rng.Next(0, width), Y = rng.Next(0, 60), Z = rng.Next(0, 3), District = districts[d] };
        return f;
    }
    // Fills the homes in turn with adults; each works in its own district, as the game requires, or 1 in 8 has no job.
    static void Populate(Fake f, Random rng, int adults)
    {
        var keys = f.Homes.Keys.OrderBy(k => k).ToList();
        for (int a = 0; a < adults; a++)
        {
            var home = keys[a % keys.Count]; var district = f.Homes[home].District;
            var jobs = f.Works.Keys.Where(w => f.DistrictOf(w) == district).OrderBy(w => w).ToList();
            f.People[G(10 + a)] = new Person { Id = G(10 + a), Home = home, District = district,
                Work = rng.Next(0, 8) == 0 || jobs.Count == 0 ? Guid.Empty : jobs[rng.Next(0, jobs.Count)] };
        }
    }
    // A compact small district right beside the big district's workplaces: the homes nearest those workplaces are
    // in the small district, where no road from them leads.
    static Fake BorderColony(int seed)
    {
        var rng = new Random(seed); var f = new Fake(); Guid small = G(9001), big = G(9000);
        int smallHomes = rng.Next(10, 31), bigHomes = rng.Next(40, 90), works = rng.Next(6, 25), adults = rng.Next(150, 300), id = 100;
        for (int h = 0; h < smallHomes; h++) f.Homes[G(id++)] = new Home { District = small, X = rng.Next(0, 6), Y = rng.Next(0, 6) };
        for (int h = 0; h < bigHomes; h++) f.Homes[G(id++)] = new Home { District = big, X = rng.Next(7, 90), Y = rng.Next(0, 40), Z = rng.Next(0, 2) };
        for (int w = 0; w < works; w++)
        {
            f.Works[G(5000 + w)] = (w < 2 ? rng.Next(0, 6) : rng.Next(0, 3) == 0 ? rng.Next(7, 12) : rng.Next(7, 90), rng.Next(0, 40), 0);
            f.WorkDistrict[G(5000 + w)] = w < 2 ? small : big;
        }
        Populate(f, rng, adults);
        foreach (var h in f.Homes) h.Value.Capacity = f.People.Values.Count(p => p.Home == h.Key);
        return f;
    }
    // District 9000: 30 homes and one workplace; district 9001: 2 homes and a workplace with a higher ID. Rows hold 32
    // homes, so pricing makes 30 + 2 real queries, exactly one tick's budget, then spends a tick only skipping padding.
    static Fake PaddingTail()
    {
        var rng = new Random(7); var f = new Fake();
        for (int h = 0; h < 32; h++)
            f.Homes[G(100 + h)] = new Home { X = h < 30 ? rng.Next(0, 60) : rng.Next(60, 70), Y = rng.Next(0, 60), District = G(h < 30 ? 9000 : 9001) };
        f.Works[G(5000)] = (30, 30, 0); f.Works[G(5001)] = (65, 30, 0); f.WorkDistrict[G(5001)] = G(9001);
        Populate(f, rng, 70);
        foreach (var h in f.Homes) h.Value.Capacity = f.People.Values.Count(p => p.Home == h.Key);
        return f;
    }
    // Per district d, 100 blocks apart: a workplace G(500 + d) at X=0 prices its 32 nearest homes (X=1), each holding one
    // of its workers. Adult G(10 + 2d) works there and lives at X=50; adult G(11 + 2d), unemployed, lives at X=40.
    // Neither home is priced, so X=40 looks 10 units closer; with blocked, no route leads from it to the workplace, and
    // the swap the solver proposes is turned down by its route check.
    static Fake FarSwap(int districts = 1, bool blocked = true)
    {
        var f = new Fake();
        for (int d = 0; d < districts; d++)
        {
            int x = 100 * d; Guid district = G(9000 + d), work = G(500 + d);
            f.Works[work] = (x, 0, 0); f.WorkDistrict[work] = district;
            for (int i = 0; i < 32; i++)
            {
                f.Homes[G(1100 + 50 * d + i)] = new Home { Capacity = 1, X = x + 1, District = district };
                f.People[G(2100 + 50 * d + i)] = new Person { Id = G(2100 + 50 * d + i), Home = G(1100 + 50 * d + i), Work = work, District = district };
            }
            f.Homes[G(200 + 2 * d)] = new Home { Capacity = 1, X = x + 50, District = district };
            f.Homes[G(201 + 2 * d)] = new Home { Capacity = 1, X = x + 40, District = district };
            f.People[G(10 + 2 * d)] = new Person { Id = G(10 + 2 * d), Home = G(200 + 2 * d), Work = work, District = district };
            f.People[G(11 + 2 * d)] = new Person { Id = G(11 + 2 * d), Home = G(201 + 2 * d), District = district };
            if (blocked) f.Blocked.Add((G(201 + 2 * d), work));
        }
        return f;
    }
    // The pass that uses the first pass's remembered costs for the last time, and prices again those still needed.
    const int DuePass = PassEngine.LearnedPasses + 1;
    static PassEngine Run(IPassWorld f, PassState state = null, int limit = 5000)
    {
        var e = Strict(new PassEngine(f, state), "A pass"); e.RequestPass();
        for (int t = 0; t < limit; t++) { e.Tick(); if (!e.Busy && !e.State.Requested) return e; }
        throw new Exception("Pass did not finish");
    }
    // A fault fails the test at once, with a readable message, instead of leaving an abandoned pass behind.
    static PassEngine Strict(PassEngine e, string who) { e.Faulted += x => throw new Exception($"{who} faulted: {x.Message}", x); return e; }
    // What a save and load hands the next engine: only what the saved state holds.
    static PassState Reload(PassState state) => JsonConvert.DeserializeObject<PassState>(JsonConvert.SerializeObject(state));
    // Costs verified by earlier passes that the running pass takes instead of an estimate: pairs in its snapshot that
    // are not in the workplace's candidate row.
    static int LearnedInUse(PassState state)
    {
        var s = state.Snap; int used = 0;
        for (int e = 0; e < state.LearnedCost.Length; e++)
        {
            int w = Array.IndexOf(s.Works, state.LearnedWork[e]), h = Array.IndexOf(s.Homes, state.LearnedHome[e]);
            if (w >= 0 && h >= 0 && !Enumerable.Range(0, s.NearK).Any(i => s.Near[w * s.NearK + i] == h)) used++;
        }
        return used;
    }
    // Best objective any arrangement of the adults into the district's beds, occupied or empty, can reach (route cost
    // minus stay bonuses). Children keep their beds, and paused homes their adults.
    static long Best(Fake f)
    {
        var adults = f.People.Values.Where(p => p.Adult).OrderBy(p => p.Id).ToList(); int n = adults.Count;
        var homes = f.Homes.Keys.OrderBy(k => k).ToList();
        // An adult keeps its bed, and may take an empty one while the home has adult beds to spare.
        var room = homes.Select(h => f.Homes[h].Usable ? Math.Max(f.AdultsIn(h), Math.Min(f.Homes[h].Capacity - f.ChildrenIn(h), f.AdultBeds(h))) : 0).ToArray();
        long best = long.MaxValue;
        long Cost1(Person a, Guid bed) => (f.Job(a) != Guid.Empty ? Cost.Fixed(f.Route(bed, a.Work)) : 0) - (bed == a.Home ? Cost.StayBonus : 0);
        void Go(int k, long sum)
        {
            if (k == n) { best = Math.Min(best, sum); return; }
            var a = adults[k];
            if (!f.Homes[a.Home].Usable) { Go(k + 1, sum + Cost1(a, a.Home)); return; }   // a paused home's adult stays
            for (int h = 0; h < homes.Count; h++)
            {
                if (room[h] == 0 || f.Homes[homes[h]].District != a.District) continue;   // nobody crosses districts
                room[h]--; Go(k + 1, sum + Cost1(a, homes[h])); room[h]++;
            }
        }
        Go(0, 0); return best;
    }
    static long Achieved(Fake f) => f.People.Values.Where(p => p.Adult).Sum(p => (f.Job(p) != Guid.Empty ? Cost.Fixed(f.Route(p.Home, p.Work)) : 0) - 0L)
        - f.People.Values.Where(p => p.Adult).Count(p => p.Home == StartHome[p.Id]) * (long)Cost.StayBonus;
    static Dictionary<Guid, Guid> StartHome = new();

    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception exception) { Console.Error.WriteLine("TEST FAILURE: " + exception); return 1; }
    }

    static void Run(string[] args)
    {
        Test("Hungarian solver matches brute force, including ties and forbidden entries", () => {
            var rng = new Random(11); const long Forbidden = 1_000_000_000;   // a pair never to be assigned
            for (int trial = 0; trial < 300; trial++)
            {
                int n = rng.Next(1, 8); var c = new long[n, n];
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) c[i, j] = rng.Next(0, 4) == 0 ? Forbidden : rng.Next(0, 6) * (rng.Next(0, 2) == 0 ? 1 : 1000) - 40;
                var u = new long[n + 1]; var v = new long[n + 1]; var p = new int[n + 1]; int row = 1;
                Check(Hungarian.Step(c, n, u, v, p, ref row, long.MaxValue, _ => 0), "Did not finish");
                long got = 0; for (int j = 1; j <= n; j++) got += c[p[j] - 1, j - 1];
                long best = long.MaxValue; var perm = Enumerable.Range(0, n).ToArray();
                void Go(int k, long s) { if (k == n) { best = Math.Min(best, s); return; } for (int i = k; i < n; i++) { (perm[k], perm[i]) = (perm[i], perm[k]); Go(k + 1, s + c[k, perm[k]]); (perm[k], perm[i]) = (perm[i], perm[k]); } }
                Go(0, 0); Check(got == best, $"Suboptimal: {got} vs {best}");
            }
        });
        Test("Hungarian can be paused after any row and resumed with identical results", () => {
            var rng = new Random(5); int n = 40; var c = new long[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) c[i, j] = rng.Next(0, 50);
            var u1 = new long[n + 1]; var v1 = new long[n + 1]; var p1 = new int[n + 1]; int r1 = 1; Hungarian.Step(c, n, u1, v1, p1, ref r1, long.MaxValue, _ => 0);
            var u2 = new long[n + 1]; var v2 = new long[n + 1]; var p2 = new int[n + 1]; int r2 = 1; int calls = 0;
            while (!Hungarian.Step(c, n, u2, v2, p2, ref r2, 1, _ => 0)) calls++;
            Check(calls >= n - 1 && p1.SequenceEqual(p2) && u1.SequenceEqual(u2) && v1.SequenceEqual(v2), "Paused solve differed");
            // Counting on from work already spent (one budget over several districts): a spent budget solves no row, and
            // one operation short of it solves exactly one, charged on top.
            var u3 = new long[n + 1]; var v3 = new long[n + 1]; var p3 = new int[n + 1]; int r3 = 1; long ops = 100;
            Check(!Hungarian.Step(c, n, u3, v3, p3, ref r3, ref ops, 100, _ => 0) && r3 == 1 && ops == 100, $"A spent budget solved up to row {r3}, {ops} operations");
            ops = 99; Check(!Hungarian.Step(c, n, u3, v3, p3, ref r3, ref ops, 100, _ => 7) && r3 == 2 && ops >= 99 + 7 + n, $"One operation short of the budget: up to row {r3}, {ops} operations");
        });
        Test("cycle splitting: simple, complete, deterministic and applicable one at a time", () => {
            var rng = new Random(3);
            for (int trial = 0; trial < 200; trial++)
            {
                int nodes = rng.Next(2, 9), edges = rng.Next(1, 25); var from = new List<int>(); var to = new List<int>();
                // Build a balanced multigraph out of random cycles.
                while (from.Count < edges) { int len = rng.Next(2, 5); var ring = Enumerable.Range(0, nodes).OrderBy(_ => rng.Next()).Take(Math.Min(len, nodes)).ToList(); for (int i = 0; i < ring.Count; i++) { from.Add(ring[i]); to.Add(ring[(i + 1) % ring.Count]); } }
                var a = Cycles.Split(nodes, from.ToArray(), to.ToArray()); var b = Cycles.Split(nodes, from.ToArray(), to.ToArray());
                Check(a.Count == b.Count && a.Zip(b).All(x => x.First.SequenceEqual(x.Second)), "Not deterministic");
                Check(a.SelectMany(x => x).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, from.Count)), "Edges lost or repeated");
                foreach (var cycle in a)
                {
                    Check(cycle.Select(e => from[e]).Distinct().Count() == cycle.Length, "Cycle is not simple");
                    for (int i = 0; i < cycle.Length; i++) Check(to[cycle[i]] == from[cycle[(i + 1) % cycle.Length]], "Cycle does not close");
                }
            }
        });
        Test("unbalanced moves are refused rather than looped on", () => {
            bool threw = false; try { Cycles.Split(3, new[] { 0 }, new[] { 1 }); } catch (InvalidOperationException) { threw = true; }
            Check(threw, "Unbalanced graph accepted");
        });
        Test("moves into empty beds split into chains and cycles, each applicable on its own", () => {
            var rng = new Random(4);
            for (int trial = 0; trial < 300; trial++)
            {
                // Adults in random homes with random spare beds, each sent to a random home with room for them all.
                int nodes = rng.Next(2, 8); var room = Enumerable.Range(0, nodes).Select(_ => rng.Next(0, 4)).ToArray();
                var home = new List<int>(); for (int h = 0; h < nodes; h++) for (int k = rng.Next(0, 4); k > 0; k--) home.Add(h);
                var count = new int[nodes]; foreach (int h in home) count[h]++;
                var cap = count.Select((c, h) => c + room[h]).ToArray(); var fill = new int[nodes];
                var from = new List<int>(); var to = new List<int>();
                foreach (int h in home)
                {
                    var open = Enumerable.Range(0, nodes).Where(x => fill[x] < cap[x]).ToList(); int target = open[rng.Next(open.Count)]; fill[target]++;
                    if (target != h) { from.Add(h); to.Add(target); }
                }
                var a = Cycles.SplitChains(nodes, from.ToArray(), to.ToArray()); var b = Cycles.SplitChains(nodes, from.ToArray(), to.ToArray());
                Check(a.Count == b.Count && a.Zip(b).All(x => x.First.SequenceEqual(x.Second)), "Not deterministic");
                Check(a.SelectMany(x => x).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, from.Count)), "Edges lost or repeated");
                foreach (var unit in a)
                {
                    for (int i = 0; i + 1 < unit.Length; i++) Check(to[unit[i]] == from[unit[i + 1]], "Moves do not follow on");
                    bool cycle = to[unit[^1]] == from[unit[0]];
                    Check(unit.Select(e => from[e]).Distinct().Count() == unit.Length, "A home is left twice");
                    // Alone, it keeps every home within its beds: a chain ends in a home with a spare bed.
                    var after = (int[])count.Clone(); foreach (int e in unit) { after[from[e]]--; after[to[e]]++; }
                    Check(Enumerable.Range(0, nodes).All(h => after[h] <= cap[h]), "Applied alone, a unit overfills a home");
                    if (cycle) Check(after.SequenceEqual(count), "A cycle changed a head count");
                }
            }
        });

        Test("the pass reaches the true optimum (brute force) on random small colonies", () => {
            for (int seed = 0; seed < 100; seed++)
            {
                var f = Colony(seed, homes: 3 + seed % 3, adults: 6 + seed % 3, works: 3, children: seed % 3, districts: seed < 60 ? 1 : 2 + seed % 2);
                StartHome = f.People.ToDictionary(p => p.Key, p => p.Value.Home);
                long best = Best(f); var counts = f.AdultCounts(); var kids = f.People.Values.Where(p => !p.Adult).ToDictionary(p => p.Id, p => p.Home);
                var e = Run(f);
                Check(Achieved(f) == best, $"seed {seed}: objective {Achieved(f)} but optimum is {best}");
                Check(f.AdultCounts().OrderBy(x => x.Key).SequenceEqual(counts.OrderBy(x => x.Key)), "A home's adult count changed");
                Check(kids.All(k => f.People[k.Key].Home == k.Value), "A child moved");
            }
        });
        Test("with empty beds, the pass reaches the true optimum (brute force) unless the breeding rule turns a move down", () => {
            for (int seed = 0; seed < 100; seed++)
            {
                var f = Colony(seed, homes: 3 + seed % 3, adults: 5 + seed % 3, works: 3, children: seed % 3, districts: seed < 60 ? 1 : 2 + seed % 2);
                var rng = new Random(seed);
                foreach (var h in f.Homes.Values) h.Capacity += rng.Next(0, 3) == 0 ? rng.Next(1, 3) : 0;
                for (int k = 0; k < 1 + seed % 2; k++)
                {
                    int d = rng.Next(0, seed < 60 ? 1 : 2 + seed % 2);   // an empty home in district d, laid out as Colony does
                    f.Homes[G(900 + k)] = new Home { Capacity = rng.Next(1, 3), X = d * 60 + rng.Next(0, 60), Y = rng.Next(0, 60), District = G(9000 + d) };
                }
                StartHome = f.People.ToDictionary(p => p.Key, p => p.Value.Home);
                long best = Best(f); var kids = f.People.Values.Where(p => !p.Adult).ToDictionary(p => p.Id, p => p.Home);
                int breeding = f.BreedingHomes(); var e = Run(f);
                // The breeding rule can turn a chain down, and only then may the result fall short of the optimum.
                Check(e.LastReport.Rejected > 0 ? Achieved(f) >= best : Achieved(f) == best, $"seed {seed}: objective {Achieved(f)} but optimum is {best}");
                Check(f.BreedingHomes() >= breeding, $"seed {seed}: fewer homes can breed");
                foreach (var h in f.Homes) Check(f.People.Values.Count(p => p.Home == h.Key) <= h.Value.Capacity, $"seed {seed}: home over capacity");
                Check(kids.All(k => f.People[k.Key].Home == k.Value), "A child moved");
                Check(f.People.Values.All(p => f.Homes[p.Home].District == p.District), "Someone crossed districts");
            }
        });
        Test("a worker moves into an empty home beside the workplace, and a second pass changes nothing", () => {
            var f = new Fake(); f.Works[G(500)] = (0, 0, 0);
            f.Homes[G(1)] = new Home { Capacity = 2, X = 40 };                 // the worker's lodge, far away and full
            f.Homes[G(2)] = new Home { Capacity = 1, X = 1 };                  // an empty mini lodge beside the workplace
            f.Homes[G(3)] = new Home { Capacity = 1, X = 2, Usable = false };  // a paused one, nearer still than the lodge
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(1), District = G(9000) };   // unemployed: stays
            var e = Run(f);
            Check(f.People[G(10)].Home == G(2) && f.People[G(11)].Home == G(1), "The worker was not moved beside work");
            Check(e.LastReport.Applied == 1 && e.LastReport.RouteCostSaved == 39, $"Report: {e.LastReport.Applied} applied, {e.LastReport.RouteCostSaved} saved");
            var fp = f.Fingerprint(); Run(f, Reload(e.State)); Check(f.Fingerprint() == fp, "A settled colony was reshuffled");
            // A lone adult is moved too.
            f.People.Remove(G(11)); f.People[G(10)].Home = G(1); Run(f); Check(f.People[G(10)].Home == G(2), "A lone worker stayed far away");
        });
        Test("a move into an empty bed is turned down if it leaves fewer homes that can breed", () => {
            // Two adults and an empty bed can breed: the worker stays, since the lodge beside work has one bed and can't.
            var f = new Fake(); f.Works[G(500)] = (0, 0, 0);
            f.Homes[G(1)] = new Home { Capacity = 3, X = 40 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 1 };
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(1), District = G(9000) };
            var e = Run(f); Check(f.People[G(10)].Home == G(1) && e.LastReport.Rejected == 1, "A breeding home was broken up");
            // The same move into a two-bed home beside work that already holds an adult makes a new breeding home: allowed.
            f.Homes[G(2)].Capacity = 3; f.People[G(12)] = new Person { Id = G(12), Home = G(2), District = G(9000) };
            Run(f); Check(f.People[G(10)].Home == G(2), "A move that keeps the breeding homes was turned down");
            // The empty bed of a three-bed home with two adults is a child's bed: the worker only swaps with an adult there,
            // so the home still breeds.
            var g = new Fake(); g.Works[G(500)] = (0, 0, 0);
            g.Homes[G(1)] = new Home { Capacity = 1, X = 40 }; g.Homes[G(2)] = new Home { Capacity = 3, X = 1 };
            g.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            for (int i = 0; i < 2; i++) g.People[G(20 + i)] = new Person { Id = G(20 + i), Home = G(2), District = G(9000) };
            Run(g); Check(g.AdultsIn(G(2)) == 2 && g.AdultsIn(G(1)) == 1 && g.CanBreed(G(2)), "An adult took the child's bed of a breeding home");
            // The game's rule: a home with as many children as half its adults can't breed, so a worker may leave it.
            var h = new Fake(); h.Works[G(500)] = (0, 0, 0);
            h.Homes[G(1)] = new Home { Capacity = 6, X = 40 }; h.Homes[G(2)] = new Home { Capacity = 1, X = 1 };
            h.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            h.People[G(11)] = new Person { Id = G(11), Home = G(1), District = G(9000) };
            h.People[G(12)] = new Person { Id = G(12), Home = G(1), District = G(9000), Adult = false };
            e = Run(h); Check(h.People[G(10)].Home == G(2) && e.LastReport.Rejected == 0, "A worker was kept in a home that can't breed");
            // Without the child it can: the move is turned down.
            h.People.Remove(G(12)); h.People[G(10)].Home = G(1);
            e = Run(h); Check(h.People[G(10)].Home == G(1) && e.LastReport.Rejected == 1, "A breeding home of six beds was broken up");
        });
        Test("passes over colonies with empty beds never leave fewer homes that can breed", () => {
            int moved = 0;
            for (int seed = 0; seed < 60; seed++)
            {
                var f = Spare(Colony(seed, homes: 30, adults: 70, works: 25, children: 8, districts: 1 + seed % 3), seed, emptyHomes: 4);
                foreach (var h in f.Homes.Values) if (h.Capacity > 1 && seed % 2 == 0) h.Capacity++;   // more homes that can breed
                int before = f.BreedingHomes(); var counts = f.AdultCounts(); var adultsBefore = f.Homes.Keys.ToDictionary(h => h, f.AdultsIn); var kids = f.People.Values.Where(p => !p.Adult).ToDictionary(p => p.Id, p => p.Home);
                var e = Run(f); var state = e.State;
                for (int day = 0; day < 3; day++)
                {
                    // Nothing changes between capture and apply here, so a stale chain means the engine offered a bed the
                    // game would refuse, such as a child's bed.
                    Check(e.LastReport.Stale == 0, $"seed {seed}: {e.LastReport.Stale} stale on day {day}");
                    Check(f.BreedingHomes() >= before, $"seed {seed}: homes that can breed fell from {before} to {f.BreedingHomes()}");
                    foreach (var h in f.Homes) Check(f.People.Values.Count(p => p.Home == h.Key) <= h.Value.Capacity, $"seed {seed}: home over capacity");
                    foreach (var h in f.Homes.Keys) Check(f.AdultsIn(h) <= Math.Max(adultsBefore[h], f.AdultBeds(h)), $"seed {seed}: an adult took a child's bed");
                    before = f.BreedingHomes(); adultsBefore = f.Homes.Keys.ToDictionary(h => h, f.AdultsIn); e = Run(f, Reload(state)); state = e.State;
                }
                Check(kids.All(k => f.People[k.Key].Home == k.Value), "A child moved");
                if (!f.AdultCounts().OrderBy(x => x.Key).SequenceEqual(counts.OrderBy(x => x.Key))) moved++;
            }
            Check(moved >= 30, $"Adults moved into empty beds in only {moved} of 60 colonies");
            Console.WriteLine($"   60 colonies with empty beds: head counts changed in {moved}, homes that can breed never fewer");
        });
        Test("a badly housed colony gets much shorter commutes and no home changes its head count", () => {
            var f = Colony(1, homes: 90, adults: 240, works: 120, children: 20); double before = f.Total(); var counts = f.AdultCounts();
            var e = Run(f);
            Check(f.Total() < before * 0.6, $"Commute only {before:F0} -> {f.Total():F0}");
            Check(f.AdultCounts().OrderBy(x => x.Key).SequenceEqual(counts.OrderBy(x => x.Key)), "Adult counts changed");
            foreach (var h in f.Homes) Check(f.People.Values.Count(p => p.Home == h.Key) <= h.Value.Capacity, "Home over capacity");
            Console.WriteLine($"   240 adults, 90 homes: commute {before:F0} -> {f.Total():F0} in {e.LastReport.Ticks} ticks, {e.LastReport.Queries} route queries, {e.LastReport.Applied} cycles");
        });
        Test("a second pass on an optimized colony changes nothing", () => {
            var f = Colony(2, homes: 40, adults: 100, works: 50); var e = Run(f); var fp = f.Fingerprint(); int applied = f.Applied.Count;
            Run(f, Reload(e.State)); Check(f.Fingerprint() == fp && f.Applied.Count == applied, "Optimized colony was reshuffled");
        });
        Test("small savings below the stay bonus do not move anyone", () => {
            var f = new Fake(); f.Homes[G(1)] = new Home { Capacity = 1, X = 11 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 10 };
            f.Works[G(500)] = (0, 0, 0);
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(2), District = G(9000) };   // unemployed
            Run(f); Check(f.Applied.Count == 0, "Moved for a 1 unit saving");
            f.Homes[G(2)].X = 4; Run(f); Check(f.People[G(10)].Home == G(2) && f.People[G(11)].Home == G(1), "Did not take a 7 unit saving");
        });
        Test("unemployed adults are shuffled out of the way for workers", () => {
            var f = new Fake(); f.Homes[G(1)] = new Home { Capacity = 1, X = 0 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 50 };
            f.Works[G(500)] = (1, 0, 0);
            f.People[G(10)] = new Person { Id = G(10), Home = G(2), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(1), District = G(9000) };
            Run(f); Check(f.People[G(10)].Home == G(1) && f.People[G(11)].Home == G(2), "Idle adult kept the best bed");
        });

        Test("input enumeration order does not change the outcome", () => {
            foreach (int districts in new[] { 1, 3 })
            {
                var a = Colony(7, 60, 150, 70, 10, districts); var b = a.Copy(); b.Reverse = true;
                var ea = Run(a); var eb = Run(b);
                Check(a.Fingerprint() == b.Fingerprint() && string.Join("|", a.Applied) == string.Join("|", b.Applied), "Outcome depends on order");
                Check(JsonConvert.SerializeObject(ea.State) == JsonConvert.SerializeObject(eb.State), "State depends on order");
            }
        });
        Test("two peers ticking in lockstep have identical serialized state at every tick of eight passes", () => {
            // Two districts of 45 homes: the second pass takes costs the first one verified outside the candidate rows.
            // Eight passes: the eighth prices again the first pass's costs that are still needed (a rejected swap each).
            foreach (var (name, start) in new[] { ("1 district", Colony(8, 50, 130, 60, 8)), ("3 districts", Colony(8, 50, 130, 60, 8, 3)), ("2 districts of 45 homes", Colony(8, 90, 230, 80, 8, 2)),
                ("2 districts, a rejected swap each", FarSwap(2)), ("3 districts with empty beds", Spare(Colony(8, 50, 130, 60, 8, 3), 8)) })
            {
                var a = start.Copy(); var b = start.Copy(); var ea = new PassEngine(a); var eb = new PassEngine(b); int passes = 1, used = 0, rechecked = 0;
                for (int t = 0; t < 5000 && (ea.Busy || ea.State.Requested || passes < DuePass); t++)
                {
                    if (!ea.Busy && !ea.State.Requested) { ea.RequestPass(); eb.RequestPass(); passes++; }   // the next day starts on both
                    ea.Tick(); eb.Tick(); Check(JsonConvert.SerializeObject(ea.State) == JsonConvert.SerializeObject(eb.State), $"{name}: peers diverged at tick {t}");
                    if (passes == 2 && ea.State.Stage == 1) used = LearnedInUse(ea.State);
                    if (passes == DuePass && ea.State.Stage == 1) rechecked = ea.State.Snap.RecheckCosts.Length;
                }
                Check(!ea.Busy && passes == DuePass && a.Fingerprint() == b.Fingerprint(), $"{name}: peers housed differently");
                Check(!name.StartsWith("2 districts") || used > 0, $"{name}: the second pass used no cost the first one verified");
                Check(!name.Contains("rejected swap") || rechecked == 4, $"{name}: pass {DuePass} priced {rechecked} remembered costs again, expected 4");
            }
        });
        Test("saving and reloading at any tick of three passes gives exactly the uninterrupted result", () => {
            // Three districts of 15 homes: padded candidate rows. PaddingTail: pricing ends with a tick that only skips padding.
            // Two districts of 45 homes: the second pass takes costs the first one verified outside the candidate rows.
            // A rejected swap in each of 2 or 16 districts: those costs decide what the later passes do; with 16 the
            // solve spans several ticks, so a reload lands mid-solve in a later district, where the rows of that district
            // solved so far are rebuilt. Six districts of 400 adults: a solve tick ends just as a district is done, so a
            // reload lands at the start of the next one.
            // Passes 1, 2 and 8 are checked; the eighth prices again the first pass's costs that are still needed.
            foreach (var (name, colony) in new[] { ("1 district", Colony(9, 45, 110, 55, 6)), ("3 districts", Colony(9, 45, 110, 55, 6, 3)), ("2 districts of 45 homes", Colony(9, 90, 220, 90, 6, 2)),
                ("2 districts, a rejected swap each", FarSwap(2)), ("16 districts, a rejected swap each", FarSwap(16)), ("padding tail", PaddingTail()),
                ("6 districts, a solve tick ends with a district", Colony(0, 120, 400, 100, 0, 6)), ("3 districts with empty beds", Spare(Colony(9, 45, 110, 55, 6, 3), 9)) })
            {
                var start = colony; PassState saved = null; bool skipOnly = false; int used = 0, rechecked = 0, midSolve = 0, between = 0; var rejected = new List<int>();
                for (int pass = 1; pass <= DuePass; pass++)
                {
                    // Each pass starts from the world and the saved state the previous one left.
                    var whole = start.Copy(); var ew = Run(whole, saved == null ? null : Reload(saved)); rejected.Add(ew.LastReport.Rejected);
                    string reference = JsonConvert.SerializeObject(ew.State); int ticks = (int)ew.LastReport.Ticks;
                    var live = start.Copy(); var el = Strict(new PassEngine(live, saved == null ? null : Reload(saved)), $"{name}: pass {pass}"); el.RequestPass();
                    for (int t = 0; t < ticks + 2 && (pass <= 2 || pass == DuePass); t++)
                    {
                        var resumedWorld = start.Copy();   // nothing is applied before the last tick, so the world is unchanged mid-pass
                        if (el.Busy && live.Applied.Count == 0)
                        {
                            if (pass > 1 && el.State.Stage == 2 && el.State.District > 0 && el.State.Row > 1) midSolve++;
                            if (el.State.Stage == 2 && el.State.District > 0 && el.State.Row == 1) between++;
                            var restored = Strict(new PassEngine(resumedWorld, Reload(el.State)), $"{name}: resume at tick {t} of pass {pass}");
                            for (int k = 0; k < 5000 && (restored.Busy || restored.State.Requested); k++) restored.Tick();
                            Check(resumedWorld.Fingerprint() == whole.Fingerprint(), $"{name}: resume at tick {t} of pass {pass} housed differently");
                            Check(JsonConvert.SerializeObject(restored.State) == reference, $"{name}: resume at tick {t} of pass {pass} ended in a different state");
                        }
                        int calls = live.Calls, stage = el.State.Stage; el.Tick();
                        if (stage == 1 && el.State.Stage == 2 && live.Calls == calls) skipOnly = true;
                        if (pass == 2 && el.State.Stage == 1) used = LearnedInUse(el.State);
                        if (pass == DuePass && el.State.Stage == 1) rechecked = el.State.Snap.RecheckCosts.Length;
                    }
                    start = whole; saved = ew.State;
                }
                Check(name != "padding tail" || skipOnly, "PaddingTail did not end pricing with a tick that only skips padding");
                Check(!name.StartsWith("2 districts") || used > 0, $"{name}: the second pass used no cost the first one verified");
                Check(!name.StartsWith("6 districts") || between > 0, $"{name}: no reload landed between two districts' solves");
                if (name.Contains("rejected swap"))
                {
                    int districts = name.StartsWith("16") ? 16 : 2;
                    Check(rejected[0] == districts && rejected.Skip(1).All(r => r == 0), $"{name}: cycles rejected per pass {string.Join(", ", rejected)}");
                    Check(rechecked == 2 * districts, $"{name}: pass {DuePass} priced {rechecked} remembered costs again, expected {2 * districts}");
                    Check(districts == 2 || midSolve > 0, $"{name}: no reload landed mid-solve in a later district, in a pass that uses remembered costs");
                }
            }
        });
        Test("a peer that reloaded mid-pass stays in lockstep, tick for tick, with one that did not", () => {
            // A multiplayer peer may load a save taken mid-pass (a rehost, a rejoin) while another peer never
            // reloaded. From then on both must do the same work each tick: a pass that finished one tick later on one
            // computer would move beavers at a different tick there, which is a desync.
            // Eight passes: the second starts from costs the first one verified, which the saved state must carry, and
            // the eighth prices again those still needed. In the rejected-swap colonies they decide what the later passes
            // do, so a peer that lost them would not keep step; with 16 districts the solve spans several ticks, so peers
            // also reload mid-solve in a later district, where the rows of that district solved so far are rebuilt.
            // Peers reload during passes 1, 2 and 8.
            string Position(PassEngine e) => $"stage {e.State.Stage} district {e.State.District} row {e.State.Row} cursor {e.State.Cursor} ticks {e.State.Ticks} queries {e.State.Queries}";
            // Two districts of 50 homes: learned costs outside the rows, and a reload mid-solve in the second district;
            // four of 25: padded rows. Six districts of 400 adults: a solve tick ends just as a district is done.
            foreach (var (name, start, every) in new[] { ("1 district", Colony(19, 100, 300, 130, 10), 5), ("2 districts", Colony(19, 100, 300, 130, 10, 2), 5),
                ("4 districts", Colony(19, 100, 300, 130, 10, 4), 5), ("2 districts, a rejected swap each", FarSwap(2), 1), ("16 districts, a rejected swap each", FarSwap(16), 1),
                ("6 districts, a solve tick ends with a district", Colony(0, 120, 400, 100, 0, 6), 5) })
            {
                var live = start.Copy(); var el = Strict(new PassEngine(live), $"{name}: the peer that never reloaded");
                var restored = new List<(int At, PassEngine Engine, Fake World)>(); int t = 0, used = 0, midSolve = 0, between = 0; var ticks = new List<long>(); var rejected = new List<int>();
                for (int pass = 1; pass <= DuePass; pass++)
                {
                    el.RequestPass(); foreach (var (_, engine, _) in restored) engine.RequestPass();   // the day starts on every peer at once
                    for (; t < 20000 && (el.Busy || el.State.Requested); t++)
                    {
                        if (el.Busy && (pass <= 2 || pass == DuePass) && (el.State.Stage == 2 || t % every == 0))
                        {
                            if (pass > 1 && el.State.Stage == 2 && el.State.District > 0 && el.State.Row > 1) midSolve++;
                            if (el.State.Stage == 2 && el.State.District > 0 && el.State.Row == 1) between++;
                            var world = live.Copy();   // nothing is applied before a pass's last tick
                            restored.Add((t, Strict(new PassEngine(world, Reload(el.State)), $"{name}: a peer that reloaded at tick {t}"), world));
                        }
                        el.Tick();
                        if (pass == 2 && el.State.Stage == 1) used = LearnedInUse(el.State);
                        foreach (var (at, engine, _) in restored)
                        {
                            engine.Tick();
                            Check(Position(engine) == Position(el), $"{name}: a peer that reloaded at tick {at} is at {Position(engine)} while the other is at {Position(el)} after tick {t}");
                        }
                    }
                    string reference = JsonConvert.SerializeObject(el.State);
                    foreach (var (at, engine, world) in restored)
                        Check(JsonConvert.SerializeObject(engine.State) == reference && world.Fingerprint() == live.Fingerprint(), $"{name}: a peer that reloaded at tick {at} ended pass {pass} differently");
                    ticks.Add(el.LastReport.Ticks); rejected.Add(el.LastReport.Rejected);
                }
                Check(restored.Count > (every == 1 ? 5 : 40), $"{name}: too few reload points exercised: {restored.Count}");
                Check(!name.StartsWith("2 districts") || used > 0, $"{name}: the second pass used no cost the first one verified");
                Check(name != "2 districts" || midSolve > 0, $"{name}: no peer reloaded mid-solve in the second district after pass 1");
                Check(!name.StartsWith("6 districts") || between > 0, $"{name}: no peer reloaded between two districts' solves");
                if (name.Contains("rejected swap"))
                {
                    int districts = name.StartsWith("16") ? 16 : 2;
                    Check(rejected[0] == districts && rejected.Skip(1).All(r => r == 0), $"{name}: cycles rejected per pass {string.Join(", ", rejected)}");
                    Check(districts == 2 || midSolve > 0, $"{name}: no peer reloaded mid-solve in a later district, in a pass that uses remembered costs");
                }
                Console.WriteLine($"   {name}: {restored.Count} reload points ({midSolve} mid-solve in a later district after pass 1, {between} between districts), each in lockstep to the end of {DuePass} passes ({ticks.Sum()} ticks)");
            }
        });

        Test("with a large colony's larger budgets, a peer that reloaded mid-pass stays in lockstep, tick for tick, with one that did not", () => {
            // Two districts of 750 adults: both budgets are larger than the least ones. They are set from the snapshot as
            // the pass starts and saved with it, so a peer that loads a save taken mid-pass keeps doing the same work on
            // every tick. Peers reload during the first pass (a randomly housed colony: a long solve) and the second (a
            // settled one), at the first tick of every stage and every few ticks, some mid-solve in the second district,
            // whose rows solved so far are rebuilt uncharged.
            string Position(PassEngine e) => $"stage {e.State.Stage} district {e.State.District} row {e.State.Row} cursor {e.State.Cursor} ticks {e.State.Ticks} queries {e.State.Queries} budgets {e.State.QueryBudget}, {e.State.SolveBudget}";
            var start = Colony(21, 350, 1500, 700, 0, 2, width: 200); var live = start.Copy(); var el = Strict(new PassEngine(live), "the peer that never reloaded");
            var restored = new List<(int At, PassEngine Engine, Fake World)>(); int t = 0, midSolve = 0, queries = 0; long ops = 0; var stages = new HashSet<(int, int)>(); var ticks = new List<long>();
            for (int pass = 1; pass <= 2; pass++)
            {
                el.RequestPass(); foreach (var (_, engine, _) in restored) engine.RequestPass();   // the day starts on every peer at once
                for (int last = 0; t < 20000 && (el.Busy || el.State.Requested); t++)
                {
                    int stage = el.State.Stage;
                    if (el.Busy && (stage != last || t % (pass == 1 ? 23 : stage == 2 ? 3 : 7) == 0))
                    {
                        stages.Add((pass, stage)); if (stage == 2 && el.State.District > 0 && el.State.Row > 1) midSolve++;
                        var world = live.Copy();   // nothing is applied before a pass's last tick
                        restored.Add((t, Strict(new PassEngine(world, Reload(el.State)), $"a peer that reloaded at tick {t}"), world));
                    }
                    last = stage; el.Tick();
                    if (el.Busy) { queries = el.State.QueryBudget; ops = el.State.SolveBudget; }
                    foreach (var (at, engine, _) in restored)
                    {
                        engine.Tick();
                        Check(Position(engine) == Position(el), $"a peer that reloaded at tick {at} is at {Position(engine)} while the other is at {Position(el)} after tick {t}");
                    }
                }
                string reference = JsonConvert.SerializeObject(el.State);
                foreach (var (at, engine, world) in restored)
                    Check(JsonConvert.SerializeObject(engine.State) == reference && world.Fingerprint() == live.Fingerprint(), $"a peer that reloaded at tick {at} ended pass {pass} differently");
                Check(queries > PassEngine.QueriesPerTick && ops > PassEngine.SolveOpsPerTick, $"pass {pass}: budgets {queries} route queries and {ops} solver operations per tick");
                ticks.Add(el.LastReport.Ticks);
            }
            Check(stages.Count == 6 && midSolve > 1 && restored.Count > 40, $"reload points: {restored.Count}, {midSolve} mid-solve in the second district, stages {string.Join(" ", stages)}");
            Console.WriteLine($"   2 districts of 750 adults: budgets {queries} route queries and {ops} solver operations per tick; {restored.Count} reload points ({midSolve} mid-solve in the second district), each in lockstep to the end of 2 passes ({string.Join(" + ", ticks)} ticks)");
        });

        Test("work is bounded per tick: route queries and solver operations stay within the pass's budgets", () => {
            foreach (int districts in new[] { 1, 6 })   // six districts of 25 homes: padded candidate rows
            {
                var f = Colony(4, 150, 400, 120, 30, districts); var e = new PassEngine(f); e.RequestPass(); int worst = 0, ticks = 0, budget = 0;
                do { f.Calls = 0; e.Tick(); worst = Math.Max(worst, f.Calls); ticks++; if (e.Busy) budget = e.State.QueryBudget; } while ((e.Busy || e.State.Requested) && ticks < 3000);
                Check(!e.Busy && ticks < 3000, "Did not finish");
                // A colony of this size keeps the least budget.
                Check(budget == PassEngine.QueriesPerTick && worst <= budget, $"{districts} district(s): {worst} route queries in one tick, budget {budget}");
                Console.WriteLine($"   400 adults, 150 homes, 120 workplaces, {districts} district(s): {ticks} ticks, {e.LastReport.Queries} queries, worst tick {worst}");
            }
            // Remembered costs priced again share the pricing budget: 17 districts price 34 of them on pass 8.
            var g = FarSwap(17); var eg = new PassEngine(g); int most = 0, rechecked = 0;
            for (int pass = 1; pass <= DuePass; pass++)
            {
                eg.RequestPass();
                for (int t = 0; t < 3000 && (eg.Busy || eg.State.Requested); t++)
                {
                    g.Calls = 0; eg.Tick(); most = Math.Max(most, g.Calls);
                    if (eg.State.Stage == 1) rechecked = eg.State.Snap.RecheckCosts.Length;
                }
            }
            Check(rechecked == 34 && most <= PassEngine.QueriesPerTick, $"17 rejected swaps: {rechecked} costs priced again on pass {DuePass}, {most} route queries in one tick");
            // One solver budget spans the districts of a tick: a solve tick charges less than the pass's budget plus the row
            // it started last (a row of an n-adult district costs at most n(n + 1)), and a tick that leaves rows for the
            // next one has spent all of it. Two districts of 800 adults get a larger budget than the least one; in the
            // second such colony the tick that ends the first district has spent more than the least budget but less than
            // its own, so it goes on into the second district.
            foreach (var (name, f) in new[] { ("4 districts of 300 adults", Colony(3, 400, 1200, 400, 0, 4)), ("6 districts of 67 adults", Colony(0, 120, 400, 100, 0, 6)),
                ("16 districts of 34 adults", FarSwap(16)), ("2 districts of 800 adults", Colony(7, 400, 1600, 800, 0, 2, width: 200)),
                ("2 districts of 800 adults, another colony", Colony(8, 400, 1600, 800, 0, 2, width: 200)) })
            {
                var e = Strict(new PassEngine(f), name); e.RequestPass(); long row = 0, peak = 0, budget = 0; int ticks = 0;
                for (int t = 0; t < 20000 && (e.Busy || e.State.Requested); t++)
                {
                    int stage = e.State.Stage;
                    if (stage == 2 && row == 0) row = e.State.Snap.AdultDistrict.GroupBy(d => d).Max(d => (long)d.Count() * (d.Count() + 1));
                    if (stage == 2) budget = e.State.SolveBudget;
                    e.Tick();
                    if (stage != 2) continue;
                    ticks++; peak = Math.Max(peak, e.SolveOps);
                    Check(e.SolveOps < budget + row, $"{name}: solve tick {ticks} charged {e.SolveOps} operations; the budget is {budget}, a row at most {row}");
                    Check(e.State.Stage != 2 || e.SolveOps >= budget, $"{name}: solve tick {ticks} stopped after {e.SolveOps} operations");
                }
                Check(!e.Busy && ticks > 1, $"{name}: {ticks} solve ticks");
                Check(name.StartsWith("2 districts") ? budget > PassEngine.SolveOpsPerTick : budget == PassEngine.SolveOpsPerTick, $"{name}: solver budget {budget}");
                Console.WriteLine($"   {name}: {ticks} solve ticks, budget {budget}, at most {peak} solver operations in one");
            }
        });
        Test("a large colony's pass ends within the day's daytime, with larger budgets set from its snapshot", () => {
            // A pass is requested when the day starts, and a day is 768 ticks, 512 of them daytime (the game's
            // DayNightCycle blueprint). The tick that starts the pass counts too. With 1,600 adults in one district
            // (the olhx harness's olh5 colony: 655 staffed workplaces) fixed budgets of 32 route queries and 250,000
            // solver operations per tick took 1,118 ticks, into the night and on into the next day's pass.
            const int Daytime = 512;
            foreach (var (name, f) in new[] { ("1,600 adults in one district", Colony(7, 400, 1600, 800, 0, 1, width: 200)),
                ("960 adults in one district", Colony(7, 240, 960, 480, 0, 1, width: 200)), ("1,600 adults in 3 districts", Colony(7, 400, 1600, 800, 0, 3, width: 200)) })
            {
                var e = Strict(new PassEngine(f), name); e.RequestPass(); int ticks = 0, worst = 0, queries = 0; long ops = 0;
                for (; ticks < 5000 && (e.Busy || e.State.Requested); ticks++)
                {
                    f.Calls = 0; e.Tick(); worst = Math.Max(worst, f.Calls);
                    if (e.Busy) { queries = e.State.QueryBudget; ops = e.State.SolveBudget; }
                }
                Check(!e.Busy && ticks == e.LastReport.Ticks + 1, $"{name}: {ticks} ticks, the report says {e.LastReport.Ticks}");
                Check(ticks <= Daytime, $"{name}: the pass took {ticks} ticks, longer than the {Daytime} daytime ticks of a day");
                Check(queries > PassEngine.QueriesPerTick && queries <= PassEngine.MaxQueriesPerTick && worst <= queries, $"{name}: {worst} route queries in one tick, budget {queries}");
                Check(ops > PassEngine.SolveOpsPerTick && ops <= PassEngine.MaxSolveOpsPerTick, $"{name}: solver budget {ops}");
                Console.WriteLine($"   {name}: {ticks} ticks, {e.LastReport.Works} staffed workplaces, {e.LastReport.Queries} route queries, {e.LastReport.Moves} moves; budgets {queries} route queries and {ops} solver operations per tick");
            }
            // A small colony keeps the least budgets, and a still larger one gets no more than the largest: its pass takes
            // longer instead. The budgets are set as the pass starts.
            foreach (var (name, f, queries, ops) in new[] { ("240 adults", Colony(1, 90, 240, 120, 20), PassEngine.QueriesPerTick, PassEngine.SolveOpsPerTick),
                ("2,600 adults in one district", Colony(7, 650, 2600, 1300, 0, 1, width: 200), PassEngine.MaxQueriesPerTick, PassEngine.MaxSolveOpsPerTick) })
            {
                var e = Strict(new PassEngine(f), name); e.RequestPass(); e.Tick();
                Check(e.State.Stage == 1 && e.State.QueryBudget == queries && e.State.SolveBudget == ops, $"{name}: budgets {e.State.QueryBudget} and {e.State.SolveBudget}, expected {queries} and {ops}");
            }
        });
        Test("far homes are never chosen without a fresh route check", () => {
            var f = Colony(5, 120, 200, 40); var e = new PassEngine(f); e.RequestPass(); var checkedPairs = new HashSet<(Guid, Guid)>(); var before = f.People.ToDictionary(p => p.Key, p => p.Value.Home);
            var probe = new Probe(f, checkedPairs); e = new PassEngine(probe); e.RequestPass();
            for (int t = 0; t < 4000 && (e.Busy || e.State.Requested); t++) { probe.Recording = e.State.Stage == 3; e.Tick(); }
            foreach (var p in f.People.Values.Where(p => p.Adult && p.Home != before[p.Id] && f.Works.ContainsKey(p.Work)))
                Check(checkedPairs.Contains((p.Home, p.Work)), "Moved to an unchecked home");
        });

        Test("a beaver whose route is cut is moved to a home that can reach work", () => {
            var f = new Fake(); f.Homes[G(1)] = new Home { Capacity = 1, X = 3 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 4 };
            f.Works[G(500)] = (0, 0, 0); f.Blocked.Add((G(1), G(500)));
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(2), District = G(9000) };
            var e = Run(f); Check(f.People[G(10)].Home == G(2) && e.LastReport.Recovered == 1, "Disconnected commute not repaired");
        });
        Test("a beaver who moves but stays cut off is not counted as a repaired commute", () => {
            var f = new Fake(); f.Homes[G(1)] = new Home { Capacity = 1, X = 0 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 50 };
            f.Works[G(500)] = (25, 0, 0); f.Works[G(501)] = (0, 0, 0);
            f.Blocked.Add((G(1), G(500))); f.Blocked.Add((G(2), G(500)));   // adult 10's workplace is out of reach from every home
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(2), Work = G(501), District = G(9000) };
            var e = Run(f);
            Check(f.People[G(11)].Home == G(1) && e.LastReport.RouteCostSaved == 50, "Adult 11's 50-unit saving was not made");
            Check(e.LastReport.Recovered == 0, $"{e.LastReport.Recovered} disconnected commute(s) reported repaired, but adult 10 is still cut off");
        });
        Test("a move is dropped when its route disappears after pricing", () => {
            var f = new Fake(); f.Homes[G(1)] = new Home { Capacity = 1, X = 40 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 1 };
            f.Works[G(500)] = (0, 0, 0);
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(2), District = G(9000) };
            var e = new PassEngine(f); e.RequestPass();
            while (e.State.Stage < 2) e.Tick();
            f.Blocked.Add((G(2), G(500)));   // a wall goes up while the solver is running
            for (int t = 0; t < 100 && (e.Busy || e.State.Requested); t++) e.Tick();
            Check(f.Applied.Count == 0 && f.People[G(10)].Home == G(1) && e.LastReport.Rejected == 1, "Moved into an unreachable home");
        });
        Test("a cycle is rejected if anyone in it would end unreachable, even when another member gains far more", () => {
            var f = new Fake(); f.Homes[G(1)] = new Home { Capacity = 1, X = 1000 }; f.Homes[G(2)] = new Home { Capacity = 1, X = 1 };
            f.Works[G(500)] = (0, 0, 0); f.Works[G(501)] = (20, 0, 0); f.Blocked.Add((G(1), G(500)));   // adult 10 is cut off today
            f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
            f.People[G(11)] = new Person { Id = G(11), Home = G(2), Work = G(501), District = G(9000) };
            var e = new PassEngine(f); e.RequestPass();
            while (e.State.Stage < 2) e.Tick();
            f.Blocked.Add((G(1), G(501)));   // ...and the swap partner would lose their route once the wall goes up
            for (int t = 0; t < 100 && (e.Busy || e.State.Requested); t++) e.Tick();
            Check(f.Applied.Count == 0 && e.LastReport.Rejected == 1, "Accepted a cycle that strands someone");
        });
        Test("workers of a workplace no home can reach are not sent after unpriced homes", () => {
            // 33 one-bed homes in a row: the workplace at X=0 prices the 32 nearest and none can reach it, so the 33rd,
            // unpriced, must not look like a way out. Moving there would only push the neighbour away from their job.
            var f = new Fake(); f.Works[G(500)] = (0, 0, 0); f.Works[G(501)] = (40, 0, 0);
            for (int i = 0; i <= 32; i++)
            {
                f.Homes[G(100 + i)] = new Home { Capacity = 1, X = i }; f.Blocked.Add((G(100 + i), G(500)));
                f.People[G(10 + i)] = new Person { Id = G(10 + i), Home = G(100 + i), District = G(9000) };
            }
            f.People[G(10)].Work = G(500); f.People[G(42)].Work = G(501);   // cut off at X=0; commutes from X=32
            var e = Run(f);
            Check(e.LastReport.Moves == 0 && e.LastReport.Rejected == 0, $"{e.LastReport.Moves} moves proposed, {e.LastReport.Rejected} cycle(s) rejected");
        });
        Test("one home that can't reach a workplace does not make its unpriced homes look unreachable", () => {
            // 33 one-bed homes in a row and two workplaces at X=0, each pricing the 32 nearest homes. Workplace 500's
            // worker lives in the unpriced home at X=32, and one of its priced homes can't reach it. Everyone else works
            // at 501, so every arrangement costs the same: moving anyone gains nothing and would be rejected.
            var f = new Fake(); f.Works[G(500)] = (0, 0, 0); f.Works[G(501)] = (0, 0, 0);
            for (int i = 0; i <= 32; i++)
            {
                f.Homes[G(100 + i)] = new Home { Capacity = 1, X = i };
                f.People[G(10 + i)] = new Person { Id = G(10 + i), Home = G(100 + i), Work = G(501), District = G(9000) };
            }
            f.People[G(42)].Work = G(500); f.Blocked.Add((G(131), G(500)));
            var e = Run(f);
            Check(e.LastReport.Moves == 0 && e.LastReport.Rejected == 0, $"{e.LastReport.Moves} moves proposed, {e.LastReport.Rejected} cycle(s) rejected");
        });
        Test("a cycle its fresh route check rejected is not proposed again on later days", () => {
            // The first pass proposes adult 10's swap to X=40 and its route check rejects it. Later passes know that from
            // the saved state (reloaded between passes, as a save does) and don't propose it. They ask for that route only
            // when the remembered cost comes due, every LearnedPasses passes, and so notice when the road is back.
            var f = FarSwap(); var e = Run(f);
            Check(e.LastReport.Moves == 2 && e.LastReport.Rejected == 1 && f.Applied.Count == 0, $"pass 1: {e.LastReport.Moves} moves, {e.LastReport.Rejected} rejected");
            int days = 2 * (PassEngine.LearnedPasses + 1);
            for (int pass = 2; pass <= days; pass++)
            {
                var asked = new HashSet<(Guid, Guid)>(); var probe = new Probe(f, asked) { Recording = true };
                e = Run(probe, Reload(e.State));
                Check(e.LastReport.Rejected == 0 && f.People[G(10)].Home == G(200), $"pass {pass}: the rejected cycle came back ({e.LastReport.Moves} moves, {e.LastReport.Rejected} rejected)");
                bool due = (pass - 1) % PassEngine.LearnedPasses == 0;
                Check(asked.Contains((G(201), G(500))) == due, due ? $"pass {pass}: the remembered missing route came due and was not checked again" : $"pass {pass}: asked again for the route known to be missing");
            }
            // The road is back: the move is made on the pass that finds it, the next one the missing route is due on.
            f.Blocked.Remove((G(201), G(500))); int movedOn = 0;
            for (int pass = days + 1; pass <= days + PassEngine.LearnedPasses && movedOn == 0; pass++)
            {
                e = Run(f, Reload(e.State));
                if (f.People[G(10)].Home == G(201)) movedOn = pass;
            }
            Check(movedOn == 3 * PassEngine.LearnedPasses + 1, $"the road came back before pass {days + 1}; the move was made on pass {movedOn}, expected {3 * PassEngine.LearnedPasses + 1}");
        });
        Test("a worker living beyond the nearest homes keeps a checked cost for home when the remembered one comes due", () => {
            // Workplace 500 at X=0 prices its 32 nearest homes (X=1). On day 1 adult 10, who works there, moves from X=80
            // to the unpriced X=40 (720). On day 3 adult 14 is hired and moves from X=90 to X=45 (800); X=45 has a
            // second, unemployed resident. When day 1's costs come due, adult 10's own must be priced again: left to the
            // estimate (896), a move to X=45 would look like a saving, and its route check would turn it down.
            var f = new Fake(); f.Works[G(500)] = (0, 0, 0);
            for (int i = 0; i < 32; i++)
            {
                f.Homes[G(1100 + i)] = new Home { Capacity = 1, X = 1 };
                f.People[G(2100 + i)] = new Person { Id = G(2100 + i), Home = G(1100 + i), Work = G(500), District = G(9000) };
            }
            void Add(int home, int x, params int[] adults)
            {
                f.Homes[G(home)] = new Home { Capacity = adults.Length, X = x };
                foreach (int a in adults) f.People[G(a)] = new Person { Id = G(a), Home = G(home), District = G(9000) };
            }
            Add(200, 80, 10); Add(201, 40, 11); Add(202, 45, 12, 13); Add(203, 90, 14); f.People[G(10)].Work = G(500);
            var e = Run(f); var rejected = new List<int> { e.LastReport.Rejected };
            Check(f.People[G(10)].Home == G(201), "day 1: adult 10 did not move to X=40");
            for (int pass = 2; pass <= 2 * (PassEngine.LearnedPasses + 1); pass++)
            {
                if (pass == 3) f.People[G(14)].Work = G(500);
                e = Run(f, Reload(e.State)); rejected.Add(e.LastReport.Rejected);
                if (pass == 3) Check(f.People[G(14)].Home == G(202), "day 3: adult 14 did not move to X=45");
            }
            Check(rejected.All(r => r == 0) && f.People[G(10)].Home == G(201) && f.People[G(14)].Home == G(202), $"cycles rejected per day: {string.Join(" ", rejected)}");
        });
        Test("a remembered cost lapses when it comes due, unless a worker lives there or no route led there", () => {
            // The swap is made on day 1: adult 10 now lives at X=40 (720) and an unemployed adult at X=50 (880). Both
            // costs are used for LearnedPasses passes; then X=40's, adult 10's own commute, is priced again and kept,
            // and X=50's, which none of the workplace's workers lives by, lapses to the estimate.
            var f = FarSwap(blocked: false); var e = Run(f);
            Check(f.People[G(10)].Home == G(201), "day 1: the swap was not made");
            string Kept(PassState st, int home)
            {
                for (int i = 0; i < st.LearnedCost.Length; i++)
                    if (st.LearnedWork[i] == G(500) && st.LearnedHome[i] == G(home)) return $"{st.LearnedCost[i]} age {st.LearnedAge[i]}";
                return "none";
            }
            for (int pass = 2; pass < DuePass; pass++) e = Run(f, Reload(e.State));
            Check(Kept(e.State, 200) == $"880 age {PassEngine.LearnedPasses - 1}" && Kept(e.State, 201) == $"720 age {PassEngine.LearnedPasses - 1}",
                $"after pass {DuePass - 1}: X=50 {Kept(e.State, 200)}, X=40 {Kept(e.State, 201)}");
            e = Run(f, Reload(e.State));
            Check(Kept(e.State, 200) == "none" && Kept(e.State, 201) == "720 age 0", $"after pass {DuePass}: X=50 {Kept(e.State, 200)}, expected none; X=40 {Kept(e.State, 201)}, expected 720 age 0");
        });
        Test("a cost priced fresh in the candidate row wins over a remembered one", () => {
            // Day 1 remembers that no route leads from X=40 to adult 10's workplace. By day 2 the road is back and a
            // filler home is paused, so X=40 is among the workplace's 32 nearest and priced fresh: that price decides.
            var f = FarSwap(); var e = Run(f);
            Check(e.LastReport.Rejected == 1, "day 1: the swap was not rejected");
            f.Blocked.Remove((G(201), G(500))); f.Homes[G(1100)].Usable = false;
            e = Run(f, Reload(e.State));
            Check(f.People[G(10)].Home == G(201), $"day 2: adult 10 still at X=50 ({e.LastReport.Moves} moves, {e.LastReport.Rejected} rejected)");
        });
        Test("at most LearnedLimit remembered costs are kept: the youngest, sorted by workplace and home", () => {
            // 200 more remembered costs than the limit, ages 0 to 5, for workplaces and homes the colony doesn't have. The
            // pass ages them by one and adds its own two (age 0); the oldest go first.
            var rng = new Random(3); int n = PassEngine.LearnedLimit + 200;
            var old = Enumerable.Range(0, n).Select(i => (Work: G(70000 + rng.Next(0, 50)), Home: G(80000 + i), Cost: 1000 + i, Age: i % (PassEngine.LearnedPasses - 1)))
                .OrderBy(x => x.Work).ThenBy(x => x.Home).ToList();
            var state = new PassState { LearnedWork = old.Select(x => x.Work).ToArray(), LearnedHome = old.Select(x => x.Home).ToArray(),
                LearnedCost = old.Select(x => x.Cost).ToArray(), LearnedAge = old.Select(x => x.Age).ToArray() };
            var e = Run(FarSwap(), Reload(state)); var s = e.State;
            Check(s.LearnedCost.Length == PassEngine.LearnedLimit && s.LearnedWork.Length == PassEngine.LearnedLimit && s.LearnedHome.Length == PassEngine.LearnedLimit && s.LearnedAge.Length == PassEngine.LearnedLimit,
                $"{s.LearnedCost.Length} remembered costs kept, limit {PassEngine.LearnedLimit}");
            for (int i = 1; i < s.LearnedCost.Length; i++)
            {
                int c = s.LearnedWork[i - 1].CompareTo(s.LearnedWork[i]);
                Check(c < 0 || c == 0 && s.LearnedHome[i - 1].CompareTo(s.LearnedHome[i]) < 0, $"entry {i} is out of order or repeated");
            }
            // Filled youngest first: every age is kept whole up to the one that runs past the limit.
            var available = old.Select(x => x.Age + 1).Concat(new[] { 0, 0 }).GroupBy(a => a).ToDictionary(g => g.Key, g => g.Count());
            var kept = s.LearnedAge.GroupBy(a => a).ToDictionary(g => g.Key, g => g.Count()); int room = PassEngine.LearnedLimit;
            for (int age = 0; age < PassEngine.LearnedPasses; age++)
            {
                int expected = Math.Min(available.GetValueOrDefault(age), room); room -= expected;
                Check(kept.GetValueOrDefault(age) == expected, $"{kept.GetValueOrDefault(age)} costs of age {age} kept, expected {expected}");
            }
            var byPair = old.ToDictionary(x => (x.Work, x.Home), x => x);
            for (int i = 0; i < s.LearnedCost.Length; i++)
                if (byPair.TryGetValue((s.LearnedWork[i], s.LearnedHome[i]), out var x)) Check(s.LearnedCost[i] == x.Cost && s.LearnedAge[i] == x.Age + 1, $"entry {i} changed its cost or age");
        });
        Test("a cost learned on an earlier day is checked again before anyone moves on it", () => {
            // As above, but the route from X=40 exists on the first day; the swap is verified and then goes stale (the
            // home is paused as it is applied). By the next day that route is gone: the swap is proposed from the
            // remembered cost, and its fresh route check turns it down.
            var f = FarSwap(blocked: false); var e = new PassEngine(f); e.RequestPass();
            while (e.State.Stage < 3) e.Tick();
            f.Homes[G(201)].Usable = false;
            for (int t = 0; t < 100 && (e.Busy || e.State.Requested); t++) e.Tick();
            Check(e.LastReport.Stale == 1 && f.Applied.Count == 0, $"pass 1: {e.LastReport.Applied} applied, {e.LastReport.Stale} stale");
            f.Homes[G(201)].Usable = true; f.Blocked.Add((G(201), G(500)));
            var asked = new HashSet<(Guid, Guid)>(); var probe = new Probe(f, asked) { Recording = true };
            e = Run(probe, Reload(e.State));
            Check(e.LastReport.Moves == 2 && asked.Contains((G(201), G(500))), "pass 2: the remembered swap was not proposed and checked");
            Check(e.LastReport.Rejected == 1 && f.Applied.Count == 0 && f.People[G(10)].Home == G(200), "pass 2: moved on a remembered cost without a fresh check");
            e = Run(f, Reload(e.State));
            Check(e.LastReport.Moves == 0 && e.LastReport.Rejected == 0, $"pass 3: {e.LastReport.Moves} moves, {e.LastReport.Rejected} rejected; the fresh cost did not replace the remembered one");
        });
        Test("beavers who changed home or job mid-pass are skipped; everyone else is still moved", () => {
            var f = Colony(12, 40, 100, 45); var trial = f.Copy(); Run(trial);
            var movers = f.People.Values.Where(p => trial.People[p.Id].Home != p.Home && f.Works.ContainsKey(p.Work)).OrderBy(p => p.Id).Take(2).ToList();
            Check(movers.Count == 2, "Need two movers");
            var e = new PassEngine(f); e.RequestPass();
            while (e.State.Stage < 3) e.Tick();
            var moved = movers;
            moved[0].Home = f.Homes.Keys.OrderBy(k => k).Last();   // the vanilla assigner re-housed them
            moved[1].Work = moved[1].Work == G(5001) ? G(5002) : G(5001);   // ...or their job changed
            var pinned = moved.ToDictionary(p => p.Id, p => (p.Home, p.Work));
            for (int t = 0; t < 200 && (e.Busy || e.State.Requested); t++) e.Tick();
            foreach (var p in moved) Check(f.People[p.Id].Home == pinned[p.Id].Home, "A stale beaver was moved anyway");
            Check(e.LastReport.Applied > 0 && e.LastReport.Stale >= 1, "Expected some cycles skipped and the rest applied");
            foreach (var h in f.Homes) Check(f.People.Values.Count(p => p.Home == h.Key) <= h.Value.Capacity || h.Key == f.Homes.Keys.OrderBy(k => k).Last(), "Home over capacity");
        });
        Test("beavers never cross districts", () => {
            var f = Colony(13, 40, 100, 45, 0, districts: 3); var district = f.People.ToDictionary(p => p.Key, p => p.Value.District);
            Run(f); foreach (var p in f.People.Values) Check(f.Homes[p.Home].District == district[p.Id], "Crossed districts");
        });
        Test("route queries stay inside the workplace's district, where the game's roads are", () => {
            // The game's road pathfinding never leaves a district, so a query for a home in another district is wasted.
            for (int seed = 0; seed < 5; seed++)
            {
                var f = Colony(20 + seed, homes: 70, adults: 180, works: 60, districts: 2); var e = new PassEngine(f); e.RequestPass();
                for (int t = 0; t < 1000 && e.State.Stage < 2; t++) e.Tick();
                // Every workplace still prices its own district's nearest homes, as many as the row holds.
                var s = e.State.Snap; int expected = 0;
                for (int w = 0; w < s.Works.Length; w++) expected += Math.Min(s.NearK, s.HomeDistrict.Count(d => d == f.DistrictOf(s.Works[w])));
                Check(e.State.Stage == 2 && f.Calls == expected, $"seed {20 + seed}: {f.Calls} route queries while pricing, expected {expected}");
                for (int t = 0; t < 5000 && (e.Busy || e.State.Requested); t++) e.Tick();
                Check(f.CrossCalls == 0, $"seed {20 + seed}: {f.CrossCalls} of {f.Calls} route queries asked for a route between districts");
            }
        });
        Test("beside a district border, the pass does as well as solving each district on its own, day after day", () => {
            // The small district's homes are the nearest ones to the big district's workplaces. They must not crowd out
            // the big district's own candidates, nor make its unpriced homes look unreachable.
            // Sixteen days in a row, each pass starting from the state the one before saved, as in the game: twice
            // through the remembered costs' lifetime, so costs come due, and are priced again or dropped, on the way.
            int days = 2 * (PassEngine.LearnedPasses + 1); var perDay = new int[days]; var perDayAlone = new int[days];
            for (int seed = 0; seed < 42; seed++)
            {
                var start = BorderColony(seed); var whole = start.Copy(); PassEngine e = null; var rejected = new int[days];
                for (int pass = 0; pass < days; pass++) { e = Run(whole, e == null ? null : Reload(e.State)); rejected[pass] = e.LastReport.Rejected; perDay[pass] += rejected[pass]; }
                double alone = 0; var aloneRejected = new int[days];
                foreach (var district in new[] { G(9000), G(9001) })
                {
                    var part = start.Only(district); PassEngine p = null;
                    for (int pass = 0; pass < days; pass++) { p = Run(part, p == null ? null : Reload(p.State)); aloneRejected[pass] += p.LastReport.Rejected; perDayAlone[pass] += p.LastReport.Rejected; }
                    alone += part.Total();
                }
                Check(whole.Total() <= alone, $"seed {seed}: commute {whole.Total():F0} after {days} passes, but {alone:F0} when each district is solved on its own");
                // After the first days every cycle a route check turned down is known, and a worker's own remembered
                // commute is priced again when it comes due, so no cycle is proposed only to be turned down.
                Check(rejected.Skip(2).All(r => r == 0) && aloneRejected.Skip(2).All(r => r == 0),
                    $"seed {seed}: cycles rejected per day {string.Join(" ", rejected)}; {string.Join(" ", aloneRejected)} when each district is solved on its own");
            }
            Console.WriteLine($"   42 border colonies over {days} days: cycles rejected per day {string.Join(" ", perDay)}; {string.Join(" ", perDayAlone)} when each district is solved on its own");
        });
        Test("each district is solved on its own: the same homes as a colony of that district alone, in no more solve ticks", () => {
            // Nobody crosses districts, so the colony's assignment splits into one per district. Solved as one, every
            // adult's row spans every district's beds, and the solve takes about three times as many ticks.
            int SolveTicks(Fake f)
            {
                var e = new PassEngine(f); Exception fault = null; e.Faulted += x => fault = x; e.RequestPass(); int ticks = 0;
                for (int t = 0; t < 20000 && (e.Busy || e.State.Requested); t++) { if (e.State.Stage == 2) ticks++; e.Tick(); }
                Check(fault == null && !e.Busy && !e.State.Requested, $"Did not finish: {fault?.Message}"); return ticks;
            }
            // A border colony with its two districts' IDs swapped: the small district comes first, then the big one.
            Fake SmallFirst(Fake f)
            {
                var g = f.Copy(); Guid Swap(Guid d) => d == G(9000) ? G(9001) : d == G(9001) ? G(9000) : d;
                foreach (var p in g.People.Values) p.District = Swap(p.District);
                foreach (var h in g.Homes.Values) h.District = Swap(h.District);
                foreach (var w in g.WorkDistrict.Keys.ToList()) g.WorkDistrict[w] = Swap(g.WorkDistrict[w]);
                return g;
            }
            foreach (var (name, start) in new[] { ("2 districts, 700 adults", Colony(3, 240, 700, 300, 0, 2)), ("3 districts, 900 adults", Colony(3, 300, 900, 300, 0, 3)),
                ("4 districts, 1,200 adults", Colony(3, 400, 1200, 400, 0, 4)), ("a small district, then a big one", SmallFirst(BorderColony(5))) })
            {
                var whole = start.Copy(); int ticks = SolveTicks(whole), alone = 0; var homes = new Dictionary<Guid, Guid>();
                foreach (var district in start.Homes.Values.Select(h => h.District).Distinct().OrderBy(d => d))
                {
                    var part = start.Only(district); alone += SolveTicks(part);
                    foreach (var p in part.People.Values) homes[p.Id] = p.Home;
                }
                Check(ticks <= alone, $"{name}: the solve took {ticks} ticks, but {alone} when each district is solved on its own");
                Check(whole.People.Values.All(p => homes[p.Id] == p.Home), $"{name}: housed differently from solving each district on its own");
                Console.WriteLine($"   {name}: {ticks} solve ticks; {alone} solving each district in a colony of its own");
            }
        });
        Test("paused homes keep their residents and never gain new ones", () => {
            var f = Colony(14, 30, 80, 30); var paused = f.Homes.Keys.OrderBy(k => k).Take(3).ToList(); foreach (var h in paused) f.Homes[h].Usable = false;
            var residents = f.People.Values.Where(p => paused.Contains(p.Home)).ToDictionary(p => p.Id, p => p.Home);
            Run(f); Check(residents.All(r => f.People[r.Key].Home == r.Value), "A paused home's resident was moved");
            Check(f.People.Values.Where(p => !residents.ContainsKey(p.Id)).All(p => !paused.Contains(p.Home)), "Someone moved into a paused home");
        });
        Test("a colony with nobody employed, or a single adult, is left alone", () => {
            var f = Colony(15, 10, 20, 5); foreach (var p in f.People.Values) p.Work = Guid.Empty; var fp = f.Fingerprint(); Run(f); Check(f.Fingerprint() == fp, "Moved unemployed colony");
            var g = Colony(16, 1, 1, 1); Run(g); Check(g.Applied.Count == 0, "Moved a lone adult");
        });
        Test("a world that throws while moving abandons the pass and recovers", () => {
            var f = Colony(17, 30, 70, 30); f.ThrowOnApply = true; Exception seen = null; var e = new PassEngine(f); e.Faulted += x => seen = x; e.RequestPass();
            for (int t = 0; t < 2000 && seen == null; t++) e.Tick();
            Check(seen != null && !e.Busy && !e.State.Requested, "Fault not contained");
            f.ThrowOnApply = false; e.RequestPass(); for (int t = 0; t < 2000 && (e.Busy || e.State.Requested); t++) e.Tick();
            Check(e.State.Passes >= 1 && f.Applied.Count > 0, "Did not recover on the next request");
        });
        Test("saved state survives a JSON round trip at every stage", () => {
            // 35 homes per district: verified costs outside the candidate rows are kept. Rejected swaps: their costs are
            // priced again when they come due, on pass 8.
            foreach (var (name, f) in new[] { ("1 district", Colony(18, 35, 85, 30)), ("2 districts", Colony(18, 70, 170, 30, districts: 2)), ("rejected swaps", FarSwap(2)) })
            {
                var e = new PassEngine(f); var stages = new HashSet<int>(); int learned = 0, rechecked = 0;
                for (int pass = 1; pass <= DuePass; pass++)
                {
                    e.RequestPass();
                    for (int t = 0; t < 2000 && (e.Busy || e.State.Requested); t++)
                    {
                        e.Tick(); stages.Add(e.State.Stage); learned = Math.Max(learned, e.State.LearnedCost.Length); string json = JsonConvert.SerializeObject(e.State);
                        if (e.State.Snap != null) rechecked = Math.Max(rechecked, e.State.Snap.RecheckCosts.Length);
                        Check(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PassState>(json)) == json, $"{name}: round trip changed the state");
                    }
                }
                Check(stages.SetEquals(new[] { 0, 1, 2, 3 }), $"{name}: did not exercise every stage: " + string.Join(",", stages));
                Check(learned > 0, $"{name}: no verified cost was kept");
                Check(name != "rejected swaps" || rechecked == 4, $"{name}: {rechecked} remembered costs priced again, expected 4");
            }
        });
        Test("an unknown saved version or stage is discarded, not trusted; a version 3 or 4 save keeps its remembered costs", () => {
            // Version 1 (1.0.1) ranked homes from every district, version 2 kept no verified costs, version 3 solved the
            // whole colony as one assignment and version 4 had fixed budgets per tick: a pass any of them saved starts over
            // rather than resuming, and so does a running pass saved without its budgets. Versions 3 and 4 kept their
            // remembered costs as this version does, so they carry over, unless the lists disagree in length. A running
            // version 4 pass that carries budgets (version 4 saved none) still starts over: its version differs.
            PassState Saved(int version, int ages = 1) => new PassState { Version = version, Stage = 2, Requested = false, Passes = 5,
                LearnedWork = new[] { G(500) }, LearnedHome = new[] { G(100) }, LearnedCost = new[] { 640 }, LearnedAge = new int[ages] };
            PassState Budgeted(PassState state) { state.QueryBudget = PassEngine.QueriesPerTick; state.SolveBudget = PassEngine.SolveOpsPerTick; return state; }
            foreach (var (name, saved, keeps) in new[] { ("Version 1", Saved(1), false), ("Version 2", Saved(2), false), ("Version 3", Saved(3), true),
                ("Version 3 with mismatched remembered costs", Saved(3, 2), false), ("Version 4", Saved(4), true), ("Version 4 with budgets", Budgeted(Saved(4)), true),
                ("A running pass without its budgets", Saved(PassState.CurrentVersion), true), ("Version 99", Saved(99), false) })
            {
                var e = new PassEngine(new Fake(), saved);
                Check(e.State.Stage == 0 && e.State.Version == PassState.CurrentVersion && e.State.Requested, $"{name} state kept");
                Check(e.State.Passes == 5, $"{name}: the pass count was lost");
                var s = e.State; bool kept = s.LearnedCost.Length == 1 && s.LearnedWork[0] == G(500) && s.LearnedHome[0] == G(100) && s.LearnedCost[0] == 640 && s.LearnedAge[0] == 0;
                bool none = s.LearnedWork.Length + s.LearnedHome.Length + s.LearnedCost.Length + s.LearnedAge.Length == 0;
                Check(keeps ? kept : none, $"{name}: remembered costs {(keeps ? "lost" : "kept")}");
            }
            // An idle version 3 or 4 save after a pass whose route check turned down a swap: the next pass does not propose it.
            foreach (int version in new[] { 3, 4 })
            {
                var f = FarSwap(); var first = Run(f); var idle = Reload(first.State); idle.Version = version; var next = Run(f, idle);
                Check(first.LastReport.Rejected == 1 && next.LastReport.Rejected == 0, $"A version {version} save: {first.LastReport.Rejected}, then {next.LastReport.Rejected} cycles rejected");
            }
        });
        Test("a save from 1.0.1 restarts the pass it was running, and keeps its counters and schedule", () => {
            // Written by the 1.0.1 engine (a87ba72) for this colony: after one pass, mid-way through the next (stage 2),
            // and idle.
            const string running = """{"Version":1,"Requested":false,"Stage":2,"Snap":{"Adults":["0000000a-0000-0000-0000-000000000000","0000000b-0000-0000-0000-000000000000","0000000c-0000-0000-0000-000000000000"],"AdultDistrict":["00002328-0000-0000-0000-000000000000","00002328-0000-0000-0000-000000000000","00002328-0000-0000-0000-000000000000"],"AdultHome":[0,2,1],"AdultWork":[0,1,-1],"Homes":["00000001-0000-0000-0000-000000000000","00000002-0000-0000-0000-000000000000","00000003-0000-0000-0000-000000000000"],"HomeDistrict":["00002328-0000-0000-0000-000000000000","00002328-0000-0000-0000-000000000000","00002328-0000-0000-0000-000000000000"],"HomePos":[0,0,0,10,0,0,20,0,0],"Works":["000001f4-0000-0000-0000-000000000000","000001f5-0000-0000-0000-000000000000"],"WorkPos":[0,0,0,20,0,0],"NearK":3,"Near":[0,1,2,2,1,0],"Costs":[80,240,400,80,240,400]},"Cursor":6,"Row":1,"U":[0,0,0,0],"V":[0,0,0,0],"P":[0,0,0,0],"VerifyCurrent":null,"VerifyTarget":null,"Queries":6,"Ticks":1,"Passes":1,"MovedAdults":2,"AppliedCycles":1,"RejectedCycles":0,"StaleCycles":0}""";
            const string idle = """{"Version":1,"Requested":false,"Stage":0,"Snap":null,"Cursor":0,"Row":1,"U":null,"V":null,"P":null,"VerifyCurrent":null,"VerifyTarget":null,"Queries":10,"Ticks":3,"Passes":1,"MovedAdults":2,"AppliedCycles":1,"RejectedCycles":0,"StaleCycles":0}""";
            Fake World()
            {
                var f = new Fake(); f.Works[G(500)] = (0, 0, 0); f.Works[G(501)] = (20, 0, 0);
                for (int h = 1; h <= 3; h++) f.Homes[G(h)] = new Home { Capacity = 1, X = (h - 1) * 10 };
                f.People[G(10)] = new Person { Id = G(10), Home = G(1), Work = G(500), District = G(9000) };
                f.People[G(11)] = new Person { Id = G(11), Home = G(3), Work = G(501), District = G(9000) };
                f.People[G(12)] = new Person { Id = G(12), Home = G(2), District = G(9000) };
                return f;
            }
            foreach (var (name, json, restarts) in new[] { ("running", running, true), ("idle", idle, false) })
            {
                var e = new PassEngine(World(), JsonConvert.DeserializeObject<PassState>(json)); Exception fault = null; e.Faulted += x => fault = x;
                Check(e.State.Version == PassState.CurrentVersion && e.State.Stage == 0 && e.State.Snap == null, $"{name}: the 1.0.1 state was kept");
                Check(e.State.Passes == 1 && e.State.MovedAdults == 2 && e.State.AppliedCycles == 1, $"{name}: the lifetime counters were lost");
                Check(e.State.Requested == restarts, $"{name}: pass requested {e.State.Requested}, expected {restarts}");
                for (int t = 0; t < 100 && (e.Busy || e.State.Requested); t++) e.Tick();
                Check(fault == null && !e.Busy && e.State.Passes == (restarts ? 2 : 1), $"{name}: {fault?.Message ?? $"{e.State.Passes} passes"}");
            }
        });

        if (args.Length == 2) Test("compiled adapter follows installed component API contract", () => AdapterApiChecks.Verify(args[0], args[1]));
        Console.WriteLine($"{passed} checks passed. Native Unity execution and two-player playtest are not exercised.");
    }

    // Records every (home, work) pair the engine priced or verified.
    sealed class Probe : IPassWorld
    {
        readonly Fake _f; readonly HashSet<(Guid, Guid)> _seen; public bool Recording;
        public Probe(Fake f, HashSet<(Guid, Guid)> seen) { _f = f; _seen = seen; }
        public Snapshot Capture() => _f.Capture();
        public bool TryRoute(Guid home, Guid work, out float cost) { if (Recording) _seen.Add((home, work)); return _f.TryRoute(home, work, out cost); }
        public bool ApplyCycle(Move[] cycle) => _f.ApplyCycle(cycle);
    }
}
