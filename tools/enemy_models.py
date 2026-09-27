"""The enemies as skinned models with their clips, on the heroes' skeleton and maths (hero_rig.py). Written by make_placeholder_models.py. They are drawn as
animated crowds (the engine's SetSkinnedCrowd), each copy at its own clip and time, so a swarm of hundreds walks, attacks and dies.

Every enemy has
    Idle      standing, looping (IDLE_SECONDS)
    Walk      one stride, as many seconds long as it is metres, so the game moves it on by the ground the enemy covers and a planted foot stays put
    Die       falling, 1 s, held at the end
and a clip for each attack it has, named after its AttackType (Shoot, Lob, Lunge, LeapSlam, Shockwave, Summon, Barrage), one second long: the wind-up over the
first WINDUP_END of it, the blow (the attack's active time) up to ACTIVE_END, the recovery after. The game maps each phase's progress onto its stretch.
A held model (the Crossbow Ghoul's crossbow, the Ghoul Mage's flame, the Ghoul Tactician's bomb) is a model of its own on the same skeleton with the same clips, drawn in the same pose.
"""

import math

import hero_rig as r
from hero_models import face, skirt_flaps, spine_upright, limbs, crystal
from make_placeholder_models import Mesh, ghoul_bomb

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
# Ghoul Beast Rider: a ghoul in scraps of iron riding a fiendish beast - a great hunched hound, horned and red-eyed, bone spikes along its back - with a
# long spear it carries upright and levels like a lance to charge. The beast walks on four legs, each planted paw sliding back exactly as far as the body
# goes forward. Its hind legs are the skeleton's legs (thigh, shin, foot, which the walk tests hold to the ground), its forelegs the fore_ ones; the rider's
# body hangs from the spine, so it sits the beast's back.

class Skeleton(r.HeroRig):
    """A skeleton laid out joint by joint rather than a hero's: <joints> is (name, parent name, bind position), parents first. <hip_y> and <ankle_y> are
    the body's and the ankles' heights at rest."""

    def __init__(self, joints, hip_y, ankle_y, toe, heel):
        self.hip_y, self.ankle_y, self.toe, self.heel = hip_y, ankle_y, toe, heel
        self.names = [name for name, _, _ in joints]
        self.index = {name: i for i, name in enumerate(self.names)}
        self.parent = {name: parent for name, parent, _ in joints}
        self.bind = {name: position for name, _, position in joints}


BEAST_HIP, BEAST_LEG_TOP, BEAST_KNEE, BEAST_ANKLE = 1.1, 1.04, 0.56, 0.1
HIND_X, HIND_Z, FORE_X, FORE_Z = 0.22, -0.55, 0.24, 0.55
RIDER_SEAT = (0.0, 1.4, -0.08)
SPEAR_GRIP = (0.26, 1.34, -0.08)      # the right fist on the spear (the hand's middle at rest)


def _leg(prefix, side, x, z):
    sx = -1.0 if side == "l" else 1.0
    upper = f"{prefix}thigh_{side}"
    lower = f"{prefix}shin_{side}"
    return [(upper, "hips", (sx * x, BEAST_LEG_TOP, z)), (lower, upper, (sx * x, BEAST_KNEE, z)), (f"{prefix}foot_{side}", lower, (sx * x, BEAST_ANKLE, z))]


BEAST_JOINTS = ([("root", None, (0.0, 0.0, 0.0)), ("hips", "root", (0.0, BEAST_HIP, 0.0))]
                + _leg("", "l", HIND_X, HIND_Z) + _leg("", "r", HIND_X, HIND_Z) + _leg("fore_", "l", FORE_X, FORE_Z) + _leg("fore_", "r", FORE_X, FORE_Z)
                + [("neck", "hips", (0.0, 1.22, 0.72)), ("beast_head", "neck", (0.0, 1.42, 1.02)), ("jaw", "beast_head", (0.0, 1.34, 1.08)),
                   ("tail", "hips", (0.0, 1.16, -0.8))])

RIDER_RIG = Skeleton(
    BEAST_JOINTS
    + [("spine", "hips", RIDER_SEAT), ("chest", "spine", (0.0, 1.52, -0.08)), ("head", "chest", (0.0, 2.02, -0.06)),
       ("upper_arm_l", "chest", (-0.26, 1.94, -0.08)), ("forearm_l", "upper_arm_l", (-0.26, 1.66, -0.08)), ("hand_l", "forearm_l", (-0.26, 1.4, -0.08)),
       ("upper_arm_r", "chest", (0.26, 1.94, -0.08)), ("forearm_r", "upper_arm_r", (0.26, 1.66, -0.08)), ("hand_r", "forearm_r", (0.26, 1.4, -0.08))],
    hip_y=BEAST_HIP, ankle_y=BEAST_ANKLE, toe=0.16, heel=0.08)

