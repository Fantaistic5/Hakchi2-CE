"""Codec des polices clover-ui (``.font`` / ``.fnt``) de la SNES Mini.

Conteneur (vérifié sur l, m, s, title, copyright, debug) ::

    offset 0   u8      0x01 (version)
    offset 1   u32 LE  taille du bloc BMFont (en-tete "BMF\x03" inclus)
    offset 5   u32 LE  taille du bloc PNG
    offset 9   BMFont binaire v3 : "BMF" + 0x03, puis blocs (type u8, taille u32)
               1 info / 2 common / 3 pages / 4 chars / 5 kerning (optionnel)
    ensuite    PNG de l'atlas (une seule page ; aucun octet apres)

Les deux tailles sont lues comme des u32 (les octets 4 et 8 valent toujours 0,
ce qui correspond au « 24 bits + 00 » observe). Les blocs sont toujours dans
l'ordre 1,2,3,4,(5) ; ``parse`` refuse tout autre agencement afin que
``build(parse(x)) == x`` reste vrai octet pour octet.
"""
from __future__ import annotations

import struct
from dataclasses import dataclass, field

_INFO = struct.Struct("<hBBHBBBBBBBB")   # size..outline (14 octets) + nom
_COMMON = struct.Struct("<HHHHHBBBBB")
_CHAR = struct.Struct("<IHHHHhhhBB")
_KERN = struct.Struct("<IIh")


@dataclass
class Info:
    size: int
    bit_field: int
    charset: int
    stretch_h: int
    aa: int
    padding: tuple  # haut, droite, bas, gauche
    spacing: tuple  # horizontal, vertical
    outline: int
    face: str


@dataclass
class Common:
    line_height: int
    base: int
    scale_w: int
    scale_h: int
    pages: int
    bit_field: int
    alpha_chnl: int
    red_chnl: int
    green_chnl: int
    blue_chnl: int


@dataclass
class Glyph:
    id: int
    x: int
    y: int
    width: int
    height: int
    xoffset: int
    yoffset: int
    xadvance: int
    page: int
    chnl: int


@dataclass
class CloverFont:
    info: Info
    common: Common
    pages: list
    chars: dict
    png: bytes
    kernings: list | None = None  # liste de (first, second, amount) ; None = pas de bloc 5


def _cstr(raw: bytes) -> str:
    if not raw.endswith(b"\x00"):
        raise ValueError("chaine non terminee par un NUL")
    return raw[:-1].decode("utf-8")


def parse(data: bytes) -> CloverFont:
    if len(data) < 13 or data[0] != 1:
        raise ValueError("en-tete clover-ui invalide")
    bmf_len, png_len = struct.unpack_from("<II", data, 1)
    if 9 + bmf_len + png_len != len(data):
        raise ValueError("tailles de l'en-tete incoherentes avec le fichier")
    bmf = data[9:9 + bmf_len]
    png = data[9 + bmf_len:]
    if bmf[:4] != b"BMF\x03":
        raise ValueError("BMFont binaire v3 attendu")
    blocks = {}
    order = []
    p = 4
    while p < len(bmf):
        if p + 5 > len(bmf):
            raise ValueError("bloc BMFont tronque")
        t = bmf[p]
        n = struct.unpack_from("<I", bmf, p + 1)[0]
        body = bmf[p + 5:p + 5 + n]
        if len(body) != n:
            raise ValueError("bloc BMFont tronque")
        blocks[t] = body
        order.append(t)
        p += 5 + n
    if order not in ([1, 2, 3, 4], [1, 2, 3, 4, 5]):
        raise ValueError(f"agencement de blocs non supporte : {order}")

    f = _INFO.unpack_from(blocks[1], 0)
    info = Info(f[0], f[1], f[2], f[3], f[4], tuple(f[5:9]), tuple(f[9:11]), f[11],
                _cstr(blocks[1][_INFO.size:]))
    common = Common(*_COMMON.unpack(blocks[2]))
    if common.pages != 1:
        raise ValueError("une seule page attendue")
    pages_raw = blocks[3]
    if not pages_raw.endswith(b"\x00"):
        raise ValueError("bloc pages invalide")
    names = [s.decode("utf-8") for s in pages_raw[:-1].split(b"\x00")]
    if len(names) != common.pages:
        raise ValueError("nombre de pages incoherent")
    if len(blocks[4]) % _CHAR.size:
        raise ValueError("bloc chars invalide")
    chars = {}
    for off in range(0, len(blocks[4]), _CHAR.size):
        g = Glyph(*_CHAR.unpack_from(blocks[4], off))
        if g.id in chars:
            raise ValueError(f"glyphe en double : {g.id}")
        chars[g.id] = g
    kernings = None
    if 5 in blocks:
        if len(blocks[5]) % _KERN.size:
            raise ValueError("bloc kerning invalide")
        kernings = [_KERN.unpack_from(blocks[5], o) for o in range(0, len(blocks[5]), _KERN.size)]
    return CloverFont(info, common, names, chars, png, kernings)


def _block(t: int, body: bytes) -> bytes:
    return struct.pack("<BI", t, len(body)) + body


def build(font: CloverFont) -> bytes:
    i = font.info
    info = _INFO.pack(i.size, i.bit_field, i.charset, i.stretch_h, i.aa,
                      *i.padding, *i.spacing, i.outline) + i.face.encode("utf-8") + b"\x00"
    c = font.common
    common = _COMMON.pack(c.line_height, c.base, c.scale_w, c.scale_h, c.pages,
                          c.bit_field, c.alpha_chnl, c.red_chnl, c.green_chnl, c.blue_chnl)
    pages = b"".join(n.encode("utf-8") + b"\x00" for n in font.pages)
    chars = b"".join(
        _CHAR.pack(g.id, g.x, g.y, g.width, g.height, g.xoffset, g.yoffset,
                   g.xadvance, g.page, g.chnl) for g in font.chars.values())
    bmf = b"BMF\x03" + _block(1, info) + _block(2, common) + _block(3, pages) + _block(4, chars)
    if font.kernings is not None:
        bmf += _block(5, b"".join(_KERN.pack(*k) for k in font.kernings))
    return struct.pack("<BII", 1, len(bmf), len(font.png)) + bmf + font.png
