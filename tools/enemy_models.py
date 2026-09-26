"""The enemies as skinned models with their clips, on the heroes' skeleton and maths (hero_rig.py). Written by make_placeholder_models.py. They are drawn as
animated crowds (the engine's SetSkinnedCrowd), each copy at its own clip and time, so a swarm of hundreds walks, attacks and dies.

Every enemy has
    Idle      standing, looping (IDLE_SECONDS)
    Walk      one stride, as many seconds long as it is metres, so the game moves it on by the ground the enemy covers and a planted foot stays put
    Die       falling, 1 s, held at the end
and a clip for each attack it has, named after its AttackType (Shoot, Lunge, LeapSlam, Shockwave, Summon, Barrage), one second long: the wind-up over the
first WINDUP_END of it, the blow (the attack's active time) up to ACTIVE_END, the recovery after. The game maps each phase's progress onto its stretch.
A held model (the Crossbow Ghoul's crossbow, the Ghoul Mage's flame) is a model of its own on the same skeleton with the same clips, drawn in the same pose.
"""

import math

import hero_rig as r
from hero_models import face, skirt_flaps, spine_upright, limbs, crystal
from make_placeholder_models import Mesh

IDLE_SECONDS = 3.0
DIE_SECONDS = 1.0
WINDUP_END, ACTIVE_END = 0.4, 0.7      # must match EnemyView.WindUpEnd and ActiveEnd in the game

SHOWN = (1.0, 1.0, 1.0)


def stage(u):
    """Where an attack clip is at <u>: ("windup" | "active" | "recover", progress 0 to 1 through that stretch)."""
    if u < WINDUP_END:
        return "windup", u / WINDUP_END
    if u < ACTIVE_END:
        return "active", (u - WINDUP_END) / (ACTIVE_END - WINDUP_END)
    return "recover", (u - ACTIVE_END) / (1.0 - ACTIVE_END)


def keyed(u, ready, wound, struck, settle=None):
    """A pose value over an attack clip: eased from <ready> to <wound> through the wind-up, snapped to <struck> early in the blow and held, eased back to
    <ready> through the recovery (by way of <settle> if given)."""
    lerp = r.v_lerp if isinstance(ready, tuple) else (lambda a, b, t: a + (b - a) * t)
    name, k = stage(u)
    if name == "windup":
        return lerp(ready, wound, r.ease(k))
    if name == "active":
        return lerp(wound, struck, r.ease(min(1.0, k * 2.5)))
    if settle is not None:
        return lerp(settle, ready, r.ease(k)) if k > 0.3 else lerp(struck, settle, r.ease(k / 0.3))
    return lerp(struck, ready, r.ease(k))


class EnemyModel:
    """An enemy's rig, its mesh (and any held model's), and its clips: {name: (seconds, pose function of the clip's 0-1 progress)}."""

    def __init__(self, file, rig, gait, mesh, clips, held=(), scaled=()):
        self.file, self.rig, self.gait, self._mesh, self._clips, self.held, self.scaled = file, rig, gait, mesh, clips, held, scaled

    def mesh(self):
        return self._mesh(self.rig)

    def clips(self):
        out = {}
        for name, (seconds, pose_fn) in self._clips.items():
            out[name] = (seconds, lambda t, f=pose_fn, s=seconds: self._frame(f, t / s))
        return out

    def _frame(self, pose_fn, u):
        pose = r.Pose(self.rig)
        scales = pose_fn(pose, u) or {}
        return {name: (rotation, translation, scales.get(name, SHOWN) if name in self.scaled else None)
                for name, (rotation, translation) in pose.frame().items()}


def hunch(pose, lean, head=0.0, turn=0.0, head_turn=0.0):
    """Stooped forward <lean> degrees at the spine and a little more at the chest, the head lifted to look ahead (<head> tips it down)."""
    spine_upright(pose, lean=lean * 0.7, turn=turn)
    pose.set("chest", r.q_euler(x=lean * 0.3))
    pose.set_world("head", r.q_euler(x=head, y=head_turn))


def collapse(pose, u, lean, height):
    """Dying: the knees go, the body folds forward and slumps, the arms fall slack; held at the end. <height> is how tall the enemy's legs are, to scale the
    drop."""
    k = r.ease(min(1.0, u / 0.7))
    r.stand_legs(pose, 0.0, 0.0, crouch=height * 0.5 * k)
    hunch(pose, lean + 50.0 * k, head=10.0 + 40.0 * k)
    for side, sx in (("l", -1.0), ("r", 1.0)):
        r.aim_arm(pose, side, (sx * (0.15 + 0.3 * k), -1.0, 0.3 + 0.4 * k), bend=10.0, elbow_pole=(sx, 0.0, -1.0))


# =========================================================================================================================================================
# Ghoul: gaunt and hunched, long arms reaching out with bone claws, red eyes

GHOUL_RIG = r.HeroRig(hip_y=0.64, spine_y=0.72, chest_y=0.8, neck_y=1.2, shoulder=(0.27, 1.14), elbow_y=0.86, wrist_y=0.58, leg_x=0.13, knee_y=0.35,
                      ankle_y=0.08, toe=0.14, heel=0.06)
GHOUL_GAIT = r.Gait(cycle=1.25, stance=0.5, lift=0.12, bob=0.02, hip_drop=0.13, width=0.13, lean=12.0)


def _ghoul_legs_and_body(m, j, rags="ghoul_rags"):
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.06, 0.2, 0.0, -0.06, 0.1, 0.16, "ghoul_skin"),
                           ("shin", 0.07, 0.18, 0.08, -0.055, 0.38, 0.055, "ghoul_skin"),
                           ("thigh", 0.065, 0.185, 0.32, -0.06, 0.66, 0.06, "ghoul_skin")])
    m.joint = j["hips"]
    m.box(-0.22, 0.52, -0.12, 0.22, 0.74, 0.12, rags)


