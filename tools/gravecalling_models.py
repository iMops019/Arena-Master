"""The Priest's Grave Calling tree: its raised servants, souls and bone spears. Written by make_placeholder_models.py (write_all).

  grave_servant.glb     a raised servant: a skeleton on the ghoul's rig (tools/enemy_models.py's GHOUL_RIG), bare bones, a ragged loincloth, a rusted
                        pauldron, a skull with pale blue soul light in its eyes. Skinned, with Idle (3 s, looping), Walk (one stride, as many seconds as metres),
                        Claw (1 s: both arms raised through the first 0.4, raked down to 0.7 - the blow lands at the middle - and back) and Die (1 s, falling apart).
                        The Bone Colossus is the same model drawn three times the size.
  grave_soul.glb        a soul left by a kill: a small orb of pale blue soul light in a halo, about 1 across, drawn small and glowing (EngineWindow.SetCrowdGlow);
                        the spirits of Vengeful Spirits and the flash of a gathered soul are drawn with it too
  bone_spear.glb        a bone spear: a long knobbled bone shaft about 2.3 m long along +Z, a sharp bone head in front, bands of soul light round it
  bone_cage.glb         a cage of bones over a held enemy: eight ribs rising from a ring of radius 0.55 on the ground and bending in over a ghoul's height, lashed at the top
  lich_aura.glb         the Lich's aura round the Priest: a ring of radius 1 of ghostly soul flames licking up, and a thin ring of light on the ground (drawn glowing)
  corpse_burst.glb      a servant bursting: a ring of radius 1 of bone shards flung outward, over a ring of soul light
  grave_rise.glb        where a servant climbs out: a ring of churned earth clods of radius about 0.8, soul light glowing in the broken ground inside it

Uses only colours already in make_placeholder_models.PALETTE: adding one changes every model's texture.
"""

import math

import hero_rig as r
from hero_models import face, skull
from enemy_models import EnemyModel, GHOUL_RIG, GHOUL_GAIT, IDLE_SECONDS, DIE_SECONDS, hunch, keyed, collapse
from make_placeholder_models import Mesh, write_glb, write_skinned_glb, _unit_noise

SERVANT_RIG = GHOUL_RIG
SERVANT_GAIT = GHOUL_GAIT
CLAW_SECONDS = 1.0


# =========================================================================================================================================================
# The raised servant: a skeleton on the ghoul's rig

def _bone(m, joint, a, b, half, colour="bone"):
    """A bone from <a> to <b>, <half> thick each way, on <joint>."""
    m.beam(a, b, half, colour, joint, joint)


def _knob(m, joint, x, y, z, s, colour="bone"):
    """A knob of bone (a joint, a vertebra) <s> each way about (x, y, z), on <joint>."""
    m.joint = joint
    m.box(x - s, y - s, z - s, x + s, y + s, z + s, colour)


