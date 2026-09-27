# Developing Optimized Local Housing

For people who build, test or change the mod. Players want the [README](README.md) and the
[website](https://timbermods.github.io/OptimizedLocalHousing/). The website's own rules are in `CLAUDE.md`.

## Build and test

Requires .NET SDK 8. Building the mod also needs a local Timberborn installation; no game DLLs are redistributed.
The tests compile only the game-independent engine and restore Newtonsoft.Json from nuget.org (13.0.4, the same
release the game ships), so they run without the game. CI (`.github/workflows/tests.yml`) runs them that way on
every push to main and every pull request, without the two arguments below: the compiled-adapter check needs the
built mod and the game, so it runs only locally.

```powershell
dotnet build OptimizedLocalHousing/OptimizedLocalHousing.csproj -c Release -p:GameManaged="C:\path\Timberborn_Data\Managed"
dotnet run --project OptimizedLocalHousing.Tests -c Release -- OptimizedLocalHousing/bin/Release/netstandard2.1/OptimizedLocalHousing.dll "C:\path\Timberborn_Data\Managed"
./package.ps1
```

The tests cover the solver against brute force, pause/resume at every row, splitting moves into cycles and chains,
optimality on random colonies with one to three districts, with and without empty beds, homes that can breed never
fewer after a pass, route queries that stay inside a district, each district solved on
its own (the same homes as a colony of that district alone, in no more ticks), the safety rules in the README,
stale-world handling, route costs carried from one pass to the next and priced again when they come due,
determinism between peers, save/reload at every tick of three passes (the first, the next, and the one where the
first pass's remembered costs come due; with one district and with several), per-tick work bounds (route
queries, and solver operations with one budget across districts), large colonies whose passes end within the
daytime with budgets set from the colony (in lockstep across a reload mid-pass), and the compiled adapter against
the installed game's component blacklist. Omitting the two arguments skips the compiled-adapter check.

## How a pass works

`GameAdapter.cs` is the thin bridge to the game; `PassEngine.cs` and `Assignment.cs` hold everything else. Each pass:

1. **Capture.** Every housed adult beaver, its home, and its assigned workplace, and every usable home with an
   empty bed.
2. **Price.** For every workplace, real route costs (`Accessible.FindRoadPath`, so ziplines and stairs count)
   from the 32 nearest homes in its district. Homes in other districts are never queried, because the game's
   route search never leaves a district. Homes farther away are estimated, and any move to one is re-checked
   with a real route before it is allowed.
3. **Solve.** The optimal way to reassign the adults to the district's beds: the ones adults occupy today and the
   empty ones (the Hungarian algorithm, with more beds than adults). Children keep their beds. As in the game, a
   third of a home's beds (rounded down) are for children and the rest for adults, and the game moves an adult out of
   a home with more adults than adult beds. So the empty beds an adult may take are the free beds, but no more than
   the adult beds left (`HomeRules.FreeForAdults`). Nobody moves between districts, so each district is solved on its own, one after another. In a
   colony with several districts that takes far fewer ticks and far less memory than solving them as one (47 solve
   ticks instead of 157 for four districts of 300 adults), and reaches the same optimum.
4. **Verify.** Every proposed move is re-priced with fresh routes. A whole cycle of moves is dropped if it would
   leave a beaver who can reach work today unable to, and each cycle or chain must save at least half a route-cost
   unit in total. A chain is also dropped if it would leave the colony fewer homes that can breed. The rule is the
   Folktails' (`ProcreationHouse`, `HomeRules.CanBreed`): at least two adults, a free bed, and fewer children than
   the home's child beds and than half its adults, rounded down. The game then rolls an 18.75% chance each time an
   adult comes home. The solver doesn't know this rule, so a chain it drops is proposed again the next day
   and dropped again, until the colony changes. The fresh costs of homes beyond the 32 nearest are remembered in place of estimates, so a move that was
   turned down is not proposed again every day (anything proposed is still re-priced first). A remembered cost is
   used for seven passes. The seventh prices it again, and keeps it, if one of that workplace's workers lives in
   the home or if there was no route (so a road that comes back is noticed); otherwise it lapses to the estimate.
5. **Apply.** Moves are applied as whole cycles (A takes B's bed, B takes C's, C takes A's) or chains (A takes an
   empty bed, B takes A's, C takes B's), in one game tick. A cycle keeps every home's head count; a chain frees a bed
   where it starts and fills one where it ends. If the game has filled that empty bed since the snapshot, the chain
   is skipped as stale.

Each beaver gets a one-route-cost-unit bonus for staying put (`StayBonus`), so a rearrangement must save at least one
unit for every beaver it moves.

## Cost

- Work is spread over ticks with budgets per tick: **32 route queries** and about 250,000 solver operations on a
  colony of a few hundred adults. A pass takes roughly a fifth of a game day (147 ticks on a 266-beaver colony),
  never one long stall.
- A larger colony's pass takes larger budgets, set from the colony when the pass starts and saved with it, so
  that it still ends within the day's 512 daytime ticks: at most **128 route queries** and about 1,000,000 solver
  operations per tick (plus the solver row it started last). On test colonies of 1,600 adults in one district a
  pass takes 480 ticks on randomly housed adults (1,118 with the smallest budgets), and about 216 (740) once they
  are settled. The route-query budget stops growing at about 770 staffed workplaces, and the solver budget at about
  1,150 adults in one district (about 920 in each of two, or 800 in each of three), so a still larger colony's
  pass takes longer instead (609 ticks on 2,000 randomly housed adults in one district).
- The replay of the real 266-beaver colony took 147 ticks, 4,388 route queries and about 22 ms of total CPU.
- Not measured: frame-time impact. The per-tick work is bounded, but the cost of a real path query has not been
  timed, nor the larger solver budget that large districts get: more than about 730 adults in one district, about
  580 in each of two, or about 500 in each of three.

## Multiplayer and saved state

- All decisions use integer arithmetic and sorted IDs, and the whole pass state (snapshot, prices, solver rows,
  verification results, and the route costs kept from earlier passes) is saved with the game. Reloading mid-pass,
  or a peer that loads a save taken mid-pass while another peer keeps running, continues exactly where the pass
  was and does the same work on every following tick, so every peer applies the same moves on the same tick.
  There is no wall-clock or frame-time rule anywhere.
- For Timber Together: the mod only ticks inside the simulation, draws no random numbers, never moves a beaver
  between districts (so colonies stay separate), and nothing it touches is patched by BeaverBuddies.
- The saved state is a few hundred bytes when idle on a settled colony, and a few KB (about 20 KB at 1,300
  adults) where beavers keep changing jobs. For seven days after a pass that moved many beavers it also holds the
  route costs that pass checked, capped at 1,024 (about 11 KB for 240 adults, about 85 KB at the cap). During a
  pass it grows with the colony: about 70 KB for 240 adults, 100 KB for 350 to 400, 380 KB for 1,300.

## Tuning constants

There are no settings. The tuning constants are in the source: `NearHomes`, `QueriesPerTick` and the other
per-tick budgets in `PassEngine.cs`, and `StayBonus` in `Assignment.cs`.