def build_ghoul_rigged(rig):
    m = Mesh()
    j = rig.index
    _ghoul_legs_and_body(m, j)
    m.joint = j["chest"]
    m.box(-0.24, 0.72, -0.12, 0.24, 1.18, 0.14, "ghoul_skin")
    m.box(-0.08, 0.84, -0.16, 0.08, 1.16, -0.12, "bone")                       # the spine ridge
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.21, 0.33, 0.84, -0.06, 1.16, 0.06, "ghoul_skin"),
                           ("forearm", 0.215, 0.325, 0.56, -0.055, 0.88, 0.055, "ghoul_skin"),
                           ("hand", 0.21, 0.33, 0.44, -0.04, 0.58, 0.08, "bone")])
        sx = -1.0 if side == "l" else 1.0
        for dx in (0.23, 0.27, 0.31):
            x0, x1 = sorted((sx * (dx - 0.012), sx * (dx + 0.012)))
            m.box(x0, 0.36, 0.0, x1, 0.46, 0.03, "bone")                          # the claws
    m.joint = j["head"]
    m.box(-0.13, 1.18, -0.1, 0.13, 1.44, 0.16, "ghoul_skin")
    m.box(-0.10, 1.31, 0.16, -0.03, 1.36, 0.165, "ghoul_eye")
    m.box(0.03, 1.31, 0.16, 0.10, 1.36, 0.165, "ghoul_eye")
    m.box(-0.10, 1.14, -0.04, 0.10, 1.2, 0.14, "bone")
    return m


def ghoul_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.5)
    sway = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.6 * sway, width=0.17, crouch=0.07)
    hunch(pose, 32.0 + 2.0 * breath, head=-18.0, head_turn=15.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 1.0))
    for side, sx, off in (("l", -1.0, 0.0), ("r", 1.0, 1.3)):
        twitch = math.sin(2 * math.pi * t / 1.1 + off)
        r.aim_arm(pose, side, (sx * 0.15, -0.8, 0.45 + 0.05 * twitch), bend=25.0, elbow_pole=(sx * 0.5, 0.0, -1.0))
        face(pose, f"hand_{side}", (0.0, 0.0, 1.0), tilt=40.0 + 10.0 * twitch)


def ghoul_walk(pose, u):
    _, swing = r.run_legs(pose, GHOUL_GAIT, u, 0.0)
    bob = math.cos(4 * math.pi * u)
    hunch(pose, 36.0 + 2.0 * bob, head=-24.0, turn=-6.0 * swing)
    # The arms reach out ahead for the prey, clawing in turn
    for side, sx, sign in (("l", -1.0, -1.0), ("r", 1.0, 1.0)):
        reach = sign * swing
        r.aim_arm(pose, side, (sx * 0.1, -0.35 + 0.1 * reach, 0.95), bend=18.0 + 14.0 * max(0.0, -reach), elbow_pole=(sx * 0.3, -1.0, 0.0))
        face(pose, f"hand_{side}", (0.0, 0.0, 1.0), tilt=70.0 + 20.0 * reach)


def ghoul_die(pose, u):
    collapse(pose, u, 32.0, 0.56)


GHOUL = EnemyModel("ghoul.glb", GHOUL_RIG, GHOUL_GAIT, build_ghoul_rigged,
                   {"Idle": (IDLE_SECONDS, ghoul_idle), "Walk": (GHOUL_GAIT.cycle, ghoul_walk), "Die": (DIE_SECONDS, ghoul_die)})


# =========================================================================================================================================================
# Crossbow Ghoul: hooded and cloaked, a quiver of bolts on its back, a crossbow held level in both hands (the crossbow is its own model)

def build_crossbow_ghoul_rigged(rig):
    m = Mesh()
    j = rig.index
    _ghoul_legs_and_body(m, j)
    m.joint = j["chest"]
    m.box(-0.23, 0.72, -0.1, 0.23, 1.18, 0.14, "ghoul_skin")
    m.box(-0.25, 0.62, -0.16, 0.25, 1.22, -0.1, "hood_dark")                    # the cloak
    m.box(0.05, 0.8, -0.26, 0.17, 1.3, -0.16, "leather")                        # the quiver
    m.box(0.07, 1.3, -0.24, 0.15, 1.38, -0.18, "bolt_glow")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.21, 0.33, 0.84, -0.06, 1.16, 0.06, "hood_dark"),
                           ("forearm", 0.215, 0.325, 0.56, -0.055, 0.88, 0.055, "ghoul_skin"),
                           ("hand", 0.21, 0.33, 0.47, -0.05, 0.58, 0.05, "bone")])
    m.joint = j["head"]
    m.box(-0.13, 1.18, -0.1, 0.13, 1.44, 0.14, "ghoul_skin")
    m.box(-0.10, 1.31, 0.14, -0.03, 1.36, 0.145, "ghoul_eye")
    m.box(0.03, 1.31, 0.14, 0.10, 1.36, 0.145, "ghoul_eye")
    m.box(-0.16, 1.16, -0.16, 0.16, 1.5, -0.08, "hood_dark")
    m.box(-0.16, 1.42, -0.16, 0.16, 1.5, 0.16, "hood_dark")
    m.box(-0.165, 1.16, -0.08, -0.13, 1.44, 0.12, "hood_dark")
    m.box(0.13, 1.16, -0.08, 0.165, 1.44, 0.12, "hood_dark")
    return m


CROSSBOW_GRIP = (0.27, 0.52, 0.0)     # the right fist on the stock (the hand's middle at rest)


