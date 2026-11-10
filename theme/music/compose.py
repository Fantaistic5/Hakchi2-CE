"""Musique de menu originale du thème B1 : « Nocturne de la Tour » (titre de travail).

Ré mineur, 135 BPM, 4/4, 32 mesures en forme A - A' - B - A (≈ 56,9 s, boucle).
Composition entièrement originale pour ce dépôt : aucune mélodie, aucun motif,
aucune grille ni aucun échantillon repris d'un jeu existant. Couleur « gothique
16 bits » : clavecin en tête, orgue à tirettes, basse carrée qui avance, batterie
légère (menu écouté plusieurs minutes).

Tempo : le plan visait ~150 BPM, mais 32 mesures de 4/4 à 150 BPM ne font que 51,2 s ;
135 BPM donne 56,9 s (dans 55-65 s) et 4 900 échantillons par double croche à
44 100 Hz, donc une boucle d'un nombre entier d'échantillons et de mesures.

La partition est de la donnée (``SCORE``) : grille d'accords, mélodie note à note,
motifs d'accompagnement. ``arrange`` la transforme en événements, ``render`` en son.

    python -m theme.music.compose --out P:\\HakchiSNESUSA\\theme_out\\bgm_home.wav
"""
from __future__ import annotations

import argparse
import re
import sys
import wave
from dataclasses import dataclass
from pathlib import Path

import numpy as np

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from theme.music import synth  # noqa: E402
else:
    from . import synth

# --------------------------------------------------------------------------- partition
# Mélodie : "Note:durée" en doubles croches (16 par mesure), "r" = silence.
# Accords : un ou deux par mesure ("Dm", "Dm/C" basse imposée, "Edim A" = deux demi-mesures).
_A_CHORDS = ["Dm", "Dm/C", "Bb", "A", "Gm", "Dm/F", "Edim A", "Dm A"]
_A_MELODY = [
    "D5:6 A4:2 D5:2 E5:2 F5:4",
    "E5:2 D5:2 C5:2 D5:2 A4:8",
    "Bb4:2 D5:2 F5:2 Bb5:2 A5:4 G5:4",
    "A5:6 G5:2 E5:4 C#5:4",
    "D5:6 Bb4:2 G4:2 Bb4:2 D5:2 G5:2",
    "F5:4 E5:2 D5:2 A4:4 D5:4",
    "Bb4:2 E5:2 G5:4 E5:2 C#5:2 A4:4",
    "D5:8 A4:2 C#5:2 E5:4",
]

