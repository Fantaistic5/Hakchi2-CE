"""Dessins B1 « Interrupteur » des sprites clover-ui.

Trois familles (voir ``docs/superpowers/notes/theme-ressources.md``) :

* ``DRAWN``   : dessinés ici de zéro (cartes, barres, boutons A/B/X/Y,
  pilules START/SELECT, dialogues, interrupteurs, fonds, cadeau, aperçus).
* ``RECOLOR`` : même forme que l'original, couleurs ramenées à la palette par
  seuils de luminance (``ref`` = le sprite d'origine, lu dans ``ui_ref``).
* ``CLEAR``   : neutralisés (transparents, même taille).
* ``KEEP``    : laissés tels quels (non produits).

Règles de dessin : aplats de la palette uniquement ; bords nets ; seul le
contour extérieur d'une courbe est anticrénelé, par l'alpha d'une couleur de
la palette ; aucun dégradé, halo ni ombre portée.
"""
from __future__ import annotations

import numpy as np
from PIL import Image

from . import palette as P

S, R, V = P.SLATE, P.RULE, P.VIOLET
SL, SD = P.STRIPE_LIGHT, P.STRIPE_DARK
LAV, T, TD = P.LAVENDER, P.TEXT, P.TEXT_DIM

# --------------------------------------------------------------------- tailles
# Dimensions seules (aucun contenu d'origine) : atlas packed.png + layout/asset.

