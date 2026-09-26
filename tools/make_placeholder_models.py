"""Writes blocky stand-in models to assets/models/ until real ones are made. Plain Python, no dependencies:
  ranger_placeholder.glb  the Ranger (tunic, hood, cape, bow, quiver)
  paladin_placeholder.glb the Paladin (plate armour, a white tabard with a red cross, a great helm, a tower shield on the
                          left arm and a flail in the right hand)
  holy_nova.glb           a flat gold ring of radius 1, scaled as a Holy Nova spreads out over the ground
  holy_circle.glb         a flat holy circle of radius 1: a gold ring with an inner ring and a cross, the ground a nova leaves
  mage_placeholder.glb    the Mage (a long blue robe, a pointed hat, a white beard, a staff crowned with an ice crystal)
  shaman_placeholder.glb  the Shaman (a fur mantle over a hide tunic, a bone mask with antlers, blue war paint, a totem staff crowned with a crackling orb)
  lightning_ball.glb      a ball of lightning: a spiky pale-blue orb with a white core, radius 0.35, centred on its middle
  lightning_arc.glb       one piece of a lightning bolt: a thin bright bar 1 m long along Z, centred on its middle (scaled to each piece's length)
  lightning_zap.glb       a flat pale-blue ring of radius 1, scaled as a zap spreads over the ground
  frost_bolt.glb          a Frost Barrage bolt: a pale ice crystal, centred on its middle, pointing +Z
  frost_blast.glb         a flat pale-blue ring of radius 1, scaled as a burst of frost spreads
  frost_shard.glb         a small ice shard, centred on its middle (the Frost Shield's and the Blizzard's swirl)
  aegis_burst.glb         a flat pale-gold ring of radius 1, scaled as the Aegis of the Dawn's burst spreads (an item's, any class)
  thunderstone_bolt.glb   one piece of the Thunderstone's bolt from the sky: a thin bright bar 1 m long along Z (an item's, any class)
  arrow_placeholder.glb   an arrow, centred on its middle, pointing +Z
  ghoul_placeholder.glb   the first enemy: a hunched ghoul with long arms and red eyes
  crossbow_ghoul_placeholder.glb  the ranged ghoul: hooded, a quiver on its back, arms held out in front to carry its crossbow
  ghoul_crossbow.glb      the Crossbow Ghoul's crossbow, drawn over its body in the same pose: a stock, limbs with glowing runes, a loaded bolt
  ghoul_bolt.glb          a Crossbow Ghoul's bolt: a dark shaft with a glowing head, centred on its middle, pointing +Z
  ghoul_mage_placeholder.glb  the Ghoul Mage: a gaunt ghoul in a long tattered robe and a tall hood, hands held out cupped in front of it
  ghoul_flame.glb         the Ghoul Mage's flame, drawn over its body in the same pose: a burning orb between its hands
  ghoul_fireball.glb      a Ghoul Mage's fireball: a fiery orb with a bright core, centred on its middle
  fireball_mark.glb       a flat burning ring of radius 1 with flames on it, marking where a fireball will land
  fireball_burst.glb      a flat ring of fire of radius 1, scaled as a fireball bursts
  xp_gem_placeholder.glb  an experience gem: a small glowing-blue crystal, centred on its middle
  brute_placeholder.glb   the elite: a hulking horned ghoul brute, 2.4 m tall, with a bone club
  hollow_king_placeholder.glb  the boss: a towering crowned ghoul king, 4.2 m tall, with a tattered cape
  telegraph_ring.glb      a flat red ring of radius 1 on y = 0, scaled to an attack's radius (outline of where it lands)
  telegraph_disc.glb      a flat dark-red disc of radius 1, scaled up inside the ring as an attack winds up
  telegraph_lane.glb      a flat red lane 1.6 m wide and 7.2 m long from the origin along +Z (a lunge's path)
  shockwave_ring.glb      a thin flat ring of radius 1 (width 0.07), scaled as a shockwave spreads
  chest_placeholder.glb   a wooden treasure chest with gold bands, 1 m wide
  crate_placeholder.glb   a breakable wooden crate, 0.9 m, planked with dark edges and a cross-brace
  pickup_magnet.glb, pickup_apple.glb, pickup_roast.glb, pickup_silver.glb, pickup_bomb.glb, pickup_frenzy.glb
                          what a crate leaves, each about 0.4 m and centred on its middle: a red horseshoe magnet with steel tips, an apple,
                          a roast leg on the bone, a pouch spilling silver coins, a black bomb with a lit fuse, a red potion with a cork
  bomb_blast.glb          a flat ring of fire of radius 1, scaled as a crate bomb's blast spreads
  loot_beam.glb           a thin gold pillar of light, 8 m tall, marking a chest from afar
  item_<rarity>.glb       an item orb in its rarity's colour (common, rare, epic, legendary), centred on its middle
  camp_tent.glb, camp_firepit.glb, camp_stash.glb, camp_target.glb, camp_gate.glb, camp_board.glb, camp_stall.glb
  camp_rack.glb           the camp: a canvas tent, a ring of stones with logs (the engine's fire burns on it), the stash chest,
                          the archery target (the passive tree station), the departure gate (a gatehouse set in the palisade: two
                          towers and closed doors, the way into a run), the bounty board (a notice board with papers pinned to it),
                          the quartermaster's stall (a counter under an awning) and the weapon rack (the class station: a bow, an
                          ice staff, a totem, and a shield with a flail)
  camp_wall.glb, camp_wall_post.glb   the palisade round the camp: a 3.5 m piece of sharpened logs, and the thick post at each corner
  camp_barrel.glb, camp_crates.glb, camp_woodpile.glb, camp_bench.glb, camp_banner.glb, camp_brazier.glb, camp_haybale.glb,
  camp_dummy.glb, camp_well.glb, camp_cart.glb, camp_cookpot.glb, camp_bedroll.glb, camp_lantern.glb, camp_sacks.glb
                          the camp's dressing: a barrel, a stack of crates, a woodpile with a chopping stump, a log bench, a banner on
                          a pole, an iron brazier (the engine's fire burns on it), a straw bale, a training dummy, a well, a supply cart,
                          a cooking fire with a pot, a bedroll, a lantern post, and grain sacks
  camp_armorstand.glb     the gear station: an armour stand wearing mail and an orange tabard, a helm on top, a sword and shield by it
  fire_nova.glb           a flat ring of bright fire of radius 1, scaled as the Emberbrand's nova spreads (gear, any class)
  delve_cache.glb         the chest a Delve boss leaves: black and iron-bound, with a burning-orange seal and trim, on a stone slab
  hollow_king_unbound.glb the Delve boss: the Hollow King grown to 5.6 m, with horns, bone pauldrons, a burning crown and eyes, broken chains
  camp_quartermaster.glb  the quartermaster behind the stall, skinned (8 joints) with a looping 6 s "Idle" clip: he breathes, shifts his
                          weight, looks along the camp one way and the other, strokes his moustache and nods, a hand on his hip

The engine's model conventions: one mesh, one primitive, colours from one base-colour texture (vertex colours are
ignored), facing +Z, feet at y = 0, metres. A skinned model adds JOINTS_0/WEIGHTS_0 (each vertex wholly on one joint), a skin and
its animations. The texture is a strip of flat colour swatches and each face's UVs point
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
    "plate": (168, 174, 184),
    "plate_dark": (104, 110, 122),
    "tabard": (232, 228, 214),
    "crusader": (178, 30, 34),
    "visor": (18, 16, 16),
    "holy": (255, 232, 140),
    "holy_light": (255, 248, 210),
    "robe": (46, 74, 150),
    "robe_dark": (28, 44, 96),
    "beard": (226, 226, 230),
    "ice": (150, 214, 255),
    "ice_light": (226, 246, 255),
    "hood_dark": (48, 40, 52),
    "bolt_glow": (255, 110, 40),
    "ghoul_robe": (74, 30, 44),
    "fire": (255, 96, 24),
    "fire_light": (255, 214, 110),
    "fur": (112, 86, 60),
    "hide": (150, 116, 80),
    "war_paint": (40, 90, 190),
    "spark": (120, 196, 255),
    "spark_light": (236, 246, 255),
    "magnet_red": (196, 38, 38),
    "apple_red": (212, 44, 36),
    "leaf": (74, 150, 52),
    "meat": (150, 72, 42),
    "coin": (206, 210, 218),
    "bomb_black": (34, 34, 40),
    "potion": (222, 40, 96),
    "cork": (172, 132, 82),
}
COLOURS = list(PALETTE)


def swatch_uv(colour):
    index = COLOURS.index(colour)
    return ((index + 0.5) / len(COLOURS), 0.5)


class Mesh:
    def __init__(self):
        self.positions, self.normals, self.uvs, self.indices = [], [], [], []
        self.joints = []   # per vertex: the one joint it moves with (only a skinned model uses them)
        self.joint = 0     # the joint the faces added next are bound to

    def quad(self, a, b, c, d, normal, colour):
        """Adds the face a-b-c-d (counter-clockwise seen from outside)."""
        base = len(self.positions)
        uv = swatch_uv(colour)
        for p in (a, b, c, d):
            self.positions.append(p)
            self.normals.append(normal)
            self.uvs.append(uv)
            self.joints.append(self.joint)
        self.indices += [base, base + 1, base + 2, base, base + 2, base + 3]

    def tri(self, a, b, c, colour):
        base = len(self.positions)
        normal = _normalize(_cross(_sub(b, a), _sub(c, a)))
        uv = swatch_uv(colour)
        for p in (a, b, c):
            self.positions.append(p)
            self.normals.append(normal)
            self.uvs.append(uv)
            self.joints.append(self.joint)
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

    def facing(self, points, outward, colour):
        """A flat convex face through <points> (3 or 4, in order around it), wound so that it faces <outward>."""
        n = _cross(_sub(points[1], points[0]), _sub(points[2], points[0]))
        if n[0] * outward[0] + n[1] * outward[1] + n[2] * outward[2] < 0:
            points = list(reversed(points))
        if len(points) == 3:
            self.tri(*points, colour)
        else:
            self.quad(*points, _normalize(outward), colour)

    def prism(self, cx, cz, r, y0, y1, colour, sides=10, top=None, bottom=False):
        """An upright round post: <sides> faces around (cx, cz), radius r, from y0 to y1, capped on top with <top> if given."""
        import math
        ring = [(cx + r * math.cos(2 * math.pi * i / sides), cz + r * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
        for i in range(sides):
            (xa, za), (xb, zb) = ring[i], ring[(i + 1) % sides]
            out = ((xa + xb) / 2 - cx, 0.0, (za + zb) / 2 - cz)
            self.facing([(xa, y0, za), (xb, y0, zb), (xb, y1, zb), (xa, y1, za)], out, colour)
        for y, on, up in ((y1, top, 1.0), (y0, bottom and (top or colour), -1.0)):
            if on:
                for i in range(sides):
                    (xa, za), (xb, zb) = ring[i], ring[(i + 1) % sides]
                    self.facing([(cx, y, cz), (xa, y, za), (xb, y, zb)], (0.0, up, 0.0), on)

    def log(self, x0, x1, cy, cz, r, colour, ends=None, sides=8):
        """A round log lying along X from x0 to x1, its axis at (cy, cz), capped at both ends with <ends>."""
        import math
        ring = [(cy + r * math.cos(2 * math.pi * i / sides), cz + r * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
        for i in range(sides):
            (ya, za), (yb, zb) = ring[i], ring[(i + 1) % sides]
            out = (0.0, (ya + yb) / 2 - cy, (za + zb) / 2 - cz)
            self.facing([(x0, ya, za), (x0, yb, zb), (x1, yb, zb), (x1, ya, za)], out, colour)
        for x, side in ((x0, -1.0), (x1, 1.0)):
            for i in range(sides):
                (ya, za), (yb, zb) = ring[i], ring[(i + 1) % sides]
                self.facing([(x, cy, cz), (x, ya, za), (x, yb, zb)], (side, 0.0, 0.0), ends or colour)


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


def build_paladin():
    m = Mesh()
    # Sabatons and greaves
    m.box(-0.21, 0.0, -0.09, -0.03, 0.16, 0.15, "plate_dark")
    m.box(0.03, 0.0, -0.09, 0.21, 0.16, 0.15, "plate_dark")
    m.box(-0.20, 0.16, -0.08, -0.04, 0.84, 0.08, "plate")
    m.box(0.04, 0.16, -0.08, 0.20, 0.84, 0.08, "plate")
    # Breastplate, belt, and the tabard hanging over it with its red cross
    m.box(-0.27, 0.74, -0.15, 0.27, 1.46, 0.15, "plate")
    m.box(-0.28, 0.84, -0.16, 0.28, 0.92, 0.16, "leather")
    m.box(-0.20, 0.52, 0.15, 0.20, 1.40, 0.17, "tabard")
    m.box(-0.20, 0.52, -0.17, 0.20, 1.40, -0.15, "tabard")
    m.box(-0.04, 0.78, 0.17, 0.04, 1.30, 0.18, "crusader")
    m.box(-0.14, 1.10, 0.17, 0.14, 1.17, 0.18, "crusader")
    # Pauldrons, arms, gauntlets
    m.box(-0.44, 1.30, -0.15, -0.22, 1.50, 0.15, "plate_dark")
    m.box(0.22, 1.30, -0.15, 0.44, 1.50, 0.15, "plate_dark")
    m.box(-0.40, 0.86, -0.08, -0.27, 1.32, 0.08, "plate")
    m.box(0.27, 0.86, -0.08, 0.40, 1.32, 0.08, "plate")
    m.box(-0.40, 0.76, -0.07, -0.27, 0.87, 0.09, "plate_dark")
    m.box(0.27, 0.76, -0.07, 0.40, 0.87, 0.09, "plate_dark")
    # Great helm with a visor slit and a gold crest
    m.box(-0.14, 1.46, -0.14, 0.14, 1.80, 0.14, "plate")
    m.box(-0.11, 1.62, 0.14, 0.11, 1.65, 0.145, "visor")
    m.box(-0.015, 1.50, 0.14, 0.015, 1.62, 0.145, "visor")
    m.box(-0.03, 1.80, -0.12, 0.03, 1.88, 0.12, "gold")
    # A cape down the back
    m.box(-0.26, 0.50, -0.21, 0.26, 1.46, -0.17, "crusader")
    # The great crusader shield on the left arm (-X), faced forward: gold rim, white field, red cross
    m.box(-0.66, 0.42, 0.13, -0.10, 1.40, 0.18, "gold")
    m.box(-0.63, 0.46, 0.18, -0.13, 1.36, 0.20, "tabard")
    m.box(-0.415, 0.52, 0.20, -0.335, 1.30, 0.215, "crusader")
    m.box(-0.58, 1.00, 0.20, -0.18, 1.08, 0.215, "crusader")
    # The flail in the right hand (+X): a haft, a short chain, and a spiked iron ball hanging forward
    m.box(0.31, 0.70, 0.04, 0.37, 1.10, 0.10, "wood")
    m.box(0.30, 1.08, 0.03, 0.38, 1.13, 0.11, "iron")
    for y, z in ((1.06, 0.15), (0.98, 0.21), (0.90, 0.26)):
        m.box(0.32, y - 0.03, z - 0.025, 0.36, y + 0.03, z + 0.025, "iron")
    cx, cy, cz, r, s = 0.34, 0.76, 0.30, 0.09, 0.025
    m.box(cx - r, cy - r, cz - r, cx + r, cy + r, cz + r, "iron")
    m.box(cx - 1.8 * r, cy - s, cz - s, cx + 1.8 * r, cy + s, cz + s, "steel")   # spikes through the ball on all three axes
    m.box(cx - s, cy - 1.8 * r, cz - s, cx + s, cy + 1.8 * r, cz + s, "steel")
    m.box(cx - s, cy - s, cz - 1.8 * r, cx + s, cy + s, cz + 1.8 * r, "steel")
    return m


def build_holy_circle():
    """The ground a Holy Nova leaves: a gold ring of radius 1, a thinner ring inside it, and a cross of light between them."""
    m = build_ring(0.9, 1.0, "holy")
    inner = build_ring(0.5, 0.56, "holy_light")
    base = len(m.positions)
    m.positions += inner.positions
    m.normals += inner.normals
    m.uvs += inner.uvs
    m.indices += [i + base for i in inner.indices]
    for x0, z0, x1, z1 in ((-0.05, -0.9, 0.05, 0.9), (-0.9, -0.05, 0.9, 0.05)):
        m.quad((x0, 0.005, z1), (x1, 0.005, z1), (x1, 0.005, z0), (x0, 0.005, z0), (0, 1, 0), "holy_light")
    return m


def build_mage():
    m = Mesh()
    # Boots under a long robe that widens to the hem
    m.box(-0.16, 0.0, -0.07, -0.04, 0.10, 0.13, "leather")
    m.box(0.04, 0.0, -0.07, 0.16, 0.10, 0.13, "leather")
    m.box(-0.28, 0.08, -0.20, 0.28, 0.55, 0.20, "robe")
    m.box(-0.23, 0.55, -0.15, 0.23, 1.42, 0.15, "robe")
    m.box(-0.24, 0.86, -0.16, 0.24, 0.93, 0.16, "leather")          # belt
    m.box(-0.05, 0.10, 0.20, 0.05, 1.40, 0.21, "robe_dark")          # the robe's front seam
    # Sleeves and hands
    m.box(-0.36, 0.88, -0.08, -0.23, 1.40, 0.08, "robe")
    m.box(0.23, 0.88, -0.08, 0.36, 1.40, 0.08, "robe")
    m.box(-0.35, 0.78, -0.05, -0.25, 0.88, 0.05, "skin")
    m.box(0.25, 0.78, -0.05, 0.35, 0.88, 0.05, "skin")
    # Head, eyes, beard, and a wide-brimmed pointed hat
    m.box(-0.12, 1.44, -0.12, 0.12, 1.68, 0.12, "skin")
    m.box(-0.08, 1.57, 0.12, -0.03, 1.61, 0.125, "dark")
    m.box(0.03, 1.57, 0.12, 0.08, 1.61, 0.125, "dark")
    m.box(-0.10, 1.26, 0.08, 0.10, 1.52, 0.16, "beard")
    m.box(-0.27, 1.68, -0.27, 0.27, 1.72, 0.27, "robe_dark")
    m.pyramid(-0.15, 1.72, -0.15, 0.15, 1.72, 0.15, (0.04, 2.20, -0.10), "robe_dark")
    # The staff in the right hand (+X), crowned with an ice crystal
    m.box(0.28, 0.02, 0.06, 0.33, 1.72, 0.11, "wood")
    top, bottom = (0.305, 2.02, 0.085), (0.305, 1.70, 0.085)
    ring = [(0.395, 1.84, 0.085), (0.305, 1.84, 0.175), (0.215, 1.84, 0.085), (0.305, 1.84, -0.005)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, top, b, "ice_light")
        m.tri(b, bottom, a, "ice")
    return m


def build_shaman():
    m = Mesh()
    # Hide boots and leggings
    m.box(-0.19, 0.0, -0.08, -0.03, 0.14, 0.13, "leather")
    m.box(0.03, 0.0, -0.08, 0.19, 0.14, 0.13, "leather")
    m.box(-0.18, 0.14, -0.07, -0.04, 0.82, 0.07, "hide")
    m.box(0.04, 0.14, -0.07, 0.18, 0.82, 0.07, "hide")
    # Hide tunic with a painted band, and a fur mantle over the shoulders
    m.box(-0.23, 0.72, -0.13, 0.23, 1.40, 0.13, "hide")
    m.box(-0.235, 0.98, 0.13, 0.235, 1.04, 0.135, "war_paint")
    m.box(-0.34, 1.26, -0.20, 0.34, 1.46, 0.18, "fur")
    m.box(-0.30, 0.60, -0.20, 0.30, 1.30, -0.13, "fur")              # the mantle hanging down the back
    # Bare arms with painted bands, and hands
    m.box(-0.35, 0.86, -0.07, -0.23, 1.30, 0.07, "skin")
    m.box(0.23, 0.86, -0.07, 0.35, 1.30, 0.07, "skin")
    m.box(-0.355, 1.08, -0.075, -0.225, 1.13, 0.075, "war_paint")
    m.box(0.225, 1.08, -0.075, 0.355, 1.13, 0.075, "war_paint")
    m.box(-0.34, 0.76, -0.05, -0.24, 0.86, 0.05, "skin")
    m.box(0.24, 0.76, -0.05, 0.34, 0.86, 0.05, "skin")
    # Head under a bone mask, with antlers
    m.box(-0.12, 1.46, -0.12, 0.12, 1.70, 0.12, "skin")
    m.box(-0.13, 1.50, 0.12, 0.13, 1.70, 0.16, "bone")
    m.box(-0.08, 1.60, 0.16, -0.03, 1.64, 0.165, "dark")
    m.box(0.03, 1.60, 0.16, 0.08, 1.64, 0.165, "dark")
    for side in (-1, 1):
        x, tip = side * 0.10, side * 0.26
        m.box(x - 0.02, 1.70, -0.02, x + 0.02, 1.95, 0.02, "bone")                 # an antler's stem
        m.box(min(x, tip), 1.88, -0.02, max(x, tip), 1.92, 0.02, "bone")           # and its branch, outward
    # A totem staff in the right hand (+X), crowned with a crackling orb
    m.box(0.28, 0.02, 0.06, 0.33, 1.78, 0.11, "wood")
    m.box(0.26, 1.50, 0.04, 0.35, 1.58, 0.13, "fur")
    top, bottom = (0.305, 2.06, 0.085), (0.305, 1.78, 0.085)
    ring = [(0.40, 1.92, 0.085), (0.305, 1.92, 0.18), (0.21, 1.92, 0.085), (0.305, 1.92, -0.01)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, top, b, "spark_light")
        m.tri(b, bottom, a, "spark")
    return m


def build_lightning_ball():
    """A ball of lightning, radius 0.35: a spiky pale-blue orb with the white core poking through."""
    m = Mesh()
    _orb(m, 0.0, 0.0, 0.0, 0.35, "spark", "spark_light")
    return m


def build_lightning_arc():
    """One piece of a lightning bolt: a thin bright bar 1 m long along Z, centred on its middle."""
    m = Mesh()
    m.box(-0.035, -0.035, -0.5, 0.035, 0.035, 0.5, "spark_light")
    return m


def build_frost_bolt():
    """A long, thin double pyramid of ice pointing +Z, centred on its middle."""
    m = Mesh()
    front, back = (0.0, 0.0, 0.32), (0.0, 0.0, -0.22)
    ring = [(0.07, 0.0, 0.0), (0.0, 0.07, 0.0), (-0.07, 0.0, 0.0), (0.0, -0.07, 0.0)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, b, front, "ice_light")
        m.tri(b, a, back, "ice")
    return m


def build_frost_shard():
    """A small ice shard, taller than wide, centred on its middle."""
    m = Mesh()
    top, bottom = (0.0, 0.14, 0.0), (0.0, -0.10, 0.0)
    ring = [(0.05, 0.0, 0.0), (0.0, 0.0, 0.05), (-0.05, 0.0, 0.0), (0.0, 0.0, -0.05)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, top, b, "ice_light")
        m.tri(b, bottom, a, "ice")
    return m


def build_rack():
    """The weapon rack, the class station: a wooden frame 2 m wide holding a bow on one side and a crusader shield with a flail on the other. Its front faces +Z."""
    m = Mesh()
    m.box(-1.0, 0.0, -0.12, -0.9, 1.8, 0.0, "wood")
    m.box(0.9, 0.0, -0.12, 1.0, 1.8, 0.0, "wood")
    m.box(-1.05, 1.75, -0.16, 1.05, 1.9, 0.04, "chest_dark")
    m.box(-1.0, 0.35, -0.10, 1.0, 0.45, -0.02, "wood")
    # The Ranger's bow and quiver, left
    m.box(-0.62, 0.45, 0.0, -0.58, 1.70, 0.05, "wood")
    m.box(-0.55, 0.52, 0.0, -0.54, 1.63, 0.01, "fletching")
    m.box(-0.40, 0.45, -0.02, -0.26, 1.05, 0.10, "leather")
    m.box(-0.38, 1.05, 0.0, -0.28, 1.16, 0.08, "fletching")
    # The Paladin's shield and flail, right
    m.box(0.18, 0.48, 0.0, 0.74, 1.62, 0.05, "gold")
    m.box(0.21, 0.52, 0.05, 0.71, 1.58, 0.07, "tabard")
    m.box(0.425, 0.60, 0.07, 0.505, 1.50, 0.085, "crusader")
    m.box(0.25, 1.12, 0.07, 0.67, 1.20, 0.085, "crusader")
    m.box(0.80, 0.9, 0.0, 0.85, 1.55, 0.05, "wood")
    m.box(0.74, 0.62, -0.03, 0.90, 0.78, 0.13, "iron")
    # The Mage's ice staff, in the middle
    m.box(-0.08, 0.40, 0.0, -0.03, 1.70, 0.05, "wood")
    m.pyramid(-0.12, 1.70, -0.03, 0.01, 1.70, 0.08, (-0.055, 1.95, 0.025), "ice")
    # The Shaman's totem, leaning by the left post, a crackling orb on top
    m.box(-0.84, 0.0, 0.02, -0.79, 1.55, 0.07, "wood")
    m.box(-0.86, 1.30, 0.0, -0.77, 1.38, 0.09, "fur")
    m.pyramid(-0.88, 1.55, -0.01, -0.75, 1.55, 0.10, (-0.815, 1.75, 0.045), "spark")
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


def build_crossbow_ghoul():
    m = Mesh()
    # Crooked legs and feet, as the ghoul's
    m.box(-0.20, 0.0, -0.06, -0.06, 0.10, 0.16, "ghoul_skin")
    m.box(0.06, 0.0, -0.06, 0.20, 0.10, 0.16, "ghoul_skin")
    m.box(-0.18, 0.10, -0.06, -0.07, 0.62, 0.06, "ghoul_skin")
    m.box(0.07, 0.10, -0.06, 0.18, 0.62, 0.06, "ghoul_skin")
    # Rags, a less hunched torso, and a hood and cloak
    m.box(-0.22, 0.52, -0.12, 0.22, 0.72, 0.12, "ghoul_rags")
    m.box(-0.23, 0.70, -0.10, 0.23, 1.18, 0.16, "ghoul_skin")
    m.box(-0.25, 0.60, -0.16, 0.25, 1.22, -0.10, "hood_dark")
    # A quiver of bolts on its back
    m.box(0.05, 0.80, -0.26, 0.17, 1.30, -0.16, "leather")
    m.box(0.07, 1.30, -0.24, 0.15, 1.38, -0.18, "bolt_glow")
    # Arms held out in front at chest height, hands where the crossbow sits
    m.box(-0.34, 0.98, -0.02, -0.22, 1.10, 0.40, "ghoul_skin")
    m.box(0.22, 0.98, -0.02, 0.34, 1.10, 0.40, "ghoul_skin")
    m.box(-0.30, 0.99, 0.40, -0.12, 1.09, 0.50, "bone")
    m.box(0.12, 0.99, 0.40, 0.30, 1.09, 0.50, "bone")
    # Hooded head, red eyes
    m.box(-0.13, 1.18, 0.02, 0.13, 1.44, 0.26, "ghoul_skin")
    m.box(-0.10, 1.31, 0.26, -0.03, 1.36, 0.265, "ghoul_eye")
    m.box(0.03, 1.31, 0.26, 0.10, 1.36, 0.265, "ghoul_eye")
    m.box(-0.16, 1.16, -0.06, 0.16, 1.50, 0.02, "hood_dark")
    m.box(-0.16, 1.42, -0.06, 0.16, 1.50, 0.24, "hood_dark")
    return m


def build_ghoul_crossbow():
    """The crossbow, in the ghoul's own space: held level at chest height in front of it, pointing +Z."""
    m = Mesh()
    m.box(-0.04, 1.00, 0.22, 0.04, 1.08, 0.92, "wood")              # stock
    m.box(-0.38, 1.03, 0.80, 0.38, 1.09, 0.86, "chest_dark")        # limbs
    m.box(-0.44, 1.02, 0.78, -0.36, 1.10, 0.88, "bolt_glow")        # glowing runes at the limb tips
    m.box(0.36, 1.02, 0.78, 0.44, 1.10, 0.88, "bolt_glow")
    m.box(-0.36, 1.055, 0.60, 0.36, 1.065, 0.62, "fletching")      # the drawn string
    m.box(-0.012, 1.08, 0.60, 0.012, 1.10, 0.96, "wood")           # the loaded bolt
    m.box(-0.03, 1.07, 0.96, 0.03, 1.11, 1.02, "bolt_glow")         # its glowing head
    return m


