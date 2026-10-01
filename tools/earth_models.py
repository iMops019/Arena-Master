"""The Shaman's Earth Alignment tree: its stones, cracks, quakes and totems. Written by make_placeholder_models.py (write_all).

  earth_stone.glb       the thrown stone: a rough, lumpy boulder of radius about 0.35 in two browns with grey patches (scaled to the stone's size, turned over
                        as it rolls: Shaman/EarthView.cs)
  earth_quake.glb       a quake: a ring of churned earth of radius 1 with clods thrown up along it (scaled to the quake's reach, spreading)
  earth_crack.glb       an Aftershock crack: a jagged dark split in the ground 2 m long along Z with a branch or two, pale earth heaved up along its lips
  earth_spikes.glb      a crack bursting: a cluster of rock spikes up to about 1.1 m tall within radius 1 (drawn thrusting up out of the ground and sinking back)
  earth_chunk.glb       a chunk of rubble about 0.3 m across, flung out when a stone shatters
  earth_totem.glb       the Earthen Totem: a carved stone post about 1.6 m tall on a broad foot - a stern face with deep eyes, jutting arms, a pale cap
  earth_totem_ring.glb  a thin ring of radius 1 with little standing stones on it (scaled to how far the totem draws enemies from)
  earth_rift.glb        one piece of a Tectonic Rift, 1 m long along Z: a dark split with slabs of earth tilted up on both sides (laid end to end along a rift)

Uses only colours already in make_placeholder_models.PALETTE: adding one changes every model's texture.
"""

import math

from make_placeholder_models import Mesh, write_glb, _unit_noise


def _cone(m, cx, cz, base_y, tip_y, radius, colour, sides=5, turn=0.0, lean=(0.0, 0.0)):
    """A spike from a ring of <radius> at height <base_y> round (cx, cz) to a point at <tip_y>, leaning by <lean>, each face wound to face out."""
    tip = (cx + lean[0], tip_y, cz + lean[1])
    ring = [(cx + math.cos(turn + math.tau * i / sides) * radius, base_y, cz + math.sin(turn + math.tau * i / sides) * radius) for i in range(sides)]
    for i in range(sides):
        a, b = ring[i], ring[(i + 1) % sides]
        mid = ((a[0] + b[0] + tip[0]) / 3 - cx, (tip_y - base_y) * 0.2, (a[2] + b[2] + tip[2]) / 3 - cz)
        m.facing([a, b, tip], mid, colour)


def _lump(m, cx, cy, cz, r, salt, colour_of, stacks=7, slices=10, rough=0.16):
    """A rough round rock of about radius <r> round (cx, cy, cz): a ball whose every point is pushed in or out a little, <colour_of>(stack, slice) colouring each face."""
    def point(i, k):
        theta, phi = math.pi * i / stacks, math.tau * (k % slices) / slices
        bump = 1.0 + rough * (2.0 * _unit_noise(i * 31 + (k % slices) * 7, salt) - 1.0) if 0 < i < stacks else 1.0
        return (cx + r * bump * math.sin(theta) * math.cos(phi), cy + r * bump * math.cos(theta), cz + r * bump * math.sin(theta) * math.sin(phi))

    for i in range(stacks):
        for k in range(slices):
            a, b, c, d = point(i, k), point(i, k + 1), point(i + 1, k + 1), point(i + 1, k)
            mid = tuple((a[n] + b[n] + c[n] + d[n]) / 4 - (cx, cy, cz)[n] for n in range(3))
            colour = colour_of(i, k)
            if i == 0:
                m.facing([a, c, d], mid, colour)
            elif i == stacks - 1:
                m.facing([a, b, c], mid, colour)
            else:
                m.facing([a, b, c], mid, colour)   # two triangles, so a pushed-about quad never folds
                m.facing([a, c, d], mid, colour)


def _flat(m, points, colour, up=(0.0, 1.0, 0.0)):
    m.facing(points, up, colour)


def build_earth_stone():
    """The thrown stone: a rough boulder of radius about 0.35, mostly brown earth, paler patches and grey stone showing through."""
    def colour(i, k):
        n = _unit_noise(i * 13 + k * 5, 2.7)
        return "stone" if n > 0.8 else "earth_light" if n > 0.55 else "earth"

    m = Mesh()
    _lump(m, 0.0, 0.0, 0.0, 0.35, 1.3, colour, stacks=8, slices=12, rough=0.14)
    return m


