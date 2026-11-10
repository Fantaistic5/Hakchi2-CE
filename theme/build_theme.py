"""Construit les images B1 et leurs planches de contrôle.

    python -m theme.build_theme --ref P:\\HakchiSNESUSA\\ui_ref --out P:\\HakchiSNESUSA\\theme_out

Sorties (jamais dans le dépôt) :

* ``<out>/resources/sprites/packed.png``  atlas repeint (``packed.json`` inchangé)
* ``<out>/resources/sprites/dot_num.png`` chiffres du compteur recolorés
* ``<out>/resources/layout/asset/sprite/**.png`` fonds, cadeau, sprites neutralisés
* ``<out>/preview_home.png``  menu d'accueil 1280x720 recomposé d'après les ``.scn``
* ``<out>/sprites_sheet.png`` planche de tous les sprites produits, nommés, sur ardoise
* ``<out>/retroarch/`` fond XMB 1280x720 et Jost-Medium.ttf (RetroArch B1)
"""
from __future__ import annotations

import argparse
import io
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from theme.b1 import art, atlas, clover_font, font_gen  # noqa: E402
    from theme.b1 import palette as P  # noqa: E402
    from theme.retroarch import build as retroarch_build  # noqa: E402
else:
    from .b1 import art, atlas, clover_font, font_gen
    from .b1 import palette as P
    from .retroarch import build as retroarch_build

JOST = Path(__file__).resolve().parent / "fonts" / "Jost-Variable.ttf"
LAYOUT_DIR = Path("resources") / "layout" / "asset" / "sprite"
SPRITES_DIR = Path("resources") / "sprites"


# ------------------------------------------------------------------ production

def build_atlas(ref_root: Path):
    png = Image.open(ref_root / SPRITES_DIR / "packed.png").convert("RGBA")
    js = json.loads((ref_root / SPRITES_DIR / "packed.json").read_text(encoding="utf-8"))
    overrides = {}
    for name, spr in js["sprites"].items():
        if name in art.KEEP:
            continue
        x, y, w, h = spr["textureFrame"]
        ref = png.crop((x, y, x + w, y + h)) if name in art.RECOLOR else None
        overrides[name] = art.draw(name, tuple(spr["sourceSize"]), ref)
    return atlas.repaint(png, js, overrides), js, overrides


def build_layout(ref_root: Path):
    out = {}
    for name in art.LAYOUT:
        ref = None
        if name in art.RECOLOR:
            ref = Image.open(ref_root / LAYOUT_DIR / name).convert("RGBA")
        out[name] = art.draw(name, art.SIZES[name], ref)
    dot_ref = Image.open(ref_root / SPRITES_DIR / "dot_num.png").convert("RGBA")
    dot = art.draw(art.DOT_NUM, art.SIZES[art.DOT_NUM], dot_ref)
    return out, dot


# ------------------------------------------------------------------ texte

class Fonts:
    """Polices clover générées (s / m / l) + Jost pour le titre (title.font
    reste celle de font_remount sur la console)."""

    def __init__(self, fonts_dir: Path):
        self.clover = {}
        for key in ("s", "m", "l"):
            p = fonts_dir / f"{key}.font"
            if p.is_file():
                f = clover_font.parse(p.read_bytes())
                self.clover[key] = (f, Image.open(io.BytesIO(f.png)).convert("RGBA"))

    def render(self, key, text, color):
        """Texte multiplié par ``color`` (le moteur multiplie le glyphe)."""
        if key in self.clover:
            f, at = self.clover[key]
            tile = font_gen.draw_text(f, at, text)
            r, g, b, a = tile.split()
            mul = [band.point(lambda v, k=k: int(v * k / 255)) for band, k in zip((r, g, b), color)]
            img = Image.merge("RGBA", (*mul, a))
            return img.crop(img.getbbox() or (0, 0, 1, 1))
        size = {"s": 18, "m": 24, "l": 36}.get(key, 18)
        return self.jost(text, size, color)

    @staticmethod
    def jost(text, size, color, weight=500):
        font = ImageFont.truetype(str(JOST), size)
        try:
            font.set_variation_by_axes([weight])
        except Exception:
            pass
        box = font.getbbox(text)
        img = Image.new("RGBA", (box[2] - box[0] + 2, box[3] - box[1] + 2), (0, 0, 0, 0))
        ImageDraw.Draw(img).text((1 - box[0], 1 - box[1]), text, font=font, fill=(*color, 255))
        return img


# ------------------------------------------------------------------ aperçu