def build_ghoul_bolt():
    """A crossbow bolt pointing +Z, centred on its middle: a dark shaft, a glowing head, small vanes."""
    m = Mesh()
    m.box(-0.018, -0.018, -0.30, 0.018, 0.018, 0.24, "chest_dark")
    tip, left, right = (0.0, 0.0, 0.40), (-0.05, 0.0, 0.24), (0.05, 0.0, 0.24)
    for y in (0.02, -0.02):
        top = (0.0, y, 0.26)
        m.tri(left, top, tip, "bolt_glow")
        m.tri(top, right, tip, "bolt_glow")
        m.tri(left, right, top, "bolt_glow")
    m.box(-0.003, -0.05, -0.30, 0.003, 0.05, -0.18, "ghoul_rags")
    m.box(-0.05, -0.003, -0.30, 0.05, 0.003, -0.18, "ghoul_rags")
    return m


def build_ghoul_mage():
    m = Mesh()
    # A long robe to the ground, widening at the hem
    m.box(-0.26, 0.0, -0.18, 0.26, 0.40, 0.20, "ghoul_robe")
    m.box(-0.22, 0.40, -0.14, 0.22, 1.20, 0.16, "ghoul_robe")
    m.box(-0.23, 0.78, -0.15, 0.23, 0.84, 0.17, "bone")              # a cord of bones for a belt
    # Thin arms held out in front at chest height, hands cupped where the flame burns
    m.box(-0.32, 1.00, -0.02, -0.20, 1.10, 0.36, "ghoul_robe")
    m.box(0.20, 1.00, -0.02, 0.32, 1.10, 0.36, "ghoul_robe")
    m.box(-0.28, 0.98, 0.36, -0.14, 1.08, 0.50, "ghoul_skin")
    m.box(0.14, 0.98, 0.36, 0.28, 1.08, 0.50, "ghoul_skin")
    # Gaunt head, red eyes, and a tall pointed hood
    m.box(-0.12, 1.20, 0.00, 0.12, 1.46, 0.24, "ghoul_skin")
    m.box(-0.09, 1.33, 0.24, -0.03, 1.38, 0.245, "ghoul_eye")
    m.box(0.03, 1.33, 0.24, 0.09, 1.38, 0.245, "ghoul_eye")
    m.box(-0.16, 1.18, -0.08, 0.16, 1.52, 0.00, "ghoul_robe")
    m.box(-0.16, 1.44, -0.08, 0.16, 1.52, 0.22, "ghoul_robe")
    m.pyramid(-0.16, 1.52, -0.08, 0.16, 1.52, 0.22, (0.0, 1.85, -0.04), "ghoul_robe")
    return m