def build_servant(rig):
    m = Mesh()
    j = rig.index
    lx, knee, ankle, thigh_y = rig.leg_x, 0.35, 0.08, rig.thigh_y
    for side, sx in (("l", -1.0), ("r", 1.0)):
        x = sx * lx
        m.joint = j[f"foot_{side}"]
        m.box(x - 0.045, 0.0, -0.05, x + 0.045, 0.05, 0.15, "bone")                      # the foot's bones
        for k in range(3):
            tx = x + (k - 1) * 0.03
            m.box(tx - 0.01, 0.0, 0.15, tx + 0.01, 0.03, 0.2, "bone")                     # toes
        _bone(m, j[f"shin_{side}"], (x, ankle + 0.03, 0.0), (x, knee - 0.02, 0.0), 0.028)
        _bone(m, j[f"shin_{side}"], (x + sx * 0.035, ankle + 0.05, -0.01), (x + sx * 0.035, knee - 0.05, -0.01), 0.014)
        _knob(m, j[f"shin_{side}"], x, knee, 0.0, 0.04)                                    # the knee
        _bone(m, j[f"thigh_{side}"], (x, knee + 0.03, 0.0), (x, thigh_y - 0.02, 0.0), 0.032)
    # The pelvis and a ragged loincloth
    m.joint = j["hips"]
    m.box(-0.17, 0.56, -0.08, 0.17, 0.66, 0.08, "bone")
    m.box(-0.19, 0.46, 0.07, 0.19, 0.62, 0.1, "ghoul_rags")
    m.box(-0.19, 0.5, -0.1, 0.19, 0.62, -0.07, "ghoul_rags")
    for k in range(4):
        x = -0.15 + k * 0.1
        m.box(x - 0.03, 0.4 + 0.03 * (k % 2), 0.075, x + 0.03, 0.47, 0.095, "ghoul_rags")  # its torn hem
    # The spine: vertebrae up from the pelvis to the skull
    for y in (0.68, 0.73, 0.78):
        _knob(m, j["spine"], 0.0, y, -0.04, 0.03)
    for y in (0.83, 0.9, 0.97, 1.04, 1.11, 1.18):
        _knob(m, j["chest"], 0.0, y, -0.07, 0.028)
    # The ribcage: ribs curving round from the spine to the breastbone
    for k, y in enumerate((0.88, 0.95, 1.02, 1.09)):
        w = 0.15 + 0.02 * min(k, 2)
        for sx in (-1.0, 1.0):
            pts = [(0.0, y + 0.02, -0.07), (sx * w * 0.8, y + 0.01, -0.05), (sx * w, y - 0.01, 0.02), (sx * w * 0.75, y - 0.03, 0.09), (sx * 0.03, y - 0.05, 0.11)]
            for a, b in zip(pts, pts[1:]):
                _bone(m, j["chest"], a, b, 0.014)
    _bone(m, j["chest"], (0.0, 0.84, 0.11), (0.0, 1.1, 0.11), 0.02)                        # the breastbone
    for sx in (-1.0, 1.0):
        _bone(m, j["chest"], (sx * 0.02, 1.15, 0.06), (sx * 0.25, 1.15, 0.0), 0.02)       # the collarbones
    # A rusted pauldron on the left shoulder
    m.joint = j["upper_arm_l"]
    m.box(-0.34, 1.1, -0.1, -0.19, 1.2, 0.1, "iron")
    m.box(-0.36, 1.06, -0.11, -0.3, 1.12, 0.11, "iron")
    # The arms: one bone above the elbow, two below, bony claws
    for side, sx in (("l", -1.0), ("r", 1.0)):
        x = sx * 0.27
        _bone(m, j[f"upper_arm_{side}"], (x, 1.1, 0.0), (x, 0.89, 0.0), 0.026)
        _knob(m, j[f"forearm_{side}"], x, 0.86, 0.0, 0.032)
        m.joint = j[f"forearm_{side}"]
        _bone(m, j[f"forearm_{side}"], (x - 0.015, 0.83, 0.0), (x - 0.015, 0.61, 0.0), 0.014)
        _bone(m, j[f"forearm_{side}"], (x + 0.015, 0.83, 0.0), (x + 0.015, 0.61, 0.0), 0.014)
        m.joint = j[f"hand_{side}"]
        m.box(x - 0.04, 0.53, -0.02, x + 0.04, 0.59, 0.03, "bone")
        for k in range(3):
            fx = x + (k - 1) * 0.028
            m.pyramid(fx - 0.01, 0.53, -0.005, fx + 0.01, 0.53, 0.015, (fx, 0.42, 0.04), "bone")   # the claws
    # The skull, soul light in its eyes
    _knob(m, j["head"], 0.0, 1.22, -0.03, 0.03)
    skull(m, 0.0, 1.33, 0.02, 0.24, eyes="soul_light")
    return m