class Screen:
    """Écran 1280x720 ; coordonnées des scènes : origine au centre, y vers le haut."""

    def __init__(self, crop, background: Image.Image):
        self.crop = crop  # nom de sprite -> image (atlas repeint)
        self.img = background.copy()

    @staticmethod
    def to_px(x, y):
        return 640 + x, 360 - y

    def put(self, img, x, y, size=None, flip_x=False, flip_y=False):
        if isinstance(img, str):
            img = self.crop(img)
        if size is not None:
            img = img.resize((round(size[0]), round(size[1])), Image.NEAREST)
        if flip_x:
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        if flip_y:
            img = img.transpose(Image.FLIP_TOP_BOTTOM)
        px, py = self.to_px(x, y)
        self.put_tl(img, px - img.width / 2, py - img.height / 2)

    def put_tl(self, img, left, top):
        """Composition alpha, l'image pouvant déborder de l'écran."""
        layer = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
        layer.paste(img, (round(left), round(top)))
        self.img.alpha_composite(layer)


def _background(wall: Image.Image) -> Image.Image:
    """prefab/bg.scn : le mur (512x184 affiché 1536x552, centré en y = -3) est
    rendu dans une cible 320x180 puis agrandi x4 (filtrage au plus proche)."""
    full = Image.new("RGBA", (1280, 720), (0, 0, 0, 255))
    big = wall.resize((1536, 552), Image.NEAREST)
    full.paste(big, (640 - 768, 363 - 276))
    small = Image.new("RGBA", (320, 180))
    src, dst = full.load(), small.load()
    for j in range(180):
        for i in range(320):
            dst[i, j] = src[4 * i + 2, 4 * j + 2]
    return small.resize((1280, 720), Image.NEAREST)


COVERS = [(0x7A, 0x2E, 0x28), (0x2D, 0x4A, 0x7B), (0x2F, 0x6B, 0x3A), (0x4D, 0x2C, 0x6E),
          (0x8A, 0x6A, 0x1F), (0x1F, 0x6F, 0x7A), (0x5A, 0x5A, 0x62)]
TITLES = ["Super Metroid", "F-Zero", "Donkey Kong Country", "Secret of Mana",
          "Kirby Super Star", "Contra III", "Star Fox"]


def _cover(w, h, k, fonts):
    """Jaquette fictive (contenu de jeu, hors thème) : aplat + titre."""
    img = Image.new("RGBA", (w, h), (*COVERS[k % len(COVERS)], 255))
    d = ImageDraw.Draw(img)
    d.rectangle((0, h - 40, w, h), fill=(0, 0, 0, 70))
    label = Fonts.jost(TITLES[k % len(TITLES)], 17, (240, 240, 240), 500)
    img.alpha_composite(label, (10, h - 30))
    return img


def _nine_slice(scr, crop, left, top, right, bottom):
    """Curseur arrondi (cursor_game_1..9) étiré autour de ``[left,right]x[top,bottom]``."""
    c = {i: crop(f"cursor_game_{i}.png") for i in range(1, 10)}
    w, h = right - left, bottom - top
    scr.put_tl(c[1], left, top)
    scr.put_tl(c[3], right - 32, top)
    scr.put_tl(c[7], left, bottom - 32)
    scr.put_tl(c[9], right - 32, bottom - 32)
    scr.put_tl(c[2].resize((w - 64, 32), Image.NEAREST), left + 32, top)
    scr.put_tl(c[8].resize((w - 64, 32), Image.NEAREST), left + 32, bottom - 32)
    scr.put_tl(c[4].resize((32, h - 64), Image.NEAREST), left, top + 32)
    scr.put_tl(c[6].resize((32, h - 64), Image.NEAREST), right - 32, top + 32)


