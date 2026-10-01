"""The Paladin's Crusade tree: its hammers of judgement, zeal and wings. Written by make_placeholder_models.py (write_all).

  crusade_hammer.glb         a hammer of light, about 1.2 m across the head: a glowing gold head with pale striking faces and a red cross on each side, its
                             handle standing straight up from it and its striking face at the origin, so it lands on the ground (drawn glowing, falling)
  crusade_hammer_mark.glb    where a hammer will land: a thin gold ring of radius 1 with four ticks pointing in (scaled to the hammer's reach)
  crusade_impact.glb         where a hammer landed: a pale ring of radius 1 with a thinner gold ring inside it, spreading out over the ground
  crusade_wings.glb, crusade_wings_raised.glb
                             Avenging Wings: a pair of golden feathered wings about 2.8 m from tip to tip, their roots at the origin (the Paladin's back) and
                             spreading out to the sides and back (-Z); spread level and raised (drawn in turn as they beat), both drawn glowing
  crusade_zeal.glb           Zeal: a ring of gold of radius about 0.8 round the feet, with small flames of light standing on it (turned and brightened as Zeal builds)

Uses only colours already in make_placeholder_models.PALETTE: adding one changes every model's texture.
"""

import math

from make_placeholder_models import Mesh, build_ring, write_glb


def _merge(m, other):
    """Adds every face of <other> to <m>."""
    base = len(m.positions)
    m.positions += other.positions
    m.normals += other.normals
    m.uvs += other.uvs
    m.joints += other.joints
    m.indices += [i + base for i in other.indices]


def _both_sides(m, points, outward, colour):
    """A flat face seen from both sides (a feather, a thin blade of light)."""
    m.facing(points, outward, colour)
    m.facing(points, tuple(-c for c in outward), colour)


def build_hammer():
    """The hammer of light: a gold head 1.2 m across (along X) whose striking face is on the ground, pale plates at both ends, a band and a red cross on each side,
    and a handle standing up 1.4 m from its top."""
    m = Mesh()
    w, h, d = 0.6, 0.62, 0.3
    m.box(-w + 0.12, 0.0, -d, w - 0.12, h, d, "gold")                         # the head
    for sx in (-1, 1):                                                         # the striking ends, wider and paler
        x0, x1 = (w - 0.14, w) if sx > 0 else (-w, -w + 0.14)
        m.box(x0, -0.02, -d - 0.05, x1, h + 0.02, d + 0.05, "holy_light")
    m.box(-0.1, -0.01, -d - 0.02, 0.1, h + 0.01, d + 0.02, "holy")              # the band round its middle
    for sz in (-1, 1):                                                         # a red cross on each broad side
        z = sz * (d + 0.03)
        for x0, y0, x1, y1 in ((-0.035, 0.12, 0.035, 0.5), (-0.13, 0.27, 0.13, 0.34)):
            m.facing([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], (0.0, 0.0, sz), "crusader")
    m.prism(0.0, 0.0, 0.07, h, h + 1.4, "holy_light", sides=8, top="holy")      # the handle
    m.prism(0.0, 0.0, 0.1, h + 1.35, h + 1.5, "gold", sides=8, top="holy_light")   # its pommel
    m.prism(0.0, 0.0, 0.1, h, h + 0.12, "holy", sides=8, top="holy")           # where it meets the head
    return m


def build_hammer_mark():
    """Where a hammer will land: a thin gold ring of radius 1 and four ticks pointing in."""
    m = build_ring(0.93, 1.0, "holy")
    up = (0.0, 1.0, 0.0)
    for k in range(4):
        a = math.tau * k / 4 + math.pi / 4
        c, s = math.cos(a), math.sin(a)
        m.facing([(c * 0.95 - s * 0.05, 0.002, s * 0.95 + c * 0.05), (c * 0.72, 0.002, s * 0.72), (c * 0.95 + s * 0.05, 0.002, s * 0.95 - c * 0.05)], up, "holy_light")
    return m


def build_impact():
    """Where a hammer landed: a pale ring of radius 1 and a thinner gold one inside it."""
    m = build_ring(0.8, 1.0, "holy_light")
    _merge(m, build_ring(0.55, 0.62, "holy", y=0.004))
    return m


