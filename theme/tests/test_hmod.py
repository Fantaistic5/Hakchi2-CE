"""Tests du hmod snes_usa_b1 (script de demarrage, empaquetage, garde-fous)."""
import hashlib
import re
import shutil
import subprocess
import tarfile
from pathlib import Path

import pytest

from theme import build_theme
from theme.b1 import art, pack

REPO = Path(__file__).resolve().parents[2]
HMOD = REPO / "theme" / "hmod"
PREINIT = HMOD / "etc" / "preinit.d" / "p8030_snes_usa_b1"
BUSYBOX = REPO / "tools" / "busybox.exe"
UI_REF = Path(r"P:\HakchiSNESUSA\ui_ref")
THEME_OUT = Path(r"P:\HakchiSNESUSA\theme_out")
ROOT = "usr/share/snes_usa_b1/resources/"
RA = "usr/share/snes_usa_b1/retroarch/"


# ------------------------------------------------------------------ sources

def test_source_files_exist_and_are_lf():
    for p in (HMOD / "readme.md", HMOD / "install", HMOD / "uninstall", PREINIT):
        assert p.is_file(), p
        assert b"\r" not in p.read_bytes(), f"CR dans {p.name}"


def test_readme_header():
    text = (HMOD / "readme.md").read_text(encoding="utf-8")
    head = text.split("---")[1]
    for key in ("Name:", "Creator:", "Category: UI", "Version:"):
        assert key in head


def test_preinit_syntax():
    if not BUSYBOX.is_file():
        pytest.skip("tools/busybox.exe absent")
    for p in (PREINIT, HMOD / "install", HMOD / "uninstall"):
        r = subprocess.run([str(BUSYBOX), "sh", "-n", str(p)], capture_output=True, text=True)
        assert r.returncode == 0, r.stderr


def test_every_bind_guarded():
    lines = PREINIT.read_text(encoding="utf-8").splitlines()
    binds = [i for i, l in enumerate(lines) if re.match(r"\s*mount_bind\b", l)]
    assert binds, "aucun mount_bind"
    for i in binds:
        j = i
        while j >= 0 and not re.match(r"^\w+\(\)\s*\{", lines[j]):
            j -= 1
        body = "\n".join(lines[j:i])
        assert '[ -f "$1" ]' in body and '[ -f "$2" ]' in body, f"ligne {i + 1} non gardee"
        assert 'mount_bind "$1" "$2"' in lines[i]


def test_binds_stay_in_own_folder_and_target_ui():
    t = PREINIT.read_text(encoding="utf-8")
    assert "usr/share/snes_usa_b1/resources" in t
    assert "usr/share/ui/$sftype-$sfregion/resources" in t
    # pas de copie par-dessus l'original
    assert not re.search(r"^\s*(cp|mv|ln|copy|rm)\b", t, re.M)


def test_uninstall_restores_title_font():
    for p in (HMOD / "uninstall", PREINIT):
        t = p.read_text(encoding="utf-8")
        assert "title.fnt" not in t and "title.font" not in t
        assert "fonts/title" not in t
    for line in (HMOD / "uninstall").read_text(encoding="utf-8").splitlines():
        if re.search(r"\brm\b", line):
            assert "snes_usa_b1" in line, line


# ------------------------------------------------------------------ paquet

@pytest.fixture(scope="module")
def built(tmp_path_factory):
    d = tmp_path_factory.mktemp("srcout")
    for rel in pack.expected_sources():
        f = d / rel
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_bytes(b"x" + rel.encode())
    out = tmp_path_factory.mktemp("o") / "snes_usa_b1.hmod"
    pack.build(out, d)
    return out, d


def _members(path):
    with tarfile.open(path, "r:gz") as t:
        return {m.name: (m, t.extractfile(m).read() if m.isfile() else None) for m in t.getmembers()}


def test_structure(built):
    m = _members(built[0])
    for name in ("readme.md", "install", "uninstall", "etc/preinit.d/p8030_snes_usa_b1",
                 RA + "apply.sh", RA + "restore.sh", RA + "b1.cfg", RA + "b1_wallpaper.png",
                 RA + "Jost-Medium.ttf", ROOT + "fonts/OFL.txt", ROOT + "sprites/packed.png", ROOT + "sprites/dot_num.png",
                 ROOT + "sounds/hvc/bgm_home.wav", ROOT + "fonts/s.font", ROOT + "fonts/m.font",
                 ROOT + "fonts/l.font", ROOT + "fonts/copyright.fnt"):
        assert name in m, name
    for n in art.LAYOUT:
        assert ROOT + "layout/asset/sprite/" + n in m, n