_ATLAS_SIZES = {
    "allow_lateral_off.png": (10, 16), "allow_lateral_on.png": (10, 16),
    "arrow_left.png": (16, 22), "arrow_right.png": (16, 22),
    "arrow_Small_left.png": (14, 20), "arrow_Small_right.png": (14, 20),
    "btn_copyright.png": (24, 24), "btn_dialog_off.png": (80, 16),
    "btn_dialog_on.png": (88, 24), "btn_display.png": (24, 24),
    "btn_init_off.png": (144, 24), "btn_init_on.png": (152, 32),
    "btn_language.png": (24, 24), "btn_menubar.png": (32, 32),
    "btn_menubar_active.png": (32, 32), "btn_option.png": (24, 24),
    "caption.png": (48, 22), "caption_arrow.png": (8, 8),
    "caption_edge.png": (4, 22), "caption_title.png": (348, 22),
    "check_on.png": (20, 14), "cmn_circle.png": (64, 64), "cmn_square.png": (64, 64),
    "curor_thumbnail_1.png": (12, 8), "curor_thumbnail_2.png": (12, 8),
    "curor_thumbnail_3.png": (12, 8),
    "cursor_1.png": (32, 32), "cursor_3.png": (32, 32), "cursor_7.png": (32, 32),
    "cursor_9.png": (32, 32),
    **{f"cursor_game_{i}.png": (32, 32) for i in range(1, 10)},
    "cursor_option.png": (8, 8),
    "dialog_corner.png": (12, 12), "dialog_corner_slim.png": (12, 12),
    "dialog_line.png": (8, 4), "dialog_line_slim.png": (8, 4),
    **{f"dot_cursor_h_{i}.png": (8, 8) for i in range(1, 4)},
    **{f"dot_cursor_v_{i}.png": (16, 8) for i in range(1, 4)},
    "game_card.png": (84, 92), "game_card_active.png": (84, 92),
    "game_card_lock.png": (264, 288),
    "icon_1P-2P.png": (22, 14), "icon_1P.png": (22, 14), "icon_2P_simultaneous.png": (22, 14),
    "icon_A_light.png": (12, 12), "icon_A_resume.png": (10, 10),
    "icon_B_light.png": (12, 12), "icon_B_resume.png": (10, 10),
    "icon_X_light.png": (12, 12), "icon_X_resume.png": (10, 10),
    "icon_Y_light.png": (12, 12), "icon_Y_resume.png": (10, 10),
    "icon_casette.png": (20, 20), "icon_casette_hvc.png": (20, 20),
    "icon_casette_nes.png": (20, 20),
    "icon_direction_down_light.png": (12, 12), "icon_direction_down_resume.png": (10, 10),
    "icon_direction_lateral_light.png": (12, 12),
    "icon_direction_lateral_resume.png": (10, 10),
    "Icon_direction_Lengthwise_light.png": (12, 12),
    "Icon_direction_Lengthwise_resume.png": (10, 10),
    "Icon_direction_light.png": (12, 12), "Icon_direction_resume.png": (10, 10),
    "icon_direction_up_light.png": (12, 12), "icon_direction_up_resume.png": (10, 10),
    "icon_disk.png": (20, 20), "icon_exclamation.png": (24, 24),
    "icon_lock.png": (14, 18), "icon_unlock.png": (14, 18),
    **{f"icon_numer_{i}.png": (14, 14) for i in range(1, 5)},
    "icon_present.png": (20, 20), "icon_Resume.png": (24, 24),
    "icon_resume_btn_B.png": (16, 16),
    "icon_resume_direction_down.png": (22, 22), "icon_resume_direction_down_2.png": (22, 22),
    "icon_resume_direction_up.png": (22, 22),
    "icon_Resume_lock.png": (6, 6), "icon_Resume_lock_active.png": (6, 6),
    "icon_Resume_off.png": (6, 6), "icon_Resume_off_active.png": (6, 6),
    "icon_Resume_on.png": (6, 6), "icon_Resume_on_active.png": (6, 6),
    "icon_resume_wing.png": (26, 18),
    "Icon_Save.png": (18, 14), "Icon_Save_Popup.png": (18, 14),
    "Icon_Save_Warning.png": (14, 14),
    "icon_select_light.png": (32, 10), "icon_select_resume.png": (30, 8),
    "Icon_SRAM_off.png": (14, 14), "Icon_SRAM_on.png": (14, 14),
    "icon_start_light.png": (28, 10), "icon_start_resume.png": (26, 8),
    **{f"icon_trash_{i}.png": (32, 48) for i in range(5)},
    "manual.png": (24, 24),
    "menubar_L.png": (432, 40), "menubar_U.png": (432, 40), "menubar_U_2.png": (8, 40),
    "meter_gauge.png": (142, 24), "meter_gauge_edge.png": (1, 22),
    "munubar_base.png": (32, 32),
    "popup_present.png": (140, 36), "popup_warning.png": (150, 36),
    "qr_code1.png": (51, 51),
    "radiobtn_off.png": (12, 12), "radiobtn_on.png": (12, 12),
    "registration_frame.png": (74, 24),
    "resume_card.png": (64, 56), "resume_card_active.png": (64, 56),
    "resume_card_active_home.png": (50, 46), "resume_card_empty.png": (68, 60),
    "resume_card_float.png": (68, 60), "resume_card_float_home.png": (54, 50),
    "resume_card_float_mini.png": (38, 36),
    "resume_wingL_1.png": (24, 32), "resume_wingL_2.png": (24, 32),
    "resume_wingM_1.png": (16, 20), "resume_wingM_2.png": (16, 20),
    "resume_wingS_1.png": (10, 16), "resume_wingS_2.png": (10, 16),
    "resumebg.png": (428, 118), "resumebg_overwrite.png": (428, 118),
    "resumebg_tail.png": (32, 18), "resumebg_tail_overwrite.png": (32, 18),
    "screen_analog_preview.png": (220, 168),
    "screen_pixel_perfect_preview.png": (192, 168),
    "scrollbar_allowdown_off.png": (16, 16), "scrollbar_allowdown_on.png": (16, 16),
    "scrollbar_allowup_off.png": (16, 16), "scrollbar_allowup_on.png": (16, 16),
    "scrollbar_Area.png": (2, 112), "scrollbar_full.png": (10, 24),
    **{f"smoke_32_{i}.png": (32, 32) for i in range(1, 6)},
    **{f"smoke_64_{i}.png": (64, 64) for i in range(1, 6)},
    "switch_disable.png": (30, 16), "switch_off.png": (30, 16), "switch_on.png": (30, 16),
    "tab_copyright_off.png": (8, 8), "tab_OSS_off.png": (8, 8),
    "thumbnail_lock.png": (40, 40),
}

# Images isolées de layout/asset/sprite (chemin relatif) + atlas annexe dot_num.
_LAYOUT_SIZES = {
    "wallpaper/wall.png": (512, 184), "wallpaper/wall_far.png": (512, 178),
    "wallpaper/wall_near.png": (1024, 100),
    **{f"wallpaper/pole0{i}.png": (32, 64) for i in range(1, 5)},
    **{f"mario/mario_{n}.png": (24, 32) for n in (
        "dush01", "dush02", "dush03", "jump01", "jump02", "look",
        "run01", "run02", "run03", "spin01", "spin02")},
    **{f"luigi/luigi_{n}.png": (24, 32) for n in (
        "jump01", "jump02", "run01", "run02", "run03")},
    "effect/hit_effect.png": (16, 16), "effect/blind.png": (1, 1),
    "unlock/halo.png": (150, 150), "unlock/halo2.png": (200, 200),
    "unlock/flash.png": (24, 24),
    "unlock/present_box.png": (264, 288), "unlock/present_white.png": (88, 96),
}
LAYOUT = tuple(_LAYOUT_SIZES)
DOT_NUM = "sprites/dot_num.png"