# Each leg: its joints, where its paw stands at rest, and which way its middle joint bends (the hind legs' hocks back, the forelegs' knees forward).
BEAST_LEGS = (("thigh_l", "shin_l", "foot_l", (-HIND_X, BEAST_ANKLE, HIND_Z), (0.0, 0.0, -1.0)),
              ("thigh_r", "shin_r", "foot_r", (HIND_X, BEAST_ANKLE, HIND_Z), (0.0, 0.0, -1.0)),
              ("fore_thigh_l", "fore_shin_l", "fore_foot_l", (-FORE_X, BEAST_ANKLE, FORE_Z), (0.0, 0.0, 1.0)),
              ("fore_thigh_r", "fore_shin_r", "fore_foot_r", (FORE_X, BEAST_ANKLE, FORE_Z), (0.0, 0.0, 1.0)))
BEAST_REST = tuple(leg[3] for leg in BEAST_LEGS)

# One stride of the walk, over this many metres: each paw planted for STANCE of it, the hind left first, then the fore left, the hind right, the fore right.
BEAST_GAIT = r.Gait(cycle=1.4, stance=0.62, lift=0.14, bob=0.015, hip_drop=0.14, width=0.0, lean=0.0)
BEAST_STEPS = (0.0, 0.5, 0.25, 0.75)


def _beast_body(m, j, saddled):
    """The beast: a deep chest and a hump of shoulders, lean haunches, bone spikes along its back (before and behind the saddle, if <saddled>)."""
    m.joint = j["hips"]
    m.box(-0.28, 0.86, -0.82, 0.28, 1.28, 0.3, "brute_hide")
    m.box(-0.34, 0.8, 0.2, 0.34, 1.38, 0.82, "brute_hide")
    m.box(-0.3, 0.92, -0.86, 0.3, 1.24, -0.4, "brute_hide")
    m.box(-0.2, 1.36, 0.3, 0.2, 1.46, 0.8, "hood_dark")                          # the mane over its shoulders
    for z, height in ((0.66, 0.34), (0.46, 0.26), (-0.42, 0.22), (-0.62, 0.28)):
        top = 1.46 if z > 0.3 else 1.28
        m.pyramid(-0.05, top, z - 0.06, 0.05, top, z + 0.06, (0.0, top + height, z - 0.1), "bone")
    if saddled:
        m.box(-0.27, 1.27, -0.32, 0.27, 1.36, 0.18, "leather")                  # the saddle
        m.box(-0.06, 1.36, 0.1, 0.06, 1.46, 0.18, "leather")
        m.box(-0.29, 0.98, -0.2, -0.27, 1.28, 0.08, "leather")                  # its flaps
        m.box(0.27, 0.98, -0.2, 0.29, 1.28, 0.08, "leather")
    else:
        m.box(-0.12, 1.28, -0.3, 0.12, 1.36, 0.3, "hood_dark")                  # the mane running on down its back, and more spikes along it
        for z, height in ((0.22, 0.3), (0.0, 0.34), (-0.22, 0.28)):
            m.pyramid(-0.05, 1.36, z - 0.06, 0.05, 1.36, z + 0.06, (0.0, 1.36 + height, z - 0.1), "bone")
    for side in ("l", "r"):
        sx = -1.0 if side == "l" else 1.0
        for prefix, x, z in (("", HIND_X, HIND_Z), ("fore_", FORE_X, FORE_Z)):
            thick = 0.15 if prefix == "" else 0.12
            x0, x1 = sorted((sx * (x - thick), sx * (x + thick)))
            m.joint = j[f"{prefix}thigh_{side}"]
            m.box(x0, 0.52, z - thick * 1.4, x1, 1.08, z + thick * 1.4, "brute_hide")
            x0, x1 = sorted((sx * (x - 0.07), sx * (x + 0.07)))
            m.joint = j[f"{prefix}shin_{side}"]
            m.box(x0, 0.1, z - 0.07, x1, 0.58, z + 0.07, "brute_skin")
            x0, x1 = sorted((sx * (x - 0.09), sx * (x + 0.09)))
            m.joint = j[f"{prefix}foot_{side}"]
            m.box(x0, 0.0, z - 0.09, x1, 0.14, z + 0.17, "brute_hide")
            for dx in (-0.05, 0.0, 0.05):
                cx = sx * x + dx
                m.pyramid(cx - 0.018, 0.0, z + 0.15, cx + 0.018, 0.04, z + 0.17, (cx, 0.0, z + 0.25), "bone")   # claws
    m.joint = j["neck"]
    m.box(-0.18, 1.06, 0.58, 0.18, 1.42, 1.06, "brute_hide")
    m.box(-0.12, 1.42, 0.6, 0.12, 1.52, 0.98, "hood_dark")
    # Its head: a long skull and snout, red eyes, horns sweeping back, fangs; the jaw hinges open
    m.joint = j["beast_head"]
    m.box(-0.2, 1.3, 0.95, 0.2, 1.6, 1.36, "brute_hide")
    m.box(-0.12, 1.34, 1.36, 0.12, 1.48, 1.74, "brute_hide")
    m.box(-0.05, 1.44, 1.66, 0.05, 1.49, 1.745, "brute_skin")                    # the nose
    m.box(-0.17, 1.47, 1.36, -0.07, 1.53, 1.365, "ghoul_eye")
    m.box(0.07, 1.47, 1.36, 0.17, 1.53, 1.365, "ghoul_eye")
    for sx in (-1.0, 1.0):
        x0, x1 = sorted((sx * 0.08, sx * 0.18))
        m.pyramid(x0, 1.6, 1.02, x1, 1.6, 1.16, (sx * 0.32, 1.86, 0.78), "horn")
        for z in (1.5, 1.66):
            m.pyramid(sx * 0.09 - 0.015, 1.34, z - 0.015, sx * 0.09 + 0.015, 1.34, z + 0.015, (sx * 0.09, 1.25, z), "bone")   # fangs
    m.joint = j["jaw"]
    m.box(-0.11, 1.2, 1.08, 0.11, 1.31, 1.68, "brute_hide")
    for z in (1.42, 1.58):
        for sx in (-1.0, 1.0):
            m.pyramid(sx * 0.08 - 0.015, 1.31, z - 0.015, sx * 0.08 + 0.015, 1.31, z + 0.015, (sx * 0.08, 1.39, z), "bone")
    m.joint = j["tail"]
    m.box(-0.06, 1.09, -1.42, 0.06, 1.2, -0.78, "brute_hide")
    m.pyramid(-0.05, 1.1, -1.42, 0.05, 1.2, -1.4, (0.0, 1.15, -1.62), "bone")


