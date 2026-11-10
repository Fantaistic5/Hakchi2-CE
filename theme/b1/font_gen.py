"""Generateur des polices du menu B1 : Jost au format clover-ui.

Pour chaque police de reference (``s.font``, ``m.font``, ``l.font``,
``copyright.fnt``, ``title.font``) on garde exactement les memes points de
code, ``lineHeight``, ``base`` et une page de taille <= a l'originale, de
sorte que la mise en page figee de ``clover-ui`` ne bouge pas. Les glyphes
sont rendus avec Pillow en blanc pre-multiplie (RGB == alpha, comme les
atlas d'origine) : la couleur vient du multiplicateur du menu.

Jost est utilise pour tout point de code qu'il couvre ; le reste (kana, kanji,
symboles) est rendu depuis une police de repli (``--fallback``).

CLI ::

    python -m theme.b1.font_gen --ref P:\\HakchiSNESUSA\\ui_ref --out <dir>
"""
from __future__ import annotations

import argparse
import io
import json
import sys
from dataclasses import dataclass, replace
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from theme.b1 import clover_font as cf

DEFAULT_JOST = Path(__file__).resolve().parents[1] / "fonts" / "Jost-Variable.ttf"
DEFAULT_FALLBACK = r"C:\Windows\Fonts\YuGothM.ttc"

WEIGHTS = {"thin": 100, "extralight": 200, "light": 300, "regular": 400,
           "medium": 500, "semibold": 600, "bold": 700, "extrabold": 800, "black": 900}

# (fichier, graisse Jost) : Medium lit bien sur TV pour les textes courants,
# SemiBold pour le titre, Regular pour les longues mentions legales.
PLAN = [("s.font", "Medium"), ("m.font", "Medium"), ("l.font", "Medium"),
        ("copyright.fnt", "Regular"), ("title.font", "SemiBold")]


def resolve_weight(weight) -> int:
    if isinstance(weight, (int, float)):
        return int(weight)
    key = str(weight).strip().lower()
    if key in WEIGHTS:
        return WEIGHTS[key]
    try:
        return int(key)
    except ValueError:
        raise ValueError(f"graisse inconnue : {weight!r}") from None


@dataclass
class Report:
    jost: int = 0
    fallback: int = 0
    blank: int = 0      # espaces / controles : cellule transparente
    missing: int = 0    # ni Jost ni repli : cellule transparente
    px: int = 0         # corps effectif de rendu
    start_px: int = 0   # corps vise (hauteur de capitale d'origine)
    gap: int = 0


_cmap_cache: dict = {}


def _cmap(path: str) -> set:
    if path not in _cmap_cache:
        from fontTools.ttLib import TTFont
        tt = TTFont(path, fontNumber=0, lazy=True)
        _cmap_cache[path] = set(tt.getBestCmap())
        tt.close()
    return _cmap_cache[path]


def _load(path: str, px: int, weight=None) -> ImageFont.FreeTypeFont:
    f = ImageFont.truetype(path, px, index=0, layout_engine=ImageFont.Layout.BASIC)
    if weight is not None:
        f.set_variation_by_axes([weight])
    return f


def _render(font: ImageFont.FreeTypeFont, ch: str, px: int):
    """-> (image L rognee ou None, gauche, haut, avance) ; gauche/haut relatifs a l'origine sur la ligne de base."""
    adv = font.getlength(ch)
    ox, oy = px * 2, px * 3
    canvas = Image.new("L", (int(adv) + px * 4, px * 5), 0)
    ImageDraw.Draw(canvas).text((ox, oy), ch, font=font, fill=255, anchor="ls")
    box = canvas.getbbox()
    if box is None:
        return None, 0, 0, adv
    return canvas.crop(box), box[0] - ox, box[1] - oy, adv


def _cap_scale(ref: cf.CloverFont, jost: ImageFont.FreeTypeFont, px: int) -> float:
    """Reduit le corps si la hauteur de capitale de Jost depasse celle de la reference."""
    g = ref.chars.get(ord("H"))
    if g is None:
        return 1.0
    pad = ref.info.padding
    ref_cap = g.height - pad[0] - pad[2]
    top = jost.getbbox("H", anchor="ls")[1]
    cap = -top
    if ref_cap <= 0 or cap <= 0:
        return 1.0
    return min(1.0, ref_cap / cap)


def _pack(sizes: dict, width: int, height: int, gap: int):
    order = sorted(sizes, key=lambda k: (-sizes[k][1], -sizes[k][0], k))
    pos, x, y, shelf = {}, 0, 0, 0
    for k in order:
        w, h = sizes[k]
        if x + w > width:
            x, y, shelf = 0, y + shelf + gap, 0
        if w > width or y + h > height:
            raise ValueError(
                f"les glyphes ne tiennent pas dans la page {width}x{height} (echec sur U+{k:04X})")
        pos[k] = (x, y)
        x += w + gap
        shelf = max(shelf, h)
    return pos


