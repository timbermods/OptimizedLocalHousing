# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Timberborn players whose beavers walk across the map to work, past homes right beside their job, and who want the
colony housed sensibly without micromanaging it. Mostly single player; some play co-op through BeaverBuddies. Mostly
non-technical, though this mod also draws players who like to see the reasoning (an optimal assignment, real route
costs, measured results). They arrive from the timbermods catalog, GitHub or a forum post, often already knowing
Bobingabout's Housing Optimize or Commute Balancer, and want to know in one look what this does differently, whether
it will upset breeding or families, and how to install it. Returning players come back to update, to read a `Pass`
line from their log, or to find out why a beaver didn't move.

## Product Purpose

The website for **Optimized Local Housing** (https://github.com/timbermods/OptimizedLocalHousing), a Timberborn mod
that, once a day, moves adult beavers into the homes that give the colony the **shortest total commute** between home
and assigned workplace. Beds are limited, so not everyone can live next to work; the mod solves that exactly (an
optimal assignment, the Hungarian algorithm, one district at a time) instead of nudging beavers one swap at a time.
Adults can move into empty beds, but only adult beds, and never in a way that leaves fewer homes able to have a kit.
Children never move.

Success, in order:
1. **Understand it:** the visitor grasps that it rearranges the adults already housed, into empty beds too (it doesn't
   build anything), that it minimizes the colony's total, not each beaver's walk, and that breeding and children are
   safe. They download the right file.
2. **Install it right:** the `OptimizedLocalHousing-vX.Y.Z.zip` under Assets (not "Source code"), game closed,
   extracted into `Documents\Timberborn\Mods` as one `OptimizedLocalHousing` folder (not doubled), enabled in the Mods
   menu, restart. Not alongside Housing Optimize or Commute Balancer.
3. **Use it:** there is nothing to set; they let the game run and confirm it worked from the `[OptimizedLocalHousing]`
   lines in `Player.log`, and can read a `Pass` line (move cycles applied, rejected, stale, route cost saved).
4. **Report problems:** a GitHub issue with the `[OptimizedLocalHousing]` log lines, versions, other mods, colony
   size, solo or multiplayer.

## Positioning

- **Versus the base game:** by the look of the game's code, the built-in assigner hands out beds without asking where
  a beaver works. This mod rearranges the adults already housed so the colony's total home-to-work route cost is as
  small as the rules allow, moving adults into empty adult beds too. It works alongside the game's assigner, which
  still houses newcomers and kits.
- **Versus Bobingabout's Housing Optimize and Commute Balancer** (as their Workshop pages describe them): Housing
  Optimize unassigns everyone once a day and refills first-come-first-served, nearest first; Commute Balancer works
  gradually over days. This mod solves each district exactly, once a day, never leaves fewer homes able to breed, and
  spreads the work over ticks. Say it truthfully: in one replay a first-come-first-served model averaged 25.6 against 24.1 for
  this mod, but that was a model with a straight-line stand-in, not a benchmark of either mod. Those mods have
  thousands of subscribers and far more real-world mileage; this one is newer. Never claim "the best housing mod".
  They must not run together: this mod turns itself off if either is enabled.
- Standalone: no Harmony, no Mod Settings, no other mods required. Distributed from GitHub, not the Steam Workshop.

## Operating Context

- **Current release: v1.2.0** (published 2026-09-27), marked Latest on GitHub. Adults can move into empty adult beds;
  a move that would leave fewer homes able to breed is turned down; children never move. Release assets:
  `OptimizedLocalHousing-v1.2.0.zip` and `OptimizedLocalHousing-v1.2.0-SHA256SUMS.txt` (ZIP SHA-256
  `1bae3d47dad8801712d4b73656e312036ec84f5527f72203a5907b8b33a21a3b`). Earlier: v1.1.1, v1.1.0, v1.0.1, v1.0.0, and
  the v0.1.0 preview (pre-release).
- **Game:** Timberborn 1.1, built against **1.1.2.4**, manifest minimum **1.1.0.0**. A future game update could break
  it. Checked on Windows only.
- **Requirements:** none. Mod name in the Mods menu: **Optimized Local Housing**; mod ID `Kyler.OptimizedLocalHousing`.
  Installed layout: `Mods\OptimizedLocalHousing\{LICENSE, README.md, version-1.1\{manifest.json,
  Scripts\OptimizedLocalHousing.dll}}`.
- **Conflicts:** disables itself for that game, with a log warning, if `BobHousingOptimize`, `BobCommuteBalancer` or
  `housingoptimize` (a mod.io listing of Housing Optimize) is enabled.
- **Co-op:** designed for lockstep (integer math, sorted IDs, the whole pass state saved, no wall-clock or frame
  time). Every player installs the same mod version and runs the same game version, and updates together;
  BeaverBuddies warns at join time when mod versions differ.
- **In-game settings players meet:** none. No menu, no UI, no options. The tuning constants (`NearHomes`,
  `QueriesPerTick` in `PassEngine.cs`; `StayBonus` in `Assignment.cs`) are in the source for people who rebuild.
- **What players see:** beavers change homes; one log line at startup (`[OptimizedLocalHousing] 1.2.0 loaded.`) and
  one per pass. A pass starts as soon as the game ticks on a save the mod hasn't seen, then at the start of every
  in-game day; it takes roughly 150 to 200 ticks on a colony of a few hundred beavers (about 90 seconds to two minutes
  at normal speed). Other log messages: `Disabled because another housing assignment mod is enabled`, `Saved state
  ignored`, `Pass abandoned until the next day`, `A move cycle failed and was rolled back`.
- **Upgrading and removal (the facts players need):** close the game, delete the old `Mods\OptimizedLocalHousing`
  folder, extract the new one; every co-op player updates together. Uninstalling is safe: beavers keep their homes and
  leftover data is ignored. Undoing the moves already made means loading a save from before installing, so back up
  first.
- **Reporting:** GitHub issues (https://github.com/timbermods/OptimizedLocalHousing/issues) with the
  `[OptimizedLocalHousing]` lines from `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log` (or
  `Player-prev.log`), the mod and game versions, OS, other enabled mods, rough colony size, solo or multiplayer, and a
  save if it's safe to share. For a desync: every player's `Player.log` and when it happened relative to a `Pass` line.

## Capabilities and Constraints

- **Stack and hosting:** plain static HTML, CSS and small vanilla JS in `docs/` on `main`, no build step. Pages:
  `index.html` (hero with the seating plan, why, features, how a pass works, what it always / never does, the
  comparison, results, tested vs not), `install.html`, `troubleshooting.html`, `faq.html`, `404.html`, plus
  `style.css`, `site.js` (theme toggle stored as `olh-theme`, the results-chart tooltip, and opening the `<details>`
  entry a `#hash` points at), `seating.js` (the hero demo), `release.js`, `favicon.svg`, `fonts/` (Libre Caslon Text,
  OFL), `textures/` and `.nojekyll`. GitHub Pages serves **`main:/docs`** (legacy build) at
  https://timbermods.github.io/OptimizedLocalHousing/, so a change is live once it is merged to `main`. One of the
  timbermods sites; https://timbermods.github.io/ is the catalog the footer links to.
- **Site tests and CI:** none. Nothing in `OptimizedLocalHousing.Tests/` or `.github/workflows/tests.yml` checks
  `docs/`; the workflow only runs the game-free C# checks (`dotnet run --project OptimizedLocalHousing.Tests -c
  Release`, SDK 8, on pushes to main and every pull request). The contracts below are therefore unenforced; keep them
  by hand:
  - Every page except `404.html` loads `site.js` and then `release.js` with `data-repo="timbermods/OptimizedLocalHousing"`
    and `data-asset="^OptimizedLocalHousing-v[\d.]+\.zip$"`.
  - Hand-written fallbacks must stay working: the download links (`data-release-href="download"`) point at
    `/releases/latest`, the footer's Latest release link carries `data-release-href="notes"`, and every
    `data-release` span (`version`, `tag`, `asset-name`, `sha256`) holds the current value written by hand (1.2.0, the
    ZIP name, the hash above). Update them at each release, since `release.js` only replaces them when the lookup
    works.
  - `data-release-pinned="1.2.0"` marks text written for one version (the home status notice and section, FAQ
    settings and stability answers); bump it when the text is re-checked for a new release, or `release.js` appends a
    "written for" note.
  - `404.html` loads its stylesheet, favicon and links by absolute `/OptimizedLocalHousing/` paths.
  - The theme bootstrap snippet in each `<head>` reads `olh-theme` before the stylesheet, to avoid a flash.
  - The results chart has direct labels and a table view, and works without `site.js`.
- **Shared file, never edited:** `docs/release.js` is a byte-for-byte copy of the script shared across timbermods sites
  (identical to MixedStorage's `site/assets/release.js`). Replace it with the shared version; never edit it here.
- **Terminology** (as in the README and log): pass; move cycle (A takes B's bed, B takes C's, C takes A's); route cost
  and route-cost unit (the game's own, so stairs, platforms and ziplines count; not straight-line distance); commute
  (home to assigned workplace); adults, children, homes, workplaces, districts; the 32 nearest homes; rejected, stale,
  disconnected commutes repaired, route cost saved; Hungarian algorithm; Housing Optimize and Commute Balancer
  (Bobingabout's). Don't mention Incremental Housing (the maintainer removed it from the docs on purpose).
- **Honest status:**
  - Played in game: the **1.0 code** (1.0.0, identical to the v0.1.0 preview) ran 16 consecutive passes on the
    maintainer's colony (about 350 adults, 104 homes, 167 workplaces) with no errors, warnings or rollbacks, including
    one hosted co-op session with a second player (about 11,000 ticks, 15 passes, no desync in the host's log).
  - **Not played in game:** 1.1.0's changes (same-district pricing, remembered route costs, per-district solve,
    larger budgets for large colonies), 1.1.1, and 1.2.0's moves into empty beds. They are covered by automated tests
    only. No live two-player session has been played on 1.1.x or 1.2.0; the client's side, joining and rehosting are
    untested.
  - Never measured: frame-time impact (the per-tick work is bounded, but a real path query hasn't been timed, nor the
    larger budgets of very large colonies). Iron Teeth untested (developed on Folktails). Only Windows. BeaverBuddies
    Timber Together was audited against the mod's code, not played with it.
  - The results are a replay of a real 266-beaver colony through the real pass engine with **straight-line distance
    standing in for route cost** (53.1 before, 24.1 after one pass, 24.0 true optimum; 147 ticks; 182 beavers rehomed
    in 20 cycles; a second pass changed nothing). Always say it's a stand-in.
  - Present it as "stable means this evidence plus the automated tests, not that every situation has been tried",
    without scaring people off.
- **How the shipped site states these facts (keep it this way):**
  - The home page's Tested list says "46 automated checks: 45 run on every change, and one runs against the installed
    game" (CI runs 45; the compiled-adapter check runs only locally with the game).
  - The replay results were made with every home keeping its number of adults (before moves into empty beds), and
    the site says so.
  - The home status, `install.html` (Multiplayer), the FAQ multiplayer and "Is it stable?" answers and the
    troubleshooting multiplayer entry say the live session ran the code the stable release started from, and that
    this release hasn't been played live by two players. None of them walks through version history.
  - The Updating steps and the "Saved state ignored" answer carry no old-save notes (fresh games are assumed).
  - Neither the README nor the site names a Timber Together version: its code was audited against this mod, not played
    with it.

## Brand Commitments

- **Voice:** a fellow player explaining a useful mod: clear, exact, a little proud of the math, never hype. "Straight
  answers, including the ones that aren't flattering." Numbers are given with their caveats. Short and plain: one
  idea per sentence, each thing said once, no internals and no history (see CLAUDE.md, *Writing README and website
  text*).
- **No official Timberborn logos or key art.** The game's own item icons are allowed where used (none are used today).
  The site's marks are its own: `favicon.svg` (a house linked to a workplace) and simple line icons.
- **License:** MIT, copyright Timbermods, for the code, docs and site.
- **Unofficial community mod**, not affiliated with or endorsed by Mechanistry. Maintained by Timbermods. Every page's
  footer says so.
- Credit Bobingabout's mods fairly and link their Workshop pages when comparing.

## Evidence on Hand

- **Images:** `docs/favicon.svg` and the procedural board and card textures in `docs/textures/` (made by
  `make_textures.py`). There is no mod icon PNG, no Open Graph image, no `Media/` folder.
- **Diagrams drawn for the site:** the hero's seating plan (a toy hall of three homes, three workplaces and eight
  made-up beavers; `seating.js` works out the best arrangement on the page, 185 → 144; captioned as an illustration
  with straight-line distances) and the move-cycle figure, which redraws that same pass. They are illustrations, not
  screenshots.
- **Real data:** the replay results above (the bar chart and its table); the live log figures (16 passes, about 183
  ticks and 5,350 route queries each, 0 to 4 cycles); two real `Pass` lines from the maintainer's log (Pass 26 and
  Pass 34; Pass 26 is quoted on the home and install pages, Pass 34 on the troubleshooting page); a measured "let adult counts change" experiment with no
  breeding rule (average 24.0 to 23.1, about 4%, while homes able to have a baby fell from 11 to 1; one colony,
  straight-line stand-in; no longer quoted on the site); the tick and memory figures in the README (47 vs 157 solve ticks; 480 vs 1,118 ticks for 1,600 adults).
- **Does not exist, and must not be faked:** in-game screenshots or clips of the mod at work (there is no UI to
  capture; a before/after of a real colony would need the maintainer's own shots, so leave a marked slot), download
  counts, player numbers, testimonials, reviews, press, benchmarks against the other housing mods, frame-time numbers,
  Iron Teeth or non-Windows results, and any 1.1.x or 1.2.x live-play results.

## Product Principles

1. **Show the mechanism, then the proof.** One total, limited beds, an exact solve: the diagrams explain it, the
   replay numbers back it, and every number carries its caveat.
2. **Breeding and families first.** No move leaves fewer homes able to have a kit, adults never take a kit's bed, and
   children never move; say it early, because
   it's the first worry of anyone who has used a housing mod.
3. **Nothing to set, so the log is the interface.** Install right, let the game run, read the `Pass` line; the
   troubleshooting page is built around those messages.
4. **Fair to the neighbours.** Compare with Housing Optimize and Commute Balancer by what they say they do, admit
   their mileage, never claim to be the best.
5. **Honest about what's been played.** The 1.0 code ran live, 1.1.x and 1.2.x are tested but not played, and frame time is
   unmeasured; said plainly, as the mod is now, with the version history left to the changelog.
