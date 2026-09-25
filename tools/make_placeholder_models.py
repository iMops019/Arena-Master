"""Writes blocky stand-in models to assets/models/ until real ones are made. Plain Python, no dependencies:
  ranger_placeholder.glb  the Ranger (tunic, hood, cape, bow, quiver)
  arrow_placeholder.glb   an arrow, centred on its middle, pointing +Z
  ghoul_placeholder.glb   the first enemy: a hunched ghoul with long arms and red eyes
  xp_gem_placeholder.glb  an experience gem: a small glowing-blue crystal, centred on its middle
  brute_placeholder.glb   the elite: a hulking horned ghoul brute, 2.4 m tall, with a bone club
  hollow_king_placeholder.glb  the boss: a towering crowned ghoul king, 4.2 m tall, with a tattered cape
  telegraph_ring.glb      a flat red ring of radius 1 on y = 0, scaled to an attack's radius (outline of where it lands)
  telegraph_disc.glb      a flat dark-red disc of radius 1, scaled up inside the ring as an attack winds up
  telegraph_lane.glb      a flat red lane 1.6 m wide and 7.2 m long from the origin along +Z (a lunge's path)
  shockwave_ring.glb      a thin flat ring of radius 1 (width 0.07), scaled as a shockwave spreads
  chest_placeholder.glb   a wooden treasure chest with gold bands, 1 m wide
  loot_beam.glb           a thin gold pillar of light, 8 m tall, marking a chest from afar
  item_<rarity>.glb       an item orb in its rarity's colour (common, rare, epic, legendary), centred on its middle
  camp_tent.glb, camp_firepit.glb, camp_stash.glb, camp_target.glb, camp_gate.glb, camp_board.glb, camp_stall.glb
                          the camp: a canvas tent, a ring of stones with logs (the engine's fire burns on it), the stash chest,
                          the archery target (the passive tree station), the departure gate (the way into a run), the bounty
                          board (a notice board with papers pinned to it) and the quartermaster's stall (a counter under an awning)

The engine's model conventions: one mesh, one primitive, colours from one base-colour texture (vertex colours are
ignored), facing +Z, feet at y = 0, metres. The texture is a strip of flat colour swatches and each face's UVs point
at the middle of its swatch.

    python tools/make_placeholder_models.py
"""

import json
import struct
import zlib
from pathlib import Path

MODELS = Path(__file__).resolve().parent.parent / "assets" / "models"

SWATCH = 8  # pixels per colour, so filtering never blends neighbours
PALETTE = {
    "tunic": (58, 112, 60),
    "hood": (32, 62, 38),
    "leather": (104, 70, 40),
    "wood": (146, 98, 52),
    "skin": (226, 186, 152),
    "trousers": (72, 62, 50),
    "fletching": (232, 230, 218),
    "dark": (28, 24, 20),
    "steel": (176, 180, 186),
    "ghoul_skin": (112, 128, 100),
    "ghoul_rags": (66, 54, 70),
    "ghoul_eye": (235, 40, 28),
    "bone": (214, 204, 176),
    "gem": (80, 214, 236),
    "gem_light": (190, 246, 255),
    "brute_skin": (96, 110, 84),
    "brute_hide": (58, 42, 34),
    "horn": (232, 222, 196),
    "king_skin": (84, 88, 104),
    "king_robe": (58, 22, 40),
    "gold": (214, 170, 60),
    "ghost_eye": (120, 255, 200),
    "warn": (235, 40, 30),
    "warn_dark": (120, 16, 14),
    "shock": (255, 196, 90),
    "chest_wood": (122, 76, 40),
    "chest_dark": (70, 42, 22),
    "beam": (255, 222, 120),
    "common": (225, 225, 220),
    "common_light": (255, 255, 250),
    "rare": (60, 130, 255),
    "rare_light": (150, 200, 255),
    "epic": (170, 70, 235),
    "epic_light": (220, 160, 255),
    "legendary": (255, 170, 30),
    "legendary_light": (255, 225, 140),
    "canvas": (206, 190, 150),
    "canvas_dark": (150, 132, 96),
    "stone": (120, 118, 112),
    "iron": (70, 74, 80),
    "straw": (214, 186, 110),
    "target_red": (190, 48, 40),
    "target_white": (236, 232, 220),
    "banner": (44, 104, 60),
}
COLOURS = list(PALETTE)


