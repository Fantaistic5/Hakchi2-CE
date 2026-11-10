"""RetroArch en B1 : cles de menu seulement, apply/restore, fond et police."""
import re
import subprocess
from pathlib import Path

import pytest
from PIL import Image

from theme.b1 import palette as P
from theme.retroarch import font, wallpaper

REPO = Path(__file__).resolve().parents[2]
RA = REPO / "theme" / "retroarch"
BUSYBOX = REPO / "tools" / "busybox.exe"
B1 = RA / "b1.cfg"

ORIGINAL = b"""# retroarch.cfg d'essai
assets_directory = "~/assets"
audio_volume = "0.000000"
config_save_on_exit = "true"
input_player1_a = "x"
menu_driver = "ozone"
menu_wallpaper = "~/wallpapers/retrocat/retrocat_04.png"
menu_wallpaper_opacity = "0.500000"
menu_dynamic_wallpaper_enable = "false"
video_font_size = "20.000000"
xmb_font = "~/fonts/_Default.ttf"
xmb_shadows_enable = "true"
xmb_theme = "0"
"""
PCSX = b'# override PS1\nvideo_smooth = "false"\nmenu_driver = "rgui"\n'
DO4 = b'# override 3DO\naudio_latency = "64"\n'


def need_busybox():
    if not BUSYBOX.is_file():
        pytest.skip("tools/busybox.exe absent")


def keys(path):
    out = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip() and not line.lstrip().startswith("#"):
            out.append(line.split("=")[0].strip())
    return out