def _attempt(reference, ttf_path, w, fallback_path, jost_cm, fb_cm, px):
    jost = _load(str(ttf_path), px, w)
    fb = _load(str(fallback_path), px) if fallback_path else None
    pad_t, pad_r, pad_b, pad_l = reference.info.padding
    gap = 0 if any(reference.info.padding) else 1
    base = reference.common.base
    rep = Report(px=px, gap=gap)

    rendered = {}  # id -> (img|None, xoffset, yoffset, xadvance, blank_w, blank_h)
    for cp, ref_g in reference.chars.items():
        ch = chr(cp)
        font = None
        if cp > 32 and cp in jost_cm:
            font, kind = jost, "jost"
        elif cp > 32 and cp in fb_cm:
            font, kind = fb, "fallback"
        if font is None:
            if cp <= 32 or ch.isspace():
                rep.blank += 1
                adv = round(jost.getlength(" ")) if cp == 32 else ref_g.xadvance
            else:
                rep.missing += 1
                adv = ref_g.xadvance
            rendered[cp] = (None, 0, ref_g.yoffset, adv, ref_g.width, ref_g.height)
            continue
        img, left, top, adv = _render(font, ch, px)
        if img is None:  # glyphe sans encre (ex. U+3000)
            rep.blank += 1
            rendered[cp] = (None, 0, ref_g.yoffset, round(adv), ref_g.width, ref_g.height)
            continue
        setattr(rep, kind, getattr(rep, kind) + 1)
        rendered[cp] = (img, left - pad_l, base + top - pad_t, round(adv), 0, 0)

    sizes = {}
    for cp, (img, _xo, _yo, _adv, bw, bh) in rendered.items():
        sizes[cp] = ((img.width + pad_l + pad_r, img.height + pad_t + pad_b) if img else (bw, bh))
    pos = _pack(sizes, reference.common.scale_w, reference.common.scale_h, gap)

    atlas = Image.new("L", (reference.common.scale_w, reference.common.scale_h), 0)
    chars = {}
    for cp, (img, xo, yo, adv, _bw, _bh) in rendered.items():
        x, y = pos[cp]
        wd, ht = sizes[cp]
        if img is not None:
            atlas.paste(img, (x + pad_l, y + pad_t))
        chars[cp] = cf.Glyph(cp, x, y, wd, ht, xo, yo, adv, 0, reference.chars[cp].chnl)
    return atlas, chars, rep


def generate_with_report(reference: cf.CloverFont, ttf_path: str, weight,
                         fallback_path: str | None = None):
    """-> (CloverFont, Report). Le corps de rendu part de celui qui egale la
    hauteur de capitale d'origine (jamais plus grand que le corps nominal) et
    est reduit tant que les glyphes ne tiennent pas dans la page d'origine ;
    la page n'est jamais agrandie (``Report.px`` donne le corps retenu)."""
    w = resolve_weight(weight)
    size = reference.info.size
    jost_cm = _cmap(str(ttf_path))
    fb_cm = _cmap(str(fallback_path)) if fallback_path else set()

    probe = _load(str(ttf_path), size, w)
    start = max(4, round(size * _cap_scale(reference, probe, size)))
    err = None
    for px in range(start, max(3, int(start * 0.75)), -1):
        try:
            atlas, chars, rep = _attempt(reference, ttf_path, w, fallback_path, jost_cm, fb_cm, px)
            break
        except ValueError as e:
            err = e
    else:
        raise ValueError(f"page trop petite meme a {px} px : {err}")
    rep.start_px = start

    rgba = Image.merge("RGBA", (atlas, atlas, atlas, atlas))
    buf = io.BytesIO()
    rgba.save(buf, "PNG", optimize=True)
    out = cf.CloverFont(
        info=replace(reference.info),
        common=replace(reference.common),
        pages=list(reference.pages),
        chars=chars,
        png=buf.getvalue(),
        kernings=None,
    )
    return out, rep


def generate(reference: cf.CloverFont, ttf_path: str, weight,
             fallback_path: str | None = None) -> cf.CloverFont:
    return generate_with_report(reference, ttf_path, weight, fallback_path)[0]


# --------------------------------------------------------------------- apercu