def build_ghoul_crossbow_rigged(rig):
    """The crossbow in the right fist, pointing +Z from it: stock, limbs with glowing runes at the tips, the drawn string, a loaded bolt."""
    m = Mesh()
    m.joint = rig.index["hand_r"]
    gx, gy, gz = CROSSBOW_GRIP
    m.box(gx - 0.04, gy - 0.04, gz - 0.2, gx + 0.04, gy + 0.04, gz + 0.5, "wood")
    m.box(gx - 0.38, gy - 0.01, gz + 0.38, gx + 0.38, gy + 0.05, gz + 0.44, "chest_dark")
    m.box(gx - 0.44, gy - 0.02, gz + 0.36, gx - 0.36, gy + 0.06, gz + 0.46, "bolt_glow")
    m.box(gx + 0.36, gy - 0.02, gz + 0.36, gx + 0.44, gy + 0.06, gz + 0.46, "bolt_glow")
    m.box(gx - 0.36, gy + 0.015, gz + 0.18, gx + 0.36, gy + 0.025, gz + 0.2, "fletching")
    m.box(gx - 0.012, gy + 0.04, gz + 0.18, gx + 0.012, gy + 0.06, gz + 0.54, "wood")
    m.box(gx - 0.03, gy + 0.03, gz + 0.54, gx + 0.03, gy + 0.07, gz + 0.6, "bolt_glow")
    return m


def _hold_crossbow(pose, grip_at, aim, pitch=0.0):
    """The crossbow's stock in the right fist at <grip_at>, pointing along <aim> tipped up <pitch> degrees, the left hand under its fore-end."""
    r.arm_ik(pose, "r", r.v_add(grip_at, (0.0, 0.06, -0.02)), elbow_pole=(1.0, -0.6, -0.4))
    face(pose, "hand_r", aim, tilt=-pitch)
    world = pose.world()
    rot, at = world["hand_r"]
    fore = r.v_add(at, r.q_rotate(rot, r.v_sub((0.27, 0.46, 0.32), GHOUL_RIG.bind["hand_r"])))
    r.arm_ik(pose, "l", r.v_add(fore, (0.0, 0.02, 0.0)), elbow_pole=(-1.0, -0.6, -0.3))
    face(pose, "hand_l", aim, tilt=60.0)


def crossbow_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.6)
    r.stand_legs(pose, breath, 0.4 * math.sin(2 * math.pi * t / IDLE_SECONDS), width=0.17, crouch=0.05)
    hunch(pose, 20.0 + 2.0 * breath, head=-10.0, head_turn=12.0 * math.sin(2 * math.pi * t / IDLE_SECONDS))
    _hold_crossbow(pose, (0.14, 0.86 + 0.01 * breath, 0.3), (0.0, 0.0, 1.0), pitch=-12.0)


def crossbow_walk(pose, u):
    _, swing = r.run_legs(pose, GHOUL_GAIT, u, 0.0)
    bob = math.cos(4 * math.pi * u)
    hunch(pose, 24.0 + 2.0 * bob, head=-14.0, turn=-4.0 * swing)
    _hold_crossbow(pose, (0.14, 0.86 + 0.02 * bob, 0.3), (0.0, 0.0, 1.0), pitch=-15.0 + 3.0 * swing)


def crossbow_shoot(pose, u):
    """Brought up to the shoulder and aimed through the wind-up, kicking up as it looses, lowered again after."""
    r.stand_legs(pose, 0.0, 0.2, width=0.2, crouch=0.06)
    hunch(pose, keyed(u, 20.0, 6.0, 2.0, settle=10.0), head=keyed(u, -10.0, 4.0, -2.0))
    grip = keyed(u, (0.14, 0.86, 0.3), (0.1, 1.1, 0.24), (0.1, 1.14, 0.16), settle=(0.1, 1.04, 0.22))
    pitch = keyed(u, -12.0, 0.0, 18.0, settle=4.0)
    _hold_crossbow(pose, grip, (0.0, 0.0, 1.0), pitch=pitch)


CROSSBOW_GHOUL = EnemyModel("crossbow_ghoul.glb", GHOUL_RIG, GHOUL_GAIT, build_crossbow_ghoul_rigged,
                            {"Idle": (IDLE_SECONDS, crossbow_idle), "Walk": (GHOUL_GAIT.cycle, crossbow_walk), "Die": (DIE_SECONDS, ghoul_die),
                             "Shoot": (1.0, crossbow_shoot)},
                            held=(("ghoul_crossbow.glb", build_ghoul_crossbow_rigged),))


# =========================================================================================================================================================
# Ghoul Mage: a gaunt ghoul in a long tattered robe and a tall hood, a flame burning between its cupped hands (the flame is its own model, and grows as
# a fireball is called up)

MAGE_RIG = r.HeroRig(hip_y=0.66, spine_y=0.74, chest_y=0.82, neck_y=1.22, shoulder=(0.25, 1.16), elbow_y=0.9, wrist_y=0.64, leg_x=0.11, knee_y=0.36,
                     ankle_y=0.08, toe=0.13, heel=0.06,
                     extra=(("robe_front", "hips", (0.0, 0.7, 0.16)), ("robe_back", "hips", (0.0, 0.7, -0.16)),
                            ("flame", "hand_r", (0.25, 0.56, 0.0))))
MAGE_GAIT = r.Gait(cycle=1.2, stance=0.52, lift=0.1, bob=0.02, hip_drop=0.12, width=0.1, lean=6.0)