SIZES = {**_ATLAS_SIZES, **_LAYOUT_SIZES, DOT_NUM: (32, 32)}

# ------------------------------------------------------------------ recolorage
# Règle = (seuils, accent). Seuils : liste (luminance max exclue, couleur),
# appliquée aux pixels peu saturés ; accent : idem pour les pixels saturés
# (None = traités comme les autres). ``hard`` : alpha seuillé à 0 / 255.


def _rule(thresh, accent=None, hard=False):
    return {"thresh": tuple(thresh), "accent": tuple(accent) if accent else None,
            "hard": hard}


def _flat(color, hard=False):
    return _rule([(256, color)], hard=hard)


ICON = _rule([(50, S), (120, R), (200, TD), (256, T)], accent=[(110, V), (256, LAV)])
ICON_MONO = _rule([(50, S), (150, TD), (256, T)], accent=[(256, LAV)])
CART = _rule([(50, S), (110, S), (200, R), (256, TD)])
CART_OVERWRITE = _rule([(50, S), (120, S), (215, SD), (256, SL)])
WING = _rule([(50, S), (200, TD), (256, T)])
TRASH = _rule([(50, S), (140, R), (256, TD)])
BUBBLE = _rule([(60, S), (256, LAV)])
OUTLINE_LAV = _rule([(60, S), (256, LAV)])
OUTLINE_TEXT = _rule([(60, S), (256, T)])
OUTLINE_DIM = _rule([(60, S), (256, TD)])
CROSS = _rule([(50, S), (150, TD), (256, T)])
CROSS_DIM = _rule([(60, S), (256, TD)])
DISC_NUM = _rule([(150, R), (256, T)])
SMOKE = _rule([(170, TD), (256, T)], hard=True)

RECOLOR = {
    # curseurs : aplat lavande (forme et alpha d'origine)
    **{n: _flat(LAV) for n in (
        "cursor_1.png", "cursor_3.png", "cursor_7.png", "cursor_9.png",
        *[f"cursor_game_{i}.png" for i in range(1, 10)],
        "curor_thumbnail_1.png", "curor_thumbnail_2.png", "curor_thumbnail_3.png")},
    "cursor_option.png": _flat(V),
    # flèches et défilement
    "arrow_left.png": OUTLINE_LAV, "arrow_right.png": OUTLINE_LAV,
    "arrow_Small_left.png": OUTLINE_LAV, "arrow_Small_right.png": OUTLINE_LAV,
    "allow_lateral_on.png": OUTLINE_LAV, "allow_lateral_off.png": OUTLINE_DIM,
    "scrollbar_Area.png": _flat(R), "scrollbar_full.png": _flat(LAV),
    "scrollbar_allowup_on.png": OUTLINE_TEXT, "scrollbar_allowdown_on.png": OUTLINE_TEXT,
    "scrollbar_allowup_off.png": OUTLINE_DIM, "scrollbar_allowdown_off.png": OUTLINE_DIM,
    # pictos de la barre de menu
    "btn_copyright.png": ICON_MONO, "btn_display.png": ICON_MONO,
    "btn_language.png": ICON_MONO, "btn_option.png": ICON_MONO,
    "manual.png": ICON_MONO,
    # bulles d'information (texte noir dessus)
    "registration_frame.png": BUBBLE, "popup_warning.png": BUBBLE,
    "popup_present.png": BUBBLE, "icon_exclamation.png": BUBBLE,
    # croix directionnelles, bouton B rond, pastilles numérotées
    **{n: CROSS for n in (
        "icon_direction_down_light.png", "icon_direction_up_light.png",
        "icon_direction_lateral_light.png", "Icon_direction_light.png",
        "Icon_direction_Lengthwise_light.png")},
    **{n: CROSS_DIM for n in (
        "icon_direction_down_resume.png", "icon_direction_up_resume.png",
        "icon_direction_lateral_resume.png", "Icon_direction_resume.png",
        "Icon_direction_Lengthwise_resume.png")},
    "icon_resume_direction_up.png": ICON, "icon_resume_direction_down.png": ICON,
    "icon_resume_direction_down_2.png": ICON, "icon_resume_btn_B.png": ICON,
    **{f"icon_numer_{i}.png": DISC_NUM for i in range(1, 5)},
    # pictos de cartes et de sauvegarde
    **{n: ICON for n in (
        "icon_1P.png", "icon_1P-2P.png", "icon_2P_simultaneous.png",
        "icon_casette.png", "icon_casette_hvc.png", "icon_casette_nes.png",
        "icon_disk.png", "icon_present.png", "icon_lock.png", "icon_unlock.png",
        "Icon_Save.png", "Icon_Save_Popup.png", "Icon_Save_Warning.png",
        "Icon_SRAM_on.png", "Icon_SRAM_off.png", "icon_Resume.png")},
    "icon_Resume_off.png": _flat(TD), "icon_Resume_off_active.png": _flat(T),
    "icon_Resume_on.png": _flat(SL), "icon_Resume_on_active.png": _flat(LAV),
    "icon_Resume_lock.png": _flat(V), "icon_Resume_lock_active.png": _flat(SL),
    **{f"icon_trash_{i}.png": TRASH for i in range(5)},
    # cartes de reprise, bandeaux, ailes
    **{n: CART for n in (
        "resume_card.png", "resume_card_float.png", "resume_card_float_home.png",
        "resume_card_float_mini.png", "resume_card_empty.png",
        "resumebg.png", "resumebg_tail.png")},
    "resumebg_overwrite.png": CART_OVERWRITE,
    "resumebg_tail_overwrite.png": CART_OVERWRITE,
    "resume_card_active.png": _rule([(120, V), (175, SD), (225, SL), (256, LAV)]),
    "resume_card_active_home.png": _rule([(120, V), (256, SL)]),
    **{n: WING for n in (
        "icon_resume_wing.png", "resume_wingL_1.png", "resume_wingL_2.png",
        "resume_wingM_1.png", "resume_wingM_2.png",
        "resume_wingS_1.png", "resume_wingS_2.png")},
    # options
    "radiobtn_off.png": OUTLINE_DIM,
    "radiobtn_on.png": _rule([(60, S), (256, T)], accent=[(256, LAV)]),
    "check_on.png": OUTLINE_LAV,
    "meter_gauge.png": _flat(LAV), "meter_gauge_edge.png": _flat(LAV),
    "tab_copyright_off.png": _flat(TD), "tab_OSS_off.png": _flat(TD),
    # fumée : deux gris, sans transparence dégradée
    **{f"smoke_32_{i}.png": SMOKE for i in range(1, 6)},
    **{f"smoke_64_{i}.png": SMOKE for i in range(1, 6)},
    # chiffres du compteur (atlas annexe)
    DOT_NUM: OUTLINE_TEXT,
}