def servant_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.8)
    sway = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.5 * sway, width=0.16, crouch=0.04)
    hunch(pose, 12.0 + 2.0 * breath, head=-4.0 + 6.0 * math.sin(2 * math.pi * t / 1.5), head_turn=22.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 0.5))
    for side, sx, off in (("l", -1.0, 0.0), ("r", 1.0, 1.7)):
        twitch = math.sin(2 * math.pi * t / 1.3 + off)
        r.aim_arm(pose, side, (sx * 0.22, -0.9, 0.25 + 0.05 * twitch), bend=20.0, elbow_pole=(sx * 0.4, 0.0, -1.0))
        face(pose, f"hand_{side}", (0.0, 0.0, 1.0), tilt=20.0 + 10.0 * twitch)


def servant_walk(pose, u):
    _, swing = r.run_legs(pose, SERVANT_GAIT, u, 0.0)
    bob = math.cos(4 * math.pi * u)
    hunch(pose, 16.0 + 2.0 * bob, head=-10.0, turn=-8.0 * swing)
    # A shambling march, the claws half raised
    for side, sx, sign in (("l", -1.0, -1.0), ("r", 1.0, 1.0)):
        reach = sign * swing
        r.aim_arm(pose, side, (sx * 0.18, -0.6 + 0.15 * reach, 0.55 + 0.25 * reach), bend=30.0, elbow_pole=(sx * 0.4, -1.0, 0.0))
        face(pose, f"hand_{side}", (0.0, 0.0, 1.0), tilt=50.0 + 20.0 * reach)


def servant_claw(pose, u):
    """Both claws thrown up over the skull through the wind-up, raked down and across in front through the blow, and back."""
    r.stand_legs(pose, 0.0, 0.0, width=0.19, crouch=keyed(u, 0.04, 0.0, 0.12, settle=0.08))
    hunch(pose, keyed(u, 14.0, -8.0, 36.0, settle=22.0), head=keyed(u, -6.0, -24.0, 12.0))
    for side, sx in (("l", -1.0), ("r", 1.0)):
        direction = keyed(u, (sx * 0.2, -0.6, 0.7), (sx * 0.35, 0.95, 0.15), (-sx * 0.15, -0.8, 0.6), settle=(sx * 0.1, -0.7, 0.7))
        r.aim_arm(pose, side, direction, bend=keyed(u, 25.0, 45.0, 10.0), elbow_pole=(sx, 0.0, -1.0))
        face(pose, f"hand_{side}", (0.0, 0.0, 1.0), tilt=keyed(u, 40.0, -40.0, 110.0))


def servant_die(pose, u):
    collapse(pose, u, 14.0, 0.5)


SERVANT = EnemyModel("grave_servant.glb", SERVANT_RIG, SERVANT_GAIT, build_servant,
                     {"Idle": (IDLE_SECONDS, servant_idle), "Walk": (SERVANT_GAIT.cycle, servant_walk), "Claw": (CLAW_SECONDS, servant_claw),
                      "Die": (DIE_SECONDS, servant_die)})


# =========================================================================================================================================================
# The rest: rigid models, drawn as crowds

def build_soul():
    """A soul: a bright core of pale soul light in a larger blue halo, radius about 0.5 (the game draws it smaller), a wisp of a flame on top."""
    m = Mesh()
    for size, colour, turn in ((0.5, "soul", 0.0), (0.3, "soul_light", math.pi / 6)):
        top, bottom = (0.0, size, 0.0), (0.0, -size, 0.0)
        ring = [(math.cos(turn + k * math.pi / 3) * size, 0.0, math.sin(turn + k * math.pi / 3) * size) for k in range(6)]
        for i in range(6):
            a, b = ring[i], ring[(i + 1) % 6]
            m.facing([a, b, top], ((a[0] + b[0]) / 2, size / 3, (a[2] + b[2]) / 2), colour)
            m.facing([b, a, bottom], ((a[0] + b[0]) / 2, -size / 3, (a[2] + b[2]) / 2), colour)
    m.pyramid(-0.12, 0.35, -0.12, 0.12, 0.35, 0.12, (0.04, 0.95, -0.03), "soul_light")    # the wisp licking up from it
    return m


