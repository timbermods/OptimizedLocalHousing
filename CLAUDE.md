# CLAUDE.md

Optimized Local Housing: a standalone Timberborn 1.1 mod (C#, no Harmony, no Mod Settings) that re-houses adult
beavers once a day for the shortest total home-to-work route cost. Mod source in `OptimizedLocalHousing/`, game-free
tests in `OptimizedLocalHousing.Tests/`, packaging in `package.ps1`, the website in `docs/`. Changes land on main by
PR → merge.

- **Tests (what CI runs, `.github/workflows/tests.yml`, SDK 8):** `dotnet run --project OptimizedLocalHousing.Tests -c Release`
  must end with `45 checks passed.` (46 exist; the compiled-adapter check needs the built mod and the game, so it runs
  only locally with two extra arguments; see `DEVELOPING.md`).
- **Build and package** (needs a local Timberborn install; never done in CI): see `DEVELOPING.md`, then `./package.ps1`.

## Standing rules

- Never launch or drive Timberborn, and never touch installed mods or saves. The maintainer (Kyler) playtests himself.
- Commit on a branch and open a PR. Kyler has said to merge PRs automatically: merge, then check the page live.
- Assume fresh games: no old-save compatibility notes on the site. Don't mention Incremental Housing (removed on purpose).
- Compare fairly with Bobingabout's Housing Optimize and Commute Balancer, by what their Workshop pages say. Never
  "the best housing mod". The 25.6 vs 24.1 figure is a model with straight-line distances, not a benchmark.

## Writing README and website text

Kyler, 2026-09-24: "simplicity and elegance is effective and desirable." Every change to the README, the website
text and the player docs follows these rules.

- **Write for a Timberborn player** who wants to download, install and use the mod. Developer detail goes in
  `DEVELOPING.md` or the changelog; link to it rather than repeating it.
- **Short.** One idea per sentence, most under about 20 words. A paragraph or FAQ answer is one to three sentences,
  a troubleshooting answer a few numbered steps.
- **Lead with the action.** Menu paths as arrow chains; on-screen labels in bold, exactly as in game.
- **Say each thing once**, where a player would look for it; link to it elsewhere.
- **Plain words.** No internals (class names, ids, formats) unless the player needs them to act.
- **Cut** filler, repeated caveats, edge cases a player won't meet, and history ("since …", "no longer", older
  builds). Describe the mod as it is now.
- **Check every fact against the code** before writing it; changelogs lag.
- **Keep, briefly:** credits, the unofficial line, the status, and safety facts.
- **Reread as a new player before publishing.** Every step works as written, and nothing is said twice.

## Website

- **Where:** `docs/`: `index.html`, `install.html`, `troubleshooting.html`, `faq.html`, `404.html`, `style.css`,
  `site.js` (theme toggle, chart tooltip, opens the `<details>` a `#hash` names), `seating.js` (the hero demo),
  `release.js`, `favicon.svg`, `.nojekyll`, `fonts/`, `textures/`. Live at https://timbermods.github.io/OptimizedLocalHousing/.
- **Published:** GitHub Pages serves `main:/docs` (legacy build), so merging to main publishes; a build takes about a minute.
- **Latest releases update themselves:** when a release becomes GitHub's Latest, `.github/workflows/latest-release.yml`
  (the shared timbermods workflow) appends the standard footer to its notes, sets the site's
  `data-release="version|tag|asset-name"` fallback text and the README lines ending in `<!-- latest -->` to the new
  version, runs the site checks and commits to main. Pre-releases change nothing. Descriptions, status lists and FAQs
  stay manual (the checklist below). Dry run: Actions → Latest release → Run workflow.
- **Look:** "The Seating Chart". A host's planner's board: tented place cards and card stock pinned to linen, the hall
  drafted in blue for the plan as arranged and madder for the plan as it was. The look is fixed: updates extend it and
  never restyle it.
