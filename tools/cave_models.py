"""The Delve's cave, the ghouls' lair under the camp: its roof, its rock, and what lies about in it. Written by make_placeholder_models.py.

  cave_ceiling.glb      the cave's roof: CEILING_SIZE across, its underside a lumpy sheet of dark rock facing down, stalactites hanging from it. Placed with its origin
                        CaveLayout.CeilingHeight up (Camp/Cave.cs) while the player is underground.
  cave_stalagmite.glb   a spike of rock about 5 m tall rising from the floor, ridged, with a smaller one against it
  cave_crystals.glb     a cluster of violet crystals about 1.4 m tall, drawn glowing (EngineWindow.SetCrowdGlow)
  cave_bones.glb        a heap of bones and a skull
  cave_lantern.glb      a ghoul lantern: a bone pole driven into the ground, a skull on it, a cage hanging from its crook
  cave_lantern_fire.glb the lantern's green fire in its cage and the skull's eyes, drawn glowing (EngineWindow.SetCrowdGlow) over the lantern
  cave_tunnel.glb       the mouth of the tunnel the stairs come down: a stone face with a dark arch, rocks piled over it (where a run starts)
  camp_descent.glb      the way down at camp, where the gate was: a stair cut going down between stone walls to the tunnel's mouth, set in the palisade
"""

import math

from make_placeholder_models import Mesh, GATE_WIDTH, _unit_noise

CEILING_SIZE = 512.0      # the map's size: the roof covers it all
CEILING_CELL = 8.0        # the roof's grid
STALACTITES = 700


def _cone(m, cx, cz, top, bottom, radius, colour, sides=6, turn=0.0, lean=(0.0, 0.0)):
    """A cone from a ring of <radius> at height <top> to a point at <bottom> (below it for a stalactite, above it for a stalagmite), each face wound to face out."""
    tip = (cx + lean[0], bottom, cz + lean[1])
    ring = [(cx + math.cos(turn + math.tau * i / sides) * radius, top, cz + math.sin(turn + math.tau * i / sides) * radius) for i in range(sides)]
    for i in range(sides):
        a, b = ring[i], ring[(i + 1) % sides]
        mid = ((a[0] + b[0] + tip[0]) / 3 - cx, 0.0, (a[2] + b[2] + tip[2]) / 3 - cz)
        m.facing([a, b, tip], mid, colour)


def _ceiling_height(x, z):
    """How far above its origin the roof's underside is at (x, z): domes and hollows a few metres deep, so it is never a flat lid."""
    return (2.2 * math.sin(x * 0.045 + 1.3) * math.cos(z * 0.052 - 0.7) + 1.6 * math.sin(x * 0.11 + z * 0.083) + 0.9 * math.cos(x * 0.21 - z * 0.17)
            + 3.0 * max(0.0, math.sin(x * 0.019 + 2.1) * math.sin(z * 0.023 + 0.4)))


def build_cave_ceiling():
    """The roof, facing down: a lumpy sheet of dark rock on a CEILING_CELL grid, lighter rock in patches, and stalactites hanging from it (1 to 12 m)."""
    m = Mesh()
    half = CEILING_SIZE / 2
    n = int(CEILING_SIZE / CEILING_CELL)
    for j in range(n):
        for i in range(n):
            x0, z0 = -half + i * CEILING_CELL, -half + j * CEILING_CELL
            x1, z1 = x0 + CEILING_CELL, z0 + CEILING_CELL
            p00, p10 = (x0, _ceiling_height(x0, z0), z0), (x1, _ceiling_height(x1, z0), z0)
            p01, p11 = (x0, _ceiling_height(x0, z1), z1), (x1, _ceiling_height(x1, z1), z1)
            cx, cz = x0 + CEILING_CELL / 2, z0 + CEILING_CELL / 2
            colour = "cave_rock_light" if math.sin(cx * 0.031 + 0.8) * math.cos(cz * 0.027 - 1.1) + 0.4 * math.sin(cx * 0.09 + cz * 0.07) > 0.75 else "cave_rock"
            m.facing([p00, p10, p01], (0.0, -1.0, 0.0), colour)
            m.facing([p10, p11, p01], (0.0, -1.0, 0.0), colour)
    for k in range(STALACTITES):
        x = -half + 8.0 + (CEILING_SIZE - 16.0) * _unit_noise(k, 1.0)
        z = -half + 8.0 + (CEILING_SIZE - 16.0) * _unit_noise(k, 2.0)
        length = 1.0 + 11.0 * _unit_noise(k, 3.0) ** 2
        radius = 0.35 + length * (0.12 + 0.08 * _unit_noise(k, 4.0))
        top = _ceiling_height(x, z) + 0.5
        _cone(m, x, z, top, top - length, radius, "cave_rock" if k % 3 else "cave_rock_light", sides=6, turn=_unit_noise(k, 5.0) * 3.0)
    return m


