# SDD ledger — plan: docs/superpowers/plans/2026-11-10-pochettes-et-socle.md

Spec: docs/superpowers/specs/2026-11-10-hakchi-snes-usa-refonte-design.md
Branch: snes-usa/pochettes (from mainline 4fb3092c; spec+plan commit 98aef422)

## Pre-flight scan

| Pair / task | Produces → consumes | Finding |
|---|---|---|
| T2 → T3 | T3 uses nothing from T2 except folder Scrapers/Covers | ok |
| T2,T3 → T4 | LibretroNames.Sanitize/ThumbnailSystem/BoxartUrl/NormalizeTitle, LibretroDatIndex.FindByCrc/FindByRomSet/Names, ICoverSource | ok, names consistent |
| T2 → T5 | GameQuery, CoverCandidate, MatchKind, ICoverSource | ok |
| T5 → T6 | GameQuery (CoverTargets.ToQuery), ICoverSource (GoogleCoverSource) | ok ; hakchi_gui already references Scrapers.csproj (csproj:116) |
| T4,T5,T6 → T7 | LibretroThumbnailsSource(HttpCache, Func<string,string>), TheGamesDbCoverSource(string), GoogleCoverSource, CoverSearchService.SearchManyAsync, CoverTargets.DefaultSelection, NesApplication.ApplyCover | ok ; T7 must supply datUrlForSystem + cache dir |
| T0 self | Step 1 branch + exclude | already done by controller (98aef422) |
| T0 self | Step 4 needs NSIS 3.10 | NSIS not installed on machine |
| T1 self | adds net8 test project to hakchi_gui.sln | CI only has .NET Core 3.0 SDK → would break fork CI |
| T6 self | Step 2 manual compat check with 3.8.0 GUI | not automatable by subagent as written |
| T7 self | Steps 2-3 manual GUI runs | subagent has no GUI driving |

Ruling: T0 Step 1 is already done by controller — implementer skips it — cost if wrong: none.
Ruling: T0 Step 4 — if NSIS absent, install NSIS 3.10 **portable (zip) under N:\nsis** and pass its path; no system-wide install — cost if wrong: build may need makensis on PATH, minor.
Ruling: T1 — Scrapers.Tests is NOT added to hakchi_gui.sln; run with `N:\dotnet\dotnet.exe test Scrapers.Tests` — spec forbids breaking existing behaviour and fork CI only has .NET Core 3.0 — cost if wrong: test project not visible in VS solution (cosmetic).
Ruling: T6 Step 2 — compat with 3.8.0 proven by an xUnit test in Scrapers.Tests? No: AppMetadata lives in hakchi_gui (net48). Prove it by deserializing a metadata.json containing "CoverSource" with Newtonsoft default settings (what 3.8.0 uses) in a tiny check, plus code reading of 3.8.0-era AppMetadata load path; record in report — cost if wrong: 3.8.0 could choke on new field (low: Newtonsoft ignores unknown members by default).
Ruling: T7 Steps 2-3 — implementer produces covers-run-1.md through an opt-in live xUnit test (Trait Category=Live) running the real sources on P:\HakchiSNESUSA\app\games metadata (no image writes); controller does the GUI smoke check with computer-use afterwards — cost if wrong: GUI-only bugs found late.

