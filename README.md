# Optimized Local Housing

A Timberborn 1.1 mod that moves adult beavers into the homes that give the colony the **shortest total commute**.
Beds are limited, so not everyone can live next to work. Once a day, the mod works out the arrangement where the
whole colony walks the least, and moves the beavers that need moving.

Version **1.1.1**. <!-- latest -->

**Website:** https://timbermods.github.io/OptimizedLocalHousing/ (install guide, troubleshooting, FAQ)

## Install

You need Timberborn 1.1 (built against 1.1.2.4) and no other mods. Back up your save first: the mod's moves can
only be undone by loading an earlier save.

1. Download `OptimizedLocalHousing-vX.Y.Z.zip` under **Assets** on the
   [latest release](https://github.com/timbermods/OptimizedLocalHousing/releases/latest) (not "Source code").
2. Close Timberborn. Extract the zip into `Documents\Timberborn\Mods`. You should get one
   `OptimizedLocalHousing` folder.
3. Start Timberborn, enable **Optimized Local Housing** in the Mods menu, and restart.
4. Load your save and let the game run. On a save the mod hasn't seen, the first pass starts as soon as the game
   runs. After that, a pass runs at the start of every in-game day.

**Don't run it with Housing Optimize or Commute Balancer.** If either is enabled, this mod turns itself off and
logs a warning.

**Multiplayer:** every player installs the same mod version and runs the same game version. Update together.

**To check it works,** quit the game and open `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`.
Search for `[OptimizedLocalHousing]`: there is one line at startup and one after each pass.
[What the pass line means](https://timbermods.github.io/OptimizedLocalHousing/troubleshooting.html#zero).

## What it does

- **The whole colony, not each beaver.** One beaver may walk a little farther so everyone together walks less.
  Each district is solved exactly, with the game's own route costs, so stairs, platforms and ziplines count.
- **Every home keeps the same number of adults.** Homes never overfill and breeding is untouched. Empty beds are
  left for the game to fill.
- **Children never move.** Neither do beavers in paused, blocked or automation-off homes, and nobody moves into one.
- **Nobody crosses districts, and nobody is made homeless.**
- **Commutes stay possible.** A beaver who can reach work is never moved to a home that can't. One who can't is
  moved to a home that can, when a bed can be arranged.
- **Unemployed adults** give up good beds to beavers who commute.
- **No churn.** A move must save at least one route-cost unit for each beaver moved. Once a colony is arranged,
  another pass changes nothing.
- **Nothing to set.** No settings, no menu. The work is spread over many ticks, so there is no long freeze.

[How a pass works](https://timbermods.github.io/OptimizedLocalHousing/#how), step by step.

## Results

A real 266-beaver colony (222 employed adults, 89 homes), replayed through the pass engine with straight-line
distance standing in for route cost:

| | Average commute |
|---|---|
| Before | 53.1 |
| After one pass | **24.1** |
| True optimum | 24.0 |

That pass took 147 ticks and rehomed 182 beavers in 20 cycles. A second pass changed nothing.

## Status

**Stable.** That means the evidence below plus automated tests, not that every situation has been tried.

- **Played:** the 1.0 code ran 16 passes in a row on the maintainer's colony (about 350 adults, 104 homes, 167
  workplaces), with no errors, warnings or rollbacks. That included one hosted co-op session with a second player:
  about 11,000 ticks, 15 passes and no desync in the host's log.
- **Tested, not played:** the 1.1 code. 41 automated checks cover it; 40 run on every change.
- **Not tried yet:** a live two-player session on 1.1, the second player's side of a co-op session, frame-time
  impact, the Iron Teeth faction, systems other than Windows, and Timber Together in a live session (its code was
  checked against this mod's, not played with it).

## Limits

- The first pass on a badly housed colony moves most adults at once, so parents can end up living away from
  their children. Later passes move only a few.
- If a beaver changes home or job during a pass, its move cycle is skipped and retried the next day.
- Each workplace checks real routes to its 32 nearest homes. A far home made cheap by a zipline could be missed.

## Uninstall

Disable the mod and restart. Beavers keep their current homes.

## Developing

Build steps, how a pass works inside, costs, saved data and the test list are in
[DEVELOPING.md](https://github.com/timbermods/OptimizedLocalHousing/blob/main/DEVELOPING.md).

## Changelog

- **Unreleased**: adults can move into empty beds. A beaver whose workplace has an empty home beside it moves there
  when that shortens the colony's total walk, so a home's number of adults can now change. A move is turned down if
  it would leave fewer homes that can breed. Adults only take adult beds, so child beds stay free. Children still never move, and nobody moves into a paused, blocked or automation-off home. A pass that was running when an older save
  was made starts over once after the update. All co-op players must update together.
- **1.1.1**: maintenance release. The conflict check now covers only Housing Optimize and Commute Balancer.
  How passes work, and what is saved, are unchanged from 1.1.0. As always, every co-op player installs the same
  version.
- **1.1.0**: better homes near district borders, fewer repeated rejections, and shorter passes on large colonies.
  All co-op players must update together.
  - A workplace now prices only the homes in its own district. The game's route search never leaves a district,
    so those queries always failed (about 10-20% of all queries in two-district colonies).
  - Route costs found while re-checking moves to far homes are remembered in the save, so a move that was turned
    down is not proposed and rejected again every day. After seven passes a remembered cost is checked again if a
    worker still lives in that home or there was no route, so a road that comes back is noticed.
  - Each district is solved on its own: the same result in a third to a half of the solve ticks, and far less
    memory, in colonies with several districts.
  - A large colony's pass takes larger per-tick budgets, set from the colony when the pass starts, so it ends
    within the day's daytime (480 ticks instead of 1,118 for 1,600 adults in one district). Colonies of a few
    hundred adults keep the same budgets as before.
  - The pass log's "disconnected commutes repaired" no longer counts a beaver that was moved but still can't reach
    its workplace.
  - A pass that was running when an older save was made starts over once after the update. Idle saves keep their
    schedule, and the log's pass count carries on.
  - Development: the tests build without the game and run on GitHub Actions.
- **1.0.1**: a peer that loads a save taken mid-pass now keeps step, tick for tick, with a peer that never reloaded
  (the rebuild after a load was charged to that tick's solver budget, so the pass could end a tick later on one
  computer). A test covers it. No change in what the mod decides.
- **1.0.0**: first stable release.

## License

MIT, maintained by [Timbermods](https://github.com/timbermods). An unofficial community mod for Timberborn, not
affiliated with or endorsed by Mechanistry. Housing Optimize and Commute Balancer are Bobingabout's.
