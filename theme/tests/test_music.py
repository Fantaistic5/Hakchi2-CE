"""Tests de la musique de menu originale (theme.music)."""
import wave

import numpy as np
import pytest

from theme.music import compose, synth

SR = 44100
FS = 32768.0


@pytest.fixture(scope="module")
def audio():
    return compose.render(compose.SCORE, sample_rate=SR)


def test_format(audio, tmp_path):
    assert audio.dtype == np.int16
    assert audio.ndim == 2 and audio.shape[1] == 2
    path = tmp_path / "bgm_home.wav"
    compose.write_wav(path, audio, SR)
    with wave.open(str(path), "rb") as w:
        assert w.getnchannels() == 2
        assert w.getsampwidth() == 2
        assert w.getframerate() == SR
        assert w.getnframes() == audio.shape[0]
        data = np.frombuffer(w.readframes(w.getnframes()), "<i2").reshape(-1, 2)
    assert np.array_equal(data, audio)


def test_length(audio):
    seconds = audio.shape[0] / SR
    assert 55.0 <= seconds <= 65.0
    per_bar = compose.samples_per_step(compose.SCORE, SR) * compose.steps_per_bar(compose.SCORE)
    assert audio.shape[0] / per_bar == pytest.approx(round(audio.shape[0] / per_bar), abs=1e-9)
    assert audio.shape[0] == compose.loop_samples(compose.SCORE, SR)


def test_loop_seam(audio):
    x = audio.astype(np.float64) / FS
    assert np.max(np.abs(x[-1] - x[0])) < 0.005
    w = np.concatenate([x[-16:], x[:16]])            # 32 échantillons autour de la couture
    d2 = np.abs(np.diff(w, n=2, axis=0))             # d2[i] centré sur w[i + 1]
    seam = d2[14:16].max()                           # centrés sur x[-1] et x[0]
    around = np.delete(d2, [14, 15], axis=0).max()
    assert seam <= max(3.0 * around, 0.005)


def test_headroom(audio):
    x = audio.astype(np.float64) / FS
    peak_db = 20 * np.log10(np.max(np.abs(x)))
    rms_db = 20 * np.log10(np.sqrt(np.mean(x ** 2)))
    assert peak_db <= -1.0
    assert -20.0 <= rms_db <= -14.0


def test_deterministic():
    short = compose.excerpt(compose.SCORE, 2)
    a = compose.render(short, sample_rate=SR)
    for cached in (synth._ks, synth.snare, synth.hat):   # vrai second calcul, sans cache
        cached.cache_clear()
    b = compose.render(short, sample_rate=SR)
    assert a.shape == (compose.loop_samples(short, SR), 2)
    assert np.array_equal(a, b)


def test_score_form():
    sc = compose.SCORE
    assert [s["name"] for s in sc["sections"]] == ["A", "A'", "B", "A"]
    assert len(compose.bars(sc)) == 32
    assert sc["key"] == "D minor"
    compose.arrange(sc)                               # chaque mesure fait 16 doubles croches


def test_bar_length_checked():
    bad = compose.excerpt(compose.SCORE, 1)
    bad["sections"][0] = dict(bad["sections"][0], melody=["D5:4"])
    with pytest.raises(ValueError):
        compose.arrange(bad)


def test_breath_before_seam():
    sc = compose.SCORE
    total = len(compose.bars(sc)) * compose.steps_per_bar(sc)
    assert all(n.start + n.dur <= total - sc["breath_steps"] + 1e-9 for n in compose.arrange(sc))


def test_note_and_chord_parsing():
    assert compose.note_midi("A4") == 69
    assert compose.note_midi("C#5") == 73
    assert compose.note_midi("Bb4") == 70
    assert compose.parse_line("D5:6 r:2") == [(74, 6), (None, 2)]
    ch = compose.parse_chord("Dm/C")
    assert ch.tones == (2, 5, 9) and ch.bass_pc == 0
    assert compose.parse_chord("Edim").tones == (4, 7, 10)
    assert compose.parse_chord("A7").tones == (9, 1, 4, 7)


def test_harpsichord_pitch():
    f0 = synth.midi_hz(69)
    y = synth.harpsichord(69, 0.5, 1.0, sr=SR)[: int(0.4 * SR)]
    spec = np.abs(np.fft.rfft(y * np.hanning(y.size), 1 << 16))
    freqs = np.fft.rfftfreq(1 << 16, 1.0 / SR)
    lo = (freqs > 300) & (freqs < 600)
    assert abs(freqs[lo][np.argmax(spec[lo])] - f0) < 2.0
    assert abs(y[0]) < 1e-3                           # attaque en rampe, pas de clic


def test_voices_quiet_tail():
    for y in (synth.harpsichord(62, 0.2), synth.organ(62, 0.2), synth.square_bass(38, 0.2)):
        assert np.max(np.abs(y[-32:])) < 0.01


def _chunks(raw):
    assert raw[:4] == b"RIFF" and raw[8:12] == b"WAVE"
    import struct
    assert struct.unpack("<I", raw[4:8])[0] == len(raw) - 8
    out, p = [], 12
    while p < len(raw):
        cid, sz = raw[p:p + 4], struct.unpack("<I", raw[p + 4:p + 8])[0]
        out.append((cid, raw[p + 8:p + 8 + sz]))
        p += 8 + sz + (sz & 1)
    assert p == len(raw)
    return out


def test_wav_has_smpl_loop_and_inst(audio, tmp_path):
    import struct
    path = tmp_path / "bgm_home.wav"
    compose.write_wav(path, audio, SR)
    ch = _chunks(path.read_bytes())
    assert [c for c, _ in ch] == [b"fmt ", b"data", b"smpl", b"inst"]
    d = dict(ch)
    assert d[b"data"] == np.ascontiguousarray(audio, "<i2").tobytes()
    smpl = d[b"smpl"]
    assert len(smpl) == 60
    f = struct.unpack("<15I", smpl)
    assert f[2] == round(1e9 / SR) and f[3] == 60
    assert f[7] == 1  # une boucle
    assert f[10] == 0 and f[11] == 0 and f[12] == audio.shape[0] - 1  # type avant, debut, fin
    assert d[b"inst"] == bytes([60, 0, 0, 0, 127, 1, 127])
    with wave.open(str(path), "rb") as w:
        assert w.getnframes() == audio.shape[0]


def test_circular_reverb_is_pinned():
    """Le reverb (echos circulaires diffus, rad/s voulu) est le son approuve : empreinte figee."""
    import hashlib
    x = np.random.default_rng(1234).standard_normal((4410, 2)) * 0.1
    y = synth.circular_reverb(x, SR)
    q = np.round(y * 1e5).astype(np.int64)
    assert hashlib.sha256(q.tobytes()).hexdigest() == "cbe0365e09d4a34c07b033ee34279381c1cb66525f39be43f4b582136eecbb87"