def _orb(m, cx, cy, cz, r, outer, inner):
    """A spiky orb: a double pyramid of <outer>, and a brighter one of <inner> turned 45 degrees whose points stick out through its faces."""
    import math
    for size, colour, turn in ((r, outer, 0.0), (r * 0.85, inner, math.pi / 4)):
        top, bottom = (cx, cy + size, cz), (cx, cy - size, cz)
        ring = [(cx + math.cos(turn + k * math.pi / 2) * size, cy, cz + math.sin(turn + k * math.pi / 2) * size) for k in range(4)]
        for i in range(4):
            a, b = ring[i], ring[(i + 1) % 4]
            m.tri(a, top, b, colour)
            m.tri(b, bottom, a, colour)


def build_ghoul_flame():
    """The flame in the Ghoul Mage's own space: a burning orb held between its hands, in front of it at chest height."""
    m = Mesh()
    _orb(m, 0.0, 1.10, 0.56, 0.14, "fire", "fire_light")
    return m


def build_ghoul_fireball():
    """A fireball, centred on its middle."""
    m = Mesh()
    _orb(m, 0.0, 0.0, 0.0, 0.26, "fire", "fire_light")
    return m


def build_fireball_mark():
    """Where a fireball will land: a burning ring of radius 1 with little flames standing on it."""
    m = build_ring(0.86, 1.0, "fire")
    import math
    for i in range(8):
        a = 2 * math.pi * i / 8
        x, z = math.cos(a) * 0.93, math.sin(a) * 0.93
        m.pyramid(x - 0.05, 0.0, z - 0.05, x + 0.05, 0.0, z + 0.05, (x, 0.16, z), "fire_light")
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


