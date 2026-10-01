"""The Mage's Pyromancy tree: its fire bolts, burns, fire trails and meteors. Written by make_placeholder_models.py (write_all).

  pyro_bolt.glb         a Fire Barrage bolt: a spiky orb of flame with a bright core and a tail of fire streaming back, centred on the orb, pointing +Z
  pyro_burst.glb        a flat ring of radius 1 with tongues of flame standing on it, leaning out, scaled as a burst of fire spreads (Combustion, the Fireball,
                        a meteor landing)
  pyro_burn.glb         the flames licking round a burning enemy: tongues of fire round a body 1.8 m tall, from its shins to its chest (scaled to the enemy)
  pyro_fire.glb         a patch of fire on the ground about 1.8 m across: a scorched blotch with tongues of flame on it, the tallest in the middle (laid in a row
                        along Fire Walk's line of fire, and scattered over a meteor's burning crater)
  pyro_meteor.glb       a meteor: a lump of dark rock about 1.8 m across, cracked with fire, a mane of flame streaming back along -Z (it flies +Z)
  pyro_meteor_mark.glb  where a meteor will land: a red ring of radius 1, an inner ring of fire, four ticks pointing in and little flames round the edge
  pyro_inferno.glb      the Inferno: a ring of radius 1 of tall flames leaning in, a smaller ring of them inside it (scaled to the firestorm's reach)
  pyro_ember.glb        an ember, radius 1 (drawn tiny): a glowing orange orb (trailing the bolts, rising off the Mage as the heat builds)

Uses only colours already in make_placeholder_models.PALETTE: adding one changes every model's texture.
"""

import math

from make_placeholder_models import Mesh, write_glb, build_ring, sphere, _orb, _unit_noise, _normalize, _cross, _sub


def _cone(m, base, tip, r, colour, sides=4, turn=0.0, cap=True):
    """A pointed cone from a ring of <sides> corners, radius <r>, round <base> (square to the way to <tip>) up to <tip>, each face wound to face out."""
    axis = _normalize(_sub(tip, base))
    side = _normalize(_cross(axis, (0.0, 1.0, 0.0) if abs(axis[1]) < 0.9 else (1.0, 0.0, 0.0)))
    up = _cross(side, axis)
    ring = []
    for i in range(sides):
        a = turn + 2 * math.pi * i / sides
        ring.append(tuple(base[k] + r * (math.cos(a) * side[k] + math.sin(a) * up[k]) for k in range(3)))
    for i in range(sides):
        a, b = ring[i], ring[(i + 1) % sides]
        out = tuple((a[k] + b[k]) / 2 - base[k] for k in range(3))
        m.facing([a, b, tip], out, colour)
    if cap:
        for i in range(sides):
            m.facing([base, ring[i], ring[(i + 1) % sides]], tuple(-v for v in axis), colour)


def _tongue(m, x, z, y0, r, h, lean=(0.0, 0.0), outer="fire", inner="fire_light"):
    """A tongue of flame standing at (x, y0, z): a four-sided spike <h> tall of <outer>, leaning by <lean> at its tip, and a shorter one of <inner> turned 45
    degrees inside it, its corners showing through."""
    _cone(m, (x, y0, z), (x + lean[0], y0 + h, z + lean[1]), r, outer, turn=0.0, cap=False)
    _cone(m, (x, y0, z), (x + lean[0] * 0.7, y0 + h * 0.68, z + lean[1] * 0.7), r * 0.85, inner, turn=math.pi / 4, cap=False)


def build_bolt():
    """A Fire Barrage bolt: a spiky orb of flame, a bright core, and a tail of fire streaming back along -Z."""
    m = Mesh()
    _orb(m, 0.0, 0.0, 0.0, 0.17, "fire", "fire_light")
    _cone(m, (0.0, 0.0, -0.04), (0.0, 0.0, -0.55), 0.12, "ember", turn=math.pi / 4)
    _cone(m, (0.0, 0.03, -0.02), (0.02, 0.07, -0.38), 0.08, "fire")
    _cone(m, (0.0, -0.03, -0.02), (-0.03, -0.06, -0.32), 0.06, "fire_light")
    return m