def build_beast_rider_rigged(rig):
    m = Mesh()
    j = rig.index
    _beast_body(m, j, saddled=True)
    # The rider: its legs astride the saddle, feet in the stirrups, all on its seat
    m.joint = j["spine"]
    m.box(-0.19, 1.3, -0.2, 0.19, 1.52, 0.06, "ghoul_rags")
    for sx in (-1.0, 1.0):
        x0, x1 = sorted((sx * 0.2, sx * 0.34))
        m.box(x0, 1.26, -0.1, x1, 1.38, 0.24, "ghoul_skin")                      # thighs
        x0, x1 = sorted((sx * 0.3, sx * 0.39))
        m.box(x0, 0.86, 0.12, x1, 1.28, 0.24, "ghoul_skin")                      # shins down the beast's flanks
        m.box(x0 - 0.01, 0.8, 0.1, x1 + 0.01, 0.88, 0.34, "iron")                 # feet in the stirrups
    m.joint = j["chest"]
    m.box(-0.2, 1.5, -0.2, 0.2, 1.96, 0.08, "ghoul_skin")
    m.box(-0.21, 1.58, 0.06, 0.21, 1.92, 0.11, "iron")                           # a rusted breastplate
    m.box(-0.08, 1.6, -0.24, 0.08, 1.94, -0.2, "bone")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.2, 0.32, 1.64, -0.14, 1.96, -0.02, "ghoul_skin"),
                           ("forearm", 0.205, 0.315, 1.38, -0.135, 1.68, -0.025, "ghoul_skin"),
                           ("hand", 0.2, 0.32, 1.28, -0.14, 1.4, -0.02, "bone")])
        limbs(m, j, side, [("upper_arm", 0.17, 0.35, 1.88, -0.17, 2.0, 0.01, "iron")])   # pauldrons
    m.joint = j["head"]
    m.box(-0.12, 1.96, -0.18, 0.12, 2.2, 0.06, "ghoul_skin")
    m.box(-0.09, 2.08, 0.06, -0.03, 2.13, 0.065, "ghoul_eye")
    m.box(0.03, 2.08, 0.06, 0.09, 2.13, 0.065, "ghoul_eye")
    m.box(-0.14, 2.14, -0.2, 0.14, 2.26, 0.08, "iron")                            # a spiked helm
    m.pyramid(-0.05, 2.26, -0.1, 0.05, 2.26, 0.0, (0.0, 2.42, -0.07), "iron")
    # The spear, along +Z from the right fist: a long shaft, a jagged iron head, a rag of a pennant
    m.joint = j["hand_r"]
    gx, gy, gz = SPEAR_GRIP
    m.box(gx - 0.022, gy - 0.022, gz - 0.8, gx + 0.022, gy + 0.022, gz + 1.3, "wood")
    m.pyramid(gx - 0.05, gy - 0.012, gz + 1.3, gx + 0.05, gy + 0.012, gz + 1.36, (gx, gy, gz + 1.66), "steel")
    m.pyramid(gx - 0.07, gy - 0.01, gz + 1.36, gx + 0.07, gy + 0.01, gz + 1.4, (gx + 0.1, gy, gz + 1.3), "steel")
    m.box(gx - 0.005, gy + 0.02, gz + 0.95, gx + 0.005, gy + 0.2, gz + 1.25, "ghoul_rags")
    return m


