"""Fond du menu XMB de RetroArch en B1 : 1280x720, aplats de la palette, sans
anti-crenelage ni degrade. Ardoise, deux filets en haut (comme le menu TV) et un
petit interrupteur raye violet en bas a droite. Rien d'autre."""
from __future__ import annotations

from PIL import Image, ImageDraw

from theme.b1 import palette as P

SIZE = (1280, 720)
RULES_Y = (88, 96)        # deux filets de 2 px, sous le titre XMB
SWITCH = (1140, 648, 1220, 676)  # x0, y0, x1, y1 (80 x 28)
COLORS = frozenset({P.SLATE, P.RULE, P.VIOLET, P.STRIPE_LIGHT, P.STRIPE_DARK})


def make() -> Image.Image:
    img = Image.new("RGB", SIZE, P.SLATE)
    d = ImageDraw.Draw(img)
    for y in RULES_Y:
        d.rectangle((0, y, SIZE[0] - 1, y + 1), fill=P.RULE)
    x0, y0, x1, y1 = SWITCH
    w, h = x1 - x0, y1 - y0
    # interrupteur : corps violet, trois stries obliques (clair / sombre) decoupees dedans
    body = Image.new("L", (w, h), 0)
    ImageDraw.Draw(body).rounded_rectangle((0, 0, w - 1, h - 1), radius=6, fill=255)
    sw = Image.new("RGB", (w, h), P.VIOLET)
    sd = ImageDraw.Draw(sw)
    for i, col in enumerate((P.STRIPE_LIGHT, P.STRIPE_DARK, P.STRIPE_LIGHT)):
        sx = 34 + i * 14
        sd.polygon([(sx + 10, 0), (sx + 18, 0), (sx + 8, h), (sx, h)], fill=col)
    img.paste(sw, (x0, y0), body)
    return img


def save(path) -> None:
    make().save(path, optimize=True)
