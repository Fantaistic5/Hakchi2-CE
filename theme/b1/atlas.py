"""Repeintre d'atlas a cadres fixes (``packed.png`` et sprites ``layout/asset``).

Chaque override remplace exactement le cadre ``textureFrame`` du sprite
correspondant dans l'atlas. Le cadre est d'abord efface (0,0,0,0), puis les
pixels de l'override sont colles tels quels (remplacement, pas de composition
alpha). Aucun pixel hors des cadres remplaces n'est modifie, et ni l'image
d'entree ni ``atlas_json`` ne sont mutes.
"""
from __future__ import annotations

from PIL import Image


def repaint(atlas_png: Image.Image, atlas_json: dict, overrides: dict) -> Image.Image:
    """Renvoie une nouvelle image RGBA de l'atlas avec les overrides colles.

    ``atlas_json`` est le dict complet de ``packed.json`` (cle ``sprites``).
    ``overrides`` associe un nom de sprite (ex. ``"arrow_left.png"``) a une
    image dont la taille doit etre exactement le ``sourceSize`` du sprite.

    Leve ``KeyError`` pour un nom inconnu, ``ValueError`` pour une taille
    incorrecte.
    """
    sprites = atlas_json["sprites"]
    out = atlas_png.convert("RGBA").copy()

    # Verification complete avant toute ecriture : pas de sortie partielle.
    plan = []
    for name, img in overrides.items():
        if name not in sprites:
            raise KeyError(name)
        sprite = sprites[name]
        src_w, src_h = sprite["sourceSize"]
        if img.size != (src_w, src_h):
            raise ValueError(
                f"{name}: taille {img.size} != sourceSize {(src_w, src_h)}"
            )
        x, y, w, h = sprite["textureFrame"]
        plan.append((x, y, w, h, img.convert("RGBA")))

    for x, y, w, h, img in plan:
        out.paste((0, 0, 0, 0), (x, y, x + w, y + h))
        out.paste(img, (x, y))
    return out