- **Design records (read these before any site change):**
  - `PRODUCT.md`: the facts, voice, honest status and every site contract (Capabilities and Constraints).
  - `DESIGN.md`: the visual system and its named rules, the source of truth for the look.
  - `.impeccable/surfaces/docs-index-html.md`: the direction contract.
  - `.impeccable/design.json`: tokens and component snippets.
  - `.impeccable/critique/`: the pre-redesign critique.

### Design rules (from DESIGN.md; keep them)

- **The Before-and-After Rule**: madder is the plan as it was, drafting blue the plan as arranged, on every line, bar,
  caption and readout. Never swap them or use either as decoration; anything else is ink (links, focus and pins excepted).
- **The Workplace Hues Stay in the Hall Rule**: lumber brown, farm green and water teal (`--w-L/F/W`) only tell the three
  workplaces apart inside diagrams. Never on text, buttons or sections.
- **The Caslon for Things on Cards Rule**: headings, names, numbers and card lettering are Libre Caslon Text; reading
  text is system-ui; guests' names are always Caslon italic.
- **The Numbers Are Figures Rule**: evidence is Caslon 700 with tabular figures in a labelled field list (`dl.fields`)
  or the readout, never stat tiles.
- **The Only Pins Cast Shadows Rule**: cards meet the board at a 1px `--rule` edge and cast nothing. Shadows exist only on
  the two 10px madder pins (`0 2px 2px rgba(0,0,0,.25)`) and the chart tooltip while it shows.
- Tokens live in `docs/style.css`: `:root` (light), then dark twice, under `@media (prefers-color-scheme: dark)
  :root:not([data-theme="light"])` and under `:root[data-theme="dark"]`; change both dark blocks together.
  Light / dark: ground `#e6e1d6` / `#1b1d21`, card `#fbf8f1` / `#26292e`, card back `#ebe4d4` / `#1f2226`, ink
  `#23201b` / `#ece6da`, muted `#5a544a` / `#b3ab9e`, rule `#cfc7b8` / `#3b3f46`, blue `#2d5f9a` / `#8fb6e8` (hover
  `#1f4677` / `#b3cdf0`), madder `#b4432f` / `#e98a74`, button ink `#fff` / `#10192a`; workplaces `#8a5a2b` `#5b7a24`
  `#2a7390` (dark `#d19a63` `#a3c46a` `#74bdd8`). Band is ink at 4% / 3.5%.
- Shape and line: 2px corners (SVG cards 1.5px, bars 1px); circles only for tables, pins, kits, chips and step
  numerals; every diagram stroke 1.4 (2.4 only on a moved or farther place card); dashes mean before (4 4) or never moves (2 2).
- Layout: 1160px column, gutter `clamp(16px, 4vw, 32px)`, sections split by a full-width ink rule (`section.ruled`),
  content as ruled lists with hairlines, never tiles. Breakpoints 960 / 860 / 720 / 560px; at 720px the comparison
  table stacks into a labelled block per row (`td[data-label]`), so every new cell needs its `data-label`.
- Fonts: Libre Caslon Text 700 and 400 italic only, self-hosted in `docs/fonts/` (OFL, `OFL-LibreCaslon.txt`); the 700
  is preloaded in each `<head>`. No other webfonts, and nothing from a CDN at runtime.
- Textures: `docs/textures/{board,card}-{light,dark}.webp`, made by `docs/textures/make_textures.py` (numpy + Pillow,
  fixed seeds; run `python make_textures.py` from that folder). Change the script and re-run it rather than editing
  images. Every shipping raster carries provenance (the `.webp.json` sidecars): run the Impeccable `embed-prompt`
  command on each new or changed image, and `embed-prompt --scan docs` to check none is missing.
- Themes: light and dark, from `prefers-color-scheme` plus the header toggle, stored in localStorage as `olh-theme`
  (`data-theme` on `<html>`); the one-line bootstrap in every `<head>` sets it before the stylesheet. Check both.