def build_crate():
    """A wooden crate 0.9 m on a side, sitting on the ground: planks, dark edges, and a brace across each face."""
    m = Mesh()
    m.box(-0.42, 0.0, -0.42, 0.42, 0.84, 0.42, "wood")
    for x in (-0.45, 0.39):                                            # dark edge posts at the corners
        for z in (-0.45, 0.39):
            m.box(x, 0.0, z, x + 0.06, 0.9, z + 0.06, "chest_dark")
    m.box(-0.45, 0.84, -0.45, 0.45, 0.9, 0.45, "chest_dark")           # the lid's rim
    m.box(-0.40, 0.40, 0.42, 0.40, 0.48, 0.44, "chest_dark")           # cross-braces on the four faces
    m.box(-0.40, 0.40, -0.44, 0.40, 0.48, -0.42, "chest_dark")
    m.box(0.42, 0.40, -0.40, 0.44, 0.48, 0.40, "chest_dark")
    m.box(-0.44, 0.40, -0.40, -0.42, 0.48, 0.40, "chest_dark")
    return m


def build_pickup_magnet():
    m = Mesh()
    m.box(-0.18, -0.20, -0.05, -0.08, 0.14, 0.05, "magnet_red")        # the two arms
    m.box(0.08, -0.20, -0.05, 0.18, 0.14, 0.05, "magnet_red")
    m.box(-0.18, 0.14, -0.05, 0.18, 0.24, 0.05, "magnet_red")          # and the bend
    m.box(-0.18, -0.26, -0.05, -0.08, -0.20, 0.05, "steel")            # steel tips
    m.box(0.08, -0.26, -0.05, 0.18, -0.20, 0.05, "steel")
    return m


def build_pickup_apple():
    m = Mesh()
    _orb(m, 0.0, 0.0, 0.0, 0.16, "apple_red", "apple_red")
    m.box(-0.012, 0.14, -0.012, 0.012, 0.22, 0.012, "wood")            # the stalk
    m.box(0.01, 0.17, -0.02, 0.10, 0.19, 0.03, "leaf")                 # a leaf
    return m


def build_pickup_roast():
    m = Mesh()
    m.box(-0.14, -0.12, -0.12, 0.10, 0.12, 0.12, "meat")               # the meat
    m.box(0.10, -0.03, -0.03, 0.26, 0.03, 0.03, "bone")                # the bone
    m.box(0.24, -0.06, -0.06, 0.30, 0.06, 0.06, "bone")                # its knob
    return m


def build_pickup_silver():
    m = Mesh()
    m.box(-0.14, -0.18, -0.12, 0.14, 0.06, 0.12, "canvas")             # the pouch
    m.box(-0.08, 0.06, -0.06, 0.08, 0.12, 0.06, "leather")             # tied at the neck
    for x, z in ((-0.20, 0.10), (0.18, 0.08), (0.02, 0.18)):           # coins spilling out
        m.box(x - 0.05, -0.18, z - 0.05, x + 0.05, -0.16, z + 0.05, "coin")
    return m


def build_pickup_bomb():
    m = Mesh()
    _orb(m, 0.0, 0.0, 0.0, 0.17, "bomb_black", "bomb_black")
    m.box(-0.03, 0.15, -0.03, 0.03, 0.20, 0.03, "iron")               # the cap
    m.box(-0.008, 0.20, -0.008, 0.008, 0.28, 0.008, "cork")            # the fuse
    m.box(-0.025, 0.28, -0.025, 0.025, 0.32, 0.025, "fire_light")      # lit
    return m


def build_pickup_frenzy():
    m = Mesh()
    m.box(-0.10, -0.20, -0.10, 0.10, 0.06, 0.10, "potion")             # the flask
    m.box(-0.04, 0.06, -0.04, 0.04, 0.14, 0.04, "potion")              # its neck
    m.box(-0.05, 0.14, -0.05, 0.05, 0.19, 0.05, "cork")                # the cork
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


# The palisade: CampWalls in Camp/Camp.cs lays these round the camp, so its segment and gate sizes must match these.
WALL_SEGMENT = 3.5   # one wall piece, along X
GATE_WIDTH = 7.0     # the gatehouse fills two pieces' worth of the wall


def _unit_noise(i, salt=0.0):
    """A steady pseudo-random number in 0..1 for i: the same every time the script runs."""
    import math
    v = math.sin(i * 12.9898 + salt * 78.233) * 43758.5453
    return v - math.floor(v)


def build_wall():
    """One piece of the palisade: sharpened logs standing side by side, WALL_SEGMENT long along X, about 3.2 m tall, bound by two rails on the camp side (+Z)."""
    m = Mesh()
    count = 11
    width = WALL_SEGMENT / count
    for i in range(count):
        x0 = -WALL_SEGMENT / 2 + i * width
        x1 = x0 + width
        top = 2.95 + 0.4 * _unit_noise(i)
        colour = "wood" if i % 3 else "chest_wood"
        m.box(x0, 0.0, -0.16, x1, top, 0.16, colour)
        m.pyramid(x0, top, -0.16, x1, top, 0.16, ((x0 + x1) / 2, top + 0.38, 0.0), colour)
    for y in (0.7, 2.3):
        m.box(-WALL_SEGMENT / 2, y, 0.16, WALL_SEGMENT / 2, y + 0.16, 0.27, "chest_dark")
    return m