def build_earth_quake():
    """A quake's ring of churned earth, radius 1: a broken band of brown with pale clods thrown up along it."""
    m = Mesh()
    segments = 36
    for i in range(segments):
        a0, a1 = math.tau * i / segments, math.tau * (i + 1) / segments
        inner = 0.72 + 0.08 * _unit_noise(i, 4.1)
        outer = 0.98 + 0.05 * _unit_noise(i, 5.3)
        colour = "earth_light" if i % 5 == 2 else "earth"
        _flat(m, [(math.cos(a0) * inner, 0.0, math.sin(a0) * inner), (math.cos(a0) * outer, 0.0, math.sin(a0) * outer),
                  (math.cos(a1) * outer, 0.0, math.sin(a1) * outer), (math.cos(a1) * inner, 0.0, math.sin(a1) * inner)], colour)
    for k in range(14):                                                    # clods thrown up
        a = math.tau * (k + 0.5 * _unit_noise(k, 6.2)) / 14
        r = 0.85 + 0.1 * _unit_noise(k, 7.4)
        _cone(m, math.cos(a) * r, math.sin(a) * r, 0.0, 0.12 + 0.1 * _unit_noise(k, 8.8), 0.07, "earth_light" if k % 2 else "earth", sides=4, turn=a)
    return m


def _jagged(m, points, width, colour, lip=None, lip_width=0.05, y=0.0):
    """A jagged strip on the ground along <points> (x, z), <width> across, and a raised pale lip either side of it if <lip>."""
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        dx, dz = x1 - x0, z1 - z0
        length = math.hypot(dx, dz) or 1.0
        sx, sz = -dz / length, dx / length                                 # square to the strip
        w = width / 2
        _flat(m, [(x0 - sx * w, y, z0 - sz * w), (x0 + sx * w, y, z0 + sz * w), (x1 + sx * w, y, z1 + sz * w), (x1 - sx * w, y, z1 - sz * w)], colour)
        if lip:
            for side in (-1.0, 1.0):
                e0, e1 = w * side, (w + lip_width) * side
                _flat(m, [(x0 + sx * e0, y + 0.01, z0 + sz * e0), (x0 + sx * e1, y + 0.04, z0 + sz * e1), (x1 + sx * e1, y + 0.04, z1 + sz * e1),
                          (x1 + sx * e0, y + 0.01, z1 + sz * e0)], lip)


def build_earth_crack():
    """An Aftershock crack: a jagged dark split 2 m long along Z, pale earth heaved up along its lips, and two short branches off it."""
    m = Mesh()
    main = [(0.12 * (2.0 * _unit_noise(i, 9.1) - 1.0), -1.0 + 2.0 * i / 8) for i in range(9)]
    _jagged(m, main, 0.13, "dark", lip="earth_light", y=0.01)
    for start, turn, salt in ((3, 0.9, 10.3), (6, -1.1, 11.7)):
        x, z = main[start]
        branch = [(x, z)]
        for step in range(1, 4):
            x += math.sin(turn) * 0.16 + 0.05 * (2.0 * _unit_noise(step, salt) - 1.0)
            z += math.cos(turn) * 0.16
            branch.append((x, z))
        _jagged(m, branch, 0.06, "dark", lip="earth", lip_width=0.03, y=0.012)
    return m


def build_earth_spikes():
    """A crack bursting: seven rock spikes, the tallest about 1.1 m, thrusting up within radius 1, on a heave of broken earth."""
    m = Mesh()
    for i in range(10):                                                    # the heaved ground round their feet
        a0, a1 = math.tau * i / 10, math.tau * (i + 1) / 10
        _flat(m, [(0.0, 0.08, 0.0), (math.cos(a0) * 0.9, 0.0, math.sin(a0) * 0.9), (math.cos(a1) * 0.9, 0.0, math.sin(a1) * 0.9)][::-1], "earth")
    spikes = ((0.0, 0.0, 1.1, 0.26, 0.05, 0.0), (0.5, 0.2, 0.8, 0.18, 0.22, 0.1), (-0.45, 0.3, 0.75, 0.17, -0.2, 0.15), (0.1, -0.55, 0.7, 0.16, 0.05, -0.25),
              (-0.35, -0.4, 0.55, 0.14, -0.15, -0.2), (0.6, -0.35, 0.5, 0.13, 0.25, -0.15), (-0.7, -0.05, 0.45, 0.12, -0.3, 0.0))
    for i, (x, z, h, r, lx, lz) in enumerate(spikes):
        _cone(m, x, z, 0.0, h, r, "stone" if i % 2 == 0 else "earth_light", sides=5, turn=i * 0.9, lean=(lx, lz))
    return m


def build_earth_chunk():
    """A chunk of rubble about 0.3 m across: a small rough rock."""
    m = Mesh()
    _lump(m, 0.0, 0.0, 0.0, 0.15, 3.9, lambda i, k: "stone" if (i + k) % 3 == 0 else "earth", stacks=4, slices=6, rough=0.3)
    return m