def beast_legs(pose, hips_y, feet, pitch=0.0, roll=0.0):
    """The beast's body at <hips_y>, pitched <pitch> degrees (its nose down if positive) and rolled <roll>, its four paws at <feet> (the hind left and right,
    the fore left and right, in model space), each flat on the ground."""
    pose.set("hips", r.q_mul(r.q_axis((0, 0, 1), roll), r.q_axis((1, 0, 0), pitch)), (0.0, hips_y, 0.0))
    short = 0.0
    for (upper, lower, foot, _, bend), ankle in zip(BEAST_LEGS, feet):
        short = max(short, r.two_bone(pose, upper, lower, ankle, bend, (0.0, 0.0, 1.0)))
        pose.set_world(foot, r.Q_IDENTITY)
    pose.shortfall = max(getattr(pose, "shortfall", 0.0), short)


def beast_head(pose, neck, jaw, tail, turn=0.0, tail_turn=0.0):
    """The neck bowed <neck> degrees (and turned <turn>), the jaw open <jaw> degrees, the tail raised <tail> degrees."""
    pose.set("neck", r.q_euler(x=neck, y=turn))
    pose.set("beast_head", r.q_euler(x=-neck * 0.4))
    pose.set("jaw", r.q_euler(x=jaw))
    pose.set("tail", r.q_euler(x=tail, y=tail_turn))


# The spear carried upright at the rider's side, and levelled under its arm to charge: (the arm's way from the shoulder, its bend, the spear's tilt)
SPEAR_UP = ((0.3, -1.0, 0.45), 85.0, -78.0)
SPEAR_LEVEL = ((0.2, -0.7, -0.35), 80.0, 2.0)
REINS = ((0.12, -0.7, 0.85), 45.0)


def ride(pose, lean, spear, reins=REINS, head=0.0, turn=0.0, roll=0.0):
    """The rider sitting up in the saddle whatever the beast is doing, leaning <lean> degrees forward, the spear held as <spear> (see SPEAR_UP), the left
    hand on the reins."""
    pose.set_world("spine", r.q_euler(x=lean, z=roll))
    pose.set_world("head", r.q_euler(x=head, y=turn, z=roll * 0.5))
    way, bend, tilt = spear
    r.aim_arm(pose, "r", way, bend=bend, elbow_pole=(1.0, -0.4, -0.8))
    face(pose, "hand_r", (0.0, 0.0, 1.0), tilt=tilt)
    way, bend = reins
    r.aim_arm(pose, "l", way, bend=bend, elbow_pole=(-1.0, -0.6, -0.4))
    face(pose, "hand_l", (0.0, 0.0, 1.0), tilt=60.0)


def _blend_spear(a, b, t):
    return r.v_lerp(a[0], b[0], t), a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t


def _blend_feet(a, b, t):
    return tuple(r.v_lerp(p, q, t) for p, q in zip(a, b))


def rider_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.5)
    beast_legs(pose, BEAST_HIP - 0.13 + 0.01 * breath, BEAST_REST, pitch=0.6 * breath)
    beast_head(pose, 12.0 + 3.0 * breath, 5.0 + 7.0 * max(0.0, math.sin(2 * math.pi * t / 0.75)), 14.0,
               turn=14.0 * math.sin(2 * math.pi * t / IDLE_SECONDS), tail_turn=22.0 * math.sin(2 * math.pi * t / 1.0))
    ride(pose, 6.0 + breath, SPEAR_UP, head=-4.0, turn=16.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 1.5))


def walk_feet(u):
    feet = []
    for (_, _, _, (x, y, z), _), step in zip(BEAST_LEGS, BEAST_STEPS):
        along, height, _ = BEAST_GAIT.foot(u + step)
        feet.append((x, y + height, z + along))
    return tuple(feet)


def rider_walk(pose, u):
    bob = math.cos(8 * math.pi * u)
    beast_legs(pose, BEAST_HIP - BEAST_GAIT.hip_drop + BEAST_GAIT.bob * bob, walk_feet(u), pitch=1.5 * math.sin(4 * math.pi * u),
               roll=2.0 * math.sin(2 * math.pi * u))
    beast_head(pose, 10.0 + 4.0 * math.sin(4 * math.pi * u), 8.0, 16.0, tail_turn=18.0 * math.sin(2 * math.pi * u))
    ride(pose, 8.0 + 1.5 * bob, SPEAR_UP, head=-6.0)