def build_ghoul_mage_rigged(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.05, 0.17, 0.0, -0.06, 0.09, 0.14, "ghoul_skin"),
                           ("shin", 0.05, 0.17, 0.08, -0.06, 0.4, 0.06, "ghoul_robe"),
                           ("thigh", 0.045, 0.175, 0.34, -0.065, 0.68, 0.065, "ghoul_robe")])
    m.joint = j["hips"]
    m.box(-0.22, 0.56, -0.15, 0.22, 0.8, 0.16, "ghoul_robe")
    m.box(-0.23, 0.72, -0.16, 0.23, 0.78, 0.17, "bone")                       # the cord of bones
    m.joint = j["robe_front"]
    m.box(-0.25, 0.02, 0.14, 0.25, 0.72, 0.19, "ghoul_robe")
    m.joint = j["robe_back"]
    m.box(-0.25, 0.02, -0.19, 0.25, 0.72, -0.14, "ghoul_robe")
    m.joint = j["chest"]
    m.box(-0.22, 0.76, -0.14, 0.22, 1.2, 0.15, "ghoul_robe")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.19, 0.31, 0.88, -0.06, 1.18, 0.06, "ghoul_robe"),
                           ("forearm", 0.19, 0.31, 0.62, -0.07, 0.92, 0.07, "ghoul_robe"),
                           ("hand", 0.2, 0.3, 0.52, -0.05, 0.64, 0.05, "ghoul_skin")])
    m.joint = j["head"]
    m.box(-0.12, 1.2, -0.1, 0.12, 1.46, 0.14, "ghoul_skin")
    m.box(-0.09, 1.33, 0.14, -0.03, 1.38, 0.145, "ghoul_eye")
    m.box(0.03, 1.33, 0.14, 0.09, 1.38, 0.145, "ghoul_eye")
    m.box(-0.16, 1.18, -0.18, 0.16, 1.52, -0.1, "ghoul_robe")
    m.box(-0.16, 1.44, -0.18, 0.16, 1.52, 0.12, "ghoul_robe")
    m.box(-0.165, 1.18, -0.1, -0.13, 1.46, 0.1, "ghoul_robe")
    m.box(0.13, 1.18, -0.1, 0.165, 1.46, 0.1, "ghoul_robe")
    m.pyramid(-0.16, 1.52, -0.18, 0.16, 1.52, 0.12, (0.0, 1.85, -0.14), "ghoul_robe")
    return m


def _orb(m, cx, cy, cz, radius, outer, inner):
    for size, colour, turn in ((radius, outer, 0.0), (radius * 0.85, inner, math.pi / 4)):
        top, bottom = (cx, cy + size, cz), (cx, cy - size, cz)
        ring = [(cx + math.cos(turn + k * math.pi / 2) * size, cy, cz + math.sin(turn + k * math.pi / 2) * size) for k in range(4)]
        for i in range(4):
            a, b = ring[i], ring[(i + 1) % 4]
            m.tri(a, top, b, colour)
            m.tri(b, bottom, a, colour)


def build_ghoul_flame_rigged(rig):
    """The flame, on its own joint between the hands."""
    m = Mesh()
    m.joint = rig.index["flame"]
    x, y, z = rig.bind["flame"]
    _orb(m, x, y, z, 0.14, "fire", "fire_light")
    return m


def _cup_flame(pose, centre, size, flicker=0.0):
    """Both hands cupped round <centre>, the flame there at <size>."""
    for side, sx in (("l", -1.0), ("r", 1.0)):
        r.arm_ik(pose, side, r.v_add(centre, (sx * 0.12, 0.02, -0.04)), elbow_pole=(sx, -0.6, -0.4))
        face(pose, f"hand_{side}", (-sx * 0.8, 0.0, 0.6), tilt=50.0)
    rot, at = pose.world()["hand_r"]
    pose.set("flame", r.Q_IDENTITY, r.q_rotate(r.q_inv(rot), r.v_sub(centre, at)))
    pose.set_world("flame", r.q_euler(y=flicker * 40.0))
    s = size * (1.0 + 0.08 * flicker)
    return {"flame": (s, s, s)}


def ghoul_mage_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.8)
    r.stand_legs(pose, breath, 0.4 * math.sin(2 * math.pi * t / IDLE_SECONDS), width=0.13, crouch=0.03)
    hunch(pose, 14.0 + 2.0 * breath, head=-6.0, head_turn=10.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 2.0))
    skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
    return _cup_flame(pose, (0.0, 0.98 + 0.01 * breath, 0.32), 1.0, math.sin(2 * math.pi * t * 3.0))


def ghoul_mage_walk(pose, u):
    _, swing = r.run_legs(pose, MAGE_GAIT, u, 0.0)
    bob = math.cos(4 * math.pi * u)
    hunch(pose, 16.0, head=-8.0, turn=-3.0 * swing)
    skirt_flaps(pose, "robe_front", "robe_back", margin=2.0, give=0.6)
    return _cup_flame(pose, (0.0, 0.98 + 0.015 * bob, 0.32), 1.0, math.sin(2 * math.pi * u * 6.0))


def ghoul_mage_shoot(pose, u):
    """The flame lifted over the head and fed until it is a fireball, then flung out ahead; a new one kindled in the hands after."""
    r.stand_legs(pose, 0.0, 0.0, width=0.15, crouch=0.04)
    hunch(pose, keyed(u, 14.0, -4.0, 18.0), head=keyed(u, -6.0, -16.0, 4.0))
    skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
    centre = keyed(u, (0.0, 0.98, 0.32), (0.0, 1.72, 0.12), (0.0, 1.34, 0.62), settle=(0.0, 1.1, 0.4))
    name, k = stage(u)
    size = {"windup": 1.0 + 0.9 * r.ease(k), "active": 1.9 * (1.0 - r.ease(min(1.0, k * 3.0))) + 0.05, "recover": 0.05 + 0.95 * r.ease(k)}[name]
    return _cup_flame(pose, centre, max(0.05, size), math.sin(2 * math.pi * u * 9.0))