def build_burst():
    """A burst of fire: a flat ring of radius 1 with 16 tongues of flame on it, leaning outward."""
    m = build_ring(0.8, 1.0, "ember", y=0.01)
    for i in range(16):
        a = 2 * math.pi * i / 16
        x, z = math.cos(a) * 0.9, math.sin(a) * 0.9
        h = 0.22 + 0.12 * _unit_noise(i, 3.1)
        _tongue(m, x, z, 0.0, 0.06, h, lean=(math.cos(a) * 0.08, math.sin(a) * 0.08))
    return m


def build_burn():
    """The flames on a burning enemy (a body 1.8 m tall): a ring of tongues from its shins up, and smaller ones higher round its chest, leaning in."""
    m = Mesh()
    for i in range(8):
        a = 2 * math.pi * i / 8 + 0.3
        rad = 0.34 + 0.05 * _unit_noise(i, 1.3)
        x, z = math.cos(a) * rad, math.sin(a) * rad
        h = 0.45 + 0.35 * _unit_noise(i, 2.7)
        _tongue(m, x, z, 0.15 + 0.15 * _unit_noise(i, 4.4), 0.08, h, lean=(-math.cos(a) * 0.12, -math.sin(a) * 0.12),
                outer="fire" if i % 2 == 0 else "ember")
    for i in range(5):
        a = 2 * math.pi * i / 5 + 1.1
        x, z = math.cos(a) * 0.28, math.sin(a) * 0.28
        _tongue(m, x, z, 0.85 + 0.2 * _unit_noise(i, 6.2), 0.06, 0.3 + 0.2 * _unit_noise(i, 7.9), lean=(-math.cos(a) * 0.08, -math.sin(a) * 0.08),
                outer="ember")
    return m


def build_fire():
    """A patch of fire on the ground: a ragged scorched blotch, a tall tongue of flame in the middle and smaller ones round it."""
    m = Mesh()
    sides = 14
    edge = [(math.cos(2 * math.pi * i / sides) * (0.7 + 0.2 * _unit_noise(i, 8.3)), math.sin(2 * math.pi * i / sides) * (0.7 + 0.2 * _unit_noise(i, 8.3)))
            for i in range(sides)]
    for i in range(sides):
        (xa, za), (xb, zb) = edge[i], edge[(i + 1) % sides]
        m.facing([(0.0, 0.01, 0.0), (xa, 0.01, za), (xb, 0.01, zb)], (0.0, 1.0, 0.0), "bomb_black")
        m.facing([(0.0, 0.02, 0.0), (xa * 0.5, 0.02, za * 0.5), (xb * 0.5, 0.02, zb * 0.5)], (0.0, 1.0, 0.0), "ember")
    _tongue(m, 0.0, 0.0, 0.0, 0.14, 0.85, lean=(0.05, -0.03))
    for i in range(6):
        a = 2 * math.pi * i / 6 + 0.5
        d = 0.3 + 0.25 * _unit_noise(i, 9.4)
        _tongue(m, math.cos(a) * d, math.sin(a) * d, 0.0, 0.09, 0.3 + 0.3 * _unit_noise(i, 5.5), lean=(math.cos(a) * 0.06, math.sin(a) * 0.06),
                outer="fire" if i % 2 else "ember")
    return m