def build_cave_stalagmite():
    """A spike of rock about 5 m tall on a broad foot, ridged in two tones, with a smaller spike leaning against it."""
    m = Mesh()
    _cone(m, 0.0, 0.0, 0.0, 5.2, 1.3, "cave_rock", sides=7, lean=(0.15, -0.1))
    _cone(m, 0.0, 0.0, 0.0, 3.2, 1.45, "cave_rock_light", sides=7, turn=0.45)
    _cone(m, 1.1, 0.5, 0.0, 2.3, 0.6, "cave_rock", sides=6, lean=(0.25, 0.1))
    _cone(m, -0.8, -0.9, 0.0, 1.2, 0.45, "cave_rock_light", sides=5)
    return m


def build_cave_crystals():
    """A cluster of violet crystals about 1.4 m tall, leaning out from a rocky base."""
    m = Mesh()
    _cone(m, 0.0, 0.0, 0.0, 0.35, 0.55, "cave_rock", sides=6)
    for i, (x, z, h, r, lx, lz) in enumerate(((0.0, 0.0, 1.4, 0.2, 0.05, 0.0), (0.28, 0.1, 0.95, 0.15, 0.35, 0.15), (-0.22, 0.15, 1.05, 0.16, -0.3, 0.2),
                                              (0.05, -0.27, 0.8, 0.13, 0.05, -0.35), (-0.18, -0.2, 0.6, 0.11, -0.25, -0.25), (0.3, -0.22, 0.55, 0.1, 0.3, -0.3))):
        colour = "crystal_light" if i % 2 == 0 else "crystal"
        _cone(m, x, z, 0.15, h, r, colour, sides=5, turn=i * 0.7, lean=(lx, lz))
        _cone(m, x, z, 0.15, 0.0, r, colour, sides=5, turn=i * 0.7)
    return m


def build_cave_bones():
    """A heap of bones - long bones crossed every way, ribs - and a skull on top, about 1.4 m across."""
    m = Mesh()
    for i in range(9):
        a = i * 1.1
        length = 0.55 + 0.35 * _unit_noise(i, 1.0)
        cx, cz = 0.45 * math.cos(i * 2.4) * _unit_noise(i, 2.0), 0.45 * math.sin(i * 2.4) * _unit_noise(i, 3.0)
        dx, dz = math.cos(a) * length / 2, math.sin(a) * length / 2
        y = 0.04 + 0.05 * (i % 3)
        x0, x1 = sorted((cx - dx, cx + dx))
        z0, z1 = sorted((cz - dz, cz + dz))
        if abs(dx) > abs(dz):
            m.box(x0, y, cz - 0.03, x1, y + 0.06, cz + 0.03, "bone")
        else:
            m.box(cx - 0.03, y, z0, cx + 0.03, y + 0.06, z1, "bone")
    for k in range(4):                                                        # ribs
        m.box(-0.35 + k * 0.12, 0.0, 0.2, -0.3 + k * 0.12, 0.25, 0.24, "bone")
    m.box(-0.12, 0.18, -0.12, 0.12, 0.4, 0.12, "bone")                        # the skull, dark-eyed
    m.box(-0.08, 0.28, 0.12, -0.02, 0.33, 0.125, "visor")
    m.box(0.02, 0.28, 0.12, 0.08, 0.33, 0.125, "visor")
    m.box(-0.1, 0.14, -0.02, 0.1, 0.19, 0.13, "bone")
    return m


