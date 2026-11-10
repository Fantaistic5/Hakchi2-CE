# SDD ledger — plan: docs/superpowers/plans/2026-11-10-theme-tv-b1.md

Spec: docs/superpowers/specs/2026-11-10-hakchi-snes-usa-refonte-design.md (§5, §5 bis)
Branch: snes-usa/theme-b1 (from snes-usa/pochettes 1fa4523a+ledger; plan commit 3982a93a)

## Pre-flight scan
| Pair / task | Produces → consumes | Finding |
|---|---|---|
| T0 → all | palette constants | ok |
| T1 → T2 | parse/build CloverFont | ok |
| T3 → T4 | repaint(atlas, json, overrides) | ok |
| T0 map → T4 | list of sprites to redraw | T4 depends on T0 note content (prose) — ok |
| T2,T4,T5,T7 → T6 | theme_out files, retroarch scripts | T7 scripts "integrated into T6 hmod" but T7 runs after T6 → T7 must amend the hmod (install/uninstall) itself |
| T4/T5/T7 self | user validation gates | controller handles gates (ask user), implementer stops at gate |
| T8 | console write | needs explicit user consent in chat — controller only |
| T2 | downloads Jost TTF from Google Fonts repo | allowed (OFL, public) |

Ruling: T0 Step 1 (branch) already done by controller — skip. — cost if wrong: none.
Ruling: T7 amends T6's hmod (adds install/uninstall hooks calling apply.sh/restore.sh) and re-packs — plan order forces it — cost if wrong: small rework of T6 tests.
Ruling: user-validation steps (T4 S3, T5 S3, T7 S1) — implementer delivers the artifact and stops; controller shows it to the user and records the answer here before the next task — cost if wrong: none.
Ruling: T8 is controller-only (consent + backup + user drives install) — security-sensitive console write.