def build_bone_spear():
    """A bone spear along +Z, centred on its middle: a knobbled shaft from -1.05 to 0.85, a sharp head to 1.3, soul light bound round it in bands."""
    m = Mesh()
    m.beam((0.0, 0.0, -1.05), (0.0, 0.0, 0.85), 0.045, "bone", 0, 0)
    for z in (-0.95, -0.45, 0.05, 0.55):
        m.box(-0.07, -0.07, z - 0.05, 0.07, 0.07, z + 0.05, "bone")                          # the knuckles along it
    for z in (-0.7, -0.2, 0.3):
        m.box(-0.06, -0.06, z - 0.025, 0.06, 0.06, z + 0.025, "soul")                        # bands of soul light
    # The head: a long four-sided point with barbs back from it
    base = [(0.1, 0.0, 0.85), (0.0, 0.1, 0.85), (-0.1, 0.0, 0.85), (0.0, -0.1, 0.85)]
    tip = (0.0, 0.0, 1.3)
    for i in range(4):
        a, b = base[i], base[(i + 1) % 4]
        m.facing([a, b, tip], ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0.3), "bone")
        m.facing([b, a, (0.0, 0.0, 0.75)], ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, -0.3), "bone")
    for sx in (-1.0, 1.0):
        m.facing([(sx * 0.05, 0.0, 0.88), (sx * 0.05, 0.02, 0.98), (sx * 0.2, 0.0, 0.8)], (0.0, 1.0, 0.0), "bone")        # barbs
        m.facing([(sx * 0.05, 0.0, 0.88), (sx * 0.2, 0.0, 0.8), (sx * 0.05, 0.02, 0.98)], (0.0, -1.0, 0.0), "bone")
    return m


def build_bone_cage():
    """A cage of bones: eight ribs rising from a ring of radius 0.55 and bending in over a ghoul's height (1.45 m) to a knot at the top, a bone ring round the foot."""
    m = Mesh()
    ribs = 8
    for k in range(ribs):
        a = math.tau * k / ribs
        c, s = math.cos(a), math.sin(a)
        pts = []
        for n in range(7):
            t = n / 6
            radius = 0.55 * (1.0 + 0.15 * math.sin(math.pi * t * 0.8)) * (1.0 - 0.85 * max(0.0, t - 0.55) / 0.45)
            pts.append((c * radius, 1.62 * t, s * radius))
        for p, q in zip(pts, pts[1:]):
            m.beam(p, q, 0.028, "bone", 0, 0)
        m.box(c * 0.55 - 0.04, 0.0, s * 0.55 - 0.04, c * 0.55 + 0.04, 0.08, s * 0.55 + 0.04, "bone")   # driven into the ground
    sides = 16
    for k in range(sides):
        a0, a1 = math.tau * k / sides, math.tau * (k + 1) / sides
        m.beam((math.cos(a0) * 0.56, 0.1, math.sin(a0) * 0.56), (math.cos(a1) * 0.56, 0.1, math.sin(a1) * 0.56), 0.025, "bone", 0, 0)
        m.beam((math.cos(a0) * 0.6, 0.8, math.sin(a0) * 0.6), (math.cos(a1) * 0.6, 0.8, math.sin(a1) * 0.6), 0.016, "bone", 0, 0)
    m.box(-0.07, 1.55, -0.07, 0.07, 1.7, 0.07, "bone")                                         # the knot at the top
    skull(m, 0.0, 1.78, 0.0, 0.14, eyes="soul_light")
    return m