def build_wall_post():
    """The thick post where two sides of the palisade meet: a round log 0.7 m across and 3.9 m tall with a pointed top and an iron band."""
    m = Mesh()
    m.prism(0.0, 0.0, 0.36, 0.0, 3.7, "chest_wood", sides=8)
    m.prism(0.0, 0.0, 0.38, 2.5, 2.65, "iron", sides=8)
    m.pyramid(-0.3, 3.7, -0.3, 0.3, 3.7, 0.3, (0.0, 4.25, 0.0), "chest_wood")
    return m


def build_gate():
    """The departure gate, set in the palisade: a gatehouse GATE_WIDTH wide with a tower on each side, closed double doors under a walkway, and a green banner
    over the doors on the camp side (+Z)."""
    m = Mesh()
    half = GATE_WIDTH / 2
    for side in (-1, 1):
        x0, x1 = sorted((side * half, side * (half - 1.4)))
        m.box(x0, 0.0, -0.7, x1, 4.3, 0.7, "chest_wood")                      # the tower
        for y in (1.2, 2.8):
            m.box(x0 - 0.02, y, -0.72, x1 + 0.02, y + 0.14, 0.72, "chest_dark")
        m.box(x0 - 0.15, 4.3, -0.85, x1 + 0.15, 4.45, 0.85, "chest_dark")    # its platform
        for cx in (x0 - 0.1, x1 + 0.1):
            for cz in (-0.8, 0.8):
                m.box(cx - 0.06, 4.45, cz - 0.06, cx + 0.06, 5.2, cz + 0.06, "wood")
        m.pyramid(x0 - 0.3, 5.2, -1.0, x1 + 0.3, 5.2, 1.0, ((x0 + x1) / 2, 6.3, 0.0), "chest_dark")   # its roof
        m.box(x0 + 0.35, 2.0, 0.7, x1 - 0.35, 2.5, 0.72, "dark")             # a slit window on the camp side
    inner = half - 1.4
    for side in (-1, 1):                                                      # the two door leaves, planked, with iron bands
        x0, x1 = sorted((side * 0.02, side * inner))
        m.box(x0, 0.0, -0.12, x1, 3.4, 0.12, "chest_wood")
        for y in (0.5, 2.7):
            m.box(x0, y, 0.12, x1, y + 0.14, 0.15, "iron")
        rx = side * 0.35
        m.box(rx - 0.05, 1.5, 0.15, rx + 0.05, 1.75, 0.2, "iron")            # the ring handles
    m.box(-0.04, 0.0, -0.08, 0.04, 3.4, 0.08, "dark")                        # the crack where they meet
    m.box(-inner, 3.4, -0.35, inner, 3.9, 0.35, "chest_dark")                # the beam over the doors
    m.box(-inner, 4.3, -0.85, inner, 4.45, 0.85, "chest_dark")               # the walkway between the towers
    for i in range(8):
        x = -inner + 0.25 + i * 0.6
        m.box(x, 4.45, 0.75, x + 0.08, 5.0, 0.85, "wood")                    # its railing
    m.box(-inner, 4.95, 0.75, inner, 5.05, 0.85, "wood")
    m.box(-0.7, 1.9, 0.2, 0.7, 3.4, 0.24, "banner")                          # the banner, and its crest
    m.tri((-0.7, 1.9, 0.24), (0.0, 1.55, 0.24), (0.7, 1.9, 0.24), "banner")
    m.box(-0.2, 2.45, 0.24, 0.2, 2.85, 0.26, "gold")
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


def build_barrel():
    """A barrel 0.6 m across and 0.9 m tall, with two iron hoops."""
    m = Mesh()
    m.prism(0.0, 0.0, 0.28, 0.0, 0.9, "chest_wood", top="chest_dark")
    m.prism(0.0, 0.0, 0.31, 0.08, 0.3, "chest_wood")
    m.prism(0.0, 0.0, 0.31, 0.6, 0.82, "chest_wood")
    for y in (0.14, 0.72):
        m.prism(0.0, 0.0, 0.32, y, y + 0.06, "iron")
    return m


def build_crates():
    """A stack of supply crates: two side by side and one on top, about 1.7 m wide and 1.5 m tall."""
    m = Mesh()
    for (cx, y, cz, size) in ((-0.43, 0.0, 0.0, 0.8), (0.42, 0.0, 0.08, 0.78), (0.0, 0.8, 0.02, 0.68)):
        h = size / 2
        m.box(cx - h, y, cz - h, cx + h, y + size, cz + h, "chest_wood")
        m.box(cx - h - 0.01, y + size * 0.42, cz - h - 0.01, cx + h + 0.01, y + size * 0.58, cz + h + 0.01, "chest_dark")
        for ex in (cx - h, cx + h - 0.07):
            m.box(ex - 0.005, y, cz - h - 0.01, ex + 0.075, y + size, cz + h + 0.01, "chest_dark")
    return m


def build_woodpile():
    """Split logs stacked four, three and two high, 1.8 m long, with a chopping stump and an axe beside them (+X)."""
    m = Mesh()
    r = 0.16
    for row, count in enumerate((4, 3, 2)):
        for i in range(count):
            z = (i - (count - 1) / 2) * 2 * r
            m.log(-0.9, 0.9, r + row * 2 * r * 0.87, z, r, "chest_wood", ends="canvas")
    m.prism(1.45, 0.0, 0.3, 0.0, 0.5, "chest_wood", top="canvas")
    m.box(1.42, 0.5, -0.03, 1.48, 1.15, 0.03, "wood")                  # the axe, its head bitten into the stump
    m.box(1.35, 0.48, -0.04, 1.55, 0.62, 0.04, "steel")
    return m


def build_bench():
    """A log bench by the fire: a log 2 m long along X on two stumps."""
    m = Mesh()
    for x in (-0.7, 0.7):
        m.prism(x, 0.0, 0.2, 0.0, 0.3, "chest_wood", sides=8, top="canvas")
    m.log(-1.0, 1.0, 0.42, 0.0, 0.17, "wood", ends="canvas")
    return m


def build_banner():
    """A banner on a pole, 4.4 m tall: a green cloth with a gold crest hanging from a crossbar, its face toward +Z."""
    m = Mesh()
    m.prism(0.0, 0.0, 0.06, 0.0, 4.2, "wood", sides=6)
    m.pyramid(-0.07, 4.2, -0.07, 0.07, 4.2, 0.07, (0.0, 4.45, 0.0), "gold")
    m.box(-0.6, 3.9, -0.04, 0.6, 3.98, 0.04, "wood")
    m.box(-0.52, 2.1, 0.04, 0.52, 3.9, 0.07, "banner")
    m.facing([(-0.52, 2.1, 0.07), (0.0, 1.7, 0.07), (0.52, 2.1, 0.07)], (0.0, 0.0, 1.0), "banner")
    m.facing([(-0.52, 2.1, 0.04), (0.0, 1.7, 0.04), (0.52, 2.1, 0.04)], (0.0, 0.0, -1.0), "banner")
    m.box(-0.18, 2.85, 0.07, 0.18, 3.25, 0.09, "gold")
    m.box(-0.52, 3.72, 0.07, 0.52, 3.8, 0.09, "gold")
    return m


def build_brazier():
    """An iron brazier on three legs, its bowl of glowing coals 1.15 m up (the engine's fire burns on it)."""
    import math
    m = Mesh()
    for i in range(3):
        a = 2 * math.pi * i / 3
        x, z = math.cos(a) * 0.3, math.sin(a) * 0.3
        m.box(x - 0.04, 0.0, z - 0.04, x + 0.04, 0.9, z + 0.04, "iron")
    m.prism(0.0, 0.0, 0.2, 0.8, 0.9, "iron", sides=8)
    m.prism(0.0, 0.0, 0.42, 0.9, 1.12, "iron", sides=10, bottom=True)
    m.prism(0.0, 0.0, 0.37, 1.12, 1.14, "fire", sides=10, top="fire")
    return m


def build_haybale():
    """A bale of straw, 1.1 m long, bound twice."""
    m = Mesh()
    m.box(-0.55, 0.0, -0.3, 0.55, 0.55, 0.3, "straw")
    for x in (-0.3, 0.26):
        m.box(x, 0.0, -0.31, x + 0.04, 0.56, 0.31, "leather")
    return m


def build_dummy():
    """A training dummy: a straw body and sack head on a post, arms of wood, a red mark on its chest (+Z)."""
    m = Mesh()
    m.box(-0.45, 0.0, -0.06, 0.45, 0.1, 0.06, "wood")                 # its feet
    m.box(-0.06, 0.0, -0.45, 0.06, 0.1, 0.45, "wood")
    m.box(-0.06, 0.0, -0.06, 0.06, 1.9, 0.06, "wood")
    m.box(-0.25, 0.8, -0.16, 0.25, 1.5, 0.16, "straw")
    m.box(-0.26, 1.0, -0.17, 0.26, 1.06, 0.17, "leather")
    m.box(-0.62, 1.32, -0.05, 0.62, 1.4, 0.05, "wood")
    m.box(-0.15, 1.52, -0.14, 0.15, 1.82, 0.14, "canvas")
    m.box(-0.1, 1.1, 0.16, 0.1, 1.3, 0.18, "target_red")
    return m


