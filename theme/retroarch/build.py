r"""Produit les binaires RetroArch B1 dans <out>/retroarch/ (jamais dans le depot) :
b1_wallpaper.png et Jost-Medium.ttf.

    python -m theme.retroarch.build --out P:\HakchiSNESUSA\theme_out
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from theme.retroarch import font, wallpaper  # noqa: E402
else:
    from . import font, wallpaper

WALLPAPER, FONT = "b1_wallpaper.png", "Jost-Medium.ttf"


def build(out: Path) -> Path:
    d = Path(out) / "retroarch"
    d.mkdir(parents=True, exist_ok=True)
    wallpaper.save(d / WALLPAPER)
    font.build(d / FONT)
    return d


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", required=True, type=Path)
    d = build(ap.parse_args(argv).out)
    print(f"retroarch : {d}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
