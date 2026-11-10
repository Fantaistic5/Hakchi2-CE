"""Voix et traitements du thème musical B1 (numpy seul, sans scipy).

Voix :

* ``harpsichord`` : clavecin Karplus-Strong (excitation filtrée, paire désaccordée
  rendue par l'appelant, étouffoir à la relâche) ;
* ``organ``       : orgue additif à tirettes (16', 8', 4', 2 2/3', 2'), vibrato léger ;
* ``square_bass`` : carrée à bande limitée, filtre passe-bas à enveloppe ;
* ``snare`` / ``hat`` : caisse claire et charleston bruités.

Traitements « circulaires » : le morceau est une boucle, donc tout filtre linéaire
(réverbération, coupe-haut du bus) est appliqué dans le domaine fréquentiel sur la
période entière. Le résultat est l'état stationnaire périodique exact : la queue de
réverbération de la fin retombe sur le début, sans clic ni trou à la couture.

Toutes les sources de bruit sont tirées de générateurs à graine fixe : deux rendus
sont identiques à l'échantillon près.
"""
from __future__ import annotations

import zlib
from functools import lru_cache

import numpy as np

SR = 44100


def midi_hz(midi: float) -> float:
    return 440.0 * 2.0 ** ((midi - 69.0) / 12.0)


def _rng(*key) -> np.random.Generator:
    return np.random.default_rng(zlib.crc32(repr(key).encode()))


def _ramp(n: int) -> np.ndarray:
    """Demi-cosinus 0 -> 1 sur ``n`` échantillons."""
    if n <= 0:
        return np.ones(0)
    return 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, n, endpoint=False))


def _gate_env(n_on: int, n_rel: int, attack: int) -> np.ndarray:
    """Enveloppe porte : attaque en demi-cosinus, plateau, relâche exponentielle."""
    env = np.ones(n_on + n_rel)
    a = min(attack, n_on)
    env[:a] = _ramp(a)
    if n_rel:
        t = np.arange(n_rel)
        rel = np.exp(-5.0 * t / n_rel) * (1.0 - t / n_rel)
        env[n_on:] = rel * env[n_on - 1] if n_on else rel
    return env