def swatch_uv(colour):
    index = COLOURS.index(colour)
    return ((index + 0.5) / len(COLOURS), 0.5)


class Mesh:
    def __init__(self):
        self.positions, self.normals, self.uvs, self.indices = [], [], [], []

    def quad(self, a, b, c, d, normal, colour):
        """Adds the face a-b-c-d (counter-clockwise seen from outside)."""
        base = len(self.positions)
        uv = swatch_uv(colour)
        for p in (a, b, c, d):
            self.positions.append(p)
            self.normals.append(normal)
            self.uvs.append(uv)
        self.indices += [base, base + 1, base + 2, base, base + 2, base + 3]

    def tri(self, a, b, c, colour):
        base = len(self.positions)
        normal = _normalize(_cross(_sub(b, a), _sub(c, a)))
        uv = swatch_uv(colour)
        for p in (a, b, c):
            self.positions.append(p)
            self.normals.append(normal)
            self.uvs.append(uv)
        self.indices += [base, base + 1, base + 2]

    def box(self, x0, y0, z0, x1, y1, z1, colour):
        self.quad((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), (0, 0, 1), colour)    # front (+Z)
        self.quad((x1, y0, z0), (x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (0, 0, -1), colour)   # back
        self.quad((x1, y0, z1), (x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (1, 0, 0), colour)    # +X
        self.quad((x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0), (-1, 0, 0), colour)   # -X
        self.quad((x0, y1, z1), (x1, y1, z1), (x1, y1, z0), (x0, y1, z0), (0, 1, 0), colour)    # top
        self.quad((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (0, -1, 0), colour)   # bottom

    def pyramid(self, x0, y0, z0, x1, y1, z1, apex, colour):
        """A four-sided pyramid on the rectangle (x0..x1, z0..z1) at height y0, up to apex."""
        a, b, c, d = (x0, y0, z1), (x1, y0, z1), (x1, y0, z0), (x0, y0, z0)
        for p, q in ((a, b), (b, c), (c, d), (d, a)):
            self.tri(p, q, apex, colour)


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = (v[0] ** 2 + v[1] ** 2 + v[2] ** 2) ** 0.5
    return (v[0] / length, v[1] / length, v[2] / length)


def build_ranger():
    m = Mesh()
    # Legs and boots
    m.box(-0.19, 0.0, -0.08, -0.03, 0.14, 0.12, "leather")
    m.box(0.03, 0.0, -0.08, 0.19, 0.14, 0.12, "leather")
    m.box(-0.18, 0.14, -0.07, -0.04, 0.82, 0.07, "trousers")
    m.box(0.04, 0.14, -0.07, 0.18, 0.82, 0.07, "trousers")
    # Tunic, belt, arms, hands
    m.box(-0.23, 0.72, -0.13, 0.23, 1.42, 0.13, "tunic")
    m.box(-0.24, 0.84, -0.14, 0.24, 0.92, 0.14, "leather")
    m.box(-0.35, 0.86, -0.07, -0.23, 1.40, 0.07, "tunic")
    m.box(0.23, 0.86, -0.07, 0.35, 1.40, 0.07, "tunic")
    m.box(-0.34, 0.76, -0.05, -0.24, 0.86, 0.05, "skin")
    m.box(0.24, 0.76, -0.05, 0.34, 0.86, 0.05, "skin")
    # Head, eyes, hood
    m.box(-0.12, 1.44, -0.12, 0.12, 1.70, 0.12, "skin")
    m.box(-0.08, 1.57, 0.12, -0.03, 1.61, 0.125, "dark")
    m.box(0.03, 1.57, 0.12, 0.08, 1.61, 0.125, "dark")
    m.box(-0.15, 1.42, -0.16, 0.15, 1.73, -0.02, "hood")      # back of the hood
    m.box(-0.15, 1.62, -0.02, 0.15, 1.73, 0.13, "hood")       # brim over the brow
    m.pyramid(-0.15, 1.73, -0.16, 0.15, 1.73, 0.13, (0.0, 1.90, -0.12), "hood")
    # Cape
    m.box(-0.24, 0.50, -0.19, 0.24, 1.42, -0.14, "hood")
    # Quiver with arrows on the back, bow in the left hand
    m.box(0.06, 0.95, -0.30, 0.18, 1.50, -0.19, "leather")
    m.box(0.08, 1.50, -0.28, 0.16, 1.60, -0.21, "fletching")
    m.box(-0.40, 0.45, 0.02, -0.36, 1.75, 0.07, "wood")       # bow stave
    m.box(-0.39, 0.52, -0.03, -0.375, 1.68, -0.015, "fletching")  # bow string
    return m


def build_arrow():
    m = Mesh()
    m.box(-0.012, -0.012, -0.40, 0.012, 0.012, 0.34, "wood")                     # shaft
    tip, left, right = (0.0, 0.0, 0.48), (-0.04, 0.0, 0.34), (0.04, 0.0, 0.34)
    for y in (0.012, -0.012):
        top = (0.0, y, 0.36)
        m.tri(left, top, tip, "steel")
        m.tri(top, right, tip, "steel")
        m.tri(left, right, top, "steel")
    m.box(-0.002, -0.05, -0.40, 0.002, 0.05, -0.26, "fletching")   # vertical vane
    m.box(-0.05, -0.002, -0.40, 0.05, 0.002, -0.26, "fletching")   # horizontal vane
    return m


def build_ghoul():
    m = Mesh()
    # Crooked legs and feet
    m.box(-0.20, 0.0, -0.06, -0.06, 0.10, 0.16, "ghoul_skin")
    m.box(0.06, 0.0, -0.06, 0.20, 0.10, 0.16, "ghoul_skin")
    m.box(-0.18, 0.10, -0.06, -0.07, 0.62, 0.06, "ghoul_skin")
    m.box(0.07, 0.10, -0.06, 0.18, 0.62, 0.06, "ghoul_skin")
    # Ragged loincloth and a hunched torso leaning forward
    m.box(-0.22, 0.52, -0.12, 0.22, 0.72, 0.12, "ghoul_rags")
    m.box(-0.24, 0.70, -0.10, 0.24, 1.10, 0.20, "ghoul_skin")
    m.box(-0.22, 1.00, 0.02, 0.22, 1.28, 0.32, "ghoul_skin")
    m.box(-0.08, 0.86, -0.14, 0.08, 1.20, -0.10, "bone")          # spine ridge
    # Long arms reaching forward and down, with claws
    m.box(-0.38, 0.62, 0.10, -0.26, 1.24, 0.24, "ghoul_skin")
    m.box(0.26, 0.62, 0.10, 0.38, 1.24, 0.24, "ghoul_skin")
    m.box(-0.39, 0.50, 0.14, -0.27, 0.62, 0.34, "bone")
    m.box(0.27, 0.50, 0.14, 0.39, 0.62, 0.34, "bone")
    # Head thrust forward, red eyes, jaw
    m.box(-0.13, 1.14, 0.28, 0.13, 1.40, 0.52, "ghoul_skin")
    m.box(-0.10, 1.27, 0.52, -0.03, 1.32, 0.525, "ghoul_eye")
    m.box(0.03, 1.27, 0.52, 0.10, 1.32, 0.525, "ghoul_eye")
    m.box(-0.10, 1.10, 0.32, 0.10, 1.16, 0.50, "bone")
    return m


def build_xp_gem():
    """An elongated octahedron, 0.3 m tall: light facets on top, deeper blue below."""
    m = Mesh()
    top, bottom = (0.0, 0.16, 0.0), (0.0, -0.14, 0.0)
    ring = [(0.09, 0.0, 0.0), (0.0, 0.0, 0.09), (-0.09, 0.0, 0.0), (0.0, 0.0, -0.09)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, top, b, "gem_light")
        m.tri(b, bottom, a, "gem")
    return m


def build_brute():
    """About 2.4 m tall, broad and stooped, horns, a bone club in the right hand."""
    m = Mesh()
    # Thick legs and hooves
    m.box(-0.42, 0.0, -0.18, -0.12, 0.18, 0.26, "brute_hide")
    m.box(0.12, 0.0, -0.18, 0.42, 0.18, 0.26, "brute_hide")
    m.box(-0.40, 0.18, -0.16, -0.14, 0.95, 0.16, "brute_skin")
    m.box(0.14, 0.18, -0.16, 0.40, 0.95, 0.16, "brute_skin")
    # Hide loincloth, barrel belly, huge chest and shoulders leaning forward
    m.box(-0.48, 0.80, -0.26, 0.48, 1.10, 0.26, "brute_hide")
    m.box(-0.52, 1.05, -0.28, 0.52, 1.60, 0.34, "brute_skin")
    m.box(-0.66, 1.55, -0.24, 0.66, 2.05, 0.46, "brute_skin")
    m.box(-0.20, 1.40, -0.36, 0.20, 2.00, -0.28, "bone")        # spine plates
    # Arms: left hanging, right gripping a club held forward
    m.box(-0.90, 0.95, 0.00, -0.64, 1.95, 0.30, "brute_skin")
    m.box(-0.92, 0.78, 0.04, -0.62, 0.98, 0.34, "brute_hide")   # fist
    m.box(0.64, 1.10, 0.10, 0.90, 1.95, 0.40, "brute_skin")
    m.box(0.62, 0.95, 0.20, 0.92, 1.15, 0.50, "brute_hide")     # fist
    m.box(0.70, 0.55, 0.30, 0.84, 1.10, 1.30, "bone")           # club shaft, forward
    m.box(0.64, 0.45, 1.10, 0.90, 0.80, 1.55, "bone")           # club head
    # Head sunk between the shoulders, red eyes, tusks, horns
    m.box(-0.22, 1.80, 0.36, 0.22, 2.20, 0.74, "brute_skin")
    m.box(-0.16, 2.02, 0.74, -0.06, 2.08, 0.745, "ghoul_eye")
    m.box(0.06, 2.02, 0.74, 0.16, 2.08, 0.745, "ghoul_eye")
    m.box(-0.16, 1.80, 0.70, -0.10, 1.92, 0.78, "horn")
    m.box(0.10, 1.80, 0.70, 0.16, 1.92, 0.78, "horn")
    m.pyramid(-0.34, 2.10, 0.44, -0.18, 2.10, 0.60, (-0.52, 2.45, 0.40), "horn")
    m.pyramid(0.18, 2.10, 0.44, 0.34, 2.10, 0.60, (0.52, 2.45, 0.40), "horn")
    return m


def build_hollow_king():
    """About 4.2 m tall: gaunt and regal, a gold crown, glowing green eyes, a tattered cape, long clawed arms."""
    m = Mesh()
    # Legs under a long robe
    m.box(-0.50, 0.0, -0.30, -0.16, 0.30, 0.40, "king_skin")
    m.box(0.16, 0.0, -0.30, 0.50, 0.30, 0.40, "king_skin")
    m.box(-0.70, 0.30, -0.40, 0.70, 1.90, 0.40, "king_robe")
    # Gaunt torso, ribs, shoulders
    m.box(-0.55, 1.90, -0.30, 0.55, 3.00, 0.34, "king_skin")
    for y in (2.15, 2.40, 2.65):
        m.box(-0.45, y, 0.34, 0.45, y + 0.08, 0.40, "bone")
    m.box(-0.95, 2.85, -0.34, 0.95, 3.25, 0.38, "king_robe")    # mantle
    m.box(-0.90, 0.40, -0.52, 0.90, 3.15, -0.40, "king_robe")   # cape
    # Long arms reaching down to the knees, bone claws
    m.box(-1.25, 1.10, -0.14, -0.95, 3.05, 0.20, "king_skin")
    m.box(0.95, 1.10, -0.14, 1.25, 3.05, 0.20, "king_skin")
    m.box(-1.30, 0.70, -0.08, -0.90, 1.10, 0.40, "bone")
    m.box(0.90, 0.70, -0.08, 1.30, 1.10, 0.40, "bone")
    # Skull-like head, glowing eyes, jaw, crown
    m.box(-0.34, 3.20, -0.24, 0.34, 3.86, 0.40, "bone")
    m.box(-0.22, 3.52, 0.40, -0.06, 3.62, 0.41, "ghost_eye")
    m.box(0.06, 3.52, 0.40, 0.22, 3.62, 0.41, "ghost_eye")
    m.box(-0.24, 3.12, 0.00, 0.24, 3.24, 0.38, "bone")
    m.box(-0.38, 3.86, -0.28, 0.38, 3.98, 0.44, "gold")
    for x, z in ((-0.30, 0.34), (0.0, 0.40), (0.30, 0.34), (-0.30, -0.20), (0.30, -0.20)):
        m.pyramid(x - 0.08, 3.98, z - 0.08, x + 0.08, 3.98, z + 0.08, (x, 4.22, z), "gold")
    return m


def build_ring(inner, outer, colour, segments=48, y=0.0):
    """A flat ring (annulus) on the ground, facing up."""
    import math
    m = Mesh()
    for i in range(segments):
        a0 = 2 * math.pi * i / segments
        a1 = 2 * math.pi * (i + 1) / segments
        p0 = (math.cos(a0) * inner, y, math.sin(a0) * inner)
        p1 = (math.cos(a0) * outer, y, math.sin(a0) * outer)
        p2 = (math.cos(a1) * outer, y, math.sin(a1) * outer)
        p3 = (math.cos(a1) * inner, y, math.sin(a1) * inner)
        # Counter-clockwise seen from above (+Y): inner0 -> inner1 -> outer1 -> outer0
        m.quad(p0, p3, p2, p1, (0, 1, 0), colour)
    return m


def build_disc(colour, segments=48, y=0.0):
    import math
    m = Mesh()
    for i in range(segments):
        a0 = 2 * math.pi * i / segments
        a1 = 2 * math.pi * (i + 1) / segments
        centre = (0.0, y, 0.0)
        p0 = (math.cos(a0), y, math.sin(a0))
        p1 = (math.cos(a1), y, math.sin(a1))
        base = len(m.positions)
        uv = swatch_uv(colour)
        for p in (centre, p1, p0):
            m.positions.append(p)
            m.normals.append((0, 1, 0))
            m.uvs.append(uv)
        m.indices += [base, base + 1, base + 2]
    return m


def build_lane():
    """A lunge's path: an outline 1.6 m wide, 7.2 m long, from the origin along +Z, with cross-bars along it."""
    m = Mesh()
    w, length, edge = 0.8, 7.2, 0.12
    up = (0, 1, 0)

    def flat(x0, z0, x1, z1, colour):
        m.quad((x0, 0.0, z1), (x1, 0.0, z1), (x1, 0.0, z0), (x0, 0.0, z0), up, colour)

    flat(-w, 0.0, w, length, "warn_dark")
    flat(-w, 0.0, -w + edge, length, "warn")
    flat(w - edge, 0.0, w, length, "warn")
    flat(-w, length - edge, w, length, "warn")
    for z in (1.8, 3.6, 5.4):
        flat(-w + edge, z, w - edge, z + edge, "warn")
    return m


def build_chest():
    m = Mesh()
    m.box(-0.50, 0.0, -0.32, 0.50, 0.50, 0.32, "chest_wood")      # body
    m.box(-0.50, 0.50, -0.32, 0.50, 0.72, 0.32, "chest_dark")     # lid
    for x in (-0.36, 0.30):                                         # gold bands over body and lid
        m.box(x, -0.005, -0.335, x + 0.07, 0.725, 0.335, "gold")
    m.box(-0.08, 0.40, 0.32, 0.08, 0.58, 0.35, "gold")             # lock plate, on the front
    return m


def build_beam():
    m = Mesh()
    m.box(-0.07, 0.0, -0.07, 0.07, 8.0, 0.07, "beam")
    return m


def build_item_orb(rarity):
    """A chunky double pyramid, 0.5 m tall: light facets above, deeper colour below."""
    m = Mesh()
    top, bottom = (0.0, 0.26, 0.0), (0.0, -0.24, 0.0)
    ring = [(0.17, 0.0, 0.0), (0.0, 0.0, 0.17), (-0.17, 0.0, 0.0), (0.0, 0.0, -0.17)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, top, b, rarity + "_light")
        m.tri(b, bottom, a, rarity)
    return m


def build_tent():
    """An A-frame canvas tent, 3 m long, 2.2 m wide, 1.9 m tall, its opening facing +Z."""
    m = Mesh()
    w, h, l = 1.1, 1.9, 1.5
    # Two sloped roof panels, facing outward
    for side in (-1, 1):
        a, b = (side * w, 0.0, l), (side * w, 0.0, -l)
        c, d = (0.0, h, -l), (0.0, h, l)
        normal = _normalize((side * h, w, 0.0))
        if side == 1:
            m.quad(a, b, c, d, normal, "canvas")
        else:
            m.quad(b, a, d, c, normal, "canvas")
    # Back wall, and a dark doorway triangle at the front
    m.tri((w, 0.0, -l), (-w, 0.0, -l), (0.0, h, -l), "canvas_dark")
    m.tri((-w * 0.45, 0.0, l - 0.01), (w * 0.45, 0.0, l - 0.01), (0.0, h * 0.8, l - 0.01), "dark")
    m.box(-0.04, 0.0, l, 0.04, h + 0.15, l + 0.08, "wood")      # front pole
    return m


def build_firepit():
    """A ring of stones 1.4 m across with a few logs laid in it."""
    import math
    m = Mesh()
    for i in range(10):
        a = 2 * math.pi * i / 10
        x, z = math.cos(a) * 0.62, math.sin(a) * 0.62
        m.box(x - 0.12, 0.0, z - 0.1, x + 0.12, 0.16, z + 0.1, "stone")
    m.box(-0.45, 0.0, -0.07, 0.45, 0.13, 0.07, "chest_dark")
    m.box(-0.07, 0.05, -0.45, 0.07, 0.18, 0.45, "wood")
    return m


def build_stash():
    """The stash: a big iron-bound chest, 1.6 m wide, on a low plinth."""
    m = Mesh()
    m.box(-0.90, 0.0, -0.55, 0.90, 0.12, 0.55, "stone")
    m.box(-0.80, 0.12, -0.48, 0.80, 0.82, 0.48, "chest_wood")
    m.box(-0.82, 0.82, -0.50, 0.82, 1.10, 0.50, "chest_dark")
    for x in (-0.62, -0.05, 0.52):
        m.box(x, 0.11, -0.51, x + 0.10, 1.11, 0.51, "iron")
    m.box(-0.12, 0.62, 0.48, 0.12, 0.90, 0.53, "gold")          # lock, on the front
    return m


def build_target():
    """An archery target: a straw boss 1.2 m across on two legs, painted rings on its front (+Z)."""
    m = Mesh()
    m.box(-0.55, 0.0, -0.30, -0.47, 1.45, -0.22, "wood")        # legs
    m.box(0.47, 0.0, -0.30, 0.55, 1.45, -0.22, "wood")
    m.box(-0.60, 0.55, -0.20, 0.60, 1.75, 0.05, "straw")        # the boss
    m.box(-0.45, 0.70, 0.05, 0.45, 1.60, 0.07, "target_white")  # rings, front to back
    m.box(-0.32, 0.83, 0.07, 0.32, 1.47, 0.09, "target_red")
    m.box(-0.19, 0.96, 0.09, 0.19, 1.34, 0.11, "target_white")
    m.box(-0.08, 1.07, 0.11, 0.08, 1.23, 0.13, "target_red")
    m.box(0.12, 1.05, 0.13, 0.15, 1.08, 0.60, "wood")           # an arrow in it
    return m


def build_gate():
    """The departure gate: two posts 4 m apart and 3.4 m tall, a crossbeam, and a green banner hanging in the middle."""
    m = Mesh()
    m.box(-2.1, 0.0, -0.15, -1.8, 3.4, 0.15, "wood")
    m.box(1.8, 0.0, -0.15, 2.1, 3.4, 0.15, "wood")
    m.box(-2.4, 3.1, -0.18, 2.4, 3.45, 0.18, "chest_dark")
    m.box(-0.7, 1.9, -0.03, 0.7, 3.1, 0.03, "banner")
    m.box(-0.2, 2.3, 0.03, 0.2, 2.7, 0.05, "gold")               # a crest on the banner
    return m


def build_board():
    """A notice board 2 m wide on two posts, its face (+Z) pinned with papers."""
    m = Mesh()
    m.box(-1.1, 0.0, -0.1, -0.95, 2.3, 0.05, "wood")
    m.box(0.95, 0.0, -0.1, 1.1, 2.3, 0.05, "wood")
    m.box(-1.0, 0.9, -0.06, 1.0, 2.1, 0.02, "chest_dark")
    m.box(-1.15, 2.1, -0.14, 1.15, 2.3, 0.1, "wood")               # a little roof over it
    for x, y in ((-0.75, 1.55), (-0.25, 1.65), (0.3, 1.5), (0.7, 1.7), (-0.55, 1.05), (0.1, 1.08), (0.6, 1.1)):
        m.box(x - 0.16, y - 0.2, 0.02, x + 0.16, y + 0.2, 0.035, "target_white")
    m.box(0.26, 1.43, 0.035, 0.34, 1.51, 0.045, "target_red")      # a wax seal on one
    return m


def build_stall():
    """The quartermaster's stall: a counter 2.4 m wide under a striped awning on four poles, a sack and a chest beside it. Its front faces +Z."""
    m = Mesh()
    m.box(-1.2, 0.0, -0.3, 1.2, 1.0, 0.3, "chest_wood")            # counter
    m.box(-1.25, 1.0, -0.35, 1.25, 1.08, 0.35, "chest_dark")       # counter top
    for x in (-1.3, 1.3):
        for z in (-0.9, 0.55):
            m.box(x - 0.05, 0.0, z - 0.05, x + 0.05, 2.5, z + 0.05, "wood")
    for i, x in enumerate((-1.4, -0.7, 0.0, 0.7)):                 # the awning, in stripes
        m.box(x, 2.5, -1.0, x + 0.7, 2.6, 0.75, "banner" if i % 2 == 0 else "canvas")
    m.box(-0.9, 1.08, -0.15, -0.5, 1.4, 0.15, "canvas_dark")       # goods on the counter
    m.box(0.3, 1.08, -0.1, 0.8, 1.25, 0.2, "gold")
    m.box(1.45, 0.0, 0.1, 1.85, 0.6, 0.45, "canvas")               # a sack
    return m


def png_bytes():
    width, height = SWATCH * len(COLOURS), SWATCH
    row = b"".join(bytes(PALETTE[c]) * SWATCH for c in COLOURS)
    raw = b"".join(b"\x00" + row for _ in range(height))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")


def pad4(data, fill=b"\x00"):
    return data + fill * (-len(data) % 4)


def write_glb(mesh, path):
    positions = b"".join(struct.pack("<3f", *p) for p in mesh.positions)
    normals = b"".join(struct.pack("<3f", *n) for n in mesh.normals)
    uvs = b"".join(struct.pack("<2f", *uv) for uv in mesh.uvs)
    indices = b"".join(struct.pack("<I", i) for i in mesh.indices)
    image = png_bytes()

    views, blob = [], b""
    for data, target in ((positions, 34962), (normals, 34962), (uvs, 34962), (indices, 34963), (image, None)):
        view = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}
        if target:
            view["target"] = target
        views.append(view)
        blob += pad4(data)

    count = len(mesh.positions)
    mins = [min(p[i] for p in mesh.positions) for i in range(3)]
    maxs = [max(p[i] for p in mesh.positions) for i in range(3)]
    gltf = {
        "asset": {"version": "2.0", "generator": "Arena Master make_placeholder_models.py"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": path.stem}],
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "indices": 3, "material": 0}]}],
        "materials": [{"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0, "roughnessFactor": 1.0}}],
        "textures": [{"source": 0, "sampler": 0}],
        "samplers": [{"magFilter": 9728, "minFilter": 9728}],
        "images": [{"bufferView": 4, "mimeType": "image/png"}],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": count, "type": "VEC3", "min": mins, "max": maxs},
            {"bufferView": 1, "componentType": 5126, "count": count, "type": "VEC3"},
            {"bufferView": 2, "componentType": 5126, "count": count, "type": "VEC2"},
            {"bufferView": 3, "componentType": 5125, "count": len(mesh.indices), "type": "SCALAR"},
        ],
        "bufferViews": views,
        "buffers": [{"byteLength": len(blob)}],
    }

    json_chunk = pad4(json.dumps(gltf, separators=(",", ":")).encode(), b" ")
    body = struct.pack("<II", len(json_chunk), 0x4E4F534A) + json_chunk + struct.pack("<II", len(blob), 0x004E4942) + blob
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body)