- Phones: no horizontal scroll at 390px, and tap targets ≥ 44px.
- Motion: the day's pass on the seating plan: place cards move round their cycles over 900ms
  `cubic-bezier(.45, 0, .2, 1)` with the walk lines hidden in transit. Buttons lift 1px over 150ms. Everything respects
  `prefers-reduced-motion` (cards jump, the totals still update).
- Don't: bring back the Timbermods landing template (cream paper, icon tiles, stat tiles, pill badges, eyebrows); no
  gradients; no shadows on cards, buttons or diagrams; no display face but Libre Caslon Text (not Fraunces); no
  official Timberborn logos or key art (the marks are `favicon.svg` and simple line icons).
- New components: build them from the tokens and components above, match the neighbouring sections, and add them to DESIGN.md.

### The seating demo (hero) and its diagrams

- `seating.js` reads the hall from the SVG's data attributes (`data-station`/`data-x`/`data-y`, `.table` `data-beds`,
  `.pc` `data-work`/`data-home`/`data-seat`), tries every arrangement (`solve()`, ties go to fewest movers) and reports
  what it computes. Today: `185 → 144`, 3 beavers moved in 1 cycle, Maple (+4) walks farther. The static readout
  (`185` in `[data-readout]`) and the move-cycle figure in `#how` (Birch B→A, Sorrel A→C, Maple C→B, and its
  aria-label) must match what the code computes. Never type numbers the code doesn't produce.
- The hall SVG in `index.html` is hand-maintained (its generator was never committed). If you change a home, bed count,
  guest or station, keep `CHAIRS` in `seating.js`, the static `x1..y2` walk lines and card transforms in step, then
  re-check: in the preview run `document.querySelector('[data-pass]').click()`, wait a second, and read
  `document.querySelector('[data-readout]').innerText`; update the static total and the cycle figure to match.

### Content rules

- Every text change follows *Writing README and website text* above.
- Describe the mod as it is now. No "New in <version>", "added in …" or version history on player pages; that belongs
  in the README changelog and the GitHub release notes. Upgrade steps players need are the only exception.
- The played and not-played status matches the README exactly: the 1.0 code (released as 1.0.0) ran 16 passes in a
  live game, including one hosted co-op session; 1.1.0's changes and anything since are covered by automated tests but
  not played; no live two-player session on 1.1.x; frame time, Iron Teeth, non-Windows and Timber Together live play untested.
  Replay numbers always say "straight-line stand-in". Never invent numbers, reviews or screenshots.
- Keep the credits: MIT, maintained by Timbermods; Housing Optimize and Commute Balancer are Bobingabout's. Keep the
  "unofficial, not affiliated with or endorsed by Mechanistry" line in every footer.
