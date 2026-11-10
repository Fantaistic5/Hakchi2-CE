"""Tests des dessins B1 (theme/b1/art.py).

Les dessins procéduraux (DRAWN) et neutralisés (CLEAR) ne dépendent d'aucun
fichier Nintendo. Les sprites « recolorés » (RECOLOR) gardent la forme de
l'original : on les teste ici sur une référence fabriquée (bruit RGBA).
Les vérifications contre ``packed.json`` ne tournent que si ``ui_ref`` existe.
"""
import json
import random
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

from theme.b1 import art
from theme.b1 import palette as P

UI_REF = Path(r"P:\HakchiSNESUSA\ui_ref\resources")
NEEDS_REF = pytest.mark.skipif(not UI_REF.is_dir(), reason="ui_ref absent")
PAL = set(P.ALL.values())


def _pixels(img):
    return [tuple(int(v) for v in p) for p in np.asarray(img.convert("RGBA")).reshape(-1, 4)]


def _lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def _on_segment(rgb, a, b, tol=2.0):
    """rgb est-il un mélange de a et b (à ``tol`` près par canal) ?"""
    best = None
    for t in [i / 255 for i in range(256)]:
        mix = [a[k] * (1 - t) + b[k] * t for k in range(3)]
        err = max(abs(mix[k] - rgb[k]) for k in range(3))
        best = err if best is None else min(best, err)
    return best <= tol


def _palette_violations(img):
    bad = []
    seen = {}
    for px in _pixels(img):
        r, g, b, a = px
        if a == 0:
            continue
        rgb = (r, g, b)
        if a == 255:
            if rgb not in PAL:
                bad.append(px)
            continue
        if rgb in PAL:
            continue
        if rgb not in seen:
            pal = list(PAL)
            seen[rgb] = any(_on_segment(rgb, pal[i], pal[j])
                            for i in range(len(pal)) for j in range(i + 1, len(pal)))
        if not seen[rgb]:
            bad.append(px)
    return bad


def _noise(size, seed=7):
    rnd = random.Random(seed)
    img = Image.new("RGBA", size)
    img.putdata([(rnd.randrange(256), rnd.randrange(256), rnd.randrange(256),
                  rnd.choice([0, 0, 90, 200, 255, 255, 255]))
                 for _ in range(size[0] * size[1])])
    return img


def _render(name):
    size = art.SIZES[name]
    ref = _noise(size) if name in art.RECOLOR else None
    return art.draw(name, size, ref)


def _gradient_triples(img):
    """Trois pixels opaques voisins, trois couleurs distinctes, luminance
    strictement monotone : la signature d'un dégradé."""
    w, h = img.size
    px = img.load()
    hits = []

    def check(seq, where):
        for i in range(len(seq) - 2):
            a, b, c = seq[i:i + 3]
            if None in (a, b, c) or len({a, b, c}) < 3:
                continue
            la, lb, lc = _lum(a), _lum(b), _lum(c)
            if la < lb < lc or la > lb > lc:
                hits.append((where, i))

    for y in range(h):
        check([px[x, y][:3] if px[x, y][3] == 255 else None for x in range(w)], ("row", y))
    for x in range(w):
        check([px[x, y][:3] if px[x, y][3] == 255 else None for y in range(h)], ("col", x))
    return hits


# ---------------------------------------------------------------- palette

@pytest.mark.parametrize("name", sorted(art.DRAWN) + sorted(art.RECOLOR) + sorted(art.CLEAR))
def test_only_palette_colors(name):
    img = _render(name)
    assert img.mode == "RGBA"
    bad = _palette_violations(img)
    assert not bad, f"{name}: {len(bad)} pixels hors palette, ex. {bad[:3]}"


def test_clear_sprites_are_transparent():
    for name in art.CLEAR:
        img = art.draw(name, art.SIZES[name])
        assert img.getchannel("A").getextrema() == (0, 0), name


def test_recolor_needs_reference():
    name = next(iter(sorted(art.RECOLOR)))
    with pytest.raises(ValueError):
        art.draw(name, art.SIZES[name])


def test_unknown_sprite():
    with pytest.raises(KeyError):
        art.draw("nope.png", (4, 4))


def test_wrong_size_rejected():
    with pytest.raises(ValueError):
        art.draw("game_card.png", (10, 10))


# ---------------------------------------------------------------- tailles

@pytest.mark.parametrize("name", sorted(art.SIZES))
def test_sizes_match_json(name):
    if name in art.KEEP:
        return
    img = _render(name)
    assert img.size == tuple(art.SIZES[name])


@NEEDS_REF
def test_size_table_matches_reference():
    js = json.loads((UI_REF / "sprites" / "packed.json").read_text(encoding="utf-8"))
    sprites = js["sprites"]
    # chaque sprite de l'atlas est classé, et à la bonne taille
    for name, spr in sprites.items():
        assert name in art.SIZES, f"{name} non classé"
        assert tuple(art.SIZES[name]) == tuple(spr["sourceSize"]), name
    for name in art.LAYOUT:
        meta = json.loads((UI_REF / "layout" / "asset" / "sprite" / name)
                          .with_suffix(".json").read_text(encoding="utf-8"))
        tex = meta["texture"]
        assert tuple(art.SIZES[name]) == (tex["width"], tex["height"]), name
    layout_dir = UI_REF / "layout" / "asset" / "sprite"
    on_disk = {p.relative_to(layout_dir).as_posix() for p in layout_dir.rglob("*.png")}
    assert on_disk == set(art.LAYOUT)