## Progress
Task 0: dispatched (BASE 3982a93a, sonnet)
Task 0: implementer DONE_WITH_CONCERNS (f41670e9) — home bg is wall.png image; Manual/Display pages clearColor #F1EEEC in defaultscene.scn; text colours in bytecode colors_eur.lua
Ruling: defaultscene.scn may be bound as a copy whose ONLY change is clearColor/colour values (no transform/layout/animation field changed), with a test asserting the JSON diff touches only colour fields — spec forbids changing layout/animations, not colours; a colour value in a data file is a resource — cost if wrong: if clover-ui validates the scn, the Manual/Display pages fail to load (uninstall restores).
Ruling: text colours living in bytecode (colors_eur.lua) stay as they are; B1 adapts sprite colours for contrast with the existing white/black text and reports it at the preview gate — cost if wrong: some text contrast off-palette.
Task 0: complete (commits 3982a93..f41670e, review clean)
Task 0: minor (deferred → T4): thumbnail_lock 40x40 missing from map; allow_lateral_on/_off and scrollbar *_off only in shorthand; ALL keys not cross-checked in test
Task 1: implementer DONE (6869cc73), 11 tests, 6 reference fonts round-trip
Task 1: complete (commits f41670e..6869cc7, review clean)
Task 1: minor (→ T2): build writes common.pages without checking len(pages); chars key vs Glyph.id unchecked; add a build-from-scratch test with different page size
Ruling: T2 title.font — reference is the user's font_remount title font ("Yu Gothic UI", has CJK/many codepoints). Generate title.font from Jost for every codepoint Jost covers, and render the rest with a fallback TTF passed on the CLI (default C:\Windows\Fonts\YuGothM.ttc, i.e. the same family the user already uses); output stays outside the repo (personal console use). Tests: same codepoint set as reference; report counts Jost vs fallback — cost if wrong: mixed-face titles for non-Latin games only.
Task 2: implementer DONE_WITH_CONCERNS (0085c547), 28 tests; preview checked by controller — clean, legible. title at 35px (page full). font_remount precedence → T6 must ship title/copyright via font_remount paths.
Task 2: review 1 — Approved with 1 Important (no baseline/yoffset test); minor: Jost cap height 10-15% smaller than stock (cap scale only shrinks)
Ruling: T2 fix round 1 takes I1 (baseline test) + cap-height match (scale Jost up to stock cap height when page fits, else largest that fits) — TV legibility is a spec goal ("fonctionnel") and shrunk labels read worse from a sofa — cost if wrong: title page may still cap at 35px.
Task 2: minor (deferred): s/m 1px higher than reference; test_menu_strings_covered near-tautological; Pillow not pinned; fallback loop error names only last px
Task 2: fix round 1/5 (2 addressed, 0 open; commit 24ff0119) — scoped re-review by controller (36-line diff): cap scale may exceed 1, ±1 px refinement, page-fit loop kept; 31 tests
Task 2: complete (commits 6869cc7..24ff011, review clean after round 1)
Task 3: dispatched (BASE 24ff0119) — note: packed.json textureFrame=[x,y,w,h], 166 sprites, none trimmed (sourceOffset 0, sourceSize==frame size)
Task 3: implementer DONE_WITH_CONCERNS (133f2221) — concerns cosmetic (Haiku trailer, extra KeyError test); 36 tests
Task 3: review — controller (44-line pure function + 5 tests): validates all overrides before writing, erase+replace, no mutation; Approved
Task 3: complete (commits 24ff011..133f222, review clean)
Task 4: dispatched (BASE 133f2221, implementer opus — design judgment)
Task 4: implementer DONE (10b99072), 476 tests; draw(name,size,ref=None) interface change; concerns: caption_title light bar w/ forced black text, dialog fill black from sys_dialog.scn, focused buttons STRIPE_LIGHT. Controller viewed preview — coherent B1; awaiting user gate
Task 4: USER GATE — user: "Ok, title in lavender" → caption_title fill LAVENDER (black text); rest approved
Task 4: user-gate change a84179c9 (lavender caption_title + test), 477 tests
Task 5: dispatched (BASE a84179c9, implementer opus) in parallel with T4 review (different files: theme/music)
Task 4: review 1 — Approved (no Critical/Important); outputs verified vs ui_ref sizes
Task 4: minor (deferred): no-gradient test misses stepped plateaus; alpha-blend segment branch over-permissive; build_theme lacks --out != --ref guard (fold into T6); outputs RGBA where originals were P/L/RGB — verify clover-ui loads RGBA wall.png at T8 (fallback: re-save in original mode); later tasks must copy packed.json/dot_num.json from ref
Task 4: complete (commits 133f222..a84179c, review clean, user gate passed)
Task 5: implementer DONE (e21d9f35), 488 tests; 135 BPM (150 would be 51 s < 55 s floor), 56.9 s, peak -1.39 dBFS, RMS -18 (stock -23.3), seam 0.27%; awaiting user listening gate
Task 5: USER GATE — user: "Music OK"
Ruling: T6 layout — hmod files go to rootfs/usr/share/snes_usa_b1/resources/** with guarded binds in p8030; never ship usr/share/fonts/title.fnt (hakchi auto-uninstall rm's every shipped path → would delete user's font_remount title); our p8030 binds s/m/l/copyright after fontfix p8025; title left to font_remount (I8) — facts in task-6-context.md (console read-only SSH) — cost if wrong: our fonts not applied if bind order differs (visible, harmless).
Task 6: dispatched (BASE e21d9f35, implementer sonnet) in parallel with T5 review
Task 5: review 1 — Approved; Important I1: circular_reverb uses rad/s as rad/sample → acts as diffuse circular echoes, not 1.3 s plate (docstring wrong)
Ruling: T5 I1 — keep the approved audio (user gate "Music OK" was on this render); final fix dispatch corrects the docstring/naming to describe what it really does and pins the current render (no audio change); a true plate reverb is offered to the user as an optional variant later — the user's ear outranks the code's intent — cost if wrong: misleading code until final fix.
Task 5: minor (deferred): arpeggio pattern only even entries used (shorten data, same sound); slash-chord bass 3/5 from bass note; _ks cache keyed on length; seam test can't see missing tail wrap; deterministic test in-process only
Task 5: complete (commits a84179c..e21d9f3, review approved, user gate passed)
Task 6: implementer DONE (a58f1c69), 506 tests; hmod 9.6 MB deterministic; .gitattributes LF for theme/hmod
Task 6: review — controller: p8030 read in full (guarded binds, fonts after p8025, title untouched, no-op off snes-eur), uninstall only rm own dir, tar listing checked; all 37 shipped resources have a target on the real console (read-only ssh find) — Approved
Task 6: minor (deferred): user's custom s/m/l via font_remount would be overridden by ours (documented)
Task 6: complete (commits e21d9f3..a58f1c6)
Task 7: Step 1 facts gathered by controller (read-only ssh) → task-7-console.txt: RA v1.8.4 (Feb 2020), ~=/etc/libretro, menu_driver ozone (ozone_menu_color_theme 1), assets ozone+xmb(monochrome) present, no rgui assets needed, rgui_menu_theme_preset "" supported, xmb_font ~/fonts/_Default.ttf, menu_wallpaper retrocat_04.png, config_save_on_exit true; existing backup dir /etc/libretro/backup_claude_20261110 (retroarch.cfg + core options, from PS1/3DO fix)
Task 7: USER GATE pending — menu driver choice (RGUI custom preset exact palette / XMB + B1 wallpaper + Jost / keep Ozone basic black)
Task 7: USER GATE — user chose "XMB + B1 wallpaper" (Jost font, monochrome icons). Task 7: dispatched (BASE a58f1c69, implementer sonnet)
Task 7: implementer DONE (c10cccf8), 518 tests; XMB, 7 keys, wallpaper 1280x720 palette-only, static Jost-Medium; install/uninstall sourced-safe (no exit), busybox roundtrip byte-identical
Task 7: review — controller read install/uninstall/apply.sh/restore.sh verbatim: backup-if-absent, only b1.cfg keys, restore from .b1bak then delete, idempotent; Jost OFL has no Reserved Font Name → static instance OK — Approved
Task 7: minor (deferred): restore appends a key at EOF if RA dropped it; XMB text/icons not recolourable (documented)
Task 7: complete (commits a58f1c6..c10cccf)
Final review: dispatched (opus) on 3982a93a..HEAD before Task 8 (console write) — Task 8 needs user consent + fresh backup
Final review: With fixes — Critical none; I1: bgm_home.wav lacks the smpl loop chunk the original has (start 69177, end 2282823) → menu music would play once. Minors: uninstall rm -rf redundant + relative paths; p8030 pipeline `|| true`; Jost-Medium not reproducible (head.modified); apply.sh truncated tmp guard; functional bind test.
Ruling: final fix dispatch takes I1 (smpl loop [0, n-1] + inst + parse test, audio unchanged) + all listed minors + T5 ruling (reverb docstring honest, no audio change). pack_uninstall (console b0030, read earlier) sources uninstall with cwd=$rootfs and ignores its status, so rm -rf is harmless but dropped for cleanliness — cost if wrong: none.
Final fix: 3811bec5 (smpl+inst loop, reverb doc pinned, uninstall abs paths, p8030 || true + functional set -e test, reproducible Jost-Medium, truncated-write guard), 526 tests — controller re-check: smpl loop 0..2508799 present in theme_out WAV, RIFF size OK, uninstall verified — clean
Task 8: user consent "yes SSH" (waiting: user retransferring games to USB Q:). Fresh backup taken read-only → P:\HakchiSNESUSA\backups\pre_b1_20261110_1726 (retroarch.cfg, core options, config/, preinit.d, fonts, hmod uninstall list, hakchi config; 194 entries) + state.txt (md5 of retroarch.cfg). Install planned: scp hmod → /var/lib/hakchi/transfer/ then reboot, after user says transfer finished.
Task 8: hmod copied to /var/lib/hakchi/transfer (md5 c43d1236… match), rebooting
Task 8: installed — uninstall-snes_usa_b1 present, p8030 in preinit, live UI md5 == ours for bgm_home.wav/packed.png/s.font/m.font (47 binds under ui path), retroarch.cfg menu_driver xmb + wallpaper + Jost, .b1bak created; awaiting user's TV check
Task 8: USER TV CHECK — "All good". Task 8: complete