CLEAR = frozenset(
    ["wallpaper/wall_near.png", "effect/hit_effect.png",
     "unlock/halo.png", "unlock/halo2.png", "unlock/flash.png"]
    + [n for n in _LAYOUT_SIZES if n.startswith(("mario/", "luigi/", "wallpaper/pole"))]
)

KEEP = frozenset([
    "cmn_circle.png", "cmn_square.png", "qr_code1.png",
    *[f"dot_cursor_h_{i}.png" for i in range(1, 4)],
    *[f"dot_cursor_v_{i}.png" for i in range(1, 4)],
])


def rule_colors(rule) -> int:
    """Nombre maximal de couleurs opaques distinctes qu'une règle produit."""
    cols = {c for _, c in rule["thresh"]}
    if rule["accent"]:
        cols |= {c for _, c in rule["accent"]}
    return len(cols)


def recolor(ref: Image.Image, rule) -> Image.Image:
    """Forme et alpha de ``ref``, couleurs de la palette par seuils."""
    a = np.asarray(ref.convert("RGBA")).astype(np.int32)
    rgb, alpha = a[..., :3], a[..., 3]
    lum = (0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2])
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1), 0.0)
    saturated = (sat > 0.35) & (mx > 60) if rule["accent"] else np.zeros(lum.shape, bool)
    out = np.zeros(a.shape, np.uint8)

    def apply(mask, thresh):
        done = np.zeros(lum.shape, bool)
        for limit, col in thresh:
            m = mask & ~done & (lum < limit)
            out[m, :3] = col
            done |= m
        rest = mask & ~done
        out[rest, :3] = thresh[-1][1]

    apply(~saturated, rule["thresh"])
    if rule["accent"]:
        apply(saturated, rule["accent"])
    if rule["hard"]:
        out[..., 3] = np.where(alpha >= 128, 255, 0)
    else:
        out[..., 3] = alpha
    out[out[..., 3] == 0] = 0
    return Image.fromarray(out, "RGBA")


# ------------------------------------------------------------------- primitives