def build_cave_lantern():
    """A ghoul lantern: a bone pole 2.6 m tall with a skull on top and a crook, a cage hanging from it with a green fire in it."""
    m = Mesh()
    m.box(-0.05, 0.0, -0.05, 0.05, 2.6, 0.05, "bone")
    for y in (0.6, 1.4):
        m.box(-0.07, y, -0.07, 0.07, y + 0.08, 0.07, "leather")
    m.box(-0.13, 2.6, -0.12, 0.13, 2.86, 0.14, "bone")                         # the skull
    m.box(-0.03, 2.4, 0.05, 0.03, 2.46, 0.55, "bone")                          # the crook
    m.box(-0.01, 2.0, 0.49, 0.01, 2.42, 0.51, "iron")                          # the chain
    for x in (-0.14, 0.14):                                                   # the cage
        for z in (0.36, 0.64):
            m.box(x - 0.015, 1.62, z - 0.015, x + 0.015, 2.0, z + 0.015, "iron")
    m.box(-0.15, 1.6, 0.35, 0.15, 1.64, 0.65, "iron")
    m.box(-0.15, 1.98, 0.35, 0.15, 2.02, 0.65, "iron")
    return m


def build_cave_lantern_fire():
    """The lantern's green fire, in its cage, and the skull's burning eyes: drawn over the lantern, glowing."""
    m = Mesh()
    _cone(m, 0.0, 0.5, 1.64, 1.97, 0.12, "ghost_eye", sides=6)
    _cone(m, 0.0, 0.5, 1.64, 1.66, 0.12, "ghost_eye", sides=6)
    m.box(-0.09, 2.7, 0.14, -0.02, 2.76, 0.15, "ghost_eye")
    m.box(0.02, 2.7, 0.14, 0.09, 2.76, 0.15, "ghost_eye")
    return m


def _portal(m, w, face, opening=1.45, arch=2.9, base=-0.3):
    """A stone face 2w wide and <face> tall at z in -0.9..0.6, a dark arch <opening> either side of the middle and <arch> tall, a skull keystone, green rune-lights,
    and rough rocks piled along its top."""
    m.box(-w, base, -0.9, -opening, face, 0.6, "stone")
    m.box(opening, base, -0.9, w, face, 0.6, "stone")
    m.box(-opening, arch + 0.9, -0.9, opening, face, 0.6, "stone")
    for k in range(7):                                                        # the arch, stepped round from the jambs to the crown
        a0, a1 = math.pi * k / 7, math.pi * (k + 1) / 7
        xa, xb = sorted((math.cos(a0) * opening, math.cos(a1) * opening))
        y = arch + math.sin((a0 + a1) / 2) * 0.9
        m.box(xa, y, -0.9, xb, arch + 0.95, 0.62, "plate_dark")
    m.box(-opening, base, -1.4, opening, arch + 0.95, -1.35, "visor")         # the dark of the tunnel inside
    m.box(-opening, base, -1.4, -opening + 0.05, arch + 0.95, -0.9, "visor")
    m.box(opening - 0.05, base, -1.4, opening, arch + 0.95, -0.9, "visor")
    m.box(-opening, arch + 0.9, -1.4, opening, arch + 0.95, -0.9, "visor")
    m.box(-w, face - 0.4, -2.3, w, face, -0.9, "stone")                      # the tunnel's roof, back over the dug ground
    m.box(-0.3, arch + 0.75, 0.6, 0.3, arch + 1.3, 0.7, "bone")               # a skull for a keystone, green-eyed
    m.box(-0.18, arch + 1.0, 0.7, -0.06, arch + 1.1, 0.72, "ghost_eye")
    m.box(0.06, arch + 1.0, 0.7, 0.18, arch + 1.1, 0.72, "ghost_eye")
    for side in (-1, 1):                                                      # rune-lights down the jambs
        x = side * (opening + 0.35)
        for y in (0.6, 1.5, 2.4):
            m.box(x - 0.1, y, 0.6, x + 0.1, y + 0.35, 0.64, "ghost_eye")
    for i in range(6):                                                        # rough rocks piled along its top
        x = -w + 0.6 + i * (2 * w - 1.2) / 5
        h = 0.5 + 0.5 * _unit_noise(i, 3.0)
        m.pyramid(x - 0.75, face, -0.9, x + 0.75, face, 0.6, (x + 0.2 * (_unit_noise(i, 5.0) - 0.5), face + h, -0.2), "stone")