def ghoul_mage_die(pose, u):
    collapse(pose, u, 14.0, 0.58)
    skirt_flaps(pose, "robe_front", "robe_back", margin=3.0)
    s = max(0.02, 1.0 - r.ease(min(1.0, u / 0.3)))   # the flame gutters out
    return {"flame": (s, s, s)}


GHOUL_MAGE = EnemyModel("ghoul_mage.glb", MAGE_RIG, MAGE_GAIT, build_ghoul_mage_rigged,
                        {"Idle": (IDLE_SECONDS, ghoul_mage_idle), "Walk": (MAGE_GAIT.cycle, ghoul_mage_walk),
                         "Die": (DIE_SECONDS, lambda pose, u: ghoul_mage_die(pose, u)),
                         "Shoot": (1.0, ghoul_mage_shoot)},
                        held=(("ghoul_flame.glb", build_ghoul_flame_rigged),), scaled=("flame",))


# =========================================================================================================================================================
# Ghoul Brute: 2.4 m, hulking and stooped, horns, a bone club in the right fist

BRUTE_RIG = r.HeroRig(hip_y=1.0, spine_y=1.12, chest_y=1.3, neck_y=1.96, shoulder=(0.74, 1.9), elbow_y=1.46, wrist_y=1.0, leg_x=0.27, knee_y=0.56,
                      ankle_y=0.14, toe=0.26, heel=0.18)
BRUTE_GAIT = r.Gait(cycle=2.0, stance=0.5, lift=0.2, bob=0.04, hip_drop=0.2, width=0.26, lean=12.0)


def build_brute_rigged(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.12, 0.42, 0.0, -0.18, 0.18, 0.26, "brute_hide"),
                           ("shin", 0.14, 0.40, 0.14, -0.15, 0.6, 0.15, "brute_skin"),
                           ("thigh", 0.13, 0.41, 0.52, -0.17, 1.02, 0.17, "brute_skin")])
    m.joint = j["hips"]
    m.box(-0.48, 0.8, -0.26, 0.48, 1.12, 0.26, "brute_hide")
    m.joint = j["chest"]
    m.box(-0.52, 1.05, -0.28, 0.52, 1.6, 0.32, "brute_skin")
    m.box(-0.66, 1.55, -0.26, 0.66, 2.05, 0.32, "brute_skin")
    m.box(-0.2, 1.4, -0.36, 0.2, 2.0, -0.26, "bone")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.62, 0.88, 1.42, -0.15, 1.98, 0.15, "brute_skin"),
                           ("forearm", 0.63, 0.87, 0.98, -0.14, 1.5, 0.14, "brute_skin"),
                           ("hand", 0.61, 0.89, 0.8, -0.15, 1.02, 0.15, "brute_hide")])
    # The club, up from the right fist: the shaft and a knobbed bone head
    m.joint = j["hand_r"]
    m.box(0.68, 0.84, -0.07, 0.82, 1.9, 0.07, "bone")
    m.box(0.62, 1.8, -0.14, 0.88, 2.25, 0.14, "bone")
    m.joint = j["head"]
    m.box(-0.22, 1.9, -0.14, 0.22, 2.3, 0.24, "brute_skin")
    m.box(-0.16, 2.12, 0.24, -0.06, 2.18, 0.245, "ghoul_eye")
    m.box(0.06, 2.12, 0.24, 0.16, 2.18, 0.245, "ghoul_eye")
    m.box(-0.16, 1.9, 0.2, -0.1, 2.02, 0.28, "horn")
    m.box(0.1, 1.9, 0.2, 0.16, 2.02, 0.28, "horn")
    m.pyramid(-0.34, 2.2, -0.06, -0.18, 2.2, 0.1, (-0.52, 2.55, -0.1), "horn")
    m.pyramid(0.18, 2.2, -0.06, 0.34, 2.2, 0.1, (0.52, 2.55, -0.1), "horn")
    return m


def _brute_arms(pose, club_at, club_tilt, left_at, club_ahead=(0.2, 0.0, 1.0)):
    r.arm_ik(pose, "r", club_at, elbow_pole=(1.0, -0.4, -0.6))
    face(pose, "hand_r", club_ahead, tilt=club_tilt)
    r.arm_ik(pose, "l", left_at, elbow_pole=(-1.0, -0.4, -0.6))
    face(pose, "hand_l", (0.0, 0.0, 1.0), tilt=30.0)


def brute_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.5)
    r.stand_legs(pose, breath, 0.5 * math.sin(2 * math.pi * t / IDLE_SECONDS), width=0.34, crouch=0.08)
    hunch(pose, 24.0 + 3.0 * breath, head=-16.0, head_turn=10.0 * math.sin(2 * math.pi * t / IDLE_SECONDS))
    _brute_arms(pose, (0.8, 1.05 + 0.02 * breath, 0.45), 70.0, (-0.8, 1.0 + 0.02 * breath, 0.3))


def brute_walk(pose, u):
    _, swing = r.run_legs(pose, BRUTE_GAIT, u, 0.0)
    bob = math.cos(4 * math.pi * u)
    hunch(pose, 26.0 + 3.0 * bob, head=-18.0, turn=-7.0 * swing)
    _brute_arms(pose, (0.82, 1.1 + 0.05 * swing, 0.4 + 0.2 * swing), 60.0 + 15.0 * swing, (-0.84, 1.05 - 0.05 * swing, 0.3 - 0.2 * swing))