def make_preview(packed: Image.Image, js: dict, layout: dict, fonts: Fonts) -> Image.Image:
    def crop(name):
        x, y, w, h = js["sprites"][name]["textureFrame"]
        return packed.crop((x, y, x + w, y + h))

    scr = Screen(crop, _background(layout["wallpaper/wall.png"]))

    # cartes de la grille (elements/cardlist : pas de 262, y = 0)
    for k in range(-3, 4):
        x = 262 * k
        active = k == 0
        scr.put("game_card_active.png" if active else "game_card.png", x, 0, (252, 276))
        scr.put(_cover(228, 204, k + 3, fonts), x, 24)
    # curseur arrondi autour de la carte choisie (lavande)
    cl, ct = scr.to_px(-126, 138)
    _nine_slice(scr, crop, cl - 22, ct - 22, cl + 252 + 22, ct + 276 + 22)

    # vignettes (thumbnails, y = -191) et marqueur animé (curor_thumbnail, x3, retourné)
    n, sel, step = 21, 10, 40
    for i in range(n):
        x = (i - sel) * step
        if i == 14:
            scr.put("thumbnail_lock.png", x, -191)
        else:
            th = Image.new("RGBA", (32, 32), (*COVERS[i % len(COVERS)], 255))
            scr.put(th, x, -191)
    scr.put("curor_thumbnail_1.png", 0, -145, (36, 24), flip_y=True)

    # barres haut / bas
    scr.put("menubar_U.png", 0, 324, (1296, 120))
    scr.put("menubar_U_2.png", 0, 324, (480, 120))
    for x, icon in [(-192, "btn_display.png"), (-96, "btn_option.png"),
                    (0, "btn_language.png"), (96, "btn_copyright.png"), (192, "manual.png")]:
        scr.put(icon, x, 298, (72, 72))
    scr.put("menubar_L.png", 0, -330, (1296, 120))

    # titre du jeu : plaque caption_title x3, texte noir (font_home_gametitle)
    scr.put("caption_title.png", 0, 182, (1044, 66))
    title = Fonts.jost(TITLES[3], 40, (0, 0, 0), 500)
    scr.put(title, 0, 184)

    # HUD de la grille (hud_gametitle, s.font blanc), centré à y = -249
    items = [("icon_direction_up_light.png", (36, 36), "Menu"),
             ("icon_direction_down_light.png", (36, 36), "Fichiers de sauvegarde"),
             ("icon_select_light.png", (96, 30), "Trier"),
             ("icon_start_light.png", (84, 30), "Démarrer le jeu")]
    parts = []
    for icon, size, text in items:
        parts.append((crop(icon).resize(size, Image.NEAREST),
                      fonts.render("s", text, (255, 255, 255))))
    gap_in, gap_out = 10, 42
    total = sum(i.width + gap_in + t.width for i, t in parts) + gap_out * (len(parts) - 1)
    px, py = 640 - total // 2, 360 + 249
    for icon, label in parts:
        scr.put_tl(icon, px, py - icon.height // 2)
        px += icon.width + gap_in
        scr.put_tl(label, px, py - label.height // 2)
        px += label.width + gap_out
    return scr.img.convert("RGB")


# ------------------------------------------------------------------ planche

def make_sheet(overrides: dict, layout: dict, dot: Image.Image) -> Image.Image:
    items = list(overrides.items()) + list(layout.items()) + [(art.DOT_NUM, dot)]
    label_font = ImageFont.truetype(str(JOST), 13)
    tiles = []
    for name, img in items:
        w, h = img.size
        z = 4 if max(w, h) <= 48 else (2 if max(w, h) <= 300 else 1)
        body = img.resize((w * z, h * z), Image.NEAREST)
        cap = f"{name}  {w}x{h}  x{z}"
        tw = int(label_font.getlength(cap)) + 4
        tile = Image.new("RGBA", (max(body.width, tw) + 8, body.height + 26), (*P.SLATE, 255))
        d = ImageDraw.Draw(tile)
        d.text((4, 2), cap, font=label_font, fill=(*P.TEXT_DIM, 255))
        # cadre fin pour situer les sprites transparents ou ardoise
        d.rectangle((3, 21, 4 + body.width, 22 + body.height), outline=(*P.RULE, 255))
        tile.alpha_composite(body, (4, 22))
        tiles.append(tile)
    width = 1600
    x = y = row_h = 0
    pos = []
    for t in tiles:
        if x + t.width > width:
            x, y, row_h = 0, y + row_h + 6, 0
        pos.append((x, y))
        x += t.width + 6
        row_h = max(row_h, t.height)
    sheet = Image.new("RGBA", (width, y + row_h + 8), (*P.SLATE, 255))
    for t, p in zip(tiles, pos):
        sheet.alpha_composite(t, p)
    return sheet.convert("RGB")


# ------------------------------------------------------------------ CLI

REPO_ROOT = Path(__file__).resolve().parents[1]


def _inside(path: Path, root: Path) -> bool:
    path, root = Path(path).resolve(), Path(root).resolve()
    return path == root or root in path.parents


def check_out_dir(ref_root: Path, out: Path) -> None:
    """Refuse une sortie egale a / dans la reference Nintendo, ou dans le depot."""
    if _inside(out, ref_root):
        raise SystemExit(f"--out ({out}) ne doit pas etre dans --ref ({ref_root})")
    if _inside(out, REPO_ROOT):
        raise SystemExit(f"--out ({out}) ne doit pas etre dans le depot ({REPO_ROOT})")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--ref", required=True, type=Path, help="copie de /usr/share/ui/snes-eur")
    ap.add_argument("--out", required=True, type=Path, help="dossier de sortie (theme_out)")
    args = ap.parse_args(argv)
    ref_root, out = args.ref, args.out
    check_out_dir(ref_root, out)

    packed, js, overrides = build_atlas(ref_root)
    layout, dot = build_layout(ref_root)

    (out / SPRITES_DIR).mkdir(parents=True, exist_ok=True)
    packed.save(out / SPRITES_DIR / "packed.png")
    dot.save(out / SPRITES_DIR / "dot_num.png")
    for name, img in layout.items():
        dest = out / LAYOUT_DIR / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        img.save(dest)

    fonts = Fonts(out / "fonts")
    make_preview(packed, js, layout, fonts).save(out / "preview_home.png")
    make_sheet(overrides, layout, dot).save(out / "sprites_sheet.png")
    retroarch_build.build(out)
    print(f"atlas : {len(overrides)} sprites repeints, {len(art.KEEP)} laissés")
    print(f"layout : {len(layout)} images ; sorties dans {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