def build_meteor():
    """A meteor flying +Z: a lump of dark rock cracked with fire, and a mane of flame streaming back from it."""
    m = Mesh()

    def cracked(i, k):
        theta, phi = math.pi * (i + 0.5) / 8, 2 * math.pi * (k + 0.5) / 12
        return "fire" if math.sin(3 * theta + 2 * phi) + math.sin(4 * phi - theta) > 1.1 else ("cave_rock" if (i + k) % 3 else "cave_rock_light")

    sphere(m, 0.0, 0.0, 0.0, 0.9, cracked, stacks=8, slices=12)
    for n in range(7):
        a = 2 * math.pi * n / 7
        off = (math.cos(a) * 0.45, math.sin(a) * 0.45)
        length = 2.0 + 1.0 * _unit_noise(n, 3.3)
        _cone(m, (off[0], off[1], -0.3), (off[0] * 1.6, off[1] * 1.6, -0.3 - length), 0.28, "fire" if n % 2 else "ember", turn=a)
    _cone(m, (0.0, 0.0, -0.5), (0.0, 0.0, -3.6), 0.5, "fire_light", turn=math.pi / 4)
    return m


def build_meteor_mark():
    """Where a meteor will land: a red ring of radius 1, an inner ring of fire, four ticks pointing in, and little flames round the edge."""
    m = build_ring(0.9, 1.0, "warn", y=0.01)
    inner = build_ring(0.45, 0.52, "fire", y=0.01)
    base = len(m.positions)
    m.positions += inner.positions
    m.normals += inner.normals
    m.uvs += inner.uvs
    m.joints += inner.joints
    m.indices += [base + i for i in inner.indices]
    up = (0.0, 1.0, 0.0)
    for k in range(4):
        a = math.pi / 2 * k
        c, s = math.cos(a), math.sin(a)
        # a tick from the ring toward the middle, 0.06 wide
        p = [(c * 0.88 - s * 0.03, 0.012, s * 0.88 + c * 0.03), (c * 0.6 - s * 0.03, 0.012, s * 0.6 + c * 0.03),
             (c * 0.6 + s * 0.03, 0.012, s * 0.6 - c * 0.03), (c * 0.88 + s * 0.03, 0.012, s * 0.88 - c * 0.03)]
        m.facing(p, up, "warn")
    for i in range(10):
        a = 2 * math.pi * i / 10 + 0.3
        _tongue(m, math.cos(a) * 0.95, math.sin(a) * 0.95, 0.0, 0.035, 0.12, outer="fire", inner="fire_light")
    return m


def build_inferno():
    """The Inferno round the Mage: a glowing ring of radius 1, tall tongues of flame on it leaning in, and a smaller ring of them inside."""
    m = build_ring(0.88, 1.0, "ember", y=0.01)
    for i in range(20):
        a = 2 * math.pi * i / 20
        x, z = math.cos(a) * 0.94, math.sin(a) * 0.94
        h = 0.28 + 0.14 * _unit_noise(i, 2.2)
        _tongue(m, x, z, 0.0, 0.05, h, lean=(-math.cos(a) * 0.06, -math.sin(a) * 0.06), outer="fire" if i % 2 else "ember")
    for i in range(10):
        a = 2 * math.pi * i / 10 + 0.3
        x, z = math.cos(a) * 0.68, math.sin(a) * 0.68
        _tongue(m, x, z, 0.0, 0.04, 0.16 + 0.08 * _unit_noise(i, 6.6), lean=(math.cos(a) * 0.03, math.sin(a) * 0.03))
    return m


def build_ember():
    """An ember, radius 1 (the game draws it tiny): a glowing orange orb with a bright heart."""
    m = Mesh()
    _orb(m, 0.0, 0.0, 0.0, 1.0, "ember", "fire_light")
    return m


PYRO_MODELS = (("pyro_bolt.glb", build_bolt), ("pyro_burst.glb", build_burst), ("pyro_burn.glb", build_burn), ("pyro_fire.glb", build_fire),
               ("pyro_meteor.glb", build_meteor), ("pyro_meteor_mark.glb", build_meteor_mark), ("pyro_inferno.glb", build_inferno),
               ("pyro_ember.glb", build_ember))


def write_all(models_dir):
    """Writes this tree's models into <models_dir>."""
    for name, build in PYRO_MODELS:
        mesh = build()
        write_glb(mesh, models_dir / name)
        print(f"Wrote {models_dir / name} ({len(mesh.positions)} vertices, {len(mesh.indices) // 3} triangles)")