def brute_lunge(pose, u):
    """Crouched to spring with the club drawn back, then the charge, head down and club leading, then winded."""
    name, k = stage(u)
    crouch = keyed(u, 0.08, 0.3, 0.2, settle=0.16)
    r.stand_legs(pose, 0.0, 0.0, width=0.36, crouch=crouch)
    hunch(pose, keyed(u, 24.0, 40.0, 55.0, settle=30.0), head=keyed(u, -16.0, -34.0, -45.0))
    _brute_arms(pose, keyed(u, (0.8, 1.05, 0.45), (0.9, 1.4, -0.35), (0.6, 1.2, 1.0), settle=(0.85, 1.0, 0.5)),
                keyed(u, 70.0, -30.0, 95.0, settle=80.0), keyed(u, (-0.8, 1.0, 0.3), (-0.9, 1.1, 0.5), (-0.7, 1.0, 0.8)))


def brute_leap(pose, u):
    """Squatting with both arms up, then in the air (knees tucked, the club raised over the head) and the club brought down as it lands, crouched after."""
    name, k = stage(u)
    if name == "active":
        r.airborne_legs(pose, tuck=1.4)
    else:
        r.stand_legs(pose, 0.0, 0.0, width=0.36, crouch=keyed(u, 0.08, 0.34, 0.3, settle=0.32))
    smash = r.ease(max(0.0, (k - 0.6) / 0.4)) if name == "active" else (1.0 if name == "recover" else 0.0)
    hunch(pose, keyed(u, 24.0, 10.0, -10.0) + 55.0 * smash * (1.0 - (r.ease(k) if name == "recover" else 0.0)), head=-10.0)
    up = (0.5, 2.5, 0.0)
    down = (0.4, 0.9, 1.1)
    club = keyed(u, (0.8, 1.05, 0.45), (0.6, 2.4, 0.1), up)
    if name == "active":
        club = r.v_lerp(up, down, smash)
    elif name == "recover":
        club = r.v_lerp(down, (0.8, 1.05, 0.45), r.ease(k))
    tilt = -20.0 + 130.0 * smash if name != "windup" else keyed(u, 70.0, -20.0, -20.0)
    if name == "recover":
        tilt = 110.0 - 40.0 * r.ease(k)
    _brute_arms(pose, club, tilt, keyed(u, (-0.8, 1.0, 0.3), (-0.6, 2.3, 0.1), (-0.6, 2.2, 0.3)))


def brute_die(pose, u):
    collapse(pose, u, 24.0, 0.86)
    face(pose, "hand_r", (0.2, 0.0, 1.0), tilt=70.0 + 50.0 * r.ease(u))   # the club drops from its hand's grip forward


BRUTE = EnemyModel("brute.glb", BRUTE_RIG, BRUTE_GAIT, build_brute_rigged,
                   {"Idle": (IDLE_SECONDS, brute_idle), "Walk": (BRUTE_GAIT.cycle, brute_walk),
                    "Die": (DIE_SECONDS, brute_die),
                    "Lunge": (1.0, brute_lunge), "LeapSlam": (1.0, brute_leap)})


# =========================================================================================================================================================
# The Hollow King (4.2 m): gaunt and regal, a long robe, a tattered cape, a skull for a head under a gold crown, long arms with bone claws. The Unbound (the
# Delve boss) is the same king grown by UNBOUND_SCALE, horned, with bone pauldrons, a burning crown and eyes, and broken chains at its wrists.

UNBOUND_SCALE = 1.32


def king_rig(s):
    return r.HeroRig(hip_y=1.9 * s, spine_y=2.05 * s, chest_y=2.2 * s, neck_y=3.12 * s, shoulder=(1.1 * s, 3.02 * s), elbow_y=2.1 * s, wrist_y=1.14 * s,
                     leg_x=0.33 * s, knee_y=1.0 * s, ankle_y=0.2 * s, toe=0.4 * s, heel=0.3 * s,
                     extra=(("robe_front", "hips", (0.0, 1.95 * s, 0.38 * s)), ("robe_back", "hips", (0.0, 1.95 * s, -0.38 * s)),
                            ("cape", "chest", (0.0, 3.15 * s, -0.42 * s))))


