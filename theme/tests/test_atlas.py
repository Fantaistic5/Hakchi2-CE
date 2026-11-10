"""Tests du repeintre d'atlas a cadres fixes (atlas fabrique en memoire)."""
import copy

import pytest
from PIL import Image

from theme.b1 import atlas as at


def make_atlas():
    """Atlas 64x32 fabrique : 3 sprites, pixels de fond distincts."""
    png = Image.new("RGBA", (64, 32), (10, 20, 30, 255))
    # sprite A en (0,0) 8x8 ; B en (16,4) 4x6 ; C en (40,20) 10x5
    for x, y, w, h, col in [
        (0, 0, 8, 8, (200, 0, 0, 255)),
        (16, 4, 4, 6, (0, 200, 0, 255)),
        (40, 20, 10, 5, (0, 0, 200, 255)),
    ]:
        png.paste(Image.new("RGBA", (w, h), col), (x, y))
    json_dict = {
        "texture": {"width": 64, "height": 32},
        "sprites": {
            "a.png": {
                "textureFrame": [0, 0, 8, 8],
                "pivot": [0.5, 0.5],
                "sourceSize": [8, 8],
                "sourceOffset": [0, 0],
            },
            "b.png": {
                "textureFrame": [16, 4, 4, 6],
                "pivot": [0.5, 0.5],
                "sourceSize": [4, 6],
                "sourceOffset": [0, 0],
            },
            "c.png": {
                "textureFrame": [40, 20, 10, 5],
                "pivot": [0.5, 0.5],
                "sourceSize": [10, 5],
                "sourceOffset": [0, 0],
            },
        },
    }
    return png, json_dict


def test_untouched_frames_identical():
    png, js = make_atlas()
    ov = {"b.png": Image.new("RGBA", (4, 6), (255, 255, 0, 255))}
    out = at.repaint(png, js, ov)
    # tout pixel hors du cadre de b.png est identique a l'entree
    for x in range(64):
        for y in range(32):
            inside = 16 <= x < 20 and 4 <= y < 10
            if not inside:
                assert out.getpixel((x, y)) == png.getpixel((x, y)), (x, y)


def test_override_pasted_exactly():
    png, js = make_atlas()
    src = Image.new("RGBA", (10, 5))
    for x in range(10):
        for y in range(5):
            # alpha partiel pour verifier le remplacement (pas de composition)
            src.putpixel((x, y), (x * 20, y * 40, 7, (x + y) * 10))
    out = at.repaint(png, js, {"c.png": src})
    for x in range(10):
        for y in range(5):
            assert out.getpixel((40 + x, 20 + y)) == src.getpixel((x, y))


def test_wrong_size_raises():
    png, js = make_atlas()
    bad = Image.new("RGBA", (9, 8), (0, 0, 0, 255))
    with pytest.raises(ValueError):
        at.repaint(png, js, {"a.png": bad})


def test_output_size_equals_input():
    png, js = make_atlas()
    js_before = copy.deepcopy(js)
    out = at.repaint(png, js, {"a.png": Image.new("RGBA", (8, 8))})
    assert out.size == png.size
    assert out is not png
    # atlas_json jamais modifie
    assert js == js_before


def test_unknown_override_raises_keyerror():
    png, js = make_atlas()
    with pytest.raises(KeyError):
        at.repaint(png, js, {"nope.png": Image.new("RGBA", (1, 1))})
