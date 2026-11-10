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


# --- Instances statiques pour l'interface PC (hakchi_gui/Theme/Fonts) -------------------------
# (fichier, wght, famille GDI [nameID 1], sous-famille GDI [nameID 2], famille typo [16], style typo [17])
# Chaque graisse a sa propre famille GDI : GDI/GDI+ ne sait pas choisir 500 ou 600 parmi les
# styles Regular/Bold d'une famille ; "Jost Medium" et "Jost SemiBold" sont donc des familles distinctes.
PC_INSTANCES = (
    ("Jost-Regular.ttf", 400, "Jost", "Regular", "Jost", "Regular"),
    ("Jost-Medium.ttf", 500, "Jost Medium", "Regular", "Jost", "Medium"),
    ("Jost-SemiBold.ttf", 600, "Jost SemiBold", "Regular", "Jost", "SemiBold"),
)


def _set_name(font, name_id: int, value: str) -> None:
    table = font["name"]
    table.removeNames(nameID=name_id)
    table.setName(value, name_id, 3, 1, 0x409)
    table.setName(value, name_id, 1, 0, 0)


def build_pc_instance(out, weight: int, family: str, subfamily: str, typo_family: str, typo_style: str) -> None:
    f = TTFont(str(JOST), recalcTimestamp=False)
    inst = instancer.instantiateVariableFont(f, {"wght": weight}, inplace=False)
    ps = typo_family + "-" + typo_style
    _set_name(inst, 1, family)
    _set_name(inst, 2, subfamily)
    _set_name(inst, 4, family if typo_style == "Regular" else typo_family + " " + typo_style)
    _set_name(inst, 6, ps)
    _set_name(inst, 16, typo_family)
    _set_name(inst, 17, typo_style)
    inst["name"].removeNames(nameID=25)
    inst["head"].created = FIXED_TIME
    inst["head"].modified = FIXED_TIME
    inst.save(str(out))


def build_pc(out_dir) -> None:
    """Ecrit Jost-Regular / Medium / SemiBold (wght 400/500/600) dans out_dir, de facon reproductible."""
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, weight, family, sub, tf, ts in PC_INSTANCES:
        build_pc_instance(out_dir / name, weight, family, sub, tf, ts)


if __name__ == "__main__":
    import sys

    target = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2] / "hakchi_gui" / "Theme" / "Fonts"
    build_pc(target)
    print("ecrit dans", target)