def build_king(rig, s, unbound):
    m = Mesh()
    j = rig.index
    crown, eyes = ("fire", "fire_light") if unbound else ("gold", "ghost_eye")

    def box(x0, y0, z0, x1, y1, z1, colour):
        m.box(x0 * s, y0 * s, z0 * s, x1 * s, y1 * s, z1 * s, colour)

    for side in ("l", "r"):
        sx = -1.0 if side == "l" else 1.0
        for prefix, a, b, y0, z0, y1, z1, colour in (("foot", 0.16, 0.5, 0.0, -0.3, 0.3, 0.4, "king_skin"),
                                                     ("shin", 0.2, 0.46, 0.25, -0.14, 1.05, 0.14, "king_robe"),
                                                     ("thigh", 0.19, 0.47, 0.95, -0.16, 1.95, 0.16, "king_robe")):
            x0, x1 = sorted((sx * a, sx * b))
            m.joint = j[f"{prefix}_{side}"]
            box(x0, y0, z0, x1, y1, z1, colour)
    m.joint = j["hips"]
    box(-0.7, 1.6, -0.4, 0.7, 2.1, 0.4, "king_robe")
    m.joint = j["robe_front"]
    box(-0.66, 0.3, 0.34, 0.66, 1.95, 0.42, "king_robe")
    m.joint = j["robe_back"]
    box(-0.66, 0.3, -0.42, 0.66, 1.95, -0.34, "king_robe")
    m.joint = j["chest"]
    box(-0.55, 1.9, -0.3, 0.55, 3.0, 0.34, "king_skin")
    for y in (2.15, 2.4, 2.65):
        box(-0.45, y, 0.34, 0.45, y + 0.08, 0.4, "bone")
    box(-0.95, 2.85, -0.34, 0.95, 3.25, 0.38, "king_robe")
    m.joint = j["cape"]
    box(-0.9, 0.4, -0.52, 0.9, 3.15, -0.4, "king_robe")
    for side in ("l", "r"):
        sx = -1.0 if side == "l" else 1.0
        for prefix, a, b, y0, z0, y1, z1, colour in (("upper_arm", 0.95, 1.25, 2.05, -0.15, 3.05, 0.15, "king_skin"),
                                                     ("forearm", 0.96, 1.24, 1.1, -0.14, 2.15, 0.14, "king_skin"),
                                                     ("hand", 0.9, 1.3, 0.7, -0.12, 1.14, 0.3, "bone")):
            x0, x1 = sorted((sx * a, sx * b))
            m.joint = j[f"{prefix}_{side}"]
            box(x0, y0, z0, x1, y1, z1, colour)
        if unbound:
            m.joint = j[f"upper_arm_{side}"]
            px0, px1 = sorted((sx * 0.75, sx * 1.35))
            box(px0, 2.95, -0.42, px1, 3.35, 0.46, "bone")
            m.pyramid(px0 * s, 3.35 * s, -0.3 * s, px1 * s, 3.35 * s, 0.34 * s, (sx * 1.05 * s, 3.75 * s, 0.02 * s), "bone")
            m.joint = j[f"hand_{side}"]
            for k in range(4):
                y = 1.08 - 0.14 * k
                box(sx * 1.1 - 0.05, y - 0.09, 0.1, sx * 1.1 + 0.05, y, 0.2, "iron")
    m.joint = j["head"]
    box(-0.34, 3.2, -0.3, 0.34, 3.86, 0.34, "bone")
    box(-0.22, 3.52, 0.34, -0.06, 3.62, 0.35, eyes)
    box(0.06, 3.52, 0.34, 0.22, 3.62, 0.35, eyes)
    box(-0.24, 3.12, -0.06, 0.24, 3.24, 0.32, "bone")
    box(-0.38, 3.86, -0.34, 0.38, 3.98, 0.38, crown)
    for x, z in ((-0.3, 0.28), (0.0, 0.34), (0.3, 0.28), (-0.3, -0.26), (0.3, -0.26)):
        m.pyramid((x - 0.08) * s, 3.98 * s, (z - 0.08) * s, (x + 0.08) * s, 3.98 * s, (z + 0.08) * s, (x * s, 4.22 * s, z * s), crown)
    if unbound:
        for side in (-1, 1):
            x0, x1 = sorted((side * 0.3, side * 0.55))
            m.pyramid(x0 * s, 3.9 * s, -0.16 * s, x1 * s, 3.9 * s, 0.14 * s, (side * 1.05 * s, 4.75 * s, -0.11 * s), "horn")
    return m


