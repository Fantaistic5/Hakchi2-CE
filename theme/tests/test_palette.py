from theme.b1 import palette as p


def test_palette_exact():
    assert p.SLATE == (0x1C, 0x1B, 0x22)
    assert p.RULE == (0x2C, 0x2A, 0x34)
    assert p.VIOLET == (0x4A, 0x3C, 0x8C)
    assert p.STRIPE_LIGHT == (0x6B, 0x5B, 0xB5)
    assert p.STRIPE_DARK == (0x57, 0x47, 0xA0)
    assert p.LAVENDER == (0xA7, 0x9F, 0xD6)
    assert p.TEXT == (0xE4, 0xE2, 0xEA)
    assert p.TEXT_DIM == (0x8E, 0x8A, 0x9C)
    assert len(p.ALL) == 8
    assert all(isinstance(v, tuple) and len(v) == 3 for v in p.ALL.values())
