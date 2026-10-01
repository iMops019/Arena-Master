"""The Warrior's Reaver tree: its thrown axes, bleeding and blood. Written by make_placeholder_models.py (write_all).

  reaver_axe.glb        a thrown hand axe, like each of the Warrior hero's two (tools/hero_models.py): a wooden haft with a leather grip, a broad steel head with a
                        pale edge forward (+Z) and an iron spike behind, about 0.76 m long. Centred on the point it spins round, a little below the head, with the
                        haft along Y, so the game turns it end over end with the crowd's pitch as it flies.
  blood_pool.glb        a pool of blood on the ground, radius about 1 (scaled to Bloodbath's 2 m): a ragged dark-red blotch, a wetter bright middle, and a few
                        splashes round its edge
  blood_drop.glb        a drop of blood about 0.1 m tall, falling from a bleeding enemy (drawn in hundreds, tiny)
  axe_storm_ring.glb    a thin blood-red ring of radius 1 with short marks round it like a spinning blade's trail, scaled to the Axe Storm's circle round the Warrior

Uses only colours already in make_placeholder_models.PALETTE: adding one changes every model's texture.
"""

import math

from make_placeholder_models import Mesh, write_glb, build_ring, _unit_noise

# Where the thrown axe spins round, in the hero's axe's own heights (its haft runs from 0.66 to 1.42, its head from 1.1 to 1.44).
AXE_PIVOT = 1.14


def build_reaver_axe():
    """A hand axe, the same as the hero's: the haft up the Y axis, the head at the top with its edge forward (+Z), centred on its pivot."""
    m = Mesh()
    p = AXE_PIVOT
    m.box(-0.03, 0.66 - p, -0.03, 0.03, 1.42 - p, 0.03, "wood")
    m.box(-0.035, 0.74 - p, -0.035, 0.035, 0.86 - p, 0.035, "leather")
    m.box(-0.02, 1.14 - p, 0.03, 0.02, 1.40 - p, 0.20, "steel")
    m.box(-0.015, 1.10 - p, 0.20, 0.015, 1.44 - p, 0.23, "fletching")
    m.box(-0.015, 1.24 - p, -0.11, 0.015, 1.30 - p, -0.03, "iron")
    m.box(-0.035, 1.36 - p, -0.035, 0.035, 1.42 - p, 0.035, "iron")   # the cap on the haft's end
    return m


def build_blood_pool():
    """A pool of blood, radius about 1: a ragged dark blotch, a wet bright middle, and a few splashes round it."""
    m = Mesh()
    up = (0.0, 1.0, 0.0)
    sides = 18
    edge = [(math.cos(math.tau * i / sides) * (0.75 + 0.25 * _unit_noise(i, 3.1)), math.sin(math.tau * i / sides) * (0.75 + 0.25 * _unit_noise(i, 3.1)))
            for i in range(sides)]
    for i in range(sides):
        (xa, za), (xb, zb) = edge[i], edge[(i + 1) % sides]
        m.facing([(0.0, 0.0, 0.0), (xa, 0.0, za), (xb, 0.0, zb)], up, "blood")
        m.facing([(0.05, 0.01, -0.04), (0.05 + xa * 0.5, 0.01, -0.04 + za * 0.5), (0.05 + xb * 0.5, 0.01, -0.04 + zb * 0.5)], up, "blood_light")
    for n in range(7):
        a, d = math.tau * _unit_noise(n, 6.7), 0.85 + 0.2 * _unit_noise(n, 8.3)
        x, z, s = math.cos(a) * d, math.sin(a) * d, 0.04 + 0.06 * _unit_noise(n, 1.9)
        m.facing([(x - s, 0.005, z), (x, 0.005, z - s), (x + s, 0.005, z), (x, 0.005, z + s)], up, "blood")
    return m


def build_blood_drop():
    """A drop of blood about 0.1 m tall: a bright rounded underside and a darker point on top, centred on its middle."""
    m = Mesh()
    ring = [(math.cos(math.tau * k / 6) * 0.03, 0.0, math.sin(math.tau * k / 6) * 0.03) for k in range(6)]
    top, bottom = (0.0, 0.065, 0.0), (0.0, -0.035, 0.0)
    for k in range(6):
        a, b = ring[k], ring[(k + 1) % 6]
        m.tri(a, top, b, "blood")
        m.tri(b, bottom, a, "blood_light")
    return m


def build_axe_storm_ring():
    """The Axe Storm's circle, radius 1: a thin blood-red ring, and short marks leaning round it like the trail of a spinning blade."""
    m = build_ring(0.95, 1.0, "blood_light")
    up = (0.0, 1.0, 0.0)
    for k in range(24):
        a0 = math.tau * k / 24
        a1 = a0 + 0.12
        m.facing([(math.cos(a0) * 0.9, 0.004, math.sin(a0) * 0.9), (math.cos(a0) * 0.95, 0.004, math.sin(a0) * 0.95),
                  (math.cos(a1) * 0.95, 0.004, math.sin(a1) * 0.95)], up, "blood")
    return m


REAVER_MODELS = (("reaver_axe.glb", build_reaver_axe), ("blood_pool.glb", build_blood_pool), ("blood_drop.glb", build_blood_drop),
                 ("axe_storm_ring.glb", build_axe_storm_ring))


def write_all(models_dir):
    """Writes this tree's models into <models_dir>."""
    for name, build in REAVER_MODELS:
        mesh = build()
        write_glb(mesh, models_dir / name)
        print(f"Wrote {models_dir / name} ({len(mesh.positions)} vertices, {len(mesh.indices) // 3} triangles)")