def build_well():
    """A stone well 1.9 m across with dark water in it, a little roof on two posts and a bucket on a rope."""
    import math
    m = Mesh()
    for i in range(14):
        a = 2 * math.pi * i / 14
        x, z = math.cos(a) * 0.82, math.sin(a) * 0.82
        m.box(x - 0.2, 0.0, z - 0.2, x + 0.2, 0.75, z + 0.2, "stone")
    m.prism(0.0, 0.0, 0.7, 0.0, 0.5, "stone", sides=12, top="robe_dark")
    for x in (-0.98, 0.98):
        m.box(x - 0.07, 0.0, -0.07, x + 0.07, 2.2, 0.07, "wood")
    m.log(-1.05, 1.05, 1.75, 0.0, 0.06, "wood")
    for side in (-1, 1):
        a, b = (-1.25, 2.1, side * 0.9), (1.25, 2.1, side * 0.9)
        c, d = (1.25, 2.65, 0.0), (-1.25, 2.65, 0.0)
        m.facing([a, b, c, d], (0.0, 0.9, side * 0.55), "chest_dark")
        m.facing([a, b, c, d], (0.0, -0.9, -side * 0.55), "chest_dark")   # and its underside
    m.box(-0.015, 1.3, -0.015, 0.015, 1.72, 0.015, "canvas")          # the rope, and its bucket
    m.prism(0.0, 0.0, 0.15, 1.05, 1.3, "chest_wood", sides=8, top="dark")
    return m


def build_cart():
    """A two-wheeled supply cart, 2.2 m long, its shafts forward (+Z), loaded with sacks and a barrel."""
    m = Mesh()
    m.box(-0.75, 0.55, -1.1, 0.75, 0.65, 1.1, "chest_wood")
    for x0, x1 in ((-0.8, -0.72), (0.72, 0.8)):
        m.box(x0, 0.65, -1.1, x1, 0.95, 1.1, "wood")
    m.box(-0.8, 0.65, -1.15, 0.8, 0.95, -1.08, "wood")
    for x in (-0.95, 0.83):
        m.log(x, x + 0.12, 0.45, 0.0, 0.45, "chest_dark", ends="wood", sides=12)
    m.log(-0.95, 0.95, 0.45, 0.0, 0.05, "iron")
    for x in (-0.55, 0.55):
        m.box(x - 0.05, 0.5, 1.1, x + 0.05, 0.6, 2.3, "wood")
    for (x0, z0, x1, z1) in ((-0.65, -0.95, -0.05, -0.35), (0.0, -0.9, 0.62, -0.3), (-0.6, -0.25, 0.0, 0.3)):
        m.box(x0, 0.65, z0, x1, 1.05, z1, "canvas")
        m.pyramid(x0, 1.05, z0, x1, 1.05, z1, ((x0 + x1) / 2, 1.2, (z0 + z1) / 2), "canvas")
    m.prism(0.35, 0.45, 0.24, 0.65, 1.3, "chest_wood", top="chest_dark")
    return m


def build_cookpot():
    """A cooking fire: a small ring of stones (the engine's fire burns in it) under a pot of stew hung from a crossbar on two forked posts."""
    import math
    m = Mesh()
    for i in range(8):
        a = 2 * math.pi * i / 8
        x, z = math.cos(a) * 0.38, math.sin(a) * 0.38
        m.box(x - 0.08, 0.0, z - 0.07, x + 0.08, 0.12, z + 0.07, "stone")
    for x in (-0.6, 0.6):
        m.box(x - 0.04, 0.0, -0.04, x + 0.04, 1.25, 0.04, "wood")
        m.box(x - 0.1, 1.2, -0.03, x + 0.1, 1.3, 0.03, "wood")
    m.log(-0.7, 0.7, 1.28, 0.0, 0.035, "wood", sides=6)
    m.box(-0.012, 0.85, -0.012, 0.012, 1.28, 0.012, "iron")
    m.prism(0.0, 0.0, 0.24, 0.5, 0.85, "iron", top="meat", sides=10)
    m.prism(0.0, 0.0, 0.18, 0.42, 0.5, "iron", sides=10, bottom=True)
    return m


def build_bedroll():
    """A bedroll laid on the ground, 1.8 m long along Z, rolled up at its head (-Z)."""
    m = Mesh()
    m.box(-0.35, 0.0, -0.8, 0.35, 0.06, 0.9, "canvas_dark")
    m.box(-0.33, 0.06, -0.3, 0.33, 0.1, 0.88, "banner")
    m.log(-0.36, 0.36, 0.14, -0.78, 0.14, "canvas", ends="canvas_dark")
    return m


def build_lantern():
    """A lantern post 2.3 m tall, the lantern hanging from an arm toward +Z."""
    m = Mesh()
    m.box(-0.06, 0.0, -0.06, 0.06, 2.35, 0.06, "wood")
    m.box(-0.03, 2.2, 0.0, 0.03, 2.28, 0.55, "wood")
    m.box(-0.01, 1.95, 0.44, 0.01, 2.2, 0.46, "iron")
    m.box(-0.12, 1.66, 0.33, 0.12, 1.95, 0.57, "iron")
    m.box(-0.1, 1.69, 0.35, 0.1, 1.92, 0.55, "holy_light")
    m.pyramid(-0.14, 1.95, 0.31, 0.14, 1.95, 0.59, (0.0, 2.05, 0.45), "iron")
    return m


def build_sacks():
    """Three grain sacks leaning together."""
    m = Mesh()
    for (x0, z0, x1, z1, h) in ((-0.55, -0.25, -0.05, 0.2, 0.62), (0.0, -0.3, 0.5, 0.15, 0.55), (-0.3, 0.2, 0.2, 0.62, 0.5)):
        m.box(x0, 0.0, z0, x1, h, z1, "canvas")
        m.pyramid(x0, h, z0, x1, h, z1, ((x0 + x1) / 2, h + 0.14, (z0 + z1) / 2), "canvas")
        m.box((x0 + x1) / 2 - 0.05, h + 0.08, (z0 + z1) / 2 - 0.05, (x0 + x1) / 2 + 0.05, h + 0.22, (z0 + z1) / 2 + 0.05, "leather")
    return m


# The quartermaster's skeleton: each joint's name, parent and where it sits in the bind pose (standing, arms hanging, facing +Z). A joint's bind frame is the
# world's axes, so a rotation in the animation turns it about its own point. The -X arm is the one on the hip, the +X arm the one that strokes the moustache.
QM_JOINTS = (
    ("root", None, (0.0, 0.0, 0.0)),
    ("hips", 0, (0.0, 0.95, 0.0)),
    ("chest", 1, (0.0, 1.05, 0.0)),
    ("head", 2, (0.0, 1.5, 0.0)),
    ("upper_arm_l", 2, (-0.33, 1.42, 0.0)),
    ("forearm_l", 4, (-0.34, 1.13, 0.0)),
    ("upper_arm_r", 2, (0.33, 1.42, 0.0)),
    ("forearm_r", 6, (0.34, 1.13, 0.0)),
)
QM_IDLE_SECONDS = 6.0   # must match QuartermasterNpc.IdleSeconds in Camp/Camp.cs


def build_quartermaster():
    """The quartermaster, who stands behind the stall: a stout old trader in a shirt, a leather waistcoat and an apron, a grey moustache and a brown felt hat.
    Skinned: every face moves with one joint of QM_JOINTS, for the Idle animation (quartermaster_idle)."""
    m = Mesh()
    m.joint = 0                                                        # legs and boots stay put
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.03, side * 0.21))
        m.box(x0 - 0.01, 0.0, -0.11, x1 + 0.01, 0.13, 0.17, "dark")
        m.box(x0, 0.13, -0.1, x1, 0.9, 0.1, "trousers")
    m.joint = 1                                                        # hips: belt, the apron's skirt
    m.box(-0.27, 0.85, -0.16, 0.27, 1.02, 0.2, "trousers")
    m.box(-0.28, 0.94, -0.17, 0.28, 1.02, 0.21, "dark")
    m.box(-0.05, 0.95, 0.21, 0.05, 1.01, 0.23, "gold")
    m.box(-0.24, 0.5, 0.21, 0.24, 0.94, 0.23, "canvas_dark")
    m.joint = 2                                                        # chest: shirt, belly, waistcoat, the apron's bib, neck
    m.box(-0.28, 1.02, -0.16, 0.28, 1.46, 0.19, "canvas")
    m.box(-0.25, 1.02, 0.19, 0.25, 1.3, 0.27, "canvas")
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.1, side * 0.29))
        m.box(x0, 1.02, 0.19, x1, 1.47, 0.28, "leather")
    m.box(-0.29, 1.02, -0.17, 0.29, 1.47, -0.12, "leather")
    m.box(-0.16, 1.02, 0.28, 0.16, 1.36, 0.3, "canvas_dark")
    for side in (-1, 1):
        m.box(side * 0.14 - 0.012, 1.36, 0.19, side * 0.14 + 0.012, 1.47, 0.3, "canvas_dark")
    m.box(-0.08, 1.46, -0.07, 0.08, 1.52, 0.07, "skin")
    m.joint = 3                                                        # head: face, moustache, hat
    m.box(-0.13, 1.51, -0.13, 0.13, 1.79, 0.13, "skin")
    m.box(-0.085, 1.66, 0.13, -0.035, 1.7, 0.135, "dark")
    m.box(0.035, 1.66, 0.13, 0.085, 1.7, 0.135, "dark")
    m.box(-0.1, 1.71, 0.13, -0.02, 1.73, 0.14, "beard")
    m.box(0.02, 1.71, 0.13, 0.1, 1.73, 0.14, "beard")
    m.box(-0.03, 1.6, 0.13, 0.03, 1.67, 0.18, "skin")
    m.box(-0.12, 1.57, 0.13, 0.12, 1.61, 0.17, "beard")
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.08, side * 0.13))
        m.box(x0, 1.51, 0.13, x1, 1.58, 0.16, "beard")
        x0, x1 = sorted((side * 0.13, side * 0.145))
        m.box(x0, 1.56, -0.12, x1, 1.72, 0.06, "beard")
    m.box(-0.13, 1.56, -0.145, 0.13, 1.74, -0.13, "beard")
    m.box(-0.2, 1.77, -0.2, 0.2, 1.8, 0.2, "chest_dark")
    m.box(-0.14, 1.8, -0.14, 0.14, 1.94, 0.14, "chest_dark")
    m.box(-0.145, 1.8, -0.145, 0.145, 1.84, 0.145, "crusader")
    for side, upper, fore in ((-1, 4, 5), (1, 6, 7)):
        x0, x1 = sorted((side * 0.28, side * 0.4))
        m.joint = upper                                                # sleeve
        m.box(x0, 1.12, -0.07, x1, 1.47, 0.07, "canvas")
        m.joint = fore                                                 # rolled cuff, bare forearm, hand
        x0, x1 = sorted((side * 0.29, side * 0.39))
        m.box(x0 - 0.005, 1.06, -0.065, x1 + 0.005, 1.14, 0.065, "canvas")
        m.box(x0, 0.88, -0.055, x1, 1.06, 0.055, "skin")
        m.box(x0 - 0.005, 0.77, -0.06, x1 + 0.005, 0.88, 0.06, "skin")
    return m