SCORE = {
    "title": "Nocturne de la Tour",
    "key": "D minor",
    "tempo": 135,
    "beats_per_bar": 4,
    "steps_per_beat": 4,
    "breath_steps": 1,      # dernière double croche de la boucle laissée libre (couture)
    "sections": [
        {
            "name": "A",
            "chords": _A_CHORDS,
            "melody": _A_MELODY,
            "lead": "harp", "arp": "eighths", "bass": "pulse",
            "pad": True, "double": False, "drums": "light",
        },
        {
            "name": "A'",
            "chords": ["Dm", "Dm/C", "Bb", "A", "Gm", "C", "F", "A7"],
            "melody": [
                "D5:6 A4:2 D5:2 E5:2 F5:2 G5:2",
                "A5:4 G5:2 F5:2 E5:2 F5:2 D5:4",
                "D5:2 F5:2 Bb5:2 D6:2 C6:4 Bb5:4",
                "A5:4 C#6:2 A5:2 E5:4 A5:4",
                "G5:4 F5:2 E5:2 D5:4 Bb4:4",
                "C5:2 E5:2 G5:2 C6:2 Bb5:4 G5:4",
                "A5:6 G5:2 F5:4 C5:4",
                "E5:4 C#5:2 E5:2 G5:4 A5:4",
            ],
            "lead": "harp", "arp": "sixteenths", "bass": "pulse_walk",
            "pad": True, "double": True, "drums": "light",
            "fills": {7: "....x.......x.xx"},
        },
        {
            "name": "B",
            "chords": ["Bb", "F/A", "Gm", "Dm", "Bb", "C", "A", "A7"],
            "melody": [
                "F5:8 D5:4 Bb4:4",
                "C5:8 F5:4 A5:4",
                "Bb5:8 A5:4 G5:4",
                "A5:12 F5:4",
                "G5:8 F5:4 D5:4",
                "E5:8 G5:4 C6:4",
                "C#6:8 B5:4 A5:4",
                "A5:4 G5:4 E5:4 C#5:4",
            ],
            "lead": "organ", "arp": "sixteenths", "bass": "walk",
            "pad": False, "double": False, "drums": "drive",
            "fills": {7: "....x.......xxxx"},
        },
        {
            "name": "A",
            "chords": _A_CHORDS[:7] + ["A7"],
            "melody": _A_MELODY[:7] + ["E5:2 F5:2 E5:2 D5:2 C#5:4 A4:3 r:1"],
            "lead": "harp", "arp": "eighths", "bass": "pulse",
            "pad": True, "double": False, "drums": "light",
        },
    ],
    # Accompagnement : indices dans l'accord voisé (0 = grave), une valeur par pas.
    "arp_patterns": {
        "eighths": {"step": 2, "idx": [0, 2, 1, 2, 3, 2, 1, 2]},
        "sixteenths": {"step": 1, "idx": [0, 2, 1, 2, 3, 2, 1, 2, 0, 2, 1, 2, 3, 2, 1, 2]},
    },
    # Basse en croches : R fondamentale, O octave, 3 tierce, 5 quinte, A approche de
    # l'accord suivant (degré conjoint de ré mineur au-dessous de sa fondamentale).
    "bass_patterns": {
        "pulse": ["R", "O", "R", "O", "R", "O", "R", "O"],
        "pulse_walk": ["R", "O", "R", "O", "R", "O", "5", "A"],
        "walk": ["R", "O", "3", "5", "O", "5", "3", "A"],
    },
    # Batterie (16 pas) : x frappe, o frappe accentuée, . rien.
    "drum_patterns": {
        "light": {"hat": "x.o.x.o.x.o.x.o.", "snare": "....x.......x..."},
        "drive": {"hat": "x.o.x.oxx.o.x.ox", "snare": "....x.......x..."},
    },
    "mix": {
        "harp_lead": 0.80, "harp_arp": 0.26, "organ_pad": 0.065, "organ_lead": 0.31,
        "organ_double": 0.115, "bass": 0.14, "snare": 0.36, "hat": 0.10,
        "reverb_send": 0.14, "highcut_hz": 11000.0, "rms_dbfs": -18.0,
    },
}

# --------------------------------------------------------------------------- solfège
_PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
_NOTE_RE = re.compile(r"^([A-G])(#|b)?(-?\d)$")
_QUAL = {"": (0, 4, 7), "m": (0, 3, 7), "dim": (0, 3, 6), "7": (0, 4, 7, 10)}
_CHORD_RE = re.compile(r"^([A-G])(#|b)?(m|dim|7)?(?:/([A-G])(#|b)?)?$")
_D_MINOR_PCS = (2, 4, 5, 7, 9, 10, 0, 1)     # ré mineur naturel + sensible do#


def note_midi(name: str) -> int:
    m = _NOTE_RE.match(name)
    if not m:
        raise ValueError(f"note invalide : {name!r}")
    pc = _PC[m.group(1)] + {"#": 1, "b": -1, None: 0}[m.group(2)]
    return 12 * (int(m.group(3)) + 1) + pc


def parse_line(bar: str) -> list[tuple[int | None, int]]:
    """``"D5:6 r:2"`` -> [(62, 6), (None, 2)]."""
    out = []
    for tok in bar.split():
        name, dur = tok.split(":")
        out.append((None if name == "r" else note_midi(name), int(dur)))
    return out


@dataclass(frozen=True)
class Chord:
    root_pc: int
    tones: tuple[int, ...]     # classes de hauteur
    bass_pc: int


def parse_chord(sym: str) -> Chord:
    m = _CHORD_RE.match(sym)
    if not m:
        raise ValueError(f"accord invalide : {sym!r}")
    root = (_PC[m.group(1)] + {"#": 1, "b": -1, None: 0}[m.group(2)]) % 12
    tones = tuple((root + i) % 12 for i in _QUAL[m.group(3) or ""])
    bass = root
    if m.group(4):
        bass = (_PC[m.group(4)] + {"#": 1, "b": -1, None: 0}[m.group(5)]) % 12
    return Chord(root, tones, bass)


def _in_range(pc: int, lo: int) -> int:
    """Plus petite hauteur MIDI >= ``lo`` de classe ``pc``."""
    return lo + (pc - lo) % 12


def voicing(ch: Chord, lo: int = 57, count: int = 4) -> list[int]:
    """Accord serré au-dessus de ``lo`` (la3 par défaut), fondamentale en bas."""
    root = _in_range(ch.root_pc, lo)
    notes = sorted(_in_range(pc, root) for pc in ch.tones)
    while len(notes) < count:
        notes.append(notes[len(notes) - len(ch.tones)] + 12)
    return notes[:count]