def build_cave_tunnel():
    """Where the stairs from camp come out into the cave: the tunnel's mouth, a stone face 7 m wide and 5 m tall with a dark arch, facing +Z (into the cave)."""
    m = Mesh()
    _portal(m, 3.5, 5.0)
    return m


DESCENT_DROP = 3.5    # how far the stairs go down, from camp's ground to the tunnel's mouth (CampLayout.ShapeGround digs the ground to match)
DESCENT_RUN = 8.0     # how far back from the tunnel's mouth the stairs start, toward camp (+Z)
DESCENT_WIDTH = 3.2   # the stairs' width, between the stone walls that hold the ground back


def build_descent():
    """The way down to the Delve, where the camp's gate was: a stair cut DESCENT_RUN long going DESCENT_DROP down between stone walls toward the north wall, ending in a
    stone portal GATE_WIDTH wide filling the palisade's gap, a dark tunnel mouth in it. The origin is the foot of the stairs at the tunnel's mouth, the stairs climbing
    toward +Z (camp)."""
    m = Mesh()
    half = DESCENT_WIDTH / 2
    steps = 14
    run = DESCENT_RUN / steps
    rise = DESCENT_DROP / steps
    for i in range(steps):                                                    # the steps, each a slab of stone, alternately paler
        z0 = 0.6 + i * run
        m.box(-half, -0.2, z0, half, (i + 1) * rise, z0 + run + 0.02, "stone" if i % 2 else "plate_dark")
    top = DESCENT_DROP + 0.45
    for side in (-1, 1):                                                      # the walls holding the ground back, a coping stone along the top
        x0, x1 = sorted((side * half, side * (half + 1.0)))
        m.box(x0, -0.3, 0.6, x1, top, DESCENT_RUN + 0.6, "stone")
        m.box(x0 - 0.05, top, 0.6, x1 + 0.05, top + 0.18, DESCENT_RUN + 0.6, "plate_dark")
        for k in range(4):                                                    # courses of blocks, the joints dark
            y = 0.75 + k * 0.8
            xi = side * half
            m.box(min(xi, xi - side * 0.01), y, 0.6, max(xi, xi - side * 0.01), y + 0.05, DESCENT_RUN + 0.6, "dark")
    _portal(m, GATE_WIDTH / 2, DESCENT_DROP + 3.4)
    return m


CAVE_MODELS = (("cave_ceiling.glb", build_cave_ceiling), ("cave_stalagmite.glb", build_cave_stalagmite), ("cave_crystals.glb", build_cave_crystals),
               ("cave_bones.glb", build_cave_bones), ("cave_lantern.glb", build_cave_lantern),
               ("cave_lantern_fire.glb", build_cave_lantern_fire), ("cave_tunnel.glb", build_cave_tunnel),
               ("camp_descent.glb", build_descent))