def _quat(axis, degrees):
    import math
    half = math.radians(degrees) / 2
    x, y, z = _normalize(axis)
    return (x * math.sin(half), y * math.sin(half), z * math.sin(half), math.cos(half))


def _qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def _euler(x=0.0, y=0.0, z=0.0):
    """A turn of x degrees about X, then z about Z, then y about Y (each about the joint's own point)."""
    return _qmul(_qmul(_quat((0, 1, 0), y), _quat((0, 0, 1), z)), _quat((1, 0, 0), x))


def _smooth(t, t0, t1):
    """0 before t0, 1 after t1, eased in between."""
    if t <= t0:
        return 0.0
    if t >= t1:
        return 1.0
    u = (t - t0) / (t1 - t0)
    return u * u * (3 - 2 * u)


def quartermaster_idle(t):
    """The quartermaster's pose t seconds into his idle: he breathes, shifts his weight, looks one way along the camp and then the other, strokes his moustache
    and nods, one hand on his hip the whole time. Every curve is back where it started at QM_IDLE_SECONDS, so the clip loops.
    Returns {joint name: (rotation quaternion, translation or None)}."""
    import math
    breath = math.sin(2 * math.pi * t / (QM_IDLE_SECONDS / 2))
    sway = math.sin(2 * math.pi * t / QM_IDLE_SECONDS)
    look = _smooth(t, 0.5, 1.3) - 1.75 * _smooth(t, 2.3, 3.2) + 0.75 * _smooth(t, 4.9, 5.7)   # 0 -> 1 -> -0.75 -> 0
    stroke = _smooth(t, 3.3, 3.9) - _smooth(t, 4.7, 5.3)
    rub = math.sin(2 * math.pi * (t - 3.3) * 1.6) * stroke
    nod = math.sin(math.pi * _smooth(t, 5.3, 5.9))
    return {
        "hips": (_euler(y=3.0 * sway), (0.0, 0.95 + 0.006 * breath, 0.0)),
        "chest": (_euler(x=-1.8 * breath, y=-2.0 * sway), None),
        "head": (_euler(x=2.0 * breath + 9.0 * nod - 6.0 * stroke, y=30.0 * look, z=-3.0 * look), None),
        "upper_arm_l": (_euler(x=6.0, z=-38.0 + 1.5 * breath), None),
        "forearm_l": (_euler(z=100.0), None),
        "upper_arm_r": (_euler(x=-2.0 * sway - 63.0 * stroke, y=-20.0 * stroke, z=4.0 - 4.0 * stroke), None),   # the hand up to the moustache
        "forearm_r": (_euler(x=-4.0 - 101.0 * stroke + 7.0 * rub, y=-40.0 * stroke), None),
    }


def build_hollow_king_unbound():
    """The Delve boss: the Hollow King grown to about 5.6 m and unbound: the king's body scaled up, with great horns, bone pauldrons, a burning-orange crown
    and eyes, and chains hanging broken from its wrists."""
    king = build_hollow_king()
    m = Mesh()
    s = 1.32
    for i in range(0, len(king.indices), 3):
        a, b, c = (king.positions[king.indices[i + k]] for k in range(3))
        colour = COLOURS[int(king.uvs[king.indices[i]][0] * len(COLOURS))]
        if colour == "ghost_eye":
            colour = "fire_light"
        elif colour == "gold":
            colour = "fire"
        m.tri(tuple(v * s for v in a), tuple(v * s for v in b), tuple(v * s for v in c), colour)
    for side in (-1, 1):
        x0, x1 = sorted((side * 0.3, side * 0.55))
        m.pyramid(x0 * s, 3.9 * s, -0.1 * s, x1 * s, 3.9 * s, 0.2 * s, (side * 1.05 * s, 4.75 * s, -0.05 * s), "horn")   # the horns
        px0, px1 = sorted((side * 0.75, side * 1.35))
        m.box(px0 * s, 3.0 * s, -0.42 * s, px1 * s, 3.4 * s, 0.46 * s, "bone")                                          # the pauldrons
        m.pyramid(px0 * s, 3.4 * s, -0.3 * s, px1 * s, 3.4 * s, 0.34 * s, (side * 1.05 * s, 3.8 * s, 0.02 * s), "bone")
        cx = side * 1.1 * s
        for k in range(4):                                                                                                # the broken chains
            y = (0.62 - 0.14 * k) * s
            m.box(cx - 0.05 * s, y - 0.09 * s, 0.1 * s, cx + 0.05 * s, y, 0.2 * s, "iron")
    return m


def build_delve_cache():
    """The Delve cache the boss leaves: a big black iron-bound chest, 1.8 m wide, with a burning-orange seal and trim, on a stone slab."""
    m = Mesh()
    m.box(-1.1, 0.0, -0.75, 1.1, 0.15, 0.75, "stone")
    m.box(-0.9, 0.15, -0.6, 0.9, 0.95, 0.6, "bomb_black")
    m.box(-0.93, 0.95, -0.62, 0.93, 1.3, 0.62, "iron")
    for x in (-0.75, 0.0, 0.7):
        m.box(x - 0.06, 0.14, -0.63, x + 0.06, 1.31, 0.63, "legendary")
    m.box(-0.95, 0.9, -0.64, 0.95, 0.97, 0.64, "legendary")
    m.box(-0.18, 0.62, 0.6, 0.18, 1.02, 0.66, "legendary_light")      # the seal, on the front
    m.box(-0.08, 0.72, 0.66, 0.08, 0.92, 0.69, "fire")
    for x in (-1.0, 1.0):
        for z in (-0.68, 0.68):
            m.pyramid(x - 0.08, 0.15, z - 0.08, x + 0.08, 0.15, z + 0.08, (x, 0.45, z), "legendary")
    return m


def build_armour_stand():
    """The gear station: a wooden armour stand 1.9 m tall wearing a mail shirt with an orange tabard, a helm on top, a sword and a round shield leaning on it (+Z)."""
    m = Mesh()
    m.box(-0.45, 0.0, -0.3, 0.45, 0.1, 0.3, "chest_wood")                # the base
    m.box(-0.05, 0.1, -0.05, 0.05, 1.2, 0.05, "wood")                    # the post
    m.box(-0.28, 1.1, -0.16, 0.28, 1.62, 0.16, "steel")                  # the mail shirt
    m.box(-0.2, 0.75, 0.16, 0.2, 1.55, 0.19, "legendary")                # its tabard
    m.box(-0.07, 1.2, 0.19, 0.07, 1.38, 0.2, "legendary_light")
    m.box(-0.42, 1.48, -0.1, 0.42, 1.64, 0.1, "plate")                   # the shoulders
    m.box(-0.14, 1.64, -0.14, 0.14, 1.9, 0.14, "plate")                  # the helm
    m.box(-0.1, 1.72, 0.14, 0.1, 1.76, 0.15, "visor")
    m.pyramid(-0.15, 1.9, -0.15, 0.15, 1.9, 0.15, (0.0, 2.02, 0.0), "plate")
    m.box(0.5, 0.0, 0.05, 0.54, 0.95, 0.09, "steel")                     # a sword leaning at its side
    m.box(0.42, 0.95, 0.03, 0.62, 0.99, 0.11, "gold")
    m.box(0.49, 0.99, 0.05, 0.55, 1.18, 0.09, "leather")
    m.prism(-0.62, 0.12, 0.34, 0.0, 0.06, "chest_wood", sides=10, top="chest_wood")   # a round shield lying at its foot
    m.prism(-0.62, 0.12, 0.1, 0.06, 0.09, "legendary", sides=8, top="legendary")
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


