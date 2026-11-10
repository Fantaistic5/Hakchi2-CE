"""Tests du generateur de polices B1 (Jost au format clover-ui)."""
import io
import json
from pathlib import Path

import pytest
from PIL import Image

from theme.b1 import clover_font as cf
from theme.b1 import font_gen as fg
from theme.tests.test_clover_font import make_font

JOST = Path(__file__).resolve().parents[1] / "fonts" / "Jost-Variable.ttf"
UI_REF = Path(r"P:\HakchiSNESUSA\ui_ref")
FONTS = UI_REF / "resources" / "fonts"
NEEDS_REF = pytest.mark.skipif(not FONTS.is_dir(), reason="ui_ref absent")
NAMES = ["s.font", "m.font", "l.font", "copyright.fnt", "title.font"]


def _synthetic_ref():
    ref = make_font(kernings=False)
    ref.chars = {32: cf.Glyph(32, 0, 0, 2, 2, 0, 17, 6, 0, 15)}
    for cp in [0x41, 0x67, 0xE9, 0x416, 0x30]:
        ref.chars[cp] = cf.Glyph(cp, 0, 0, 1, 1, 0, 0, 1, 0, 15)
    ref.common.scale_w = ref.common.scale_h = 128
    ref.common.line_height, ref.common.base = 22, 17
    ref.info.padding = (1, 1, 1, 1)
    ref.info.spacing = (0, 0)
    return ref


def test_synthetic_same_codepoints_and_metrics():
    ref = _synthetic_ref()
    out = fg.generate(ref, str(JOST), "Medium")
    assert set(out.chars) == set(ref.chars)
    assert out.common.line_height == ref.common.line_height
    assert out.common.base == ref.common.base
    assert out.common.scale_w <= ref.common.scale_w
    assert out.common.scale_h <= ref.common.scale_h
    assert cf.parse(cf.build(out)) == out


def test_synthetic_glyphs_inside_page_and_white_alpha():
    out = fg.generate(_synthetic_ref(), str(JOST), "SemiBold")
    im = Image.open(io.BytesIO(out.png))
    assert im.size == (out.common.scale_w, out.common.scale_h) and im.mode == "RGBA"
    for g in out.chars.values():
        assert g.x >= 0 and g.y >= 0
        assert g.x + g.width <= im.width and g.y + g.height <= im.height
    px = list(im.get_flattened_data())
    assert all(p[0] == p[1] == p[2] == p[3] for p in px)  # blanc pre-multiplie
    assert any(p[3] > 200 for p in px)
    a = out.chars[0x41]
    assert a.width > 2 and a.height > 2 and 0 < a.xadvance < 30


def test_weight_changes_rendering():
    ref = _synthetic_ref()
    assert fg.generate(ref, str(JOST), "Regular").png != fg.generate(ref, str(JOST), "SemiBold").png
    assert fg.resolve_weight("Medium") == 500 and fg.resolve_weight("650") == 650
    with pytest.raises(ValueError):
        fg.resolve_weight("Fat")


def test_glyph_cells_do_not_overlap():
    out = fg.generate(_synthetic_ref(), str(JOST), "Medium")
    cells = [(g.x, g.y, g.x + g.width, g.y + g.height) for g in out.chars.values()]
    for i, a in enumerate(cells):
        for b in cells[i + 1:]:
            assert a[2] <= b[0] or b[2] <= a[0] or a[3] <= b[1] or b[3] <= a[1]


def test_page_too_small_is_reported():
    ref = _synthetic_ref()
    ref.common.scale_w = ref.common.scale_h = 8
    with pytest.raises(ValueError, match="page"):
        fg.generate(ref, str(JOST), "Medium")


def test_fallback_used_for_missing_codepoints():
    ref = _synthetic_ref()
    ref.chars[0x3042] = cf.Glyph(0x3042, 0, 0, 1, 1, 0, 0, 1, 0, 15)  # hiragana : absent de Jost
    ref.common.scale_w = ref.common.scale_h = 256
    yu = Path(r"C:\Windows\Fonts\YuGothM.ttc")
    if not yu.exists():
        pytest.skip("YuGothM.ttc absent")
    out, rep = fg.generate_with_report(ref, str(JOST), "Medium", fallback_path=str(yu))
    assert rep.fallback == 1 and rep.missing == 0 and rep.jost == len(ref.chars) - 1 - rep.blank
    assert out.chars[0x3042].width > 4
    out2, rep2 = fg.generate_with_report(ref, str(JOST), "Medium", fallback_path=None)
    assert rep2.missing == 1


@NEEDS_REF
@pytest.mark.parametrize("name", NAMES)
def test_reference_metrics_and_codepoints(name):
    ref = cf.parse((FONTS / name).read_bytes())
    weight = {"title.font": "SemiBold", "copyright.fnt": "Regular"}.get(name, "Medium")
    out = fg.generate(ref, str(JOST), weight)
    assert set(out.chars) == set(ref.chars)  # test_same_codepoints
    assert out.common.line_height == ref.common.line_height  # test_metrics_preserved
    assert out.common.base == ref.common.base
    assert out.common.scale_w <= ref.common.scale_w and out.common.scale_h <= ref.common.scale_h
    im = Image.open(io.BytesIO(out.png))
    assert im.size == (out.common.scale_w, out.common.scale_h)
    for g in out.chars.values():  # test_glyph_fits_page
        assert 0 <= g.x and 0 <= g.y and g.x + g.width <= im.width and g.y + g.height <= im.height
    assert cf.parse(cf.build(out)) == out


def _lng_chars(lang):
    raw = (UI_REF / "resources" / "strings" / lang / "strings.lng").read_text(encoding="utf-8")
    data = json.loads(raw)["strings"]
    return {c for v in data.values() if isinstance(v, str) for c in v}


@NEEDS_REF
@pytest.mark.parametrize("name", ["s.font", "m.font", "l.font"])
def test_menu_strings_covered(name):
    """Tout caractere des strings.lng que la police d'origine savait afficher
    doit l'etre aussi par la police generee (memes points de code)."""
    ref = cf.parse((FONTS / name).read_bytes())
    out = fg.generate(ref, str(JOST), "Medium")
    for lang in ["eur_fr", "usa_en", "eur_de", "eur_es", "eur_it", "eur_nl", "eur_pt", "eur_ru"]:
        wanted = {c for c in _lng_chars(lang) if ord(c) in ref.chars}
        assert all(ord(c) in out.chars for c in wanted), lang
        assert {c for c in wanted if c.isalpha()}, lang


def test_baseline_synthetic():
    ref = _synthetic_ref()
    ref.chars = {c: cf.Glyph(c, 0, 0, 1, 1, 0, 0, 1, 0, 15) for c in (0x48, 0x67)}
    ref.common.scale_w = ref.common.scale_h = 128
    ref.common.base = 17
    out = fg.generate(ref, str(JOST), "Medium")
    pad_b = out.info.padding[2]
    h = out.chars[0x48]
    assert abs(h.yoffset + h.height - pad_b - out.common.base) <= 1
    g = out.chars[0x67]
    assert g.yoffset + g.height - pad_b > out.common.base


@NEEDS_REF
@pytest.mark.parametrize("name", ["s.font", "m.font"])
def test_cap_height_matches_reference(name):
    ref = cf.parse((FONTS / name).read_bytes())
    out = fg.generate(ref, str(JOST), "Medium")
    ink = lambda f: f.chars[0x48].height - f.info.padding[0] - f.info.padding[2]
    assert abs(ink(out) - ink(ref)) <= 1