# --------------------------------------------------------------------------- arrangement
@dataclass(frozen=True)
class Note:
    track: str          # harp_lead, harp_arp, organ_pad, organ_lead, organ_double, bass, snare, hat
    start: int          # en doubles croches depuis le début de la boucle
    dur: float          # en doubles croches (porte effective)
    midi: int
    vel: float


def steps_per_bar(score: dict) -> int:
    return score["beats_per_bar"] * score["steps_per_beat"]


def bars(score: dict) -> list[dict]:
    """Liste à plat des mesures : accords, mélodie et réglages de section."""
    out = []
    for sec in score["sections"]:
        for i, (ch, mel) in enumerate(zip(sec["chords"], sec["melody"], strict=True)):
            out.append({"section": sec["name"], "index": i, "chords": ch.split(),
                        "melody": mel, "sec": sec})
    return out


def excerpt(score: dict, n_bars: int) -> dict:
    """Partition réduite aux ``n_bars`` premières mesures (tests rapides)."""
    sections, left = [], n_bars
    for sec in score["sections"]:
        if left <= 0:
            break
        k = min(left, len(sec["chords"]))
        s = dict(sec, chords=sec["chords"][:k], melody=sec["melody"][:k])
        sections.append(s)
        left -= k
    return dict(score, sections=sections)


def _approach(next_root_pc: int, lo: int) -> int:
    """Degré de ré mineur (avec do#) juste sous la fondamentale suivante."""
    target = _in_range(next_root_pc, lo)
    for d in (1, 2):
        if (target - d) % 12 in _D_MINOR_PCS:
            return target - d
    return target - 1