class Canvas:
    """Tampon RGBA ; chaque forme est peinte d'après une distance signée."""

    SS = 4  # suréchantillonnage pour la couverture du contour

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.a = np.zeros((h, w, 4), np.uint8)
        ys, xs = np.mgrid[0:h, 0:w]
        self.cx, self.cy = xs + 0.5, ys + 0.5
        o = (np.arange(self.SS) + 0.5) / self.SS - 0.5
        self.offs = [(dx, dy) for dx in o for dy in o]

    def image(self):
        out = self.a.copy()
        out[out[..., 3] == 0] = 0
        return Image.fromarray(out, "RGBA")

    def shape(self, sdf, layers, aa=True):
        """``sdf(x, y)`` < 0 à l'intérieur. ``layers`` : [(profondeur, couleur)]
        triées ; la couleur d'un pixel est celle de la dernière couche dont la
        profondeur (distance au bord, au centre du pixel) est atteinte.
        Couleur ``None`` : laisser le pixel tel quel."""
        depth = -sdf(self.cx, self.cy)
        if aa:
            cov = np.zeros(depth.shape)
            for dx, dy in self.offs:
                cov += sdf(self.cx + dx, self.cy + dy) < 0
            cov /= len(self.offs)
        else:
            cov = (depth > 0).astype(float)
        col_idx = np.zeros(depth.shape, int)
        for i, (d0, _) in enumerate(layers):
            col_idx[depth >= d0] = i
        for i, (_, col) in enumerate(layers):
            if col is None:
                continue
            sel = (col_idx == i) & (cov > 0)
            opaque_below = self.a[..., 3] == 255
            # sur un aplat existant : bord net (pas de mélange de couleurs)
            full = sel & ((cov >= 1) | (opaque_below & (cov >= 0.5)))
            self.a[full, :3] = col
            self.a[full, 3] = 255
            # sur du transparent : contour anticrénelé par l'alpha seul
            soft = sel & ~opaque_below & (cov < 1)
            self.a[soft, :3] = col
            self.a[soft, 3] = (cov[soft] * 255).round().astype(np.uint8)
        return self

    def rect(self, x0, y0, x1, y1, col):
        """Rectangle plein, bornes entières [x0, x1) x [y0, y1)."""
        self.a[y0:y1, x0:x1, :3] = col
        self.a[y0:y1, x0:x1, 3] = 255
        return self

    def clear(self, x0, y0, x1, y1):
        self.a[y0:y1, x0:x1] = 0
        return self

    def bitmap(self, x, y, rows, col, scale=1):
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch != ".":
                    self.rect(x + i * scale, y + j * scale,
                              x + (i + 1) * scale, y + (j + 1) * scale, col)
        return self

    def template(self, rows, colors):
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch in colors:
                    self.rect(i, j, i + 1, j + 1, colors[ch])
        return self


def sd_rrect(x0, y0, x1, y1, r):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r

    def f(px, py):
        qx, qy = np.abs(px - cx) - hx, np.abs(py - cy) - hy
        out = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
        return out + np.minimum(np.maximum(qx, qy), 0) - r
    return f


def sd_circle(cx, cy, r):
    return lambda px, py: np.hypot(px - cx, py - cy) - r


def sd_union(*fs):
    return lambda px, py: np.minimum.reduce([f(px, py) for f in fs])


def sd_inter(*fs):
    return lambda px, py: np.maximum.reduce([f(px, py) for f in fs])


def sd_padlock(cx, body_top, bw, bh, ro, ri, br):
    """Cadenas : corps arrondi + anse (demi-anneau et deux jambages)."""
    body = sd_rrect(cx - bw / 2, body_top, cx + bw / 2, body_top + bh, br)
    ring_cy = body_top - (ro - ri)
    rm, half = (ro + ri) / 2, (ro - ri) / 2

    def ring(px, py):
        return np.abs(np.hypot(px - cx, py - ring_cy) - rm) - half
    upper = sd_inter(ring, lambda px, py: py - ring_cy)
    leg_l = sd_rrect(cx - ro, ring_cy, cx - ri, body_top + 1, 0)
    leg_r = sd_rrect(cx + ri, ring_cy, cx + ro, body_top + 1, 0)
    return sd_union(body, upper, leg_l, leg_r)