@NEEDS_REF
def test_real_references_palette_only():
    """Recolorage des vrais sprites d'origine : palette seule (lecture seule de ui_ref)."""
    sp = UI_REF / "sprites"
    png = Image.open(sp / "packed.png").convert("RGBA")
    js = json.loads((sp / "packed.json").read_text(encoding="utf-8"))
    for name, spr in js["sprites"].items():
        if name not in art.RECOLOR:
            continue
        x, y, w, h = spr["textureFrame"]
        out = art.draw(name, (w, h), png.crop((x, y, x + w, y + h)))
        assert not _palette_violations(out), name
    dot = Image.open(sp / "dot_num.png").convert("RGBA")
    assert not _palette_violations(art.draw(art.DOT_NUM, dot.size, dot))


def test_classes_disjoint_and_complete():
    groups = [art.DRAWN, art.RECOLOR, art.CLEAR, art.KEEP]
    total = sum(len(g) for g in groups)
    assert total == len(set().union(*map(set, groups)))
    assert set().union(*map(set, groups)) == set(art.SIZES)


# ---------------------------------------------------------------- dégradés

@pytest.mark.parametrize("name", sorted(art.DRAWN))
def test_no_gradient(name):
    img = _render(name)
    hits = _gradient_triples(img)
    assert not hits, f"{name}: rampe de couleurs en {hits[:4]}"


def test_recolor_flattens_gradient():
    """Un dégradé lisse recoloré n'a pas plus de couleurs que la règle."""
    ramp = Image.new("RGBA", (256, 4))
    ramp.putdata([(x, x, x, 255) for _ in range(4) for x in range(256)])
    for name, rule in art.RECOLOR.items():
        size = art.SIZES[name]
        src = ramp.resize(size, Image.NEAREST)
        out = art.draw(name, size, src)
        opaque = {p[:3] for p in _pixels(out) if p[3] == 255}
        assert len(opaque) <= art.rule_colors(rule), name


# ---------------------------------------------------------------- motifs B1

def test_wall_slate_with_two_rules_and_tileable():
    w, h = art.SIZES["wallpaper/wall.png"]
    img = art.draw("wallpaper/wall.png", (w, h))
    px = img.load()
    rows = [px[0, y][:3] for y in range(h)]
    assert rows.count(P.RULE) >= 2
    # deux filets distincts dans le haut, ardoise partout ailleurs
    rule_rows = [y for y, c in enumerate(rows) if c == P.RULE]
    assert max(rule_rows) < 20
    runs = sum(1 for i, y in enumerate(rule_rows) if i == 0 or y != rule_rows[i - 1] + 1)
    assert runs == 2
    assert all(c == P.SLATE for y, c in enumerate(rows) if y not in rule_rows)
    for y in range(h):  # chaque ligne est uniforme : défilement horizontal sans couture
        assert len({px[x, y] for x in range(w)}) == 1


def test_active_card_has_striped_switch():
    img = art.draw("game_card_active.png", art.SIZES["game_card_active.png"])
    px = img.load()
    strip = [px[x, y][:3] for y in range(72, 88) for x in range(84)]
    assert P.STRIPE_LIGHT in strip and P.STRIPE_DARK in strip
    # bandes de 1 a 2 px qui alternent
    row = [px[x, 80][:3] for x in range(84)]
    stripes = [c for c in row if c in (P.STRIPE_LIGHT, P.STRIPE_DARK)]
    runs, cur = [], 0
    for i, c in enumerate(stripes):
        cur += 1
        if i == len(stripes) - 1 or stripes[i + 1] != c:
            runs.append(cur)
            cur = 0
    assert len(runs) >= 6 and max(runs) <= 2


def test_ab_violet_xy_lavender():
    for n in ("icon_A_light.png", "icon_B_light.png", "icon_A_resume.png", "icon_B_resume.png"):
        img = art.draw(n, art.SIZES[n])
        cols = [p[:3] for p in _pixels(img) if p[3] == 255]
        assert cols.count(P.VIOLET) > cols.count(P.LAVENDER), n
    for n in ("icon_X_light.png", "icon_Y_light.png", "icon_X_resume.png", "icon_Y_resume.png"):
        img = art.draw(n, art.SIZES[n])
        cols = [p[:3] for p in _pixels(img) if p[3] == 255]
        assert cols.count(P.LAVENDER) > cols.count(P.VIOLET), n


def test_title_bar_is_lavender():
    """Validé utilisateur : barre de titre lavande (le titre reste noir)."""
    img = art.draw("caption_title.png", art.SIZES["caption_title.png"])
    opaque = {p[:3] for p in _pixels(img) if p[3] == 255}
    assert opaque == {P.LAVENDER}