# --------------------------------------------------------------------------- clavecin
@lru_cache(maxsize=4096)
def _ks(freq: float, n: int, seed: int, cutoff: float, t60: float, sr: int) -> np.ndarray:
    """Corde Karplus-Strong accordée par ré-échantillonnage (lecture seule)."""
    period = sr / freq                      # période voulue (échantillons)
    N = max(2, int(round(period - 0.5)))    # la moyenne à 2 points ajoute 0,5
    f_ks = sr / (N + 0.5)
    ratio = freq / f_ks                     # vitesse de lecture
    m = int(np.ceil(n * ratio)) + 2

    rng = np.random.default_rng(seed)
    exc = rng.uniform(-1.0, 1.0, N)
    # excitation passe-bas (4e ordre en module) : attaque franche mais pas de grésillement
    f = np.fft.rfftfreq(N, 1.0 / (N * f_ks))
    exc = np.fft.irfft(np.fft.rfft(exc) / np.sqrt(1.0 + (f / cutoff) ** 8), N)
    exc -= exc.mean()
    exc -= 0.55 * np.roll(exc, max(1, N // 7))   # position de pincement (plectre)
    exc /= np.max(np.abs(exc)) + 1e-12

    rho = 0.001 ** (1.0 / (f_ks * t60))     # pertes supplémentaires par période
    nb = m // N + 2
    out = np.empty(nb * N)
    out[:N] = exc
    prev2_last = 0.0
    for k in range(1, nb):
        prev = out[(k - 1) * N:k * N]
        shifted = np.empty(N)
        shifted[0] = prev2_last
        shifted[1:] = prev[:-1]
        prev2_last = prev[-1]
        out[k * N:(k + 1) * N] = rho * 0.5 * (prev + shifted)
    pos = np.arange(n) * ratio
    y = np.interp(pos, np.arange(out.size), out)
    y.setflags(write=False)
    return y


def harpsichord(midi: float, dur_s: float, vel: float = 1.0, *, detune_cents: float = 0.0,
                variant: int = 0, sr: int = SR) -> np.ndarray:
    """Une corde de clavecin : tenue ``dur_s`` puis étouffoir (40 ms)."""
    freq = midi_hz(midi) * 2.0 ** (detune_cents / 1200.0)
    n_on = max(1, int(round(dur_s * sr)))
    n_rel = int(0.040 * sr)
    t60 = float(np.clip(2.2 * (220.0 / freq) ** 0.35, 0.6, 3.5))
    cutoff = 3000.0 if vel > 0.8 else 2300.0
    seed = zlib.crc32(repr(("harp", round(midi, 3), variant)).encode())
    y = _ks(round(freq, 6), n_on + n_rel, seed, cutoff, t60, sr)
    return y * _gate_env(n_on, n_rel, int(0.0025 * sr)) * vel


# --------------------------------------------------------------------------- orgue
DRAWBARS_PAD = (0.30, 1.00, 0.30, 0.10, 0.06)    # 16' 8' 4' 2 2/3' 2'
DRAWBARS_LEAD = (0.20, 1.00, 0.50, 0.22, 0.14)
_RATIOS = (0.5, 1.0, 2.0, 3.0, 4.0)


def organ(midi: float, dur_s: float, vel: float = 1.0, *, drawbars=DRAWBARS_PAD,
          detune_cents: float = 0.0, sr: int = SR) -> np.ndarray:
    freq = midi_hz(midi) * 2.0 ** (detune_cents / 1200.0)
    n_on = max(1, int(round(dur_s * sr)))
    n_rel = int(0.080 * sr)
    n = n_on + n_rel
    t = np.arange(n) / sr
    vib = 1.0 + 0.0018 * np.sin(2 * np.pi * 5.6 * t) * np.clip(t / 0.25, 0, 1)
    phase = 2 * np.pi * freq * np.cumsum(vib) / sr
    y = np.zeros(n)
    for r, w in zip(_RATIOS, drawbars):
        if w and freq * r < 0.45 * sr:
            y += w * np.sin(r * phase)
    y /= sum(drawbars)
    return y * _gate_env(n_on, n_rel, int(0.012 * sr)) * vel


# --------------------------------------------------------------------------- basse
def square_bass(midi: float, dur_s: float, vel: float = 1.0, *, sr: int = SR) -> np.ndarray:
    """Carrée à bande limitée (harmoniques impairs), passe-bas 2e ordre à enveloppe."""
    freq = midi_hz(midi)
    n_on = max(1, int(round(dur_s * sr)))
    n_rel = int(0.030 * sr)
    n = n_on + n_rel
    t = np.arange(n) / sr
    fc = 260.0 + 1500.0 * vel * np.exp(-t / 0.07)
    kmax = int(min(6000.0, 0.45 * sr) // freq)
    y = np.zeros(n)
    for k in range(1, kmax + 1, 2):
        a = (4 / np.pi) / k / np.sqrt(1.0 + (k * freq / fc) ** 4)
        y += a * np.sin(2 * np.pi * k * freq * t)
    amp = 0.72 + 0.28 * np.exp(-t / 0.12)
    return y * amp * _gate_env(n_on, n_rel, int(0.004 * sr)) * vel


# --------------------------------------------------------------------------- batterie
def _band(x: np.ndarray, lo: float, hi: float, sr: int) -> np.ndarray:
    f = np.fft.rfftfreq(x.size, 1.0 / sr)
    g = np.ones_like(f)
    if lo:
        g *= 1.0 / np.sqrt(1.0 + (lo / np.maximum(f, 1.0)) ** 4)
    if hi:
        g *= 1.0 / np.sqrt(1.0 + (f / hi) ** 4)
    return np.fft.irfft(np.fft.rfft(x) * g, x.size)


@lru_cache(maxsize=8)
def snare(variant: int = 0, sr: int = SR) -> np.ndarray:
    n = int(0.22 * sr)
    t = np.arange(n) / sr
    noise = _band(_rng("snare", variant).uniform(-1, 1, n), 1100.0, 5000.0, sr)
    noise /= np.max(np.abs(noise))
    f = 190.0 - 40.0 * (1 - np.exp(-t / 0.03))
    body = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t / 0.035)
    y = 0.75 * noise * np.exp(-t / 0.05) + 0.55 * body
    y[: int(0.001 * sr)] *= _ramp(int(0.001 * sr))
    y /= np.max(np.abs(y))
    y.setflags(write=False)
    return y


@lru_cache(maxsize=8)
def hat(variant: int = 0, sr: int = SR) -> np.ndarray:
    n = int(0.07 * sr)
    t = np.arange(n) / sr
    noise = _band(_rng("hat", variant).uniform(-1, 1, n), 6500.0, 11000.0, sr)
    noise /= np.max(np.abs(noise))
    y = noise * np.exp(-t / 0.016)
    y[: int(0.0007 * sr)] *= _ramp(int(0.0007 * sr))
    y.setflags(write=False)
    return y


# --------------------------------------------------------------------------- bus circulaires
def pan_gains(pan: float) -> tuple[float, float]:
    """Panoramique à puissance constante, ``pan`` dans [-1, 1]."""
    a = (pan + 1.0) * np.pi / 4.0
    return float(np.cos(a)), float(np.sin(a))


def circular_highcut(x: np.ndarray, fc: float, sr: int = SR) -> np.ndarray:
    """Coupe-haut doux (2e ordre en module) appliqué à une boucle (axe 0)."""
    f = np.fft.rfftfreq(x.shape[0], 1.0 / sr)
    g = 1.0 / np.sqrt(1.0 + (f / fc) ** 4)
    return np.fft.irfft(np.fft.rfft(x, axis=0) * g[:, None], x.shape[0], axis=0)


def circular_reverb(x: np.ndarray, sr: int = SR, t60: float = 1.3,
                    predelay_ms: float = 14.0, damp: float = 0.5,
                    return_cut: float = 3200.0) -> np.ndarray:
    """Réverbération Schroeder (4 peignes amortis + 2 passe-tout par canal) calculée
    comme réponse en fréquence sur la boucle entière : sortie 100 % mouillée, retour
    coupé au-dessus de ``return_cut`` (queue sombre, pas de sifflement)."""
    n = x.shape[0]
    X = np.fft.rfft(x, axis=0)
    w = 2 * np.pi * np.fft.rfftfreq(n, 1.0 / sr)
    z1 = np.exp(-1j * w)
    lp = (1.0 - damp) / (1.0 - damp * z1)
    combs = {0: (29.7, 37.1, 41.1, 43.7), 1: (30.9, 35.8, 42.6, 45.3)}
    aps = {0: (5.0, 1.7), 1: (5.3, 1.9)}
    out = np.empty_like(X)
    pre = np.exp(-1j * w * predelay_ms * 1e-3 * sr)
    pre = pre / np.sqrt(1.0 + (w / (2 * np.pi * return_cut)) ** 4)
    for ch in range(x.shape[1]):
        H = np.zeros_like(w, dtype=complex)
        for ms in combs[ch]:
            D = ms * 1e-3 * sr
            g = 10 ** (-3.0 * D / (t60 * sr))
            H += 1.0 / (1.0 - g * lp * np.exp(-1j * w * D))
        H /= len(combs[ch])
        for ms in aps[ch]:
            zD = np.exp(-1j * w * ms * 1e-3 * sr)
            H *= (-0.7 + zD) / (1.0 - 0.7 * zD)
        out[:, ch] = X[:, ch] * H * pre
    return np.fft.irfft(out, n, axis=0)


def soft_limit(x: np.ndarray, threshold: float = 0.70, ceiling: float = 0.88) -> np.ndarray:
    """Limiteur sans mémoire (donc sans effet sur la couture) : linéaire sous le seuil,
    tangente hyperbolique au-dessus, jamais au-delà de ``ceiling``."""
    a = np.abs(x)
    over = a > threshold
    y = x.copy()
    k = ceiling - threshold
    y[over] = np.sign(x[over]) * (threshold + k * np.tanh((a[over] - threshold) / k))
    return y
