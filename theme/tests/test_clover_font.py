"""Tests du codec de polices clover-ui (.font / .fnt)."""
import struct
import zlib
from pathlib import Path

import pytest

from theme.b1 import clover_font as cf

REF_DIR = Path(r"P:\HakchiSNESUSA\ui_ref\resources\fonts")
REF_FILES = ["l.font", "m.font", "s.font", "copyright.fnt", "title.font", "debug.font"]


def _png(w=2, h=2):
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))

    raw = b"".join(b"\x00" + b"\xff\xff\xff\x80" * w for _ in range(h))
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def make_font(kernings=True):
    return cf.CloverFont(
        info=cf.Info(size=18, bit_field=0, charset=0, stretch_h=100, aa=1,
                     padding=(1, 2, 3, 4), spacing=(1, 1), outline=0, face="Jost"),
        common=cf.Common(line_height=22, base=17, scale_w=2, scale_h=2, pages=1,
                         bit_field=0, alpha_chnl=0, red_chnl=0, green_chnl=0, blue_chnl=0),
        pages=["page0.png"],
        chars={
            65: cf.Glyph(65, 0, 0, 1, 2, 0, 3, 12, 0, 15),
            0x416: cf.Glyph(0x416, 1, 0, 1, 2, -1, 4, 13, 0, 15),
        },
        kernings=[(65, 0x416, -2)] if kernings else None,
        png=_png(),
    )


def test_roundtrip_synthetic():
    font = make_font()
    data = cf.build(font)
    assert data[0] == 1 and data[9:13] == b"BMF\x03"
    assert cf.parse(data) == font
    assert cf.build(cf.parse(data)) == data


def test_roundtrip_synthetic_without_kerning():
    font = make_font(kernings=False)
    data = cf.build(font)
    assert cf.parse(data) == font


def test_parse_rejects_garbage():
    with pytest.raises(ValueError):
        cf.parse(b"\x02" + bytes(20))
    data = bytearray(cf.build(make_font()))
    data[9:12] = b"XYZ"
    with pytest.raises(ValueError):
        cf.parse(bytes(data))


@pytest.mark.skipif(not REF_DIR.is_dir(), reason="ui_ref absent")
@pytest.mark.parametrize("name", REF_FILES)
def test_roundtrip_reference(name):
    data = (REF_DIR / name).read_bytes()
    font = cf.parse(data)
    assert font.png.startswith(b"\x89PNG")
    assert len(font.chars) > 0
    assert cf.build(font) == data


@pytest.mark.skipif(not REF_DIR.is_dir(), reason="ui_ref absent")
def test_reference_counts():
    assert len(cf.parse((REF_DIR / "s.font").read_bytes()).chars) == 117
    assert len(cf.parse((REF_DIR / "m.font").read_bytes()).chars) == 132
    assert len(cf.parse((REF_DIR / "l.font").read_bytes()).chars) == 99