def run(script, *args):
    r = subprocess.run([str(BUSYBOX), "sh", str(RA / script), *map(str, args)],
                       capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    return r


@pytest.fixture
def console(tmp_path):
    cfg = tmp_path / "retroarch.cfg"
    cfg.write_bytes(ORIGINAL)
    (tmp_path / "config" / "PCSX-ReARMed").mkdir(parents=True)
    (tmp_path / "config" / "4DO").mkdir(parents=True)
    (tmp_path / "config" / "PCSX-ReARMed" / "PCSX-ReARMed.cfg").write_bytes(PCSX)
    (tmp_path / "config" / "4DO" / "4DO.cfg").write_bytes(DO4)
    return tmp_path


def test_only_menu_keys():
    ks = keys(B1)
    assert ks
    for k in ks:
        assert re.match(r"^(menu|xmb|rgui|ozone)_", k), k
        assert not re.match(r"^(audio|video|input)_", k), k
        assert "_core_" not in k, k
    assert len(ks) == len(set(ks))
    assert b"\r" not in B1.read_bytes()


def test_scripts_are_lf_and_parse():
    for n in ("apply.sh", "restore.sh"):
        data = (RA / n).read_bytes()
        assert b"\r" not in data
        assert data.startswith(b"#!/bin/sh")
    need_busybox()
    for n in ("apply.sh", "restore.sh"):
        r = subprocess.run([str(BUSYBOX), "sh", "-n", str(RA / n)], capture_output=True, text=True)
        assert r.returncode == 0, r.stderr


def test_apply_sets_keys_and_keeps_others(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    run("apply.sh", cfg)
    new = cfg.read_text(encoding="utf-8")
    assert 'menu_driver = "xmb"' in new
    assert 'menu_wallpaper = "/usr/share/snes_usa_b1/retroarch/b1_wallpaper.png"' in new
    assert 'xmb_shadows_enable = "false"' in new
    assert 'menu_shader_pipeline = "0"' in new  # absente avant : ajoutee
    # lignes hors B1 intactes, dans le meme ordre
    other = [l for l in ORIGINAL.decode().splitlines() if l.split("=")[0].strip() not in keys(B1)]
    assert [l for l in new.splitlines() if l in other] == other
    assert new.count("menu_driver") == 1
    assert (console / "retroarch.cfg.b1bak").read_bytes() == ORIGINAL


def test_apply_then_restore_roundtrip(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    run("apply.sh", cfg)
    assert cfg.read_bytes() != ORIGINAL
    run("restore.sh", cfg)
    assert cfg.read_bytes() == ORIGINAL
    assert not (console / "retroarch.cfg.b1bak").exists()
    assert (console / "config" / "PCSX-ReARMed" / "PCSX-ReARMed.cfg").read_bytes() == PCSX
    assert (console / "config" / "4DO" / "4DO.cfg").read_bytes() == DO4


def test_apply_idempotent(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    run("apply.sh", cfg)
    once = cfg.read_bytes()
    run("apply.sh", cfg)
    assert cfg.read_bytes() == once
    assert (console / "retroarch.cfg.b1bak").read_bytes() == ORIGINAL  # jamais ecrasee
    run("restore.sh", cfg)
    assert cfg.read_bytes() == ORIGINAL


def test_restore_keeps_later_user_changes(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    run("apply.sh", cfg)
    cfg.write_bytes(cfg.read_bytes().replace(b'audio_volume = "0.000000"', b'audio_volume = "-6.000000"'))
    run("restore.sh", cfg)
    t = cfg.read_text(encoding="utf-8")
    assert 'audio_volume = "-6.000000"' in t
    assert 'menu_driver = "ozone"' in t and "menu_shader_pipeline" not in t


def test_restore_without_backup_is_noop(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    run("restore.sh", cfg)
    assert cfg.read_bytes() == ORIGINAL


def test_wallpaper_size_and_palette_only():
    img = wallpaper.make()
    assert img.size == (1280, 720) and img.mode == "RGB"
    used = {c for _, c in img.getcolors(maxcolors=64)}
    assert used <= {P.SLATE, P.RULE, P.VIOLET, P.STRIPE_LIGHT, P.STRIPE_DARK}
    assert {P.SLATE, P.RULE, P.VIOLET} <= used
    px = img.load()
    assert px[0, 0] == P.SLATE and px[640, 360] == P.SLATE
    for y in wallpaper.RULES_Y:
        assert all(px[x, y] == P.RULE for x in (0, 640, 1279))


def test_static_font_is_not_variable(tmp_path):
    from fontTools.ttLib import TTFont
    from PIL import ImageFont
    out = tmp_path / "Jost-Medium.ttf"
    font.build(out)
    tt = TTFont(str(out))
    assert "fvar" not in tt and "glyf" in tt
    assert ImageFont.truetype(str(out), 24).getlength("Menu") > 0


def test_static_font_build_is_reproducible(tmp_path):
    a, b = tmp_path / "a.ttf", tmp_path / "b.ttf"
    font.build(a)
    import time
    time.sleep(1.1)  # un horodatage reel aurait change
    font.build(b)
    assert a.read_bytes() == b.read_bytes()


def run_env(script, cfg, **env):
    import os
    return subprocess.run([str(BUSYBOX), "sh", str(RA / script), str(cfg)], capture_output=True,
                          text=True, env={**os.environ, **env})


def test_apply_refuses_truncated_write(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    r = run_env("apply.sh", cfg, B1_TEST_KEEP_LINES="3")
    assert r.returncode == 1 and "tronquee" in r.stderr
    assert cfg.read_bytes() == ORIGINAL
    assert (console / "retroarch.cfg.b1bak").read_bytes() == ORIGINAL
    assert not (console / "retroarch.cfg.b1tmp").exists()


def test_restore_refuses_truncated_write_and_keeps_backup(console):
    need_busybox()
    cfg = console / "retroarch.cfg"
    run("apply.sh", cfg)
    applied = cfg.read_bytes()
    r = run_env("restore.sh", cfg, B1_TEST_KEEP_LINES="3")
    assert r.returncode == 1 and "tronquee" in r.stderr
    assert cfg.read_bytes() == applied
    assert (console / "retroarch.cfg.b1bak").read_bytes() == ORIGINAL
    # sans incident, la restauration marche ensuite
    run("restore.sh", cfg)
    assert cfg.read_bytes() == ORIGINAL