def write_skinned_glb(mesh, path, joints, clips):
    """Writes a skinned model: <mesh>'s faces each bound to one joint (mesh.joints), the skeleton <joints> ((name, parent index or None, bind position) each,
    parents first), and <clips> ({name: (seconds, pose function)}: the function gives {joint name: (rotation, translation or None)} at a time), sampled 30 times
    a second. The engine bakes every clip at load time and the game picks one and a time each frame."""
    count = len(mesh.positions)
    image = png_bytes()
    views, blob, accessors = [], b"", []

    def add(data, target=None):
        nonlocal blob
        view = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}
        if target:
            view["target"] = target
        views.append(view)
        blob += pad4(data)
        return len(views) - 1

    def accessor(data, component, kind, n, target=None, bounds=None):
        entry = {"bufferView": add(data, target), "componentType": component, "count": n, "type": kind}
        if bounds:
            entry["min"], entry["max"] = bounds
        accessors.append(entry)
        return len(accessors) - 1

    mins = [min(p[i] for p in mesh.positions) for i in range(3)]
    maxs = [max(p[i] for p in mesh.positions) for i in range(3)]
    position = accessor(b"".join(struct.pack("<3f", *p) for p in mesh.positions), 5126, "VEC3", count, 34962, (mins, maxs))
    normal = accessor(b"".join(struct.pack("<3f", *n) for n in mesh.normals), 5126, "VEC3", count, 34962)
    uv = accessor(b"".join(struct.pack("<2f", *t) for t in mesh.uvs), 5126, "VEC2", count, 34962)
    joint = accessor(b"".join(struct.pack("<4H", j, 0, 0, 0) for j in mesh.joints), 5123, "VEC4", count, 34962)
    weight = accessor(b"".join(struct.pack("<4f", 1.0, 0.0, 0.0, 0.0) for _ in mesh.joints), 5126, "VEC4", count, 34962)
    index = accessor(b"".join(struct.pack("<I", i) for i in mesh.indices), 5125, "SCALAR", len(mesh.indices), 34963)
    image_view = add(image)

    # The inverse bind matrices: the bind pose has no turns, so each is just a step back from the joint's place (column-major).
    ibm = b"".join(struct.pack("<16f", 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -x, -y, -z, 1) for (_, _, (x, y, z)) in joints)
    inverse_binds = accessor(ibm, 5126, "MAT4", len(joints))

    nodes = [{"name": path.stem, "mesh": 0, "skin": 0}]
    for i, (name, parent, (x, y, z)) in enumerate(joints):
        px, py, pz = joints[parent][2] if parent is not None else (0.0, 0.0, 0.0)
        node = {"name": name, "translation": [x - px, y - py, z - pz]}
        children = [1 + c for c, (_, up, _) in enumerate(joints) if up == i]
        if children:
            node["children"] = children
        nodes.append(node)

    animations = []
    names = [name for (name, _, _) in joints]
    for clip, (seconds, pose) in clips.items():
        steps = int(round(seconds * 30))
        times = [seconds * k / steps for k in range(steps + 1)]
        frames = [pose(t) for t in times]
        time_accessor = accessor(b"".join(struct.pack("<f", t) for t in times), 5126, "SCALAR", len(times), None, ([0.0], [seconds]))
        samplers, channels = [], []
        for name in frames[0]:
            rotations = b"".join(struct.pack("<4f", *f[name][0]) for f in frames)
            samplers.append({"input": time_accessor, "output": accessor(rotations, 5126, "VEC4", len(times)), "interpolation": "LINEAR"})
            channels.append({"sampler": len(samplers) - 1, "target": {"node": 1 + names.index(name), "path": "rotation"}})
            if frames[0][name][1] is not None:
                moves = b"".join(struct.pack("<3f", *f[name][1]) for f in frames)
                samplers.append({"input": time_accessor, "output": accessor(moves, 5126, "VEC3", len(times)), "interpolation": "LINEAR"})
                channels.append({"sampler": len(samplers) - 1, "target": {"node": 1 + names.index(name), "path": "translation"}})
        animations.append({"name": clip, "samplers": samplers, "channels": channels})

    gltf = {
        "asset": {"version": "2.0", "generator": "Arena Master make_placeholder_models.py"},
        "scene": 0,
        "scenes": [{"nodes": [0, 1]}],
        "nodes": nodes,
        "skins": [{"joints": list(range(1, 1 + len(joints))), "inverseBindMatrices": inverse_binds, "skeleton": 1}],
        "meshes": [{"primitives": [{"attributes": {"POSITION": position, "NORMAL": normal, "TEXCOORD_0": uv, "JOINTS_0": joint, "WEIGHTS_0": weight},
                                    "indices": index, "material": 0}]}],
        "materials": [{"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0, "roughnessFactor": 1.0}}],
        "textures": [{"source": 0, "sampler": 0}],
        "samplers": [{"magFilter": 9728, "minFilter": 9728}],
        "images": [{"bufferView": image_view, "mimeType": "image/png"}],
        "accessors": accessors,
        "animations": animations,
        "bufferViews": views,
        "buffers": [{"byteLength": len(blob)}],
    }

    json_chunk = pad4(json.dumps(gltf, separators=(",", ":")).encode(), b" ")
    body = struct.pack("<II", len(json_chunk), 0x4E4F534A) + json_chunk + struct.pack("<II", len(blob), 0x004E4942) + blob
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body)


if __name__ == "__main__":
    for name, build in (("ranger_placeholder.glb", build_ranger), ("paladin_placeholder.glb", build_paladin),
                        ("holy_nova.glb", lambda: build_ring(0.88, 1.0, "holy")), ("holy_circle.glb", build_holy_circle),
                        ("mage_placeholder.glb", build_mage), ("frost_bolt.glb", build_frost_bolt),
                        ("shaman_placeholder.glb", build_shaman), ("lightning_ball.glb", build_lightning_ball),
                        ("lightning_arc.glb", build_lightning_arc), ("lightning_zap.glb", lambda: build_ring(0.85, 1.0, "spark")),
                        ("frost_blast.glb", lambda: build_ring(0.86, 1.0, "ice_light")), ("frost_shard.glb", build_frost_shard),
                        ("aegis_burst.glb", lambda: build_ring(0.9, 1.0, "holy_light")), ("thunderstone_bolt.glb", build_lightning_arc),
                        ("arrow_placeholder.glb", build_arrow), ("ghoul_placeholder.glb", build_ghoul),
                        ("crossbow_ghoul_placeholder.glb", build_crossbow_ghoul), ("ghoul_crossbow.glb", build_ghoul_crossbow),
                        ("ghoul_bolt.glb", build_ghoul_bolt),
                        ("ghoul_mage_placeholder.glb", build_ghoul_mage), ("ghoul_flame.glb", build_ghoul_flame),
                        ("ghoul_fireball.glb", build_ghoul_fireball), ("fireball_mark.glb", build_fireball_mark),
                        ("fireball_burst.glb", lambda: build_ring(0.75, 1.0, "fire")), ("xp_gem_placeholder.glb", build_xp_gem),
                        ("brute_placeholder.glb", build_brute), ("hollow_king_placeholder.glb", build_hollow_king),
                        ("telegraph_ring.glb", lambda: build_ring(0.9, 1.0, "warn")), ("telegraph_disc.glb", lambda: build_disc("warn_dark")),
                        ("telegraph_lane.glb", build_lane), ("shockwave_ring.glb", lambda: build_ring(0.965, 1.035, "shock")),
                        ("chest_placeholder.glb", build_chest), ("loot_beam.glb", build_beam), ("crate_placeholder.glb", build_crate),
                        ("pickup_magnet.glb", build_pickup_magnet), ("pickup_apple.glb", build_pickup_apple), ("pickup_roast.glb", build_pickup_roast),
                        ("pickup_silver.glb", build_pickup_silver), ("pickup_bomb.glb", build_pickup_bomb), ("pickup_frenzy.glb", build_pickup_frenzy),
                        ("bomb_blast.glb", lambda: build_ring(0.8, 1.0, "fire")),
                        ("item_common.glb", lambda: build_item_orb("common")), ("item_rare.glb", lambda: build_item_orb("rare")),
                        ("item_epic.glb", lambda: build_item_orb("epic")), ("item_legendary.glb", lambda: build_item_orb("legendary")),
                        ("camp_tent.glb", build_tent), ("camp_firepit.glb", build_firepit), ("camp_stash.glb", build_stash),
                        ("camp_target.glb", build_target), ("camp_gate.glb", build_gate),
                        ("camp_board.glb", build_board), ("camp_stall.glb", build_stall), ("camp_rack.glb", build_rack),
                        ("camp_wall.glb", build_wall), ("camp_wall_post.glb", build_wall_post), ("camp_barrel.glb", build_barrel),
                        ("camp_crates.glb", build_crates), ("camp_woodpile.glb", build_woodpile), ("camp_bench.glb", build_bench),
                        ("camp_banner.glb", build_banner), ("camp_brazier.glb", build_brazier), ("camp_haybale.glb", build_haybale),
                        ("camp_dummy.glb", build_dummy), ("camp_well.glb", build_well), ("camp_cart.glb", build_cart),
                        ("camp_cookpot.glb", build_cookpot), ("camp_bedroll.glb", build_bedroll), ("camp_lantern.glb", build_lantern),
                        ("camp_sacks.glb", build_sacks), ("camp_armorstand.glb", build_armour_stand),
                        ("delve_cache.glb", build_delve_cache), ("fire_nova.glb", lambda: build_ring(0.82, 1.0, "fire_light")), ("hollow_king_unbound.glb", build_hollow_king_unbound)):
        mesh = build()
        write_glb(mesh, MODELS / name)
        print(f"Wrote {MODELS / name} ({len(mesh.positions)} vertices, {len(mesh.indices) // 3} triangles)")

    mesh = build_quartermaster()
    write_skinned_glb(mesh, MODELS / "camp_quartermaster.glb", QM_JOINTS, {"Idle": (QM_IDLE_SECONDS, quartermaster_idle)})
    print(f"Wrote {MODELS / 'camp_quartermaster.glb'} ({len(mesh.positions)} vertices, {len(QM_JOINTS)} joints, skinned)")