def _gallop(g):
    """The charge: a bounding gallop, <g> (0 to 1) through one bound - the forelegs reaching out while the hind legs kick back, then gathered under."""
    c, s = math.cos(2 * math.pi * g), math.sin(2 * math.pi * g)
    feet = []
    for i, (_, _, _, (x, y, z), _) in enumerate(BEAST_LEGS):
        fore = i >= 2
        lag = 0.12 if i % 2 else 0.0                   # the right legs a little behind the left
        cl, sl = math.cos(2 * math.pi * (g - lag)), math.sin(2 * math.pi * (g - lag))
        reach = (0.42 * cl) if fore else (-0.4 * cl)
        lift = 0.2 * max(0.0, sl if fore else -sl)
        feet.append((x, y + lift, z + reach))
    return BEAST_HIP - 0.2 + 0.05 * abs(s), 6.0 * s, tuple(feet)


CROUCH = (BEAST_HIP - 0.32, -9.0, ((-HIND_X, BEAST_ANKLE, HIND_Z + 0.16), (HIND_X, BEAST_ANKLE, HIND_Z + 0.16),
                                   (-FORE_X, BEAST_ANKLE, FORE_Z - 0.04), (FORE_X, BEAST_ANKLE, FORE_Z - 0.04)))


def rider_lunge(pose, u):
    """The beast crouching back on its haunches, head low and snarling, a forepaw raking the ground, while the rider levels its spear; then the charge at a
    bounding gallop, spear couched; then pulling up and settling."""
    name, k = stage(u)
    stand = (BEAST_HIP - 0.13, 0.0, BEAST_REST)
    if name == "windup":
        e = r.ease(k)
        hips_y, pitch, feet = stand[0] + (CROUCH[0] - stand[0]) * e, CROUCH[1] * e, list(_blend_feet(BEAST_REST, CROUCH[2], e))
        rake = math.sin(2 * math.pi * 3.0 * k) * e
        x, y, z = feet[3]
        feet[3] = (x, y + 0.07 * max(0.0, rake), z + 0.14 * rake)          # pawing at the ground
        beast_legs(pose, hips_y, feet, pitch)
        beast_head(pose, 12.0 + 20.0 * e, 6.0 + 30.0 * e, 14.0 + 30.0 * e, tail_turn=8.0 * math.sin(2 * math.pi * 4.0 * k))
        ride(pose, 6.0 + 22.0 * e, _blend_spear(SPEAR_UP, SPEAR_LEVEL, e), head=-4.0 + 10.0 * e)
    elif name == "active":
        hips_y, pitch, feet = _gallop((k * 2.0) % 1.0)
        into = r.ease(min(1.0, k / 0.15))                                 # springing out of the crouch
        beast_legs(pose, CROUCH[0] + (hips_y - CROUCH[0]) * into, _blend_feet(CROUCH[2], feet, into), CROUCH[1] + (pitch - CROUCH[1]) * into)
        beast_head(pose, 26.0, 36.0, 4.0)
        ride(pose, 30.0, SPEAR_LEVEL, head=8.0)
    else:
        e = r.ease(k)
        hips_y, pitch, feet = _gallop(0.0)
        skid = math.sin(math.pi * k)
        beast_legs(pose, hips_y + (stand[0] - hips_y) * e, _blend_feet(feet, BEAST_REST, e), pitch * (1.0 - e) - 8.0 * skid)
        beast_head(pose, 26.0 - 14.0 * e, 36.0 - 28.0 * e, 4.0 + 10.0 * e)
        ride(pose, 30.0 - 24.0 * e - 10.0 * skid, _blend_spear(SPEAR_LEVEL, SPEAR_UP, e), head=8.0 - 12.0 * e)


def rider_die(pose, u):
    """The beast's legs buckle and it drops on its belly, head down; the rider slumps over sideways and the spear falls."""
    k = r.ease(min(1.0, u / 0.7))
    splay = tuple((x * (1.0 + 0.5 * k), y, z + (0.3 if z > 0 else -0.25) * k) for x, y, z in BEAST_REST)
    beast_legs(pose, BEAST_HIP - 0.13 - 0.52 * k, splay, pitch=10.0 * k, roll=10.0 * k)
    beast_head(pose, 12.0 + 22.0 * k, 5.0 + 20.0 * k, 14.0 - 30.0 * k)
    ride(pose, 6.0 + 55.0 * k, _blend_spear(SPEAR_UP, ((0.5, -0.8, 0.3), 20.0, 70.0), k), reins=((0.4, -1.0, 0.4), 10.0 + 30.0 * (1.0 - k)),
         head=-4.0 + 40.0 * k, roll=25.0 * k)


BEAST_RIDER = EnemyModel("beast_rider.glb", RIDER_RIG, BEAST_GAIT, build_beast_rider_rigged,
                         {"Idle": (IDLE_SECONDS, rider_idle), "Walk": (BEAST_GAIT.cycle, rider_walk), "Die": (DIE_SECONDS, rider_die),
                          "Lunge": (1.0, rider_lunge)})