def test_never_ships_title_font_or_foreign_paths(built):
    for name in _members(built[0]):
        assert "title." not in name
        if name in ("readme.md", "install", "uninstall", "etc", "etc/preinit.d", "etc/preinit.d/p8030_snes_usa_b1",
                    "usr", "usr/share"):
            continue
        assert name.startswith("usr/share/snes_usa_b1"), name


def test_metadata_and_lf(built):
    m = _members(built[0])
    assert list(m) == sorted(m)
    for name, (info, data) in m.items():
        assert info.uid == info.gid == 0 and info.uname == info.gname == ""
        assert info.mtime == pack.MTIME
        if info.isdir():
            assert info.mode == 0o755
        elif name in ("etc/preinit.d/p8030_snes_usa_b1", RA + "apply.sh", RA + "restore.sh"):
            assert info.mode == 0o755
            assert b"\r" not in data
        else:
            assert info.mode == 0o644
    assert b"\r" not in m["uninstall"][1] and b"\r" not in m["readme.md"][1]


def test_deterministic(built, tmp_path):
    out2 = tmp_path / "again.hmod"
    pack.build(out2, built[1])
    assert out2.read_bytes() == built[0].read_bytes()


def test_crlf_in_script_is_normalised(tmp_path, monkeypatch):
    src = tmp_path / "hm"
    (src / "etc" / "preinit.d").mkdir(parents=True)
    (src / "readme.md").write_bytes(b"a\r\nb\r\n")
    (src / "uninstall").write_bytes(b"x\r\n")
    (src / "install").write_bytes(b"y\r\n")
    (src / "etc" / "preinit.d" / "p8030_snes_usa_b1").write_bytes(b"echo 1\r\necho 2\r\n")
    monkeypatch.setattr(pack, "HMOD_DIR", src)
    for rel in pack.expected_sources():
        f = tmp_path / "so" / rel
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_bytes(b"z")
    out = tmp_path / "o"
    pack.build(out, tmp_path / "so")
    m = _members(out)
    assert m["etc/preinit.d/p8030_snes_usa_b1"][1] == b"echo 1\necho 2\n"


def test_missing_source_is_an_error(tmp_path):
    with pytest.raises(FileNotFoundError):
        pack.build(tmp_path / "x.hmod", tmp_path)


# ------------------------------------------------------------------ install / uninstall

def _sh(cwd, code):
    return subprocess.run([str(BUSYBOX), "sh", "-c", code], cwd=str(cwd), capture_output=True, text=True)


def _posix(p):
    return str(p).replace("\\", "/")


def test_install_uninstall_scripts_never_exit():
    for n in ("install", "uninstall"):
        t = (HMOD / n).read_text(encoding="utf-8")
        assert not re.search(r"^\s*exit\b", t, re.M), n  # scripts sources par preinit
        assert t.rstrip().endswith("true"), n
    assert "retroarch" not in PREINIT.read_text(encoding="utf-8")  # p8030 ne lie que resources/


def _extract(built, tmp_path):
    transfer = tmp_path / "transfer"
    with tarfile.open(built[0], "r:gz") as t:
        t.extractall(transfer, filter="data")
    return transfer