def king_poses(s, gait):
    """The king's clips at size <s> (every place in them is scaled with the body)."""
    def p(v):
        return (v[0] * s, v[1] * s, v[2] * s)

    def arms(pose, left, right, claw_tilt=30.0):
        for side, sx, at in (("l", -1.0, left), ("r", 1.0, right)):
            r.arm_ik(pose, side, p(at), elbow_pole=(sx, -0.3, -0.7))
            face(pose, f"hand_{side}", (0.0, 0.0, 1.0), tilt=claw_tilt)

    def idle(pose, u):
        t = u * IDLE_SECONDS
        breath = math.sin(2 * math.pi * t / 2.0)
        r.stand_legs(pose, breath, 0.5 * math.sin(2 * math.pi * t / IDLE_SECONDS), width=0.42 * s, crouch=0.05 * s)
        hunch(pose, 10.0 + 2.0 * breath, head=-4.0, head_turn=14.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 0.5))
        skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
        pose.set("cape", r.q_euler(x=2.0 + breath))
        arms(pose, (-1.25, 1.45 + 0.02 * breath, 0.35), (1.25, 1.45 + 0.02 * breath, 0.35))

    def walk(pose, u):
        _, swing = r.run_legs(pose, gait, u, 0.0)
        hunch(pose, 14.0, head=-6.0, turn=-5.0 * swing)
        skirt_flaps(pose, "robe_front", "robe_back", margin=2.0, give=0.7)
        pose.set("cape", r.q_euler(x=8.0 + 3.0 * math.sin(4 * math.pi * u)))
        arms(pose, (-1.3, 1.5, 0.3 - 0.35 * swing), (1.3, 1.5, 0.3 + 0.35 * swing))

    def leap(pose, u):
        name, k = stage(u)
        if name == "active":
            r.airborne_legs(pose, tuck=1.2)
        else:
            r.stand_legs(pose, 0.0, 0.0, width=0.46 * s, crouch=keyed(u, 0.05, 0.5, 0.45, settle=0.4) * s)
        skirt_flaps(pose, "robe_front", "robe_back", margin=4.0)
        land = r.ease(max(0.0, (k - 0.7) / 0.3)) if name == "active" else (1.0 - r.ease(k) if name == "recover" else 0.0)
        hunch(pose, keyed(u, 10.0, 25.0, 0.0) + 40.0 * land, head=-10.0 + 10.0 * land)
        pose.set("cape", r.q_euler(x=keyed(u, 2.0, 10.0, 40.0)))
        up_l, up_r = (-1.1, 3.9, 0.3), (1.1, 3.9, 0.3)
        down_l, down_r = (-0.9, 0.6, 1.2), (0.9, 0.6, 1.2)
        if name == "windup":
            arms(pose, r.v_lerp((-1.25, 1.45, 0.35), up_l, r.ease(k)), r.v_lerp((1.25, 1.45, 0.35), up_r, r.ease(k)))
        else:
            arms(pose, r.v_lerp(up_l, down_l, land), r.v_lerp(up_r, down_r, land), claw_tilt=30.0 + 60.0 * land)

    def shockwave(pose, u):
        """Both arms raised high, then the claws driven into the ground, held while the ring runs out, and up again."""
        name, k = stage(u)
        r.stand_legs(pose, 0.0, 0.0, width=0.46 * s, crouch=keyed(u, 0.05, 0.0, 0.55, settle=0.3) * s)
        skirt_flaps(pose, "robe_front", "robe_back", margin=3.0)
        hunch(pose, keyed(u, 10.0, -12.0, 58.0, settle=30.0), head=keyed(u, -4.0, -25.0, -40.0))
        pose.set("cape", r.q_euler(x=keyed(u, 2.0, 4.0, 30.0)))
        arms(pose, keyed(u, (-1.25, 1.45, 0.35), (-1.0, 4.1, 0.1), (-0.8, 0.2, 1.3), settle=(-1.1, 1.0, 0.8)),
             keyed(u, (1.25, 1.45, 0.35), (1.0, 4.1, 0.1), (0.8, 0.2, 1.3), settle=(1.1, 1.0, 0.8)), claw_tilt=keyed(u, 30.0, -20.0, 90.0))

    def summon(pose, u):
        """Arms thrown wide and up, the skull back, calling its court up out of the ground."""
        r.stand_legs(pose, 0.0, 0.0, width=0.46 * s, crouch=0.05 * s)
        skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
        hunch(pose, keyed(u, 10.0, -6.0, -14.0), head=keyed(u, -4.0, -30.0, -40.0))
        pose.set("cape", r.q_euler(x=keyed(u, 2.0, 12.0, 18.0)))
        arms(pose, keyed(u, (-1.25, 1.45, 0.35), (-1.9, 3.4, 0.4), (-2.1, 3.9, 0.2)), keyed(u, (1.25, 1.45, 0.35), (1.9, 3.4, 0.4), (2.1, 3.9, 0.2)),
             claw_tilt=keyed(u, 30.0, -60.0, -80.0))

    def lunge(pose, u):
        """Gathering itself low, then charging with its claws out ahead."""
        r.stand_legs(pose, 0.0, 0.0, width=0.46 * s, crouch=keyed(u, 0.05, 0.45, 0.3, settle=0.2) * s)
        skirt_flaps(pose, "robe_front", "robe_back", margin=4.0)
        hunch(pose, keyed(u, 10.0, 30.0, 45.0, settle=20.0), head=keyed(u, -4.0, -30.0, -40.0))
        pose.set("cape", r.q_euler(x=keyed(u, 2.0, 8.0, 45.0)))
        arms(pose, keyed(u, (-1.25, 1.45, 0.35), (-1.4, 1.8, -0.6), (-0.8, 2.3, 1.9)), keyed(u, (1.25, 1.45, 0.35), (1.4, 1.8, -0.6), (0.8, 2.3, 1.9)),
             claw_tilt=keyed(u, 30.0, 10.0, 100.0))

    def barrage(pose, u):
        """Both claws raised to the sky to call the fire down, then flung out as it falls."""
        r.stand_legs(pose, 0.0, 0.0, width=0.46 * s, crouch=keyed(u, 0.05, 0.0, 0.15) * s)
        skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
        hunch(pose, keyed(u, 10.0, -16.0, 20.0), head=keyed(u, -4.0, -45.0, 0.0))
        pose.set("cape", r.q_euler(x=keyed(u, 2.0, 10.0, 20.0)))
        arms(pose, keyed(u, (-1.25, 1.45, 0.35), (-0.5, 4.4, 0.3), (-1.6, 2.6, 1.4)), keyed(u, (1.25, 1.45, 0.35), (0.5, 4.4, 0.3), (1.6, 2.6, 1.4)),
             claw_tilt=keyed(u, 30.0, -30.0, 80.0))

    def die(pose, u):
        collapse(pose, u, 10.0, 1.7 * s)
        skirt_flaps(pose, "robe_front", "robe_back", margin=2.0)
        pose.set("cape", r.q_euler(x=10.0 * u))

    return {"Idle": (IDLE_SECONDS, idle), "Walk": (gait.cycle, walk), "Die": (DIE_SECONDS, die), "LeapSlam": (1.0, leap), "Shockwave": (1.0, shockwave),
            "Summon": (1.0, summon), "Lunge": (1.0, lunge), "Barrage": (1.0, barrage)}


KING_GAIT = r.Gait(cycle=3.3, stance=0.55, lift=0.3, bob=0.05, hip_drop=0.3, width=0.34, lean=6.0)
UNBOUND_GAIT = r.Gait(cycle=3.3 * UNBOUND_SCALE, stance=0.55, lift=0.3 * UNBOUND_SCALE, bob=0.06, hip_drop=0.4, width=0.34 * UNBOUND_SCALE, lean=8.0)
KING_RIG = king_rig(1.0)
UNBOUND_RIG = king_rig(UNBOUND_SCALE)

HOLLOW_KING = EnemyModel("hollow_king.glb", KING_RIG, KING_GAIT, lambda rig: build_king(rig, 1.0, False), king_poses(1.0, KING_GAIT))
HOLLOW_KING_UNBOUND = EnemyModel("hollow_king_unbound.glb", UNBOUND_RIG, UNBOUND_GAIT, lambda rig: build_king(rig, UNBOUND_SCALE, True),
                                 king_poses(UNBOUND_SCALE, UNBOUND_GAIT))

ENEMIES = {"ghoul": GHOUL, "crossbow_ghoul": CROSSBOW_GHOUL, "ghoul_mage": GHOUL_MAGE, "brute": BRUTE, "hollow_king": HOLLOW_KING,
           "hollow_king_unbound": HOLLOW_KING_UNBOUND}