# =========================================================================================================================================================
# Ghoul Beast: the rider's fiendish hound running wild, with no one on its back - it stalks, circling low with its head down, bolts when it is looked at
# or hurt, and pounces when it has you from behind.

BEAST_RIG = Skeleton(BEAST_JOINTS, hip_y=BEAST_HIP, ankle_y=BEAST_ANKLE, toe=0.16, heel=0.08)


def build_ghoul_beast_rigged(rig):
    m = Mesh()
    _beast_body(m, rig.index, saddled=False)
    return m


PROWL = 0.2      # how much lower than standing it carries its body, stalking


def beast_idle(pose, u):
    """Crouched low, head down and sniffing, swinging to look about; the tail low and twitching."""
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.5)
    sniff = max(0.0, math.sin(2 * math.pi * t / 0.75))
    beast_legs(pose, BEAST_HIP - PROWL + 0.01 * breath, BEAST_REST, pitch=2.0 + 0.6 * breath)
    beast_head(pose, 24.0 + 4.0 * sniff, 4.0 + 8.0 * max(0.0, math.sin(2 * math.pi * t / 1.5 + 1.0)), -4.0,
               turn=22.0 * math.sin(2 * math.pi * t / IDLE_SECONDS), tail_turn=10.0 * math.sin(2 * math.pi * t / 0.75))


def beast_walk(pose, u):
    """Prowling: low, head down and level, the tail out straight behind."""
    bob = math.cos(8 * math.pi * u)
    beast_legs(pose, BEAST_HIP - PROWL - 0.02 + BEAST_GAIT.bob * bob, walk_feet(u), pitch=2.0 + 1.5 * math.sin(4 * math.pi * u),
               roll=2.0 * math.sin(2 * math.pi * u))
    beast_head(pose, 22.0 + 3.0 * math.sin(4 * math.pi * u), 6.0, 0.0, tail_turn=12.0 * math.sin(2 * math.pi * u))


BEAST_COIL = (BEAST_HIP - 0.4, -6.0, ((-HIND_X, BEAST_ANKLE, HIND_Z + 0.22), (HIND_X, BEAST_ANKLE, HIND_Z + 0.22),
                                      (-FORE_X, BEAST_ANKLE, FORE_Z - 0.06), (FORE_X, BEAST_ANKLE, FORE_Z - 0.06)))


def beast_pounce(pose, u):
    """Coiling down on its haunches, jaws opening; the spring - up off the ground, forelegs reaching, hind legs thrown back; landing and gathering itself."""
    name, k = stage(u)
    prowl = (BEAST_HIP - PROWL, 2.0, BEAST_REST)
    if name == "windup":
        e = r.ease(k)
        beast_legs(pose, prowl[0] + (BEAST_COIL[0] - prowl[0]) * e, _blend_feet(BEAST_REST, BEAST_COIL[2], e), prowl[1] + (BEAST_COIL[1] - prowl[1]) * e)
        beast_head(pose, 24.0 - 6.0 * e, 6.0 + 26.0 * e, -4.0 + 10.0 * e, tail_turn=6.0 * math.sin(2 * math.pi * 5.0 * k))
    elif name == "active":
        arc = math.sin(math.pi * k)                                        # up and down again over the leap
        stretch = r.ease(min(1.0, k / 0.35)) * (1.0 - r.ease(max(0.0, (k - 0.7) / 0.3)))
        hips_y = BEAST_COIL[0] + (BEAST_HIP - 0.1 - BEAST_COIL[0]) * r.ease(min(1.0, k / 0.25)) + 0.45 * arc
        feet = []
        for (x, y, z), fore in zip(BEAST_REST, (False, False, True, True)):
            reach = (0.5 if fore else -0.45) * stretch
            feet.append((x, y + 0.45 * arc + (0.1 if fore else 0.05) * stretch, z + reach))
        beast_legs(pose, hips_y, feet, -8.0 * stretch + 10.0 * r.ease(max(0.0, (k - 0.6) / 0.4)))
        beast_head(pose, 6.0, 40.0, 4.0)
    else:
        e = r.ease(k)
        land = (BEAST_HIP - 0.34, 6.0, ((-HIND_X, BEAST_ANKLE, HIND_Z - 0.1), (HIND_X, BEAST_ANKLE, HIND_Z - 0.1),
                                        (-FORE_X, BEAST_ANKLE, FORE_Z + 0.2), (FORE_X, BEAST_ANKLE, FORE_Z + 0.2)))
        beast_legs(pose, land[0] + (prowl[0] - land[0]) * e, _blend_feet(land[2], BEAST_REST, e), land[1] + (prowl[1] - land[1]) * e)
        beast_head(pose, 6.0 + 18.0 * e, 40.0 - 34.0 * e, 4.0 - 8.0 * e)