def arrange(score: dict) -> list[Note]:
    spb = steps_per_bar(score)
    bl = bars(score)
    notes: list[Note] = []
    # suite des accords (pour l'approche de basse), en boucle
    chord_seq = []
    for b in bl:
        for c in b["chords"]:
            chord_seq.append(parse_chord(c))
    ci = 0
    for bi, b in enumerate(bl):
        sec = b["sec"]
        t0 = bi * spb
        # mélodie
        t = t0
        for k, (m, d) in enumerate(parse_line(b["melody"])):
            if m is not None:
                vel = 1.0 if (t - t0) % 4 == 0 else 0.86
                if sec["lead"] == "harp":
                    notes.append(Note("harp_lead", t, d * 0.92, m, vel))
                    if sec["double"]:
                        notes.append(Note("organ_double", t, d * 0.95, m - 12, 0.9))
                else:
                    notes.append(Note("organ_lead", t, d * 0.96, m, vel))
            t += d
        if t - t0 != spb:
            raise ValueError(f"mesure {bi + 1} : {t - t0} doubles croches au lieu de {spb}")
        # accords
        parts = b["chords"]
        span = spb // len(parts)
        for pi, sym in enumerate(parts):
            ch = chord_seq[ci]
            nxt = chord_seq[(ci + 1) % len(chord_seq)]
            ci += 1
            cs = t0 + pi * span
            # arpège de clavecin
            pat = score["arp_patterns"][sec["arp"]]
            v = voicing(ch)
            first = pi * span
            for j in range(first, first + span, pat["step"]):
                idx = pat["idx"][j % len(pat["idx"])]
                acc = 0.9 if j % 4 == 0 else 0.72
                notes.append(Note("harp_arp", t0 + j, pat["step"] * 0.9, v[idx], acc))
            # nappe d'orgue
            if sec["pad"]:
                for m in voicing(ch, lo=53, count=3):
                    notes.append(Note("organ_pad", cs, span * 0.97, m, 0.9))
            # basse en croches
            root = _in_range(ch.bass_pc, 33)
            third = _in_range(ch.tones[1], root)
            fifth = _in_range(ch.tones[2], root)
            bpat = score["bass_patterns"][sec["bass"]]
            for j in range(0, span, 2):
                code = bpat[((first + j) // 2) % len(bpat)]
                if code == "A" and j != span - 2:
                    code = "R"
                m = {"R": root, "O": root + 12, "3": third, "5": fifth}.get(code)
                if code == "A":
                    m = _approach(nxt.bass_pc, 33)
                vel = 1.0 if j % 4 == 0 else 0.82
                notes.append(Note("bass", cs + j, 2 * 0.78, m, vel))
        # batterie
        dp = dict(score["drum_patterns"][sec["drums"]])
        fill = sec.get("fills", {}).get(b["index"])
        if fill:
            dp["snare"] = fill
        for inst in ("hat", "snare"):
            for j, c in enumerate(dp[inst]):
                if c in "xo":
                    vel = 1.0 if c == "o" else 0.7
                    if inst == "snare" and fill and j >= 13:
                        vel = 0.45 + 0.15 * (j - 12)
                    notes.append(Note(inst, t0 + j, 1, 0, vel))
    # respiration de la couture : rien ne joue sur la dernière double croche
    total = len(bl) * spb
    limit = total - score.get("breath_steps", 0)
    out = []
    for n in notes:
        if n.start >= limit:
            continue
        if n.start + n.dur > limit:
            n = Note(n.track, n.start, limit - n.start, n.midi, n.vel)
        out.append(n)
    return out


# --------------------------------------------------------------------------- rendu
def samples_per_step(score: dict, sample_rate: int = 44100) -> float:
    return sample_rate * 60.0 / score["tempo"] / score["steps_per_beat"]


def loop_samples(score: dict, sample_rate: int = 44100) -> int:
    n_steps = len(bars(score)) * steps_per_bar(score)
    return int(round(n_steps * samples_per_step(score, sample_rate)))


_PAN = {
    "harp_lead": (-0.28, 0.28), "harp_arp": (-0.45, 0.05), "organ_pad": (0.20, 0.45),
    "organ_lead": (-0.15, 0.15), "organ_double": (0.10, 0.30), "bass": (0.0, 0.0),
    "snare": (0.05, 0.05), "hat": (0.35, 0.35),
}
_DETUNE = {"harp_lead": 3.0, "harp_arp": 2.5, "organ_pad": 2.0, "organ_lead": 1.5,
           "organ_double": 2.0}


def _voice(n: Note, dur_s: float, side: int, sr: int) -> np.ndarray:
    cents = _DETUNE.get(n.track, 0.0) * (1 if side else -1)
    if n.track.startswith("harp"):
        return synth.harpsichord(n.midi, dur_s, n.vel, detune_cents=cents, variant=side, sr=sr)
    if n.track == "organ_lead":
        return synth.organ(n.midi, dur_s, n.vel, drawbars=synth.DRAWBARS_LEAD,
                           detune_cents=cents, sr=sr)
    if n.track.startswith("organ"):
        return synth.organ(n.midi, dur_s, n.vel, drawbars=synth.DRAWBARS_PAD,
                           detune_cents=cents, sr=sr)
    if n.track == "bass":
        return synth.square_bass(n.midi, dur_s, n.vel, sr=sr)
    if n.track == "snare":
        return synth.snare(0, sr) * n.vel
    return synth.hat(0, sr) * n.vel


def render_stems(score: dict, sample_rate: int = 44100) -> tuple[np.ndarray, np.ndarray]:
    """Mélange sec (L, 2) et envoi réverbe (L, 2) en flottants, queues repliées."""
    sps = samples_per_step(score, sample_rate)
    L = loop_samples(score, sample_rate)
    tail = int(3.0 * sample_rate)
    dry = np.zeros((L + tail, 2))
    send = np.zeros((L + tail, 2))
    mix = score["mix"]
    for n in arrange(score):
        start = int(round(n.start * sps))
        dur_s = n.dur * sps / sample_rate
        gain = mix[n.track]
        stereo = n.track in _DETUNE
        sides = (0, 1) if stereo else (0,)
        for side in sides:
            y = _voice(n, dur_s, side, sample_rate) * gain
            if stereo:
                y = y / np.sqrt(2.0)
            pan = _PAN[n.track][side]
            gl, gr = synth.pan_gains(pan)
            seg = slice(start, start + y.size)
            dry[seg, 0] += gl * y
            dry[seg, 1] += gr * y
            if n.track != "bass":
                send[seg, 0] += gl * y
                send[seg, 1] += gr * y
    # repli circulaire des queues qui dépassent la fin de boucle
    for buf in (dry, send):
        buf[: tail] += buf[L:]
    return dry[:L], send[:L] * mix["reverb_send"]


def render(score: dict = SCORE, sample_rate: int = 44100) -> np.ndarray:
    """Boucle stéréo int16 (n, 2), d'un nombre entier de mesures, sans couture."""
    dry, send = render_stems(score, sample_rate)
    x = dry + synth.circular_reverb(send, sample_rate)
    x = synth.circular_highcut(x, score["mix"]["highcut_hz"], sample_rate)
    x -= x.mean(axis=0)
    rms = np.sqrt(np.mean(x ** 2))
    target = 10 ** (score["mix"]["rms_dbfs"] / 20.0)
    x *= target / (rms + 1e-12)
    x = synth.soft_limit(x)
    return np.clip(np.round(x * 32767.0), -32768, 32767).astype(np.int16)


def write_wav(path: str | Path, audio: np.ndarray, sample_rate: int = 44100) -> None:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(audio.shape[1])
        w.setsampwidth(2)
        w.setframerate(sample_rate)
        w.writeframes(np.ascontiguousarray(audio, dtype="<i2").tobytes())


def measure(audio: np.ndarray) -> dict:
    x = audio.astype(np.float64) / 32768.0
    peak = np.max(np.abs(x))
    rms = np.sqrt(np.mean(x ** 2))
    jump = np.max(np.abs(x[0] - x[-1]))
    inner = np.max(np.abs(np.diff(x, axis=0)))
    w = int(0.05 * 44100)
    return {
        "seconds": audio.shape[0] / 44100.0,
        "peak_dbfs": 20 * np.log10(peak + 1e-12),
        "rms_dbfs": 20 * np.log10(rms + 1e-12),
        "seam_jump_pct_fs": 100 * jump,
        "max_step_pct_fs": 100 * inner,
        "rms_last50ms_dbfs": 20 * np.log10(np.sqrt(np.mean(x[-w:] ** 2)) + 1e-12),
        "rms_first50ms_dbfs": 20 * np.log10(np.sqrt(np.mean(x[:w] ** 2)) + 1e-12),
    }


def write_spectrogram(path: str | Path, audio: np.ndarray, score: dict = SCORE,
                      sample_rate: int = 44100, width: int = 1400, height: int = 480) -> None:
    """Spectrogramme (fréquence log 30 Hz - 20 kHz, -90..0 dB), traits aux sections."""
    from PIL import Image, ImageDraw

    x = audio.astype(np.float64).mean(axis=1) / 32768.0
    nfft = 4096
    hop = max(1, (x.size - nfft) // width)
    win = np.hanning(nfft)
    frames = np.stack([x[i * hop: i * hop + nfft] * win for i in range(width)])
    mag = np.abs(np.fft.rfft(frames, axis=1)) / (nfft / 4)
    freqs = np.fft.rfftfreq(nfft, 1.0 / sample_rate)
    fy = np.geomspace(30.0, 20000.0, height)[::-1]
    idx = np.clip(np.searchsorted(freqs, fy), 0, freqs.size - 1)
    db = 20 * np.log10(mag[:, idx].T + 1e-9)
    v = np.clip((db + 90.0) / 90.0, 0, 1)
    stops = np.array([[0, 0, 4], [40, 10, 90], [150, 30, 110], [230, 90, 40],
                      [250, 200, 60], [255, 255, 230]], float)
    pos = v * (len(stops) - 1)
    i0 = np.floor(pos).astype(int).clip(0, len(stops) - 2)
    fr = (pos - i0)[..., None]
    rgb = stops[i0] * (1 - fr) + stops[i0 + 1] * fr
    img = Image.fromarray(rgb.astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    n_bars = len(bars(score))
    bar = 0
    for sec in score["sections"]:
        xx = int(bar / n_bars * width)
        d.line([(xx, 0), (xx, height)], fill=(80, 255, 80))
        d.text((xx + 3, 3), sec["name"], fill=(80, 255, 80))
        bar += len(sec["chords"])
    for f in (100, 1000, 5000, 10000):
        yy = int(np.searchsorted(-fy, -f))
        d.line([(0, yy), (12, yy)], fill=(200, 200, 200))
        d.text((14, yy - 6), f"{f}", fill=(200, 200, 200))
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    img.save(path)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", required=True, help="WAV de la boucle (bgm_home.wav)")
    ap.add_argument("--x2", help="WAV de contrôle : la boucle deux fois (défaut <out>_x2.wav)")
    ap.add_argument("--spectrogram", help="PNG (défaut bgm_spectrogram.png à côté de --out)")
    a = ap.parse_args(argv)
    out = Path(a.out)
    audio = render(SCORE)
    write_wav(out, audio)
    x2 = Path(a.x2) if a.x2 else out.with_name(out.stem + "_x2.wav")
    write_wav(x2, np.concatenate([audio, audio]))
    spec = Path(a.spectrogram) if a.spectrogram else out.with_name("bgm_spectrogram.png")
    write_spectrogram(spec, audio)
    for k, v in measure(audio).items():
        print(f"{k:20s} {v:9.3f}")
    print(out, x2, spec, sep="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