- Terminology: pass; move cycle; route cost / route-cost unit (the game's own, not straight-line); commute; adults,
  children, homes, workplaces, districts; the 32 nearest homes; rejected, stale; Hungarian algorithm.
- `docs/release.js` is shared across timbermods sites and byte-identical: replace it, never edit it. Check:
  `git hash-object docs/release.js` equals `gh api repos/timbermods/MixedStorage/contents/docs/assets/release.js -q .sha`.
- `404.html` uses absolute `/OptimizedLocalHousing/` paths and loads no scripts (unstyled on the local preview; that's expected).

### Update the website for a new release

When asked to "update the website for the latest release, consistent with the design" (write every change by
*Writing README and website text* above):
1. Read the release and the docs: `gh release list -R timbermods/OptimizedLocalHousing -L 5`,
   `gh release view <tag> -R timbermods/OptimizedLocalHousing`, README (incl. Changelog and "What has not been
   measured"), PRODUCT.md. List every player-facing change.
2. Update every place the site states a changed fact (`grep -rn "1\.1\.1" docs/` finds the version ones):
   - Static release fallbacks: `data-release="tag"` / `"version"` (index hero and closing Download, install Download
     and `loaded.` log line, troubleshooting log table and mod-list line), `data-release="asset-name"` and
     `"sha256"` (install `#checksum`; take the hash from the release's SHA256SUMS asset).
   - `data-release-pinned`: index `.status-note` and `#status` head, FAQ `#does-it-have-settings` and `#is-it-stable`;
     bump after re-checking the text.
   - Status: index `.status-note`, `#status` Tested / Not yet lists (incl. "41 automated checks: 40 run on every
     change": match the test output), install `#multiplayer`, FAQ `#does-it-work-in-multiplayer` and `#is-it-stable`,
     troubleshooting `#multiplayer`.
   - Requirements and game version (built against 1.1.2.4, minimum 1.1.0.0): install `#requirements`, FAQ
     `#which-game-versions-operating-systems`, troubleshooting `#update`, index hero lead and meta description.
   - Behaviour: index `#features` (six rules; the head says "Six rules"), `#how` steps, `#rules` always / never,
     `#compare` table, `#results`; FAQ `#does-it-have-settings` (links the tuning constants in `DEVELOPING.md`); log messages in troubleshooting
     `#log` table and symptoms; install `#layout` (the `version-1.1` folder tree), `#update`, `#uninstall`.
   - `<meta name="description">` / `og:description` on every page, and PRODUCT.md's Operating Context.
3. Put new content into the existing components: a rule → `ul.rules-key` item (h3 + p); always / never → `ul.checks` /
   `ul.checks.never`; tested / not yet → `.status-grid` `ul.checks` / `ul.checks.open`; a log message → a troubleshooting
   table row plus a `details.q` with an `id`; a question → `details.q` with `div.answer` in the right FAQ group;
   evidence → `dl.fields`; install steps → `ol.steps`. Don't restyle anything.
4. Test: `dotnet run --project OptimizedLocalHousing.Tests -c Release` (ends `45 checks passed.`). There is no site
   test in CI; check the contracts in PRODUCT.md by hand, the release.js hash above, and the seating demo numbers.
5. Preview: `python -m http.server 8784 -d docs` (background), then open http://localhost:8784/. Capture light, dark
   and a 390px phone. If the personal `impeccable-site-flow` skill is available, use
   `python <skill>/scripts/capsite.py http://localhost:8784/ <out> "" install.html troubleshooting.html faq.html`
   (overflow must be 0); otherwise use the Browser pane in both colour schemes at desktop and mobile sizes. Check the
   changed sections and that there's no horizontal scroll. Stop the server afterwards.
6. Optional but recommended: run the detector,
   `"$(ls -d ~/.claude/plugins/cache/impeccable/impeccable/*/skills/impeccable | tail -1)/scripts/impeccable" detect --json docs`
   (exits 2 when it finds anything; parse from the first `[`). Baseline is 23 findings, all known false positives:
   `cramped-padding` on `.ruled`, `.page-head`, `.compare`, `.sheet`, `.stock` (padding via clamp and nesting);
   `flat-type-hierarchy` (footer h2s); `design-system-color` rgb(0,0,0) on 404 (absolute CSS path it can't resolve);
   `design-system-font-size` for 1.02rem, 1.16rem, .94rem, .82rem, 15px and `clamp(1.55rem, 2.8vw, 2rem)` (sizes
   DESIGN.md gives as ranges). Anything new is real until shown otherwise.
7. If the look changed (a new component or layout), update DESIGN.md and `.impeccable/design.json`.
8. Update the README if it repeats the facts.
9. Ship: branch → commit → push → `gh pr create`. After Kyler says merge: `gh pr merge <n> --merge` (that publishes),
   then verify:
   - `gh api repos/timbermods/OptimizedLocalHousing/pages/builds/latest -q .status` is `built`;
   - `curl -s https://timbermods.github.io/OptimizedLocalHousing/ | grep -c "<a changed string>"` finds the change.

### Full redesign

A new look goes through the whole Impeccable flow (init → critique → audit → direction → build → finish review →
DESIGN.md). With the personal skill: "use the impeccable-site-flow skill to redesign this site".