def beast_die(pose, u):
    k = r.ease(min(1.0, u / 0.7))
    splay = tuple((x * (1.0 + 0.5 * k), y, z + (0.3 if z > 0 else -0.25) * k) for x, y, z in BEAST_REST)
    beast_legs(pose, BEAST_HIP - PROWL - 0.45 * k, splay, pitch=10.0 * k, roll=14.0 * k)
    beast_head(pose, 24.0 + 10.0 * k, 4.0 + 24.0 * k, -4.0 - 20.0 * k)


GHOUL_BEAST = EnemyModel("ghoul_beast.glb", BEAST_RIG, BEAST_GAIT, build_ghoul_beast_rigged,
                         {"Idle": (IDLE_SECONDS, beast_idle), "Walk": (BEAST_GAIT.cycle, beast_walk), "Die": (DIE_SECONDS, beast_die),
                          "Lunge": (1.0, beast_pounce)})


# =========================================================================================================================================================
# Ghoul Tactician: a ghoul in a tattered officer's coat and a bicorne, gold at the shoulders, one eye a ghostly green lens, a bandolier of bombs across its
# chest and a satchel at its hip. A bomb glows in its right fist (the bomb is its own model, gone once it is thrown and drawn fresh from the satchel after).

BOMB_AT = (0.27, 0.46, 0.08)
TACTICIAN_RIG = r.HeroRig(hip_y=0.64, spine_y=0.72, chest_y=0.8, neck_y=1.2, shoulder=(0.27, 1.14), elbow_y=0.86, wrist_y=0.58, leg_x=0.13, knee_y=0.35,
                          ankle_y=0.08, toe=0.14, heel=0.06,
                          extra=(("coat_front", "hips", (0.0, 0.62, 0.13)), ("coat_back", "hips", (0.0, 0.62, -0.13)), ("bomb", "hand_r", BOMB_AT)))


def build_ghoul_tactician_rigged(rig):
    m = Mesh()
    j = rig.index
    _ghoul_legs_and_body(m, j, rags="king_robe")
    m.box(-0.31, 0.5, -0.06, -0.21, 0.7, 0.12, "leather")                       # the satchel, and the green glow leaking from under its flap
    m.box(-0.315, 0.66, -0.03, -0.3, 0.69, 0.09, "ghost_eye")
    m.joint = j["coat_front"]
    m.box(-0.23, 0.3, 0.12, 0.23, 0.62, 0.16, "king_robe")
    m.joint = j["coat_back"]
    m.box(-0.23, 0.2, -0.16, 0.23, 0.62, -0.11, "king_robe")
    m.joint = j["chest"]
    m.box(-0.25, 0.7, -0.13, 0.25, 1.18, 0.15, "king_robe")
    for y in (0.82, 0.92, 1.02):
        for x in (-0.07, 0.07):
            m.box(x - 0.018, y - 0.018, 0.15, x + 0.018, y + 0.018, 0.165, "gold")
    for i in range(6):                                                           # the bandolier, shoulder to hip, bombs in it
        t = i / 5.0
        x, y = -0.19 + 0.36 * t, 1.13 - 0.38 * t
        m.box(x - 0.04, y - 0.035, 0.15, x + 0.04, y + 0.035, 0.18, "leather")
        if i % 2 == 1:
            m.box(x - 0.035, y - 0.035, 0.18, x + 0.035, y + 0.035, 0.24, "bomb_black")
            m.box(x - 0.037, y - 0.008, 0.18, x + 0.037, y + 0.008, 0.245, "ghost_eye")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.21, 0.33, 0.84, -0.065, 1.16, 0.065, "king_robe"),
                           ("forearm", 0.21, 0.33, 0.6, -0.06, 0.88, 0.06, "king_robe"),
                           ("hand", 0.21, 0.33, 0.46, -0.04, 0.6, 0.07, "ghoul_skin"),
                           ("upper_arm", 0.18, 0.36, 1.1, -0.09, 1.2, 0.09, "gold")])   # epaulettes
    m.joint = j["head"]
    m.box(-0.13, 1.18, -0.1, 0.13, 1.44, 0.14, "ghoul_skin")
    m.box(-0.10, 1.31, 0.14, -0.03, 1.36, 0.145, "ghoul_eye")
    m.box(0.02, 1.29, 0.14, 0.11, 1.38, 0.15, "gold")                            # the lens and its rim
    m.box(0.035, 1.305, 0.15, 0.095, 1.365, 0.155, "ghost_eye")
    m.box(-0.10, 1.14, -0.04, 0.10, 1.2, 0.14, "bone")
    m.box(-0.24, 1.42, -0.08, 0.24, 1.52, 0.1, "hood_dark")                      # the bicorne
    m.pyramid(-0.24, 1.52, -0.08, 0.24, 1.52, 0.1, (0.0, 1.68, 0.01), "hood_dark")
    m.box(-0.245, 1.42, -0.085, 0.245, 1.445, 0.105, "gold")
    return m