def test_install_then_uninstall_roundtrip(built, tmp_path):
    if not BUSYBOX.is_file():
        pytest.skip("tools/busybox.exe absent")
    transfer = _extract(built, tmp_path)
    rootfs = tmp_path / "rootfs"
    cfgdir = rootfs / "etc" / "libretro"
    (cfgdir / "config" / "4DO").mkdir(parents=True)
    cfg = cfgdir / "retroarch.cfg"
    original = b'menu_driver = "ozone"\nxmb_font = "~/fonts/_Default.ttf"\nvideo_vsync = "true"\n'
    cfg.write_bytes(original)
    do4 = cfgdir / "config" / "4DO" / "4DO.cfg"
    do4.write_bytes(b'audio_latency = "64"\n')
    title = rootfs / "usr" / "share" / "fonts" / "title.fnt"
    title.parent.mkdir(parents=True)
    title.write_bytes(b"user title font")

    # install : source par le shell de preinit, cwd = hmod ; "after" prouve qu'on ne quitte pas
    r = _sh(transfer, f'rootfs="{_posix(rootfs)}"; . ./install; echo after rc=$?')
    assert r.returncode == 0, r.stderr
    assert "after rc=0" in r.stdout
    assert b'menu_driver = "xmb"' in cfg.read_bytes()
    assert (cfgdir / "retroarch.cfg.b1bak").read_bytes() == original

    # hakchi copie ensuite les fichiers du hmod dans le rootfs
    shutil.copytree(transfer / "usr", rootfs / "usr", dirs_exist_ok=True)
    assert (rootfs / "usr/share/snes_usa_b1/retroarch/restore.sh").is_file()

    # uninstall : source, cwd = rootfs, avant les rm de hakchi
    r = _sh(rootfs, f'rootfs="{_posix(rootfs)}"; . "{_posix(transfer)}/uninstall"; echo after rc=$?')
    assert r.returncode == 0, r.stderr
    assert "after rc=0" in r.stdout
    assert cfg.read_bytes() == original
    assert not (cfgdir / "retroarch.cfg.b1bak").exists()
    assert not (rootfs / "usr/share/snes_usa_b1").exists()
    assert do4.read_bytes() == b'audio_latency = "64"\n'
    assert title.read_bytes() == b"user title font"


def test_install_survives_missing_retroarch_cfg(built, tmp_path):
    if not BUSYBOX.is_file():
        pytest.skip("tools/busybox.exe absent")
    transfer = _extract(built, tmp_path)
    r = _sh(transfer, f'rootfs="{_posix(tmp_path / "nothing")}"; . ./install; echo after rc=$?')
    assert "after rc=0" in r.stdout


# ------------------------------------------------------------------ garde-fous

def test_build_theme_refuses_out_in_ref(tmp_path):
    ref = tmp_path / "ref"
    ref.mkdir()
    for out in (ref, ref / "sub", ref / "a" / "b"):
        with pytest.raises(SystemExit):
            build_theme.check_out_dir(ref, out)


def test_build_theme_refuses_out_in_repo():
    for out in (REPO, REPO / "theme", REPO / "theme" / "out"):
        with pytest.raises(SystemExit):
            build_theme.check_out_dir(Path(r"P:\elsewhere\ref"), out)


def test_build_theme_accepts_sibling(tmp_path):
    build_theme.check_out_dir(tmp_path / "ref", tmp_path / "out")


def test_main_refuses_before_writing(tmp_path):
    ref = tmp_path / "ref"
    ref.mkdir()
    with pytest.raises(SystemExit):
        build_theme.main(["--ref", str(ref), "--out", str(ref / "o")])
    assert not (ref / "o").exists()


# ------------------------------------------------------------------ Nintendo

def _sha1(p):
    return hashlib.sha1(p.read_bytes()).hexdigest()


def test_no_nintendo_files_in_repo():
    if not UI_REF.is_dir():
        pytest.skip("ui_ref absent")
    ref = {}
    for p in UI_REF.rglob("*"):
        if p.is_file():
            ref.setdefault(p.stat().st_size, set()).add(_sha1(p))
    for p in (REPO / "theme").rglob("*"):
        if p.is_file() and "__pycache__" not in p.parts and ".pytest_cache" not in p.parts:
            sizes = ref.get(p.stat().st_size)
            if sizes:
                assert _sha1(p) not in sizes, f"fichier identique a ui_ref : {p}"


@pytest.mark.skipif(not (THEME_OUT / "bgm_home.wav").is_file(), reason="theme_out absent")
def test_real_package_has_no_nintendo_file(tmp_path):
    if not UI_REF.is_dir():
        pytest.skip("ui_ref absent")
    out = tmp_path / "r.hmod"
    pack.build(out, THEME_OUT)
    ref = {(p.stat().st_size, _sha1(p)) for p in UI_REF.rglob("*") if p.is_file()}
    for name, (info, data) in _members(out).items():
        if info.isfile():
            assert (len(data), hashlib.sha1(data).hexdigest()) not in ref, name