def _wing(m, side, lift):
    """One wing on <side> (+1 right, -1 left), lifted <lift> radians at its root: an arm of three points going out, up and back, and feathers hanging from it,
    longest at the tip."""
    def place(x, y, z):
        # Turn about the root (the Z axis through the origin) to lift the wing, then mirror for the side.
        c, s = math.cos(lift), math.sin(lift)
        return (side * (x * c - y * s), x * s + y * c, z)

    arm = [(0.08, 0.05, -0.05), (0.55, 0.3, -0.22), (1.0, 0.45, -0.36), (1.42, 0.52, -0.46)]
    for i in range(len(arm) - 1):                                               # the arm: a gold band along the top
        a, b = arm[i], arm[i + 1]
        _both_sides(m, [place(*a), place(*b), place(b[0], b[1] - 0.1, b[2]), place(a[0], a[1] - 0.1, a[2])], (0.0, 0.0, -1.0), "gold")
    feathers = 9
    for k in range(feathers):                                                   # feathers hanging from it, the outer ones longer
        t = (k + 0.5) / feathers
        seg = min(int(t * (len(arm) - 1)), len(arm) - 2)
        u = t * (len(arm) - 1) - seg
        a, b = arm[seg], arm[seg + 1]
        root = tuple(a[n] + (b[n] - a[n]) * u for n in range(3))
        length = 0.45 + 0.75 * t
        half = 0.07 + 0.03 * t
        tip = (root[0] + 0.25 * t, root[1] - 0.1 - length, root[2] - 0.08)
        colour = "holy" if k % 2 == 0 else "holy_light"
        quad = [place(root[0] - half, root[1] - 0.05, root[2]), place(root[0] + half, root[1] - 0.05, root[2]),
                place(tip[0] + half * 0.6, tip[1], tip[2]), place(tip[0] - half * 0.4, tip[1] + 0.05, tip[2])]
        _both_sides(m, quad, (0.0, 0.0, -1.0), colour)
    for k in range(5):                                                          # short covert feathers over the arm
        t = (k + 0.5) / 5
        seg = min(int(t * (len(arm) - 1)), len(arm) - 2)
        u = t * (len(arm) - 1) - seg
        a, b = arm[seg], arm[seg + 1]
        root = tuple(a[n] + (b[n] - a[n]) * u for n in range(3))
        quad = [place(root[0] - 0.08, root[1] - 0.04, root[2] - 0.01), place(root[0] + 0.08, root[1] - 0.04, root[2] - 0.01),
                place(root[0] + 0.06, root[1] - 0.34, root[2] - 0.03), place(root[0] - 0.06, root[1] - 0.3, root[2] - 0.03)]
        _both_sides(m, quad, (0.0, 0.0, -1.0), "gold")


def build_wings(lift):
    """A pair of golden wings, their roots at the origin, lifted <lift> radians."""
    m = Mesh()
    for side in (-1, 1):
        _wing(m, side, lift)
    return m


def build_zeal():
    """Zeal round the feet: a gold ring of radius about 0.8 with eight small flames of light standing on it."""
    m = build_ring(0.74, 0.82, "holy")
    for k in range(8):
        a = math.tau * k / 8
        cx, cz = math.cos(a) * 0.78, math.sin(a) * 0.78
        tip = (cx, 0.32 if k % 2 == 0 else 0.22, cz)
        m.pyramid(cx - 0.05, 0.0, cz - 0.05, cx + 0.05, 0.0, cz + 0.05, tip, "holy_light" if k % 2 == 0 else "ember")
    return m


CRUSADE_MODELS = (("crusade_hammer.glb", build_hammer), ("crusade_hammer_mark.glb", build_hammer_mark), ("crusade_impact.glb", build_impact),
                  ("crusade_wings.glb", lambda: build_wings(0.0)), ("crusade_wings_raised.glb", lambda: build_wings(0.45)),
                  ("crusade_zeal.glb", build_zeal))


def write_all(models_dir):
    """Writes this tree's models into <models_dir>."""
    for name, build in CRUSADE_MODELS:
        write_glb(build(), models_dir / name)