def build_tactician_bomb_rigged(rig):
    """The bomb in the Tactician's right fist, on its own joint."""
    m = Mesh()
    m.joint = rig.index["bomb"]
    x, y, z = rig.bind["bomb"]
    ghoul_bomb(m, x, y, z, 0.12)
    return m


def _hold_bomb(pose, wrist, tilt=30.0, elbow_pole=(1.0, -0.6, -0.4)):
    r.arm_ik(pose, "r", wrist, elbow_pole=elbow_pole)
    face(pose, "hand_r", (0.0, 0.0, 1.0), tilt=tilt)


BOMB_READY = (0.24, 0.8, 0.28)


def tactician_idle(pose, u):
    """Tossing the bomb idly in its right hand, the left behind its back, looking over the field."""
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.5)
    toss = max(0.0, math.sin(2 * math.pi * t / 1.5))
    r.stand_legs(pose, breath, 0.4 * math.sin(2 * math.pi * t / IDLE_SECONDS), width=0.17, crouch=0.05)
    hunch(pose, 16.0 + 2.0 * breath, head=-8.0, head_turn=16.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 0.7))
    skirt_flaps(pose, "coat_front", "coat_back", margin=1.0)
    _hold_bomb(pose, r.v_add(BOMB_READY, (0.0, 0.07 * toss, 0.0)), tilt=30.0 - 20.0 * toss)
    r.arm_ik(pose, "l", (-0.12, 0.74, -0.2), elbow_pole=(-1.0, 0.0, -0.3))


def tactician_walk(pose, u):
    _, swing = r.run_legs(pose, GHOUL_GAIT, u, 0.0)
    bob = math.cos(4 * math.pi * u)
    hunch(pose, 22.0 + 2.0 * bob, head=-12.0, turn=-4.0 * swing)
    skirt_flaps(pose, "coat_front", "coat_back", margin=2.0, give=0.6)
    _hold_bomb(pose, r.v_add(BOMB_READY, (0.0, 0.02 * bob, 0.06 * swing)))
    r.aim_arm(pose, "l", (-0.15, -1.0, -0.35 * swing), bend=20.0, elbow_pole=(-0.3, 0.0, -1.0))


def tactician_lob(pose, u):
    """Pointing out the target with its left hand while the right draws the bomb back over its shoulder, the bomb swelling with green light; then the lob,
    up and over; then a new bomb from the satchel."""
    name, k = stage(u)
    r.stand_legs(pose, 0.0, keyed(u, 0.0, -0.7, 0.8), width=0.2, crouch=0.06)
    hunch(pose, keyed(u, 18.0, 2.0, 30.0, settle=24.0), head=keyed(u, -8.0, -14.0, -2.0), turn=keyed(u, 0.0, -16.0, 12.0))
    skirt_flaps(pose, "coat_front", "coat_back", margin=2.0)
    _hold_bomb(pose, keyed(u, BOMB_READY, (0.34, 1.46, -0.28), (0.2, 1.3, 0.5), settle=(-0.04, 0.76, 0.2)),
               tilt=keyed(u, 30.0, -50.0, 80.0, settle=60.0), elbow_pole=(1.0, -0.2, -0.8))
    r.aim_arm(pose, "l", keyed(u, (-0.2, -0.9, 0.3), (-0.05, 0.3, 1.0), (-0.2, -0.5, 0.6)), bend=keyed(u, 25.0, 4.0, 25.0),
              elbow_pole=(-1.0, -0.5, -0.5))
    size = {"windup": 1.0 + 0.2 * r.ease(k), "active": 1.2 * (1.0 - r.ease(min(1.0, k * 4.0))), "recover": r.ease((k - 0.3) / 0.5)}[name]
    s = max(0.02, size)
    return {"bomb": (s, s, s)}


def tactician_die(pose, u):
    collapse(pose, u, 18.0, 0.56)
    skirt_flaps(pose, "coat_front", "coat_back", margin=3.0)


GHOUL_TACTICIAN = EnemyModel("ghoul_tactician.glb", TACTICIAN_RIG, GHOUL_GAIT, build_ghoul_tactician_rigged,
                             {"Idle": (IDLE_SECONDS, tactician_idle), "Walk": (GHOUL_GAIT.cycle, tactician_walk), "Die": (DIE_SECONDS, tactician_die),
                              "Lob": (1.0, tactician_lob)},
                             held=(("ghoul_tactician_bomb.glb", build_tactician_bomb_rigged),), scaled=("bomb",))


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

ENEMIES = {"ghoul": GHOUL, "crossbow_ghoul": CROSSBOW_GHOUL, "ghoul_mage": GHOUL_MAGE, "beast_rider": BEAST_RIDER, "ghoul_beast": GHOUL_BEAST, "ghoul_tactician": GHOUL_TACTICIAN,
           "brute": BRUTE, "hollow_king": HOLLOW_KING, "hollow_king_unbound": HOLLOW_KING_UNBOUND}