if __name__ == "__main__":
    for name, build in (("ranger_placeholder.glb", build_ranger), ("arrow_placeholder.glb", build_arrow), ("ghoul_placeholder.glb", build_ghoul), ("xp_gem_placeholder.glb", build_xp_gem),
                        ("brute_placeholder.glb", build_brute), ("hollow_king_placeholder.glb", build_hollow_king),
                        ("telegraph_ring.glb", lambda: build_ring(0.9, 1.0, "warn")), ("telegraph_disc.glb", lambda: build_disc("warn_dark")),
                        ("telegraph_lane.glb", build_lane), ("shockwave_ring.glb", lambda: build_ring(0.965, 1.035, "shock")),
                        ("chest_placeholder.glb", build_chest), ("loot_beam.glb", build_beam),
                        ("item_common.glb", lambda: build_item_orb("common")), ("item_rare.glb", lambda: build_item_orb("rare")),
                        ("item_epic.glb", lambda: build_item_orb("epic")), ("item_legendary.glb", lambda: build_item_orb("legendary")),
                        ("camp_tent.glb", build_tent), ("camp_firepit.glb", build_firepit), ("camp_stash.glb", build_stash),
                        ("camp_target.glb", build_target), ("camp_gate.glb", build_gate),
                        ("camp_board.glb", build_board), ("camp_stall.glb", build_stall)):
        mesh = build()
        write_glb(mesh, MODELS / name)
        print(f"Wrote {MODELS / name} ({len(mesh.positions)} vertices, {len(mesh.indices) // 3} triangles)")