def draw_text(font: cf.CloverFont, atlas: Image.Image, text: str):
    """Rend ``text`` avec la police generee : image RGBA pre-multipliee (blanc)."""
    lh = font.common.line_height
    width = sum(font.chars[ord(c)].xadvance for c in text if ord(c) in font.chars) + 8
    out = Image.new("RGBA", (max(width, 1), lh + 6), (0, 0, 0, 0))
    pen = 2
    for c in text:
        g = font.chars.get(ord(c))
        if g is None:
            continue
        if g.width > 2 or g.height > 2:
            tile = atlas.crop((g.x, g.y, g.x + g.width, g.y + g.height))
            out.alpha_composite(tile, (pen + g.xoffset, 3 + g.yoffset))
        pen += g.xadvance
    return out


def _phrases(strings_root: Path, lang: str, font: cf.CloverFont, count: int):
    path = strings_root / lang / "strings.lng"
    if not path.is_file():
        return []
    data = json.loads(path.read_text(encoding="utf-8")).get("strings", {})
    ok = [v for v in data.values() if isinstance(v, str) and 10 <= len(v) <= 44
          and "\n" not in v and all(ord(c) in font.chars for c in v)]
    ok = sorted(set(ok))
    if len(ok) <= count:
        return ok
    step = len(ok) / count
    return [ok[int(i * step)] for i in range(count)]


def make_preview(fonts: dict, strings_root: Path, out_path: Path) -> None:
    """Planche FR/EN (+ RU, JA quand couverts) : blanc sur #1C1B22."""
    groups = [("eur_fr", 4), ("usa_en", 3), ("eur_ru", 1), ("jpn_ja", 1)]
    bg = (0x1C, 0x1B, 0x22, 255)
    rows = []
    for name, font in fonts.items():
        atlas = Image.open(io.BytesIO(font.png)).convert("RGBA")
        rows.append(("label", f"{name}  ({font.info.size} px, ligne {font.common.line_height})"))
        seen = set()
        for lang, n in groups:
            for ph in _phrases(strings_root, lang, font, n):
                if ph in seen:
                    continue
                seen.add(ph)
                rows.append((lang, draw_text(font, atlas, ph)))
    label_font = ImageFont.truetype(str(DEFAULT_JOST), 14)
    width = 760
    height = sum(30 if k == "label" else v.height + 4 for k, v in rows) + 20
    sheet = Image.new("RGBA", (width, height), bg)
    d = ImageDraw.Draw(sheet)
    y = 10
    for kind, item in rows:
        if kind == "label":
            y += 6
            d.text((10, y), item, font=label_font, fill=(0xA7, 0x9F, 0xD6, 255))
            y += 24
        else:
            tile = item
            # le menu multiplie le glyphe blanc par la couleur de texte B1
            col = Image.new("RGBA", tile.size, (0xE4, 0xE2, 0xEA, 255))
            col.putalpha(tile.getchannel("A"))
            sheet.alpha_composite(col, (14, y))
            y += tile.height + 4
    sheet.convert("RGB").save(out_path)


# ------------------------------------------------------------------------ CLI

def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Genere les polices Jost du menu B1")
    ap.add_argument("--ref", required=True, help="dossier ui_ref (contenant resources/) ou resources/fonts")
    ap.add_argument("--out", required=True, help="dossier de sortie")
    ap.add_argument("--jost", default=str(DEFAULT_JOST), help="TTF variable Jost")
    ap.add_argument("--fallback", default=DEFAULT_FALLBACK,
                    help="TTF/TTC de repli pour les points de code absents de Jost")
    ap.add_argument("--no-preview", action="store_true")
    args = ap.parse_args(argv)

    ref_root = Path(args.ref)
    fonts_dir = ref_root / "resources" / "fonts"
    if not fonts_dir.is_dir():
        fonts_dir = ref_root
    strings_root = fonts_dir.parent / "strings"
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    fallback = args.fallback if args.fallback and Path(args.fallback).is_file() else None
    if args.fallback and fallback is None:
        print(f"police de repli introuvable : {args.fallback}", file=sys.stderr)

    generated = {}
    for name, weight in PLAN:
        ref = cf.parse((fonts_dir / name).read_bytes())
        font, rep = generate_with_report(ref, args.jost, weight, fallback)
        (out_dir / name).write_bytes(cf.build(font))
        generated[name] = font
        print(f"{name}: {len(font.chars)} glyphes, Jost {weight} {rep.px}px : "
              f"(vise {rep.start_px}) Jost={rep.jost} repli={rep.fallback} vides={rep.blank} manquants={rep.missing} "
              f"page {font.common.scale_w}x{font.common.scale_h}")
    if not args.no_preview and strings_root.is_dir():
        make_preview(generated, strings_root, out_dir / "fonts_preview.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