def build_earth_totem():
    """The Earthen Totem, about 1.6 m tall: a broad stone foot, a squared post carved with a stern face (deep dark eyes, a grim mouth), arms jutting out below a
    pale cap stone, and pale bands of carving."""
    m = Mesh()
    m.box(-0.38, 0.0, -0.38, 0.38, 0.22, 0.38, "stone")                   # the foot
    m.box(-0.26, 0.22, -0.24, 0.26, 1.3, 0.24, "earth")                    # the post
    m.box(-0.28, 0.5, -0.26, 0.28, 0.56, 0.26, "earth_light")               # carved bands
    m.box(-0.28, 0.95, -0.26, 0.28, 1.0, 0.26, "earth_light")
    # The face, on the front (+Z): brows, deep eyes, a nose, a grim mouth.
    m.box(-0.2, 1.12, 0.24, 0.2, 1.17, 0.29, "stone")
    for x in (-0.11, 0.11):
        m.box(x - 0.06, 1.03, 0.24, x + 0.06, 1.1, 0.26, "dark")
    m.box(-0.035, 0.9, 0.24, 0.035, 1.05, 0.3, "stone")
    m.box(-0.13, 0.78, 0.24, 0.13, 0.83, 0.265, "dark")
    for side in (-1.0, 1.0):                                                # the arms
        m.box(min(side * 0.26, side * 0.52), 0.8, -0.09, max(side * 0.26, side * 0.52), 0.94, 0.09, "stone")
        m.box(min(side * 0.44, side * 0.52), 0.94, -0.09, max(side * 0.44, side * 0.52), 1.08, 0.09, "stone")
    m.pyramid(-0.32, 1.3, -0.3, 0.32, 1.3, 0.3, (0.0, 1.6, 0.0), "earth_light")   # the cap
    return m


def build_earth_totem_ring():
    """How far a totem draws enemies from: a thin pale ring of radius 1 with eight little standing stones on it."""
    m = Mesh()
    for i in range(48):
        a0, a1 = math.tau * i / 48, math.tau * (i + 1) / 48
        _flat(m, [(math.cos(a0) * 0.97, 0.0, math.sin(a0) * 0.97), (math.cos(a0), 0.0, math.sin(a0)), (math.cos(a1), 0.0, math.sin(a1)),
                  (math.cos(a1) * 0.97, 0.0, math.sin(a1) * 0.97)], "earth_light")
    for k in range(8):
        a = math.tau * k / 8
        c, s = math.cos(a) * 0.985, math.sin(a) * 0.985
        m.pyramid(c - 0.02, 0.0, s - 0.02, c + 0.02, 0.0, s + 0.02, (c, 0.06, s), "stone")
    return m


def build_earth_rift():
    """One piece of a rift, 1 m long along Z: a dark jagged split with slabs of brown earth tilted up along both sides."""
    m = Mesh()
    split = [(0.08 * (2.0 * _unit_noise(i, 12.5) - 1.0), -0.5 + i / 4) for i in range(5)]
    _jagged(m, split, 0.22, "dark", y=0.0)
    for side in (-1.0, 1.0):
        for j in range(3):
            z0, z1 = -0.5 + j / 3, -0.5 + (j + 1) / 3
            x_in, x_out = side * 0.12, side * (0.34 + 0.06 * _unit_noise(j, 13.1 + side))
            lift = 0.1 + 0.06 * _unit_noise(j, 14.7 + side)
            colour = "earth" if (j + (side > 0)) % 2 else "earth_light"
            m.facing([(x_in, lift, z0), (x_in, lift, z1), (x_out, 0.0, z1), (x_out, 0.0, z0)], (side * 0.4, 1.0, 0.0), colour)   # tilted up to the split
            m.facing([(x_in, 0.0, z0), (x_in, 0.0, z1), (x_in, lift, z1), (x_in, lift, z0)], (-side, 0.0, 0.0), "earth")          # its broken edge
    return m


EARTH_MODELS = (("earth_stone.glb", build_earth_stone), ("earth_quake.glb", build_earth_quake), ("earth_crack.glb", build_earth_crack),
                ("earth_spikes.glb", build_earth_spikes), ("earth_chunk.glb", build_earth_chunk), ("earth_totem.glb", build_earth_totem),
                ("earth_totem_ring.glb", build_earth_totem_ring), ("earth_rift.glb", build_earth_rift))


def write_all(models_dir):
    """Writes this tree's models into <models_dir>."""
    for file, build in EARTH_MODELS:
        mesh = build()
        write_glb(mesh, models_dir / file)
        print(f"Wrote {models_dir / file} ({len(mesh.positions)} vertices)")