def build_lich_aura():
    """The Lich's aura: a thin ring of soul light of radius 1 on the ground and a ring of twelve ghostly flames licking up round it, taller and shorter in turn."""
    m = Mesh()
    sides = 36
    for k in range(sides):
        a0, a1 = math.tau * k / sides, math.tau * (k + 1) / sides
        m.facing([(math.cos(a0) * 0.9, 0.0, math.sin(a0) * 0.9), (math.cos(a0), 0.0, math.sin(a0)),
                  (math.cos(a1), 0.0, math.sin(a1)), (math.cos(a1) * 0.9, 0.0, math.sin(a1) * 0.9)], (0.0, 1.0, 0.0), "soul_light")
    flames = 12
    for k in range(flames):
        a = math.tau * k / flames
        c, s = math.cos(a), math.sin(a)
        height = 0.9 if k % 2 == 0 else 0.55
        colour = "soul" if k % 2 == 0 else "soul_light"
        lean = (c * 0.97 - s * 0.15, height, s * 0.97 + c * 0.15)
        m.pyramid(c * 0.95 - 0.07, 0.0, s * 0.95 - 0.07, c * 0.95 + 0.07, 0.0, s * 0.95 + 0.07, lean, colour)
    return m


def build_corpse_burst():
    """A servant bursting: a ring of soul light of radius 1 on the ground and bone shards round it, points outward."""
    m = Mesh()
    sides = 32
    for k in range(sides):
        a0, a1 = math.tau * k / sides, math.tau * (k + 1) / sides
        m.facing([(math.cos(a0) * 0.8, 0.0, math.sin(a0) * 0.8), (math.cos(a0), 0.0, math.sin(a0)),
                  (math.cos(a1), 0.0, math.sin(a1)), (math.cos(a1) * 0.8, 0.0, math.sin(a1) * 0.8)], (0.0, 1.0, 0.0), "soul")
    shards = 14
    for k in range(shards):
        a = math.tau * (k + 0.3 * _unit_noise(k, 1.9)) / shards
        c, s = math.cos(a), math.sin(a)
        d = 0.85 + 0.2 * _unit_noise(k, 3.1)
        y = 0.1 + 0.4 * _unit_noise(k, 4.4)
        m.pyramid(c * d - 0.04, y - 0.04, s * d - 0.04, c * d + 0.04, y - 0.04, s * d + 0.04, (c * (d + 0.3), y + 0.08, s * (d + 0.3)), "bone")
    return m


def build_grave_rise():
    """Where a servant climbs out: churned earth clods in a ring of radius about 0.8, and soul light glowing in the broken ground inside."""
    m = Mesh()
    sides = 16
    for k in range(sides):
        a0, a1 = math.tau * k / sides, math.tau * (k + 1) / sides
        m.facing([(0.0, 0.01, 0.0), (math.cos(a0) * 0.55, 0.01, math.sin(a0) * 0.55), (math.cos(a1) * 0.55, 0.01, math.sin(a1) * 0.55)],
                 (0.0, 1.0, 0.0), "soul" if k % 2 == 0 else "earth")
    clods = 14
    for k in range(clods):
        a = math.tau * k / clods + 0.2 * _unit_noise(k, 2.7)
        c, s = math.cos(a), math.sin(a)
        d = 0.65 + 0.25 * _unit_noise(k, 6.1)
        size = 0.08 + 0.07 * _unit_noise(k, 8.3)
        colour = "earth" if k % 3 else "earth_light"
        m.box(c * d - size, 0.0, s * d - size, c * d + size, size * 1.4, s * d + size, colour)
    return m


RIGID = (("grave_soul.glb", build_soul), ("bone_spear.glb", build_bone_spear), ("bone_cage.glb", build_bone_cage),
         ("lich_aura.glb", build_lich_aura), ("corpse_burst.glb", build_corpse_burst), ("grave_rise.glb", build_grave_rise))


def write_all(models_dir):
    """Writes this tree's models into <models_dir>."""
    for name, build in RIGID:
        mesh = build()
        write_glb(mesh, models_dir / name)
        print(f"Wrote {models_dir / name} ({len(mesh.positions)} vertices, {len(mesh.indices) // 3} triangles)")
    mesh = SERVANT.mesh()
    clips = SERVANT.clips()
    write_skinned_glb(mesh, models_dir / SERVANT.file, SERVANT.rig.gltf_joints(), clips)
    print(f"Wrote {models_dir / SERVANT.file} ({len(mesh.positions)} vertices, {len(SERVANT.rig.names)} joints, clips {', '.join(clips)})")