## Progress
Task 0: complete (commits 98aef42..35b1bc1, review clean)
Task 0: minor (deferred): build-local.md — makensis failure masked (exit 0), add warning near Step 4 command
Task 0: minor (deferred): mutex patch offset unscripted; helper scripts live in N:\tools outside repo
Task 0: minor (deferred): baseline-perf measure A is mods dialog, B (768 ms) is the real main window
Task 0: note: P:\HakchiSNESUSA\app\hakchi_mtx.exe = test copy with mutex renamed (user's 3.8.0 was running)
Ruling: Tasks 1 and 2 dispatched together as one unit — Task 1 is pure scaffolding for Task 2's tests (plan right-sizing rule) — cost if wrong: one larger review diff.
Task 1: complete (commits 35b1bc1..ff336f1, review clean — reviewed with Task 2)
Task 2: complete (commits ff336f1..a42be32, review clean)
Task 2: minor (deferred): Uri.EscapeDataString escapes ( ) differently net48 vs netcore — harmless for servers
Task 3: complete (commits a42be32..5cfe11c, review clean)
Task 3: note: verified DAT URLs — no-intro: https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/<System>.dat ; arcade: .../metadat/fbneo-split/FBNeo%20-%20Arcade%20Games.dat (dat/ path is 404)
Task 3: minor (deferred): multi-line rom( blocks unsupported; StartsWith("game") loose; Names may hold duplicates; no test for multi-track CD entry / duplicate CRC
Ruling: Task 4 adds `public static string DatUrl(string thumbnailSystem)` to LibretroNames (no-intro for cartridge systems, metadat/redump for CD systems, fbneo-split for arcade) with a test, so Task 7 passes `LibretroNames.DatUrl` as datUrlForSystem — mapping is pure data and belongs with the other libretro naming rules, testable — cost if wrong: small extra API surface.
Task 4: review 1 — Approved with 1 Important (no single-flight DAT index load per system)
Task 4: minor (deferred): HttpCache orphan *.tmp on Move failure; no in-flight dedup per URL; body read not cancellable; HttpCache not IDisposable (must be singleton); title fallback rebuilds normalized names per query (pre-build lookup); Region label = first tag while ranking uses any tag; FbNeo constant duplicated; up to ~10 sequential HEADs per title fallback on cold cache
Task 4: fix round 1/5 (1 addressed, 0 open; commits 91734de..ccc6f8e)
Task 4: minor (deferred): shared DAT load bound to first caller's CancellationToken — fine while a batch shares one token; harden if per-item tokens appear; 404 DAT not negatively cached
Task 4: complete (commits 5cfe11c..ccc6f8e, review clean after round 1)
Task 5: review 1 — Needs fixes: (I1) shared TGDB Scraper not thread-safe under 4 parallel games; (I2) platform token heuristic + items[0] fallback picks wrong platforms
Ruling: Task 5 I2 fixed with an explicit System→TGDB platform name map and no fallback — spec says "jaquette exacte, sinon belle image" and a wrong-platform cover is neither — cost if wrong: TGDB returns nothing for systems missing from the map or with differently spelled platform names (libretro/Google still cover them).
Task 5: minor (deferred): TGDB call not cancellable mid-request; no in-flight cancel test; lastError keeps only last message
Task 5: fix round 1/5 (2 addressed, 0 open; commits 725f7a2..52b773b)
Task 5: minor (deferred): InternalsVisibleTo csproj item ignored on SDK<5 (test project only, not in CI); verify live that TGDB IScraperData.Platform carries display names matching the map (Task 7 live run); TGDB serialized = throughput bottleneck
Task 5: complete (commits ccc6f8e..52b773b, review clean after round 1)
Task 6: complete (commits 52b773b..e7fbe64, review clean)
Task 6: minor (deferred): SetThumbnailFile doesn't reset CoverSource; Google call not interruptible (~60 s worst case after cancel); ServicePointManager.DefaultConnectionLimit=2 on net48 throttles parallel requests (address in Task 7); NeedsCover(null)/ToQuery(null) inconsistency
Task 7: dispatched (BASE e7fbe640, implementer sonnet)
Task 7: implementer DONE_WITH_CONCERNS (f89c2932, db9e21de) — GUI not driven; cancel loses in-flight chunk of 16
Task 7: review 1 — Needs fixes: (I1) HttpClient timeout (TaskCanceledException) treated as user cancel → aborts previews/apply batch
Ruling: Task 7 fix round 1 also takes cheap minors M2 (ImageList leak), M3 (distinct "annulé" state), M4 (offline surfaced), M5 (service cached per window), M6 (alt downloads token+dedupe), M7 (allow non-user close) — all in CoverBatchForm, window is the user-facing deliverable before smoke test — cost if wrong: slightly larger fix diff.
Task 7: fix round 1/5 (implementer: 7 addressed; commit b2cd5b3f) — awaiting re-review
Task 7: fix round 1/5 (7 addressed, 0 open; commits db9e21d..b2cd5b3)
Task 7: minor (controller fixing inline before smoke test): Msg.Cancelled text says "not found"; UpdateSummary counts Cancelled/NetworkError as not found; check ChooseAlternativeAsync catch after dispose
Task 7: controller inline fix (Cancelled rows excluded from not-found count, Msg.Cancelled wording) 64c6b663; GUI smoke pending — user active on desktop (focus contention with Unity / hakchi 3.8.0)
Task 7: GUI smoke (controller, computer-use): found 2 bugs — NRE on open (ListView replays ItemChecked at handle creation, null entries in VisibleRows) and DefaultSelection included 21 original console games (no CustomCoverArt) → fixed 52a6c4fb. Verified on screen: open, search (11 ready/24 check/3 not found before fix), alternatives pick, Apply (12 applied on P: copy), cancel (rows "Annulée", no dialog), default list now 5. Offline NOT tested (would need system network change).
Task 7: minor (deferred): a new search after cancel resets earlier results; TGDB "Neo Geo" platform not in arcade map; TGDB previews ArtSize.Original heavy
Task 7: complete (commits e7fbe64..52a6c4fb, review clean after round 1 + controller smoke fixes)
Final review: With fixes — I1 compression ignored in ApplyCover, I2 cross-thread bitmap in Apply, I3 preferred region hard-coded Europe; minors listed in final-fix-brief.md
Ruling: final fix dispatch also takes minors M1-M6 (ticks lost on toggle, apply status text, untick during apply, network rows in not-found count, Neo Geo TGDB platform, 404 DAT negative cache); parked: Google/TGDB uninterruptible calls, new search after cancel resets results, cold-cache HEAD chain, full-size preview memory, region label first tag — all for project 3 redesign of the window — cost if wrong: user waits a few s after Cancel.
Final fix: 1fa4523a (I1-I3, M1-M6), 35 tests pass — awaiting scoped re-review + smoke
Final review: fix round complete (1fa4523a), re-review clean (all addressed); GUI re-smoke OK on 1fa4523a (open, search, include-existing toggle keeps results). Minor parked: progress bar ends short when a row is skipped during Apply.
