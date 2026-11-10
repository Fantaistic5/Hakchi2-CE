r"""Empaquetage du hmod ``snes_usa_b1`` (tar.gz deterministe).

    python -m theme.b1.pack --out P:\HakchiSNESUSA\theme_out\snes_usa_b1.hmod

La racine du hmod reflete le rootfs de hakchi : ``readme.md``, ``uninstall``,
``etc/preinit.d/p8030_snes_usa_b1`` et ``usr/share/snes_usa_b1/resources/**``.
Les ressources sont lues dans le dossier de sortie de ``build_theme`` (par defaut
le dossier du ``--out``). Aucune police ``title`` n'est embarquee (voir README).
"""
from __future__ import annotations

import argparse
import gzip
import io
import sys
import tarfile
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from theme.b1 import art  # noqa: E402
else:
    from . import art

THEME = Path(__file__).resolve().parents[1]
HMOD_DIR = THEME / "hmod"
OFL = THEME / "fonts" / "OFL.txt"
MTIME = 1762732800  # 2025-11-10T00:00:00Z, fixe pour un paquet reproductible
ROOT = "usr/share/snes_usa_b1/resources/"
SCRIPT = "etc/preinit.d/p8030_snes_usa_b1"
# fichiers texte du hmod (copies de theme/hmod/, fins de ligne forcees en LF)
TEXT = ("readme.md", "uninstall", SCRIPT)
FONTS = ("fonts/s.font", "fonts/m.font", "fonts/l.font", "fonts/copyright.fnt")
SOUND_SRC, SOUND_DST = "bgm_home.wav", "sounds/hvc/bgm_home.wav"


def _mapping() -> dict:
    """chemin dans le hmod -> chemin relatif dans le dossier de sortie du theme."""
    m = {ROOT + "sprites/packed.png": "resources/sprites/packed.png",
         ROOT + "sprites/dot_num.png": "resources/sprites/dot_num.png",
         ROOT + SOUND_DST: SOUND_SRC}
    for n in art.LAYOUT:
        m[ROOT + "layout/asset/sprite/" + n] = "resources/layout/asset/sprite/" + n
    for f in FONTS:
        m[ROOT + f] = f
    return m


def expected_sources() -> list:
    """Fichiers attendus dans le dossier de sortie (hors textes et licence)."""
    return sorted(_mapping().values())


def _lf(data: bytes) -> bytes:
    return data.replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def collect(src: Path) -> dict:
    files = {}
    for rel in TEXT:
        files[rel] = _lf((HMOD_DIR / rel).read_bytes())
    files[ROOT + "fonts/OFL.txt"] = _lf(OFL.read_bytes())
    for dst, rel in _mapping().items():
        p = Path(src) / rel
        if not p.is_file():
            raise FileNotFoundError(f"ressource manquante : {p}")
        files[dst] = p.read_bytes()
    for rel in TEXT:
        if b"\r" in files[rel]:
            raise ValueError(f"CR restant dans {rel}")
    return files


def build(out: Path, src: Path) -> list:
    files = collect(src)
    dirs = set()
    for name in files:
        parts = name.split("/")[:-1]
        for i in range(1, len(parts) + 1):
            dirs.add("/".join(parts[:i]))
    entries = sorted(set(files) | dirs)
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    raw = io.BytesIO()
    with tarfile.open(fileobj=raw, mode="w", format=tarfile.GNU_FORMAT) as tar:
        for name in entries:
            ti = tarfile.TarInfo(name)
            ti.mtime, ti.uid, ti.gid, ti.uname, ti.gname = MTIME, 0, 0, "", ""
            if name in files:
                data = files[name]
                ti.size = len(data)
                ti.mode = 0o755 if name == SCRIPT else 0o644
                tar.addfile(ti, io.BytesIO(data))
            else:
                ti.type, ti.mode = tarfile.DIRTYPE, 0o755
                tar.addfile(ti)
    with open(out, "wb") as f:
        with gzip.GzipFile(filename="", mode="wb", fileobj=f, mtime=0, compresslevel=9) as gz:
            gz.write(raw.getvalue())
    return [(n, len(files[n]) if n in files else None) for n in entries]


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", required=True, type=Path, help="fichier .hmod a produire")
    ap.add_argument("--src", type=Path, help="dossier theme_out (defaut : dossier du --out)")
    args = ap.parse_args(argv)
    src = args.src or args.out.parent
    listing = build(args.out, src)
    for name, size in listing:
        print(f"{'' if size is None else size:>9}  {name}")
    print(f"{args.out} : {args.out.stat().st_size} octets")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
