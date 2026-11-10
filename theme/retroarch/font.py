"""Instance statique de Jost (wght=500) : le freetype de RetroArch 1.8.4 ne gere
pas forcement les polices variables."""
from __future__ import annotations

from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

JOST = Path(__file__).resolve().parents[1] / "fonts" / "Jost-Variable.ttf"
WEIGHT = 500


# date figee (secondes depuis 1904-01-01) : 2020-01-01 00:00:00 UTC, pour une sortie reproductible
FIXED_TIME = 3660681600


def build(out) -> None:
    f = TTFont(str(JOST), recalcTimestamp=False)
    inst = instancer.instantiateVariableFont(f, {"wght": WEIGHT}, inplace=False)
    inst["head"].created = FIXED_TIME
    inst["head"].modified = FIXED_TIME
    inst.save(str(out))