# Petite police pixel 3x5 (pilules START / SELECT) et 4x5 (pastilles A/B/X/Y).
FONT3 = {
    "A": [".#.", "#.#", "###", "#.#", "#.#"],
    "C": [".##", "#..", "#..", "#..", ".##"],
    "E": ["###", "#..", "##.", "#..", "###"],
    "L": ["#..", "#..", "#..", "#..", "###"],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"],
    "S": [".##", "#..", ".#.", "..#", "##."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."],
}
FONT_DISC = {
    12: {  # 6 x 6, centré dans le disque de 12
        "A": ["..##..", ".#..#.", "#....#", "######", "#....#", "#....#"],
        "B": ["#####.", "#....#", "#####.", "#....#", "#....#", "#####."],
        "X": ["#....#", ".#..#.", "..##..", "..##..", ".#..#.", "#....#"],
        "Y": ["#....#", ".#..#.", "..##..", "..##..", "..##..", "..##.."],
    },
    10: {  # 5 lignes ; X et Y sur 6 colonnes pour garder leurs diagonales
        "A": [".##.", "#..#", "####", "#..#", "#..#"],
        "B": ["###.", "#..#", "###.", "#..#", "###."],
        "X": ["#....#", ".#..#.", "..##..", ".#..#.", "#....#"],
        "Y": ["#....#", ".#..#.", "..##..", "..##..", "..##.."],
    },
}


def _text3(c, x, y, text, col):
    for ch in text:
        c.bitmap(x, y, FONT3[ch], col)
        x += 4
    return c


def _text3_width(text):
    return len(text) * 4 - 1


# ------------------------------------------------------------------ dessins

def _game_card(active):
    # Cadre plat, coins nets : RULE au repos, VIOLET sélectionné. Pas de bord
    # lavande : le curseur natif (cursor_game_*, lavande) encadre déjà la carte.
    w, h = 84, 92
    c = Canvas(w, h)
    c.rect(0, 0, w, h, V if active else R)
    c.rect(4, 4, 80, 72, S)  # fenêtre de la jaquette (texture posée dessus)
    if active:
        # I2 : petit interrupteur strié façon POWER / RESET, dans le bandeau bas
        x0, x1, y0, y1 = 26, 58, 76, 84
        c.rect(x0, y0, x1, y1, S)  # logement
        for i, x in enumerate(range(x0 + 1, x1 - 1, 2)):
            c.rect(x, y0 + 1, min(x + 2, x1 - 1), y1 - 1, SL if i % 2 == 0 else SD)
    return c.image()


def _frame_plate(w, h, r, unit):
    """Plaque ardoise à bord TEXT_DIM (cadre du cadeau / du jeu verrouillé)."""
    c = Canvas(w, h)
    c.shape(sd_rrect(0, 0, w, h, r), [(0, TD), (unit, S)])
    return c


def _game_card_lock():
    w, h, u = 264, 288, 3
    c = _frame_plate(w, h, 3 * u, u)
    cx = w / 2
    c.shape(sd_padlock(cx, 138, 84, 66, 30, 18, 2 * u), [(0, LAV)])
    c.shape(sd_circle(cx, 163, 8), [(0, S)], aa=False)
    c.rect(int(cx) - 3, 166, int(cx) + 3, 183, S)
    return c.image()


def _thumbnail_lock():
    c = Canvas(40, 40)
    c.rect(0, 12, 40, 40, TD)
    c.rect(1, 13, 39, 39, S)
    c.shape(sd_padlock(20, 24, 14, 11, 5, 3, 1), [(0, LAV)], aa=False)
    c.rect(19, 27, 21, 32, S)
    return c.image()


def _present_box():
    w, h, u = 264, 288, 3
    c = _frame_plate(w, h, 3 * u, u)
    cx, cy, band = w // 2, h // 2, 36
    c.rect(cx - band // 2, u, cx + band // 2, h - u, V)
    c.rect(u, cy - band // 2, w - u, cy + band // 2, V)
    # médaillon : disque lavande, anneau ardoise
    c.shape(sd_circle(cx, cy, 39), [(0, LAV), (9, S), (12, LAV)])
    return c.image()


def _present_white():
    c = Canvas(88, 96)
    c.shape(sd_rrect(0, 0, 88, 96, 3), [(0, T)])
    return c.image()


def _wall(w, h):
    """Aplat ardoise + deux filets en haut (2 texels chacun : le fond passe par
    une cible 320x180, un filet d'un texel pourrait disparaître). Lignes
    uniformes : le défilement horizontal reste sans couture."""
    c = Canvas(w, h)
    c.rect(0, 0, w, h, S)
    c.rect(0, 4, w, 6, R)
    c.rect(0, 8, w, 10, R)
    return c.image()


def _flat_img(w, h, col):
    return Canvas(w, h).rect(0, 0, w, h, col).image()


def _menubar_L():
    c = Canvas(432, 40)
    c.rect(0, 0, 432, 40, S)
    c.rect(0, 0, 432, 1, R)  # filet supérieur (bord de la barre du bas)
    return c.image()


def _menubar_U_2():
    c = Canvas(8, 40)
    c.rect(0, 19, 8, 40, S)
    return c.image()


CAPTION_EDGE = (["...."] * 5 + ["SS..", "BBS."] + ["FFBS"] * 13 + ["BBS.", "SS.."])
CAPTION_ARROW = [
    "........",
    "...S....",
    "..SBS...",
    "..SBS...",
    ".SBFBS..",
    ".SBFBS..",
    "SBFFFBS.",
    "BFFFFFB.",
]
_CAP_COLORS = {"S": S, "B": TD, "F": R}


def _caption():
    c = Canvas(48, 22)
    c.rect(0, 5, 48, 6, S).rect(0, 6, 48, 7, TD).rect(0, 7, 48, 20, R)
    c.rect(0, 20, 48, 21, TD).rect(0, 21, 48, 22, S)
    return c.image()


def _caption_title():
    c = Canvas(348, 22)
    c.rect(3, 2, 345, 20, LAV)  # validé utilisateur : lavande, titre noir dessus
    return c.image()


def _button_disc(w, letter, ab):
    c = Canvas(w, w)
    r = w / 2
    fill, rim, ink = (V, SL, T) if ab else (LAV, SL, S)
    main = sd_circle(r, r, r)
    c.shape(main, [(0, fill)])
    # bombé (A/B) : reflet clair en haut ; creusé (X/Y) : lèvre sombre en bas
    shift = 1 if ab else -1
    other = sd_circle(r, r + shift, r)
    crescent = sd_inter(main, lambda px, py: -other(px, py))
    c.shape(crescent, [(0, rim)], aa=False)
    glyph = FONT_DISC[w][letter]
    gx = (w - len(glyph[0])) // 2
    gy = (w - len(glyph)) // 2
    c.bitmap(gx, gy, glyph, ink)
    return c.image()


def _pill(w, h, text, resume):
    """Pilule START / SELECT : bord TEXT_DIM, fond RULE, lettres pixel."""
    c = Canvas(w, h)
    x1 = w - 1  # la colonne de droite reste vide (comme l'original)
    c.shape(sd_rrect(0, 1, x1, h, (h - 1) / 2), [(0, TD), (1, R)])
    ink = TD if resume else T
    tw = _text3_width(text)
    tx = (x1 - tw) // 2
    ty = 1 + (h - 1 - 5) // 2
    return _text3(c, tx, ty, text, ink).image()


def _dialog_corner(slim):
    c = Canvas(12, 12)
    layers = ([(0, S), (1, LAV), (2, S), (4, None)] if slim
              else [(0, S), (1, LAV), (3, S), (4, None)])
    c.shape(sd_rrect(0, 0, 40, 40, 4), layers)
    return c.image()


def _dialog_line(slim):
    c = Canvas(8, 4)
    rows = [S, LAV, S, S] if slim else [S, LAV, LAV, S]
    for y, col in enumerate(rows):
        c.rect(0, y, 8, y + 1, col)
    return c.image()


def _btn_off(w, h):
    c = Canvas(w, h)
    c.shape(sd_rrect(0, 0, w, h, 2), [(0, T)])
    return c.image()


def _btn_on(w, h):
    """Focus : anneau lavande détaché (comme un outline-offset), plaque
    STRIPE_LIGHT. Le texte du bouton est noir ou blanc selon le bytecode :
    STRIPE_LIGHT reste lisible dans les deux cas (voir rapport)."""
    c = Canvas(w, h)
    c.shape(sd_rrect(0, 0, w, h, 4), [(0, LAV), (2, None)])
    c.shape(sd_rrect(4, 4, w - 4, h - 4, 2), [(0, SL)])
    return c.image()


def _switch(state):
    c = Canvas(30, 16)
    outer = sd_rrect(0, 0, 30, 16, 3)
    if state == "on":
        c.shape(outer, [(0, LAV), (1, V)])
        x0, x1 = 15, 27
        for i, x in enumerate(range(x0, x1, 2)):
            c.rect(x, 3, x + 2, 13, SL if i % 2 == 0 else SD)
    elif state == "off":
        c.shape(outer, [(0, TD), (1, R)])
        c.rect(3, 3, 15, 13, TD)
    else:
        c.shape(outer, [(0, TD), (1, R)])
    return c.image()


# Petite scène géométrique neutre (64 x 56) pour les aperçus de filtre.
def _preview_scene():
    c = Canvas(64, 56)
    c.rect(0, 0, 64, 56, S)
    for x, y in [(6, 6), (21, 11), (47, 5), (58, 13), (33, 3)]:
        c.rect(x, y, x + 1, y + 1, TD)
    for x0, x1, top in [(0, 9, 30), (9, 17, 25), (40, 50, 27), (50, 58, 22), (58, 64, 29)]:
        c.rect(x0, top, x1, 40, R)
    c.rect(0, 40, 64, 41, LAV)
    c.rect(0, 41, 64, 56, V)
    for x in range(2, 64, 8):
        c.rect(x, 46, x + 4, 48, SD)
    c.rect(36, 14, 44, 22, T).rect(38, 16, 42, 20, R)
    c.rect(14, 32, 22, 40, LAV).rect(18, 34, 20, 36, S)
    return np.asarray(c.image())


def _screen_preview(analog):
    scene = _preview_scene()
    if not analog:
        img = Image.fromarray(scene, "RGBA").resize((192, 168), Image.NEAREST)
        return img
    img = Image.fromarray(scene, "RGBA").resize((220, 168), Image.NEAREST)
    a = np.asarray(img).copy()
    a[2::3, :, :3] = S  # lignes de balayage (CRT) : ardoise pleine
    return Image.fromarray(a, "RGBA")


DRAWN = {
    "game_card.png": lambda: _game_card(False),
    "game_card_active.png": lambda: _game_card(True),
    "game_card_lock.png": _game_card_lock,
    "thumbnail_lock.png": _thumbnail_lock,
    "menubar_L.png": _menubar_L,
    "menubar_U.png": lambda: _flat_img(432, 40, S),
    "menubar_U_2.png": _menubar_U_2,
    "munubar_base.png": lambda: _flat_img(32, 32, S),
    "btn_menubar.png": lambda: _flat_img(32, 32, R),
    "btn_menubar_active.png": lambda: _flat_img(32, 32, V),
    "caption.png": _caption,
    "caption_edge.png": lambda: Canvas(4, 22).template(CAPTION_EDGE, _CAP_COLORS).image(),
    "caption_arrow.png": lambda: Canvas(8, 8).template(CAPTION_ARROW, _CAP_COLORS).image(),
    "caption_title.png": _caption_title,
    **{f"icon_{k}_light.png": (lambda k=k: _button_disc(12, k, k in "AB")) for k in "ABXY"},
    **{f"icon_{k}_resume.png": (lambda k=k: _button_disc(10, k, k in "AB")) for k in "ABXY"},
    "icon_start_light.png": lambda: _pill(28, 10, "START", False),
    "icon_select_light.png": lambda: _pill(32, 10, "SELECT", False),
    "icon_start_resume.png": lambda: _pill(26, 8, "START", True),
    "icon_select_resume.png": lambda: _pill(30, 8, "SELECT", True),
    "dialog_corner.png": lambda: _dialog_corner(False),
    "dialog_corner_slim.png": lambda: _dialog_corner(True),
    "dialog_line.png": lambda: _dialog_line(False),
    "dialog_line_slim.png": lambda: _dialog_line(True),
    "btn_dialog_off.png": lambda: _btn_off(80, 16),
    "btn_dialog_on.png": lambda: _btn_on(88, 24),
    "btn_init_off.png": lambda: _btn_off(144, 24),
    "btn_init_on.png": lambda: _btn_on(152, 32),
    "switch_on.png": lambda: _switch("on"),
    "switch_off.png": lambda: _switch("off"),
    "switch_disable.png": lambda: _switch("disable"),
    "screen_analog_preview.png": lambda: _screen_preview(True),
    "screen_pixel_perfect_preview.png": lambda: _screen_preview(False),
    "wallpaper/wall.png": lambda: _wall(512, 184),
    "wallpaper/wall_far.png": lambda: _wall(512, 178),
    "effect/blind.png": lambda: _flat_img(1, 1, S),
    "unlock/present_box.png": _present_box,
    "unlock/present_white.png": _present_white,
}


def draw(sprite_name: str, size, ref: Image.Image | None = None) -> Image.Image:
    """Image RGBA B1 du sprite ``sprite_name`` à la taille ``size``.

    ``ref`` (sprite d'origine) n'est requis que pour les sprites de ``RECOLOR``.
    ``KeyError`` : nom inconnu ou laissé tel quel ; ``ValueError`` : mauvaise
    taille ou référence manquante.
    """
    if sprite_name not in SIZES or sprite_name in KEEP:
        raise KeyError(sprite_name)
    size = tuple(size)
    if size != SIZES[sprite_name]:
        raise ValueError(f"{sprite_name}: taille {size} != {SIZES[sprite_name]}")
    if sprite_name in CLEAR:
        return Image.new("RGBA", size, (0, 0, 0, 0))
    if sprite_name in DRAWN:
        img = DRAWN[sprite_name]()
    else:
        if ref is None:
            raise ValueError(f"{sprite_name}: sprite recoloré, référence requise")
        if ref.size != size:
            raise ValueError(f"{sprite_name}: référence {ref.size} != {size}")
        img = recolor(ref, RECOLOR[sprite_name])
    assert img.size == size, (sprite_name, img.size, size)
    return img
