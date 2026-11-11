# Hakchi2-CE · SNES USA edition

A fork of [TeamShinkansen/Hakchi2-CE](https://github.com/TeamShinkansen/Hakchi2-CE), the PC tool that adds games, mods and themes to the NES / SNES / Famicom / Super Famicom Classic Mini consoles.

This fork keeps the hakchi engine (flashing, sync, mods, kernels) untouched and adds three things, all styled after the North American Super Nintendo: slate grey, purple and lavender, with the Jost typeface. It's meant to be functional and plain, with no gradients, glows or drop shadows.

## What's new

### 1. Covers that are found again
Batch cover search for the whole library, instead of one game at a time.

- Sources, in order: **libretro-thumbnails** (matched by CRC, ROM name or title), then **TheGamesDB**, then Google Images as a last resort.
- A review window ("Chercher les jaquettes") lists every game with its best candidate. Exact matches are ticked automatically; you confirm the rest.
- Nothing is written to a game folder until you apply.
- Code: `Scrapers/Covers/`, `hakchi_gui/Covers/`, `hakchi_gui/CoverBatchForm.cs`.

### 2. "B1" TV theme (hmod)
A theme for the console's own menu, generated from Python scripts. No Nintendo files are stored in this repository.

- Redrawn menu sprites, the B1 palette, Jost fonts.
- An original menu music loop.
- A matching RetroArch XMB look.
- Installed and removed like any other hmod (`snes_usa_b1`).
- See [`theme/README.md`](theme/README.md).

### 3. Redesigned PC interface
Every window of hakchi is redrawn in the B1 style, with no function removed.

- **Main window "A · Jaquettes":**
  - a systems / collections sidebar;
  - a cover grid with filters ("in the library" / "on the console");
  - a game sheet with a large cover;
  - a footer with the storage bar and the Add games / Synchronize buttons.
- All other windows, including ModHub, the folders manager, settings, and the task and flash dialogs. The flash and install dialogs behave exactly as before.
- Startup is as fast as the original interface. Measurements are in [`docs/superpowers/notes/baseline-perf.md`](docs/superpowers/notes/baseline-perf.md).
- Translations are kept. New strings are in English and French; other languages fall back to English for those strings.

The original engine has one fix: the FEL driver check no longer crashes on 64-bit Windows (`pnputil` is reached through `Sysnative` from the 32-bit process).

## Building

Requirements:
- Windows;
- Visual Studio 2022 (MSBuild);
- the .NET Framework 4.8 targeting pack;
- the git submodules.

```
git submodule update --init --recursive
nuget restore hakchi_gui.sln
msbuild Installer/Installer.vcxproj "/p:SolutionDir=<repo>\" "/p:SolutionPath=<repo>\hakchi_gui.sln" "/p:Platform=Any CPU"
```

The result is `hakchi_gui\bin\Debug\net48\hakchi.exe` (32-bit, like upstream). `tools\` must be on `PATH` during the build, because the post-build step uses `busybox`. NSIS is only needed for the installer.

A detailed walkthrough is in [`docs/superpowers/notes/build-local.md`](docs/superpowers/notes/build-local.md): it covers building without a full .NET SDK, and the known pitfalls.

### TheGamesDB key (optional)
TheGamesDB needs an API key. Put yours in `hakchi_gui/ApiKeys/TheGamesDB.txt`. That folder is git-ignored, so **never commit a key**. Without one, cover search uses libretro-thumbnails and Google only.

## Tests

```
dotnet test hakchi_gui.Tests      # UI, theme, grid, translations, no-regression guards (net48, x86)
dotnet test Scrapers.Tests        # cover sources and matching
python -m pytest theme/tests -q   # TV theme generator (see theme/README.md for the venv)
```

The UI tests guard against regressions in three ways:
- **Menus and windows:** every menu item and every window's controls are compared against a snapshot taken before the redesign (`hakchi_gui.Tests/Data/*_before.json`).
- **Mod installation:** the mod-install code path is checked by an IL fingerprint.
- **Colours:** each window is rendered and its pixels are checked against the palette.

Some tests use real sample games. They read the `HAKCHI_SAMPLES` environment variable (a hakchi portable folder) and are skipped when it is not set or the folder is missing.

`tools/FormGallery` renders every window off-screen to PNG, which is handy for reviewing the look. `tools/perf/` holds the startup measuring scripts. They launch the app, so **unplug the console** first, and pass `-ConsoleUnplugged`.

## Safety

- Back up your console (`/var/lib/hakchi`) and your game storage before flashing, installing a kernel or installing the theme hmod.
- Unless you change the mutex name, only one hakchi instance can run at a time. Close your usual hakchi before trying this build.
- This project ships no Nintendo firmware, ROMs, BIOS or copyrighted artwork.

## Design documents

- Spec: [`docs/superpowers/specs/2026-11-10-hakchi-snes-usa-refonte-design.md`](docs/superpowers/specs/2026-11-10-hakchi-snes-usa-refonte-design.md)
- Plans: [`docs/superpowers/plans/`](docs/superpowers/plans/)
- Delivery notes and work logs: [`docs/superpowers/notes/`](docs/superpowers/notes/)

These documents are written in French.

## Credits and licence

- hakchi2 by Cluster (clusterrr), and Hakchi2-CE by Team Shinkansen and contributors. All engine work is theirs.
- Cover data: [libretro-thumbnails](https://github.com/libretro-thumbnails) and [TheGamesDB](https://thegamesdb.net).
- Typeface: [Jost](https://github.com/indestructible-type/Jost) by indestructible type*, under the SIL Open Font License (`hakchi_gui/Theme/Fonts/OFL.txt`).

Licensed under the **GNU General Public License v3.0**, like upstream (see [`LICENSE`](LICENSE)).

Not affiliated with or endorsed by Nintendo. "Super Nintendo", "SNES" and "Classic Mini" are trademarks of Nintendo.
