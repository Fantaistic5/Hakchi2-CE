"""Instance statique de Jost (wght=500) : le freetype de RetroArch 1.8.4 ne gere
pas forcement les polices variables."""
from __future__ import annotations

from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

JOST = Path(__file__).resolve().parents[1] / "fonts" / "Jost-Variable.ttf"
WEIGHT = 500


def build(out) -> None:
    f = TTFont(str(JOST))
    inst = instancer.instantiateVariableFont(f, {"wght": WEIGHT}, inplace=False)
    inst.save(str(out))
