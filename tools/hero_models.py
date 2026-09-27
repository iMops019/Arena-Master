"""The playable heroes as skinned models with their clips (see hero_rig.py for the skeleton and the run cycle's maths). Written by make_placeholder_models.py.

Every hero has the same moving clips, so the game drives them all one way:
    Idle                  standing, looping (IDLE_SECONDS)
    Run, RunRightAhead, RunRight, RunLeftAhead, RunLeft
                          one stride running ahead, 45 and 90 degrees to the right and to the left (the body still faces ahead); each clip is as many seconds
                          long as the stride is metres, so the game moves its time on by the distance covered and a planted foot stays put. Running
                          backward plays them backward.
    Air                   in the air (a jump or a fall), held
and each class adds its own (the Ranger's Shoot). An attack clip is laid over the chest and everything under it while the legs go on with the rest.
"""

import math

import hero_rig as r
from make_placeholder_models import Mesh

IDLE_SECONDS = 4.0

# The run clips and the heading each runs toward (degrees, + to the right). Must match HeroMotion.Runs in the game.
RUNS = (("RunLeft", -90.0), ("RunLeftAhead", -45.0), ("Run", 0.0), ("RunRightAhead", 45.0), ("RunRight", 90.0))

HIDDEN = (0.001, 0.001, 0.001)
SHOWN = (1.0, 1.0, 1.0)


class Hero:
    """A hero's model: its rig, a builder for its mesh, and its pose functions."""

    def __init__(self, name, file, rig, mesh, gait, idle, run, air, extra_clips=None):
        self.name, self.file, self.rig, self._mesh, self.gait = name, file, rig, mesh, gait
        self._idle, self._run, self._air, self._extra = idle, run, air, extra_clips or {}

    def mesh(self):
        return self._mesh(self.rig)

    def clips(self):
        """{clip name: (seconds, pose function of the time)}; each pose gives every joint (rotation, translation, scale) as write_skinned_glb wants."""
        clips = {"Idle": (IDLE_SECONDS, lambda t: self._frame(self._idle, t / IDLE_SECONDS))}
        for clip, heading in RUNS:
            clips[clip] = (self.gait.cycle, lambda t, h=heading: self._frame(lambda pose, phase: self._run(pose, phase, h), t / self.gait.cycle))
        clips["Air"] = (1.0, lambda t: self._frame(self._air, t))
        for clip, (seconds, pose) in self._extra.items():
            clips[clip] = (seconds, lambda t, p=pose, s=seconds: self._frame(p, t / s))
        return clips

    def _frame(self, pose_fn, u):
        pose = r.Pose(self.rig)
        scales = pose_fn(pose, u) or {}
        frame = pose.frame()
        return {name: (rotation, translation, scales.get(name, SHOWN) if name in self.scaled else None)
                for name, (rotation, translation) in frame.items()}

    @property
    def scaled(self):
        """The joints whose size a clip can change (an arrow that comes and goes); the rest have no scale channel."""
        return getattr(self, "_scaled", ())


def spine_upright(pose, lean=0.0, turn=0.0):
    """Sets the spine to face ahead (+Z) whatever the hips are doing, leaning forward <lean> degrees and turned <turn>."""
    pose.set_world("spine", r.q_euler(x=lean, y=turn))


# =========================================================================================================================================================
# The Ranger: a hooded archer in green, a cape, a quiver on the back, and a longbow in the left hand

RANGER_RIG = r.HeroRig(hip_y=0.86, spine_y=0.94, chest_y=1.0, neck_y=1.44, shoulder=(0.29, 1.38), elbow_y=1.12, wrist_y=0.87, leg_x=0.11, knee_y=0.47,
                       ankle_y=0.1, extra=(("cape", "chest", (0.0, 1.40, -0.17)),
                                           ("nock", "hand_l", (-0.29, 0.82, -0.11)),
                                           ("arrow", "nock", (-0.29, 0.82, -0.11))))
RANGER_GAIT = r.Gait(cycle=2.6, stance=0.34, lift=0.2, bob=0.035, hip_drop=0.1, width=0.09, lean=9.0)

# The bow, held in the left hand (the grip's middle at the fist), its tips toward the archer (-Z) and its string between them.
BOW_GRIP = (-0.29, 0.82, 0.0)
BOW_TOP, BOW_BOTTOM = (-0.29, 1.40, -0.11), (-0.29, 0.24, -0.11)
ARROW_LENGTH = 0.74


def build_ranger_hero(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        sx = -1.0 if side == "l" else 1.0
        x0, x1 = sorted((sx * 0.03, sx * 0.19))
        m.joint = j[f"foot_{side}"]                                              # boots
        m.box(x0, 0.0, -0.08, x1, 0.15, 0.13, "leather")
        m.box(x0 - 0.005, 0.12, -0.085, x1 + 0.005, 0.17, 0.085, "dark")         # the boot's cuff
        m.joint = j[f"shin_{side}"]
        m.box(x0 + 0.01, 0.13, -0.07, x1 - 0.01, 0.5, 0.07, "trousers")
        m.joint = j[f"thigh_{side}"]
        m.box(x0 + 0.005, 0.45, -0.075, x1 - 0.005, 0.86, 0.075, "trousers")
    m.joint = j["hips"]                                                           # the tunic's skirt and the belt
    m.box(-0.23, 0.70, -0.13, 0.23, 0.92, 0.13, "tunic")
    m.box(-0.24, 0.84, -0.14, 0.24, 0.92, 0.14, "leather")
    m.box(-0.04, 0.85, 0.14, 0.04, 0.91, 0.15, "gold")
    m.joint = j["chest"]                                                          # the tunic, the quiver on the back
    m.box(-0.23, 0.9, -0.13, 0.23, 1.42, 0.13, "tunic")
    m.box(-0.235, 1.0, -0.135, 0.235, 1.05, 0.135, "leather")                    # a strap across
    m.box(0.06, 0.95, -0.30, 0.18, 1.50, -0.19, "leather")
    for dx in (0.08, 0.12):
        m.box(dx, 1.50, -0.28, dx + 0.035, 1.62, -0.24, "fletching")
    m.joint = j["cape"]
    m.box(-0.24, 0.62, -0.20, 0.24, 1.42, -0.15, "hood")
    m.joint = j["head"]                                                           # head, eyes, hood
    m.box(-0.12, 1.44, -0.12, 0.12, 1.70, 0.12, "skin")
    m.box(-0.08, 1.57, 0.12, -0.03, 1.61, 0.125, "dark")
    m.box(0.03, 1.57, 0.12, 0.08, 1.61, 0.125, "dark")
    m.box(-0.15, 1.42, -0.16, 0.15, 1.73, -0.02, "hood")
    m.box(-0.15, 1.62, -0.02, 0.15, 1.73, 0.13, "hood")
    m.box(-0.155, 1.44, -0.02, -0.12, 1.66, 0.1, "hood")
    m.box(0.12, 1.44, -0.02, 0.155, 1.66, 0.1, "hood")
    m.pyramid(-0.15, 1.73, -0.16, 0.15, 1.73, 0.13, (0.0, 1.90, -0.12), "hood")
    for side in ("l", "r"):
        sx = -1.0 if side == "l" else 1.0
        x0, x1 = sorted((sx * 0.23, sx * 0.35))
        m.joint = j[f"upper_arm_{side}"]
        m.box(x0, 1.1, -0.07, x1, 1.43, 0.07, "tunic")
        m.joint = j[f"forearm_{side}"]
        m.box(x0 + 0.005, 0.87, -0.065, x1 - 0.005, 1.14, 0.065, "tunic")
        m.box(x0, 0.87, -0.07, x1, 0.97, 0.07, "leather")                        # a bracer
        m.joint = j[f"hand_{side}"]
        m.box(x0 + 0.01, 0.76, -0.05, x1 - 0.01, 0.87, 0.05, "skin")
    # The longbow in the left hand: a wrapped grip and two limbs curving back to the tips
    m.joint = j["hand_l"]
    gx, gy, gz = BOW_GRIP
    m.box(gx - 0.025, gy - 0.09, gz - 0.03, gx + 0.025, gy + 0.09, gz + 0.03, "leather")
    for sign in (1.0, -1.0):
        mid = (gx, gy + sign * 0.33, gz - 0.035)
        tip = BOW_TOP if sign > 0 else BOW_BOTTOM
        m.beam((gx, gy + sign * 0.08, gz), mid, 0.022, "wood", j["hand_l"], j["hand_l"])
        m.beam(mid, tip, 0.017, "wood", j["hand_l"], j["hand_l"])
    # The string, from each tip to the nock: it stretches back with the drawing hand
    nock = rig.bind["nock"]
    m.beam(BOW_TOP, nock, 0.006, "fletching", j["hand_l"], j["nock"])
    m.beam(nock, BOW_BOTTOM, 0.006, "fletching", j["nock"], j["hand_l"])
    # The arrow on the string, pointing +Z from the nock: shaft, fletching and head
    m.joint = j["arrow"]
    ax, ay, az = rig.bind["arrow"]
    m.box(ax - 0.01, ay - 0.01, az, ax + 0.01, ay + 0.01, az + ARROW_LENGTH, "wood")
    m.box(ax - 0.003, ay - 0.035, az + 0.02, ax + 0.003, ay + 0.035, az + 0.14, "fletching")
    m.box(ax - 0.035, ay - 0.003, az + 0.02, ax + 0.035, ay + 0.003, az + 0.14, "fletching")
    m.pyramid(ax - 0.022, ay - 0.022, az + ARROW_LENGTH, ax + 0.022, ay - 0.022, az + ARROW_LENGTH + 0.001, (ax, ay, az + ARROW_LENGTH + 0.08), "steel")
    return m


def _ranger_bow_hand(pose, ahead, cant, tip_forward=0.0):
    """The bow upright in the left hand, its belly toward <ahead>, the top tip canted <cant> degrees toward +X and tipped <tip_forward> forward."""
    yaw = math.degrees(math.atan2(ahead[0], ahead[2]))
    pose.set_world("hand_l", r.q_mul(r.q_axis((0, 1, 0), yaw), r.q_mul(r.q_axis((1, 0, 0), tip_forward), r.q_axis((0, 0, 1), -cant))))


def _ranger_rest_string(pose):
    """The string's own place (not drawn): the nock at its bind offset from the hand, the arrow put away."""
    pose.set("nock", r.Q_IDENTITY, r.v_sub(RANGER_RIG.bind["nock"], RANGER_RIG.bind["hand_l"]))
    pose.set("arrow", r.Q_IDENTITY, (0.0, 0.0, 0.0))
    return {"arrow": HIDDEN}


def ranger_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 2.0)
    shift = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.6 * shift)
    spine_upright(pose, lean=2.0 + 1.2 * breath, turn=-3.0 * shift)
    pose.set("chest", r.q_euler(x=-1.0 * breath))
    pose.set_world("head", r.q_euler(x=3.0, y=12.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 1.0)))
    pose.set("cape", r.q_euler(x=3.0 + 1.5 * breath))
    # The bow held out in front, low and ready, the right hand relaxed by the side
    r.aim_arm(pose, "l", (-0.25, -0.75, 0.6), bend=40.0, elbow_pole=(-1.0, -0.3, -0.4))
    _ranger_bow_hand(pose, (0.3, 0.0, 1.0), cant=12.0, tip_forward=38.0)
    r.aim_arm(pose, "r", (0.18, -1.0, 0.1 + 0.03 * breath), bend=18.0, elbow_pole=(0.2, 0.0, -1.0))
    pose.set_world("hand_r", r.q_euler(y=-10.0))
    return _ranger_rest_string(pose)


def ranger_run(pose, phase, heading):
    turn, swing = r.run_legs(pose, RANGER_GAIT, phase, heading)
    bounce = math.cos(4 * math.pi * phase)
    spine_upright(pose, lean=7.0, turn=-4.0 * swing)
    pose.set("chest", r.q_euler(x=1.0 * bounce))
    pose.set_world("head", r.q_euler(x=-3.0))
    pose.set("cape", r.q_euler(x=20.0 + 5.0 * math.sin(4 * math.pi * phase + 0.8)))
    # The bow carried out in front at an angle, swinging a little with the stride; the right arm pumps
    r.aim_arm(pose, "l", (-0.3, -0.6, 0.72 - 0.25 * swing), bend=50.0 - 10.0 * swing, elbow_pole=(-1.0, -0.4, -0.3))
    _ranger_bow_hand(pose, (0.3, 0.0, 1.0), cant=10.0, tip_forward=45.0 - 6.0 * swing)
    r.aim_arm(pose, "r", (0.12, -0.8, 0.62 * swing), bend=75.0 + 15.0 * swing, elbow_pole=(0.2, 0.0, -1.0))
    pose.set_world("hand_r", r.q_euler(x=-20.0 * max(0.0, swing)))
    return _ranger_rest_string(pose)


def ranger_air(pose, u):
    r.airborne_legs(pose)
    spine_upright(pose, lean=4.0)
    pose.set_world("head", r.q_euler(x=-2.0))
    pose.set("cape", r.q_euler(x=35.0))
    r.aim_arm(pose, "l", (-0.6, -0.3, 0.75), bend=35.0, elbow_pole=(-1.0, -0.3, -0.4))
    _ranger_bow_hand(pose, (0.3, 0.0, 1.0), cant=40.0)
    r.aim_arm(pose, "r", (0.7, -0.2, -0.3), bend=40.0, elbow_pole=(0.2, 0.0, -1.0))
    return _ranger_rest_string(pose)


# The Shoot clip, over one shot's time (0: the arrow has just left; 1: the next is about to): follow through, bring the bow in and nock, push the bow
# out while drawing to the cheek, hold, loose.
SHOOT_ANCHOR = (0.075, 1.51, 0.09)                  # where the drawing hand comes to: the right side of the jaw
SHOOT_AHEAD = r.v_norm((0.24, 0.05, 1.0))           # the bow arm at full draw, the arrow flying straight ahead from the anchor
NOCK_AT, DRAW_FROM, DRAWN_AT = 0.34, 0.36, 0.84


def _key(t, keys):
    """A value eased between (time, value) keys."""
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1:
            k = r.ease((t - t0) / (t1 - t0)) if t1 > t0 else 1.0
            if isinstance(v0, tuple):
                return r.v_lerp(v0, v1, k)
            return v0 + (v1 - v0) * k
    return keys[-1][1]


def ranger_shoot(pose, u):
    r.stand_legs(pose, 0.0, 0.3, width=0.16)
    twist = _key(u, ((0.0, 60.0), (0.1, 60.0), (0.3, 14.0), (0.8, 60.0), (1.0, 62.0)))
    extend = _key(u, ((0.0, 1.0), (0.08, 1.0), (0.3, 0.25), (0.78, 1.0), (1.0, 1.0)))
    ahead = _key(u, ((0.0, SHOOT_AHEAD), (0.1, SHOOT_AHEAD), (0.3, r.v_norm((0.55, -0.25, 0.8))), (0.78, SHOOT_AHEAD), (1.0, SHOOT_AHEAD)))
    recoil = math.sin(math.pi * min(1.0, u / 0.12)) if u < 0.12 else 0.0
    spine_upright(pose, lean=1.0)
    pose.set("chest", r.q_euler(y=twist, x=-2.0))
    pose.set_world("head", r.q_euler(x=4.0, z=-6.0 * extend))                  # cheek down to the string
    pose.set("cape", r.q_euler(x=6.0))

    # The bow arm: brought in and bent to nock, pushed out straight to draw; the bow jumps a little as the arrow leaves
    bend = 4.0 + 115.0 * (1.0 - extend)
    bow_dir = r.v_norm(r.v_add(ahead, (0.0, -0.08 * recoil, 0.0)))
    r.aim_arm(pose, "l", bow_dir, bend=bend, elbow_pole=(-1.0, -0.5, 0.0))
    _ranger_bow_hand(pose, (ahead[0], 0.0, ahead[2]), cant=_key(u, ((0.0, 12.0), (0.3, 35.0), (0.78, 12.0), (1.0, 12.0))), tip_forward=18.0 * recoil)

    world = pose.world()
    hand_rot, hand_at = world["hand_l"]
    nock_rest = r.v_add(hand_at, r.q_rotate(hand_rot, r.v_sub(RANGER_RIG.bind["nock"], RANGER_RIG.bind["hand_l"])))
    grip = r.v_add(hand_at, r.q_rotate(hand_rot, r.v_sub(BOW_GRIP, RANGER_RIG.bind["hand_l"])))

    # The drawing hand: flung back past the ear as it looses, to the string, then back to the jaw with it
    follow = r.v_add(SHOOT_ANCHOR, (0.12, 0.03, -0.16))
    if u < 0.12:
        target = r.v_lerp(SHOOT_ANCHOR, follow, r.ease(u / 0.12))
    elif u < NOCK_AT:
        target = r.v_lerp(follow, nock_rest, r.ease((u - 0.12) / (NOCK_AT - 0.12)))
    elif u < DRAWN_AT:
        target = r.v_lerp(nock_rest, SHOOT_ANCHOR, r.ease((u - DRAW_FROM) / (DRAWN_AT - DRAW_FROM)) if u > DRAW_FROM else 0.0)
    else:
        target = r.v_add(SHOOT_ANCHOR, (0.01 * (u - DRAWN_AT) / (1 - DRAWN_AT), 0.0, -0.015 * (u - DRAWN_AT) / (1 - DRAWN_AT)))
    # The target is where the fingers hold the string: the wrist sits a hand's length up the forearm from it
    r.arm_ik(pose, "r", r.v_add(target, (0.02, 0.05, -0.02)), elbow_pole=(0.8, 0.6, -1.0), front=(0.0, 1.0, 0.0))
    pose.set_world("hand_r", r.q_look(r.v_sub(grip, target), (0.0, 1.0, 0.0)))

    # The string: at rest until the hand takes it, then with the fingers until the arrow is loosed
    if u < NOCK_AT:
        _ranger_rest_string(pose)
        return {"arrow": HIDDEN}
    world = pose.world()
    nock_local = r.q_rotate(r.q_inv(hand_rot), r.v_sub(target, hand_at))
    pose.set("nock", r.Q_IDENTITY, nock_local)
    pose.set("arrow", r.Q_IDENTITY, (0.0, 0.0, 0.0))
    pose.set_world("arrow", r.q_look(r.v_sub(grip, target), (0.0, 1.0, 0.0)))
    return {"arrow": SHOWN}


RANGER = Hero("ranger", "ranger_hero.glb", RANGER_RIG, build_ranger_hero, RANGER_GAIT, ranger_idle, ranger_run, ranger_air,
              {"Shoot": (1.0, ranger_shoot)})
RANGER._scaled = ("arrow",)

HEROES = {"ranger": RANGER}


# =========================================================================================================================================================
# Shared by the four below. An attack clip is one second with the blow landing at ATTACK_RELEASE (the wind-up before it, the follow-through after) and the
# ready pose at both ends, so the game holds it at 0 between blows.

ATTACK_RELEASE = 0.5


def attack_moment(u, windup_from, struck_by, ready_by):
    """Where a blow is at <u> of its clip: ("ready", 0), ("windup", 0 -> 1) up to the release, ("strike", 0 -> 1) through the blow, ("recover", 0 -> 1) back to
    ready by <ready_by>."""
    if u < windup_from:
        return "ready", 0.0
    if u < ATTACK_RELEASE:
        return "windup", r.ease((u - windup_from) / (ATTACK_RELEASE - windup_from))
    if u < struck_by:
        return "strike", r.ease((u - ATTACK_RELEASE) / (struck_by - ATTACK_RELEASE))
    if u < ready_by:
        return "recover", r.ease((u - struck_by) / (ready_by - struck_by))
    return "ready", 0.0


def attack_value(moment, ready, wound, struck):
    """A pose value at an attack_moment: ready, eased to <wound>, snapped through to <struck>, eased back to ready."""
    stage, k = moment
    lerp = r.v_lerp if isinstance(ready, tuple) else (lambda a, b, t: a + (b - a) * t)
    if stage == "windup":
        return lerp(ready, wound, k)
    if stage == "strike":
        return lerp(wound, struck, k)
    if stage == "recover":
        return lerp(struck, ready, k)
    return ready


def skirt_flaps(pose, front, back, margin=6.0, give=0.75):
    """Swings a skirt's front and back flaps (joints hanging from the hips) out with the leading and the trailing thigh: <give> of the thigh's swing (at 1
    the legs never go through the cloth; less, and a knee shows through at the top of its stride, but the cloth doesn't fly out so far)."""
    world = pose.world()
    hips_rot = world["hips"][0]
    ahead = behind = 0.0
    for side in "lr":
        down = r.q_rotate(r.q_inv(hips_rot), r.q_rotate(world[f"thigh_{side}"][0], (0.0, -1.0, 0.0)))
        angle = math.degrees(math.atan2(down[2], -down[1]))
        ahead, behind = max(ahead, angle), max(behind, -angle)
    if front:
        pose.set(front, r.q_axis((1, 0, 0), -(give * ahead + margin)))
    if back:
        pose.set(back, r.q_axis((1, 0, 0), give * behind + margin * 0.5))


def face(pose, joint, ahead, tilt=0.0, roll=0.0, spin=0.0):
    """Turns <joint> to face <ahead> (flat), tipped forward <tilt> degrees (its +Y toward <ahead>), rolled <roll> (its top toward +X) and spun <spin> about
    its own +Y first (what a haft's blade faces)."""
    yaw = math.degrees(math.atan2(ahead[0], ahead[2]))
    turn = r.q_mul(r.q_axis((0, 1, 0), yaw), r.q_mul(r.q_axis((1, 0, 0), tilt), r.q_axis((0, 0, 1), -roll)))
    pose.set_world(joint, r.q_mul(turn, r.q_axis((0, 1, 0), spin)))


def hang(pose, joint, down, ahead=(0.0, 0.0, 1.0)):
    """Lets <joint> hang along <down> (a flail's chain and ball, pulled by its swing)."""
    pose.set_world(joint, r.q_aim(r.v_norm(down), ahead))


def limbs(m, j, side, parts):
    """Adds one side's boxes: <parts> is [(joint prefix, x from, x to, y0, z0, y1, z1, colour)] with x measured outward from the middle."""
    sx = -1.0 if side == "l" else 1.0
    for prefix, a, b, y0, z0, y1, z1, colour in parts:
        x0, x1 = sorted((sx * a, sx * b))
        m.joint = j[f"{prefix}_{side}"] if prefix not in j else j[prefix]
        m.box(x0, y0, z0, x1, y1, z1, colour)


def crystal(m, x, bottom, top, mid, r_, light, dark, z=0.0):
    """A four-sided crystal from <bottom> to <top>, widest (<r_>) at <mid>."""
    ring = [(x + r_, mid, z), (x, mid, z + r_), (x - r_, mid, z), (x, mid, z - r_)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.tri(a, (x, top, z), b, light)
        m.tri(b, (x, bottom, z), a, dark)


# =========================================================================================================================================================
# The Paladin: plate, a white tabard with a red cross, a great helm, a cape, the tower shield held out in front on the left arm, a flail in the right

PALADIN_RIG = r.HeroRig(hip_y=0.88, spine_y=0.96, chest_y=1.02, neck_y=1.46, shoulder=(0.33, 1.40), elbow_y=1.12, wrist_y=0.87, leg_x=0.12, knee_y=0.48,
                        ankle_y=0.11, toe=0.15, heel=0.09,
                        extra=(("cape", "chest", (0.0, 1.44, -0.19)),
                               ("tabard_front", "hips", (0.0, 0.92, 0.17)),
                               ("tabard_back", "hips", (0.0, 0.92, -0.17)),
                               ("flail", "hand_r", (0.33, 1.20, 0.0))))
PALADIN_GAIT = r.Gait(cycle=2.4, stance=0.36, lift=0.17, bob=0.03, hip_drop=0.1, width=0.1, lean=7.0)
SHIELD_AT = (-0.33, 0.81, 0.0)          # the fist on the shield's grip


def build_paladin_hero(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.03, 0.21, 0.0, -0.09, 0.17, 0.16, "plate_dark"),
                           ("shin", 0.04, 0.2, 0.15, -0.08, 0.51, 0.08, "plate"),
                           ("shin", 0.035, 0.205, 0.44, -0.085, 0.52, 0.09, "plate_dark"),        # the knee cop
                           ("thigh", 0.04, 0.2, 0.48, -0.08, 0.88, 0.08, "plate")])
    m.joint = j["hips"]
    m.box(-0.27, 0.74, -0.15, 0.27, 0.94, 0.15, "plate")
    m.box(-0.28, 0.84, -0.16, 0.28, 0.93, 0.16, "leather")
    m.joint = j["tabard_front"]
    m.box(-0.2, 0.52, 0.15, 0.2, 0.94, 0.17, "tabard")
    m.box(-0.04, 0.78, 0.17, 0.04, 0.94, 0.18, "crusader")
    m.joint = j["tabard_back"]
    m.box(-0.2, 0.52, -0.17, 0.2, 0.94, -0.15, "tabard")
    m.joint = j["chest"]
    m.box(-0.27, 0.9, -0.15, 0.27, 1.46, 0.15, "plate")
    m.box(-0.2, 0.92, 0.15, 0.2, 1.40, 0.17, "tabard")
    m.box(-0.2, 0.92, -0.17, 0.2, 1.40, -0.15, "tabard")
    m.box(-0.04, 0.92, 0.17, 0.04, 1.30, 0.18, "crusader")
    m.box(-0.14, 1.10, 0.17, 0.14, 1.17, 0.18, "crusader")
    m.joint = j["cape"]
    m.box(-0.26, 0.56, -0.21, 0.26, 1.46, -0.17, "crusader")
    m.joint = j["head"]
    m.box(-0.14, 1.46, -0.14, 0.14, 1.80, 0.14, "plate")
    m.box(-0.11, 1.62, 0.14, 0.11, 1.65, 0.145, "visor")
    m.box(-0.015, 1.50, 0.14, 0.015, 1.62, 0.145, "visor")
    m.box(-0.03, 1.80, -0.12, 0.03, 1.88, 0.12, "gold")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.21, 0.45, 1.28, -0.15, 1.50, 0.15, "plate_dark"),     # the pauldron
                           ("upper_arm", 0.27, 0.40, 1.1, -0.075, 1.32, 0.075, "plate"),
                           ("forearm", 0.275, 0.395, 0.86, -0.07, 1.14, 0.07, "plate"),
                           ("hand", 0.265, 0.405, 0.75, -0.07, 0.87, 0.08, "plate_dark")])
    # The tower shield, gripped at its middle and faced forward from the fist: gold rim, white field, red cross
    m.joint = j["hand_l"]
    cx, cy, cz = SHIELD_AT
    m.box(cx - 0.29, cy - 0.47, cz + 0.08, cx + 0.29, cy + 0.53, cz + 0.13, "gold")
    m.box(cx - 0.26, cy - 0.43, cz + 0.13, cx + 0.26, cy + 0.49, cz + 0.15, "tabard")
    m.box(cx - 0.04, cy - 0.35, cz + 0.15, cx + 0.04, cy + 0.41, cz + 0.165, "crusader")
    m.box(cx - 0.21, cy + 0.12, cz + 0.15, cx + 0.21, cy + 0.2, cz + 0.165, "crusader")
    m.box(cx - 0.05, cy - 0.03, cz + 0.02, cx + 0.05, cy + 0.03, cz + 0.08, "leather")    # the strap the fist holds
    # The flail: a haft up from the right fist, and from its top a short chain and a spiked ball, hanging
    m.joint = j["hand_r"]
    hx, hy = 0.33, 0.81
    m.box(hx - 0.03, hy - 0.1, -0.03, hx + 0.03, 1.2, 0.03, "wood")
    m.box(hx - 0.04, 1.17, -0.04, hx + 0.04, 1.22, 0.04, "iron")
    m.joint = j["flail"]
    for y in (1.16, 1.09, 1.02):
        m.box(hx - 0.02, y - 0.035, -0.02, hx + 0.02, y + 0.01, 0.02, "iron")
    bx, by, bz, br, s = hx, 0.9, 0.0, 0.09, 0.025
    m.box(bx - br, by - br, bz - br, bx + br, by + br, bz + br, "iron")
    m.box(bx - 1.8 * br, by - s, bz - s, bx + 1.8 * br, by + s, bz + s, "steel")
    m.box(bx - s, by - 1.8 * br, bz - s, bx + s, by + 1.8 * br, bz + s, "steel")
    m.box(bx - s, by - s, bz - 1.8 * br, bx + s, by + s, bz + 1.8 * br, "steel")
    return m


def _paladin_arms(pose, shield_at, flail_at, haft_tilt, ball_down, shield_tilt=-4.0):
    """The shield held out in front and the flail out to the side: each hand reached to its place, the shield faced ahead, the ball hanging."""
    r.arm_ik(pose, "l", shield_at, elbow_pole=(-1.0, -0.6, -0.2))
    face(pose, "hand_l", (0.12, 0.0, 1.0), tilt=shield_tilt, roll=-4.0)
    r.arm_ik(pose, "r", flail_at, elbow_pole=(1.0, -0.6, -0.4))
    face(pose, "hand_r", (0.25, 0.0, 1.0), tilt=haft_tilt, roll=12.0)
    hang(pose, "flail", ball_down)


def paladin_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 2.2)
    shift = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.5 * shift, width=0.15, crouch=0.02)
    spine_upright(pose, lean=2.0 + breath, turn=-2.0 * shift)
    pose.set("chest", r.q_euler(x=-1.0 * breath))
    pose.set_world("head", r.q_euler(x=2.0, y=8.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 0.7)))
    pose.set("cape", r.q_euler(x=3.0 + breath))
    skirt_flaps(pose, "tabard_front", "tabard_back", margin=2.0)
    sway = math.sin(2 * math.pi * t / 1.6)
    _paladin_arms(pose, (-0.2, 1.0 + 0.008 * breath, 0.44), (0.4, 0.97 + 0.008 * breath, 0.3), 32.0, (0.1 * sway, -1.0, 0.08))


def paladin_run(pose, phase, heading):
    turn, swing = r.run_legs(pose, PALADIN_GAIT, phase, heading)
    bounce = math.cos(4 * math.pi * phase)
    spine_upright(pose, lean=6.0, turn=-3.0 * swing)
    pose.set("chest", r.q_euler(x=1.0 * bounce))
    pose.set_world("head", r.q_euler(x=-2.0))
    pose.set("cape", r.q_euler(x=24.0 + 5.0 * math.sin(4 * math.pi * phase + 0.8)))
    skirt_flaps(pose, "tabard_front", "tabard_back")
    # The shield stays up in front, bobbing with the stride; the flail arm swings a little and the ball trails behind it
    _paladin_arms(pose, (-0.2, 1.02 + 0.02 * bounce, 0.42), (0.42, 0.98 + 0.03 * swing, 0.26 + 0.1 * swing), 40.0,
                  (0.1 * swing, -1.0, -0.35 - 0.2 * swing))


def paladin_air(pose, u):
    r.airborne_legs(pose)
    spine_upright(pose, lean=3.0)
    pose.set("cape", r.q_euler(x=32.0))
    skirt_flaps(pose, "tabard_front", "tabard_back")
    _paladin_arms(pose, (-0.22, 1.1, 0.4), (0.5, 1.15, 0.2), 30.0, (0.2, -1.0, -0.4))


def paladin_smite(pose, u):
    """The nova: the flail swung up over the shoulder with the ball whirling behind, then brought down hard in front as the light bursts; the shield braced."""
    r.stand_legs(pose, 0.0, 0.0, width=0.17, crouch=0.04)
    at = attack_moment(u, 0.12, 0.6, 0.95)
    spine_upright(pose, lean=attack_value(at, 2.0, -3.0, 12.0))
    pose.set("chest", r.q_euler(y=attack_value(at, 0.0, 26.0, -14.0)))
    pose.set_world("head", r.q_euler(x=attack_value(at, 2.0, -6.0, 10.0)))
    pose.set("cape", r.q_euler(x=attack_value(at, 3.0, 6.0, 12.0)))
    skirt_flaps(pose, "tabard_front", "tabard_back", margin=2.0)
    _paladin_arms(pose,
                  attack_value(at, (-0.2, 1.0, 0.44), (-0.18, 1.08, 0.4), (-0.22, 0.98, 0.5)),
                  attack_value(at, (0.4, 0.97, 0.3), (0.32, 1.78, -0.1), (0.12, 0.9, 0.6)),
                  attack_value(at, 32.0, -35.0, 85.0),
                  attack_value(at, (0.0, -1.0, 0.08), (0.2, -0.2, -1.0), (0.0, -0.5, 1.0)))


PALADIN = Hero("paladin", "paladin_hero.glb", PALADIN_RIG, build_paladin_hero, PALADIN_GAIT, paladin_idle, paladin_run, paladin_air,
               {"Smite": (1.0, paladin_smite)})
HEROES["paladin"] = PALADIN


# =========================================================================================================================================================
# The Mage: a long blue robe split front and back over dark leggings, a pointed hat, a white beard, a staff crowned with an ice crystal held out in the
# right hand

MAGE_RIG = r.HeroRig(hip_y=0.86, spine_y=0.94, chest_y=1.0, neck_y=1.44, shoulder=(0.29, 1.38), elbow_y=1.12, wrist_y=0.88, leg_x=0.1, knee_y=0.47,
                     ankle_y=0.09, toe=0.14, heel=0.07,
                     extra=(("robe_front", "hips", (0.0, 0.9, 0.17)),
                            ("robe_back", "hips", (0.0, 0.9, -0.17))))
MAGE_GAIT = r.Gait(cycle=2.5, stance=0.35, lift=0.16, bob=0.03, hip_drop=0.09, width=0.09, lean=8.0)


def build_mage_hero(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.04, 0.16, 0.0, -0.07, 0.11, 0.14, "leather"),
                           ("shin", 0.045, 0.155, 0.1, -0.065, 0.5, 0.065, "robe_dark"),
                           ("thigh", 0.04, 0.16, 0.45, -0.07, 0.88, 0.07, "robe_dark")])
    m.joint = j["hips"]
    m.box(-0.25, 0.7, -0.17, 0.25, 0.95, 0.17, "robe")
    m.box(-0.26, 0.86, -0.18, 0.26, 0.93, 0.18, "leather")
    m.joint = j["robe_front"]
    m.box(-0.26, 0.12, 0.15, 0.26, 0.9, 0.2, "robe")
    m.box(-0.05, 0.12, 0.2, 0.05, 0.9, 0.21, "robe_dark")
    m.box(-0.26, 0.12, 0.145, 0.26, 0.18, 0.205, "robe_dark")                         # the hem
    m.joint = j["robe_back"]
    m.box(-0.26, 0.12, -0.2, 0.26, 0.9, -0.15, "robe")
    m.box(-0.26, 0.12, -0.205, 0.26, 0.18, -0.145, "robe_dark")
    m.joint = j["chest"]
    m.box(-0.23, 0.9, -0.15, 0.23, 1.42, 0.15, "robe")
    m.box(-0.05, 0.92, 0.15, 0.05, 1.40, 0.16, "robe_dark")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.23, 0.36, 1.1, -0.08, 1.42, 0.08, "robe"),
                           ("forearm", 0.225, 0.365, 0.86, -0.09, 1.14, 0.09, "robe"),
                           ("forearm", 0.22, 0.37, 0.86, -0.095, 0.92, 0.095, "robe_dark"),        # the sleeve's cuff
                           ("hand", 0.25, 0.35, 0.78, -0.05, 0.88, 0.05, "skin")])
    m.joint = j["head"]
    m.box(-0.12, 1.44, -0.12, 0.12, 1.68, 0.12, "skin")
    m.box(-0.08, 1.57, 0.12, -0.03, 1.61, 0.125, "dark")
    m.box(0.03, 1.57, 0.12, 0.08, 1.61, 0.125, "dark")
    m.box(-0.10, 1.26, 0.08, 0.10, 1.52, 0.16, "beard")
    m.box(-0.27, 1.68, -0.27, 0.27, 1.72, 0.27, "robe_dark")
    m.pyramid(-0.15, 1.72, -0.15, 0.15, 1.72, 0.15, (0.04, 2.20, -0.10), "robe_dark")
    # The staff in the right hand, gripped a third of the way up, the ice crystal on top
    m.joint = j["hand_r"]
    sx = 0.3
    m.box(sx - 0.025, 0.12, -0.025, sx + 0.025, 1.8, 0.025, "wood")
    m.box(sx - 0.035, 1.72, -0.035, sx + 0.035, 1.8, 0.035, "leather")
    crystal(m, sx, 1.76, 2.1, 1.92, 0.09, "ice_light", "ice")
    return m


def _mage_staff(pose, hand_at, tilt, roll=8.0):
    r.arm_ik(pose, "r", hand_at, elbow_pole=(1.0, -0.5, -0.4))
    face(pose, "hand_r", (0.2, 0.0, 1.0), tilt=tilt, roll=roll)


def mage_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 2.0)
    shift = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.5 * shift, width=0.12)
    spine_upright(pose, lean=3.0 + breath, turn=-3.0 * shift)
    pose.set("chest", r.q_euler(x=-1.0 * breath))
    pose.set_world("head", r.q_euler(x=4.0, y=10.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 1.3)))
    skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
    # The staff held out at the side, planted forward; the free hand turned up, frost gathering in it
    _mage_staff(pose, (0.33, 1.0 + 0.008 * breath, 0.3), 12.0)
    r.arm_ik(pose, "l", (-0.3, 1.02 + 0.015 * math.sin(2 * math.pi * t / 1.3), 0.3), elbow_pole=(-1.0, -0.6, -0.3))
    face(pose, "hand_l", (0.3, 0.0, 1.0), tilt=-60.0)


def mage_run(pose, phase, heading):
    turn, swing = r.run_legs(pose, MAGE_GAIT, phase, heading)
    bounce = math.cos(4 * math.pi * phase)
    spine_upright(pose, lean=7.0, turn=-4.0 * swing)
    pose.set("chest", r.q_euler(x=1.0 * bounce))
    pose.set_world("head", r.q_euler(x=-2.0))
    skirt_flaps(pose, "robe_front", "robe_back", margin=3.0, give=0.6)
    # The staff carried out in front at a slant; the free arm pumps
    _mage_staff(pose, (0.3, 1.0 + 0.02 * bounce, 0.34 + 0.06 * swing), 38.0 + 4.0 * swing, roll=20.0)
    r.aim_arm(pose, "l", (-0.12, -0.8, -0.62 * swing), bend=80.0 - 15.0 * swing, elbow_pole=(-0.2, 0.0, -1.0))


def mage_air(pose, u):
    r.airborne_legs(pose)
    spine_upright(pose, lean=3.0)
    skirt_flaps(pose, "robe_front", "robe_back", margin=3.0, give=0.6)
    _mage_staff(pose, (0.42, 1.15, 0.25), 20.0, roll=25.0)
    r.aim_arm(pose, "l", (-0.8, -0.3, 0.2), bend=30.0, elbow_pole=(0.0, -0.3, -1.0))


def mage_cast(pose, u):
    """A barrage: the staff drawn back and up, then thrust out ahead, crystal first, the free palm pushed out beside it as the bolts leave."""
    r.stand_legs(pose, 0.0, 0.0, width=0.14)
    at = attack_moment(u, 0.2, 0.62, 0.95)
    spine_upright(pose, lean=attack_value(at, 3.0, -2.0, 9.0))
    pose.set("chest", r.q_euler(y=attack_value(at, 0.0, 16.0, -8.0)))
    pose.set_world("head", r.q_euler(x=attack_value(at, 4.0, -4.0, 6.0)))
    skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
    _mage_staff(pose, attack_value(at, (0.33, 1.0, 0.3), (0.34, 1.42, 0.02), (0.2, 1.28, 0.6)), attack_value(at, 12.0, -12.0, 72.0),
                roll=attack_value(at, 8.0, 12.0, 4.0))
    r.arm_ik(pose, "l", attack_value(at, (-0.3, 1.02, 0.3), (-0.3, 1.2, 0.05), (-0.18, 1.3, 0.58)), elbow_pole=(-1.0, -0.6, -0.3))
    face(pose, "hand_l", (0.3, 0.0, 1.0), tilt=attack_value(at, -60.0, -40.0, -95.0))


MAGE = Hero("mage", "mage_hero.glb", MAGE_RIG, build_mage_hero, MAGE_GAIT, mage_idle, mage_run, mage_air, {"Cast": (1.0, mage_cast)})
HEROES["mage"] = MAGE


# =========================================================================================================================================================
# The Shaman: a fur mantle over a hide tunic, a bone mask with antlers, blue war paint, a totem staff crowned with a crackling orb held out in the right hand

SHAMAN_RIG = r.HeroRig(hip_y=0.86, spine_y=0.94, chest_y=1.0, neck_y=1.46, shoulder=(0.29, 1.36), elbow_y=1.1, wrist_y=0.86, leg_x=0.11, knee_y=0.47,
                       ankle_y=0.1, extra=(("mantle", "chest", (0.0, 1.32, -0.17)),))
SHAMAN_GAIT = r.Gait(cycle=2.6, stance=0.34, lift=0.2, bob=0.035, hip_drop=0.1, width=0.09, lean=10.0)


def build_shaman_hero(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.03, 0.19, 0.0, -0.08, 0.14, 0.13, "leather"),
                           ("shin", 0.04, 0.18, 0.13, -0.07, 0.5, 0.07, "hide"),
                           ("shin", 0.035, 0.185, 0.2, -0.075, 0.24, 0.075, "fur"),                # a fur wrap round the calf
                           ("thigh", 0.04, 0.18, 0.45, -0.07, 0.86, 0.07, "hide")])
    m.joint = j["hips"]
    m.box(-0.23, 0.7, -0.13, 0.23, 0.94, 0.13, "hide")
    m.box(-0.24, 0.86, -0.14, 0.24, 0.92, 0.14, "leather")
    m.joint = j["chest"]
    m.box(-0.23, 0.9, -0.13, 0.23, 1.40, 0.13, "hide")
    m.box(-0.235, 0.98, 0.13, 0.235, 1.04, 0.135, "war_paint")
    m.box(-0.34, 1.26, -0.20, 0.34, 1.46, 0.18, "fur")
    m.joint = j["mantle"]
    m.box(-0.30, 0.6, -0.20, 0.30, 1.30, -0.13, "fur")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.23, 0.35, 1.08, -0.07, 1.34, 0.07, "skin"),
                           ("upper_arm", 0.225, 0.355, 1.13, -0.075, 1.18, 0.075, "war_paint"),
                           ("forearm", 0.235, 0.345, 0.86, -0.065, 1.1, 0.065, "skin"),
                           ("forearm", 0.23, 0.35, 0.87, -0.07, 0.95, 0.07, "leather"),
                           ("hand", 0.24, 0.34, 0.76, -0.05, 0.86, 0.05, "skin")])
    m.joint = j["head"]
    m.box(-0.12, 1.46, -0.12, 0.12, 1.70, 0.12, "skin")
    m.box(-0.13, 1.50, 0.12, 0.13, 1.70, 0.16, "bone")
    m.box(-0.08, 1.60, 0.16, -0.03, 1.64, 0.165, "dark")
    m.box(0.03, 1.60, 0.16, 0.08, 1.64, 0.165, "dark")
    for side in (-1, 1):
        x, tip = side * 0.10, side * 0.26
        m.box(x - 0.02, 1.70, -0.02, x + 0.02, 1.95, 0.02, "bone")
        m.box(min(x, tip), 1.88, -0.02, max(x, tip), 1.92, 0.02, "bone")
    # The totem staff in the right hand, the orb crackling on top
    m.joint = j["hand_r"]
    sx = 0.29
    m.box(sx - 0.025, 0.08, -0.025, sx + 0.025, 1.86, 0.025, "wood")
    m.box(sx - 0.045, 1.56, -0.045, sx + 0.045, 1.64, 0.045, "fur")
    m.box(sx - 0.035, 1.64, -0.012, sx + 0.035, 1.7, 0.012, "bone")
    crystal(m, sx, 1.84, 2.14, 1.99, 0.095, "spark_light", "spark")
    return m


def _shaman_staff(pose, hand_at, tilt, roll=10.0):
    r.arm_ik(pose, "r", hand_at, elbow_pole=(1.0, -0.5, -0.4))
    face(pose, "hand_r", (0.2, 0.0, 1.0), tilt=tilt, roll=roll)


def shaman_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 2.0)
    shift = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.7 * shift, width=0.15, crouch=0.03)
    spine_upright(pose, lean=5.0 + breath, turn=-4.0 * shift)
    pose.set("chest", r.q_euler(x=-1.2 * breath))
    pose.set_world("head", r.q_euler(x=2.0, y=14.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 2.0)))
    pose.set("mantle", r.q_euler(x=3.0 + breath))
    _shaman_staff(pose, (0.36, 0.98 + 0.008 * breath, 0.28), 10.0)
    flick = math.sin(2 * math.pi * t / 0.9)
    r.arm_ik(pose, "l", (-0.33, 0.96 + 0.01 * flick, 0.24), elbow_pole=(-1.0, -0.4, -0.4))
    face(pose, "hand_l", (0.2, 0.0, 1.0), tilt=-30.0 + 8.0 * flick, roll=-20.0)


def shaman_run(pose, phase, heading):
    turn, swing = r.run_legs(pose, SHAMAN_GAIT, phase, heading)
    bounce = math.cos(4 * math.pi * phase)
    spine_upright(pose, lean=9.0, turn=-5.0 * swing)
    pose.set("chest", r.q_euler(x=1.0 * bounce))
    pose.set_world("head", r.q_euler(x=-4.0))
    pose.set("mantle", r.q_euler(x=18.0 + 4.0 * math.sin(4 * math.pi * phase + 0.8)))
    _shaman_staff(pose, (0.32, 1.02 + 0.02 * bounce, 0.36 + 0.07 * swing), 42.0 + 5.0 * swing, roll=22.0)
    r.aim_arm(pose, "l", (-0.12, -0.8, -0.62 * swing), bend=80.0 - 15.0 * swing, elbow_pole=(-0.2, 0.0, -1.0))


def shaman_air(pose, u):
    r.airborne_legs(pose)
    spine_upright(pose, lean=4.0)
    pose.set("mantle", r.q_euler(x=26.0))
    _shaman_staff(pose, (0.42, 1.2, 0.22), 18.0, roll=25.0)
    r.aim_arm(pose, "l", (-0.8, -0.2, 0.2), bend=30.0, elbow_pole=(0.0, -0.3, -1.0))


def shaman_hurl(pose, u):
    """A cast: the free hand reaches back over the shoulder with the lightning in it and lobs it out ahead, the staff raised with it."""
    r.stand_legs(pose, 0.0, 0.0, width=0.16, crouch=0.03)
    at = attack_moment(u, 0.18, 0.62, 0.95)
    spine_upright(pose, lean=attack_value(at, 5.0, -3.0, 12.0))
    pose.set("chest", r.q_euler(y=attack_value(at, 0.0, -28.0, 20.0)))
    pose.set_world("head", r.q_euler(x=attack_value(at, 2.0, -8.0, 4.0)))
    pose.set("mantle", r.q_euler(x=attack_value(at, 3.0, 6.0, 10.0)))
    _shaman_staff(pose, attack_value(at, (0.36, 0.98, 0.28), (0.36, 1.3, 0.2), (0.36, 1.15, 0.36)), attack_value(at, 10.0, -8.0, 28.0))
    r.arm_ik(pose, "l", attack_value(at, (-0.33, 0.96, 0.24), (-0.3, 1.62, -0.26), (-0.12, 1.3, 0.6)), elbow_pole=(-1.0, 0.2, -0.6))
    face(pose, "hand_l", (0.2, 0.0, 1.0), tilt=attack_value(at, -30.0, -150.0, -80.0), roll=-20.0)


SHAMAN = Hero("shaman", "shaman_hero.glb", SHAMAN_RIG, build_shaman_hero, SHAMAN_GAIT, shaman_idle, shaman_run, shaman_air, {"Hurl": (1.0, shaman_hurl)})
HEROES["shaman"] = SHAMAN


# =========================================================================================================================================================
# The Warrior: a bare, war-painted chest under a fur mantle, a horned iron helm, a beard, fur boots, a hide kilt, and an axe held out in each hand

WARRIOR_RIG = r.HeroRig(hip_y=0.9, spine_y=0.98, chest_y=1.04, neck_y=1.48, shoulder=(0.36, 1.40), elbow_y=1.13, wrist_y=0.86, leg_x=0.12, knee_y=0.5,
                        ankle_y=0.12, toe=0.14, heel=0.09, extra=(("mantle", "chest", (0.0, 1.40, -0.17)),))
WARRIOR_GAIT = r.Gait(cycle=2.5, stance=0.36, lift=0.18, bob=0.04, hip_drop=0.12, width=0.11, lean=10.0)


def build_warrior_hero(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.03, 0.21, 0.0, -0.09, 0.22, 0.14, "fur"),
                           ("shin", 0.045, 0.195, 0.2, -0.075, 0.53, 0.075, "leather"),
                           ("thigh", 0.04, 0.2, 0.48, -0.08, 0.9, 0.08, "leather")])
    m.joint = j["hips"]
    m.box(-0.27, 0.80, -0.15, 0.27, 0.92, 0.15, "chest_dark")
    m.box(-0.05, 0.81, 0.15, 0.05, 0.89, 0.16, "iron")
    m.box(-0.25, 0.64, -0.14, 0.25, 0.82, 0.14, "hide")
    m.joint = j["chest"]
    m.box(-0.28, 0.9, -0.14, 0.28, 1.46, 0.14, "skin")
    m.box(-0.285, 1.18, 0.14, 0.285, 1.23, 0.145, "target_red")
    m.box(-0.06, 0.96, 0.14, 0.06, 1.18, 0.145, "target_red")
    m.box(-0.40, 1.32, -0.18, 0.40, 1.52, 0.16, "fur")
    m.joint = j["mantle"]
    m.box(-0.30, 0.72, -0.20, 0.30, 1.40, -0.14, "fur")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.28, 0.44, 1.1, -0.08, 1.38, 0.08, "skin"),
                           ("forearm", 0.285, 0.435, 0.86, -0.075, 1.14, 0.075, "skin"),
                           ("forearm", 0.275, 0.445, 0.86, -0.085, 0.98, 0.085, "leather"),
                           ("hand", 0.29, 0.43, 0.74, -0.06, 0.86, 0.08, "skin")])
    m.joint = j["head"]
    m.box(-0.13, 1.48, -0.13, 0.13, 1.74, 0.13, "skin")
    m.box(-0.13, 1.40, 0.08, 0.13, 1.60, 0.17, "brute_hide")
    m.box(-0.09, 1.62, 0.13, -0.03, 1.66, 0.135, "dark")
    m.box(0.03, 1.62, 0.13, 0.09, 1.66, 0.135, "dark")
    m.box(-0.15, 1.70, -0.15, 0.15, 1.86, 0.15, "iron")
    m.box(-0.02, 1.58, 0.13, 0.02, 1.72, 0.16, "iron")
    for side in (-1, 1):
        x = side * 0.15
        m.box(min(x, side * 0.26), 1.76, -0.03, max(x, side * 0.26), 1.82, 0.03, "horn")
        m.box(min(side * 0.24, side * 0.29), 1.82, -0.03, max(side * 0.24, side * 0.29), 2.00, 0.03, "horn")
    # An axe in each fist: the haft up from the grip, the broad head at the top with its edge forward, an iron spike behind
    for side, sx in (("l", -1.0), ("r", 1.0)):
        m.joint = j[f"hand_{side}"]
        hx = sx * 0.36
        m.box(hx - 0.03, 0.66, -0.03, hx + 0.03, 1.42, 0.03, "wood")
        m.box(hx - 0.035, 0.74, -0.035, hx + 0.035, 0.86, 0.035, "leather")
        m.box(hx - 0.02, 1.14, 0.03, hx + 0.02, 1.4, 0.2, "steel")
        m.box(hx - 0.015, 1.1, 0.2, hx + 0.015, 1.44, 0.23, "fletching")
        m.box(hx - 0.015, 1.24, -0.11, hx + 0.015, 1.3, -0.03, "iron")
    return m


def _warrior_axes(pose, left_at, right_at, tilt, spread=15.0, spin=0.0, left_tilt=None, left_spin=None):
    r.arm_ik(pose, "l", left_at, elbow_pole=(-1.0, -0.5, -0.5))
    r.arm_ik(pose, "r", right_at, elbow_pole=(1.0, -0.5, -0.5))
    face(pose, "hand_l", (-0.2, 0.0, 1.0), tilt=tilt if left_tilt is None else left_tilt, roll=-spread, spin=spin if left_spin is None else left_spin)
    face(pose, "hand_r", (0.2, 0.0, 1.0), tilt=tilt, roll=spread, spin=spin)


def warrior_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 1.8)
    shift = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.6 * shift, width=0.19, crouch=0.05)
    spine_upright(pose, lean=6.0 + 1.5 * breath, turn=-3.0 * shift)
    pose.set("chest", r.q_euler(x=-2.0 * breath))
    pose.set_world("head", r.q_euler(x=0.0, y=10.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 0.4)))
    pose.set("mantle", r.q_euler(x=4.0 + breath))
    # Both axes held out ahead and low, ready, heads up
    lift = 0.012 * breath
    _warrior_axes(pose, (-0.44, 0.98 + lift, 0.3), (0.44, 0.98 + lift, 0.3), 42.0, spread=18.0)


def warrior_run(pose, phase, heading):
    turn, swing = r.run_legs(pose, WARRIOR_GAIT, phase, heading)
    bounce = math.cos(4 * math.pi * phase)
    spine_upright(pose, lean=10.0, turn=-6.0 * swing)
    pose.set("chest", r.q_euler(x=1.5 * bounce))
    pose.set_world("head", r.q_euler(x=-6.0))
    pose.set("mantle", r.q_euler(x=20.0 + 5.0 * math.sin(4 * math.pi * phase + 0.8)))
    # The axes pump with the arms, carried out ahead of the body
    _warrior_axes(pose, (-0.44, 1.0 - 0.05 * swing, 0.3 - 0.16 * swing), (0.44, 1.0 + 0.05 * swing, 0.3 + 0.16 * swing), 50.0, spread=20.0)


def warrior_air(pose, u):
    r.airborne_legs(pose)
    spine_upright(pose, lean=6.0)
    pose.set("mantle", r.q_euler(x=30.0))
    _warrior_axes(pose, (-0.55, 1.2, 0.2), (0.55, 1.2, 0.2), 20.0, spread=35.0)


def warrior_cleave(pose, u):
    """A cleave: both axes cocked back over the right shoulder, then swept flat across in front to the left as the wave goes out, and back to ready."""
    r.stand_legs(pose, 0.0, 0.0, width=0.2, crouch=0.07)
    at = attack_moment(u, 0.12, 0.64, 0.97)
    spine_upright(pose, lean=attack_value(at, 6.0, 2.0, 12.0))
    pose.set("chest", r.q_euler(y=attack_value(at, 0.0, 40.0, -38.0)))
    pose.set_world("head", r.q_euler(x=attack_value(at, 0.0, -4.0, 6.0), y=attack_value(at, 0.0, -18.0, 16.0)))
    pose.set("mantle", r.q_euler(x=attack_value(at, 4.0, 8.0, 14.0)))
    stage, _ = at
    _warrior_axes(pose,
                  attack_value(at, (-0.44, 0.98, 0.3), (0.12, 1.45, -0.12), (-0.62, 1.12, 0.28)),
                  attack_value(at, (0.44, 0.98, 0.3), (0.46, 1.5, -0.2), (-0.3, 1.12, 0.6)),
                  attack_value(at, 42.0, -30.0, 88.0), spread=attack_value(at, 18.0, 10.0, 0.0),
                  spin=attack_value(at, 0.0, -30.0, -90.0))


WARRIOR = Hero("warrior", "warrior_hero.glb", WARRIOR_RIG, build_warrior_hero, WARRIOR_GAIT, warrior_idle, warrior_run, warrior_air,
               {"Cleave": (1.0, warrior_cleave)})
HEROES["warrior"] = WARRIOR


# =========================================================================================================================================================
# The Priest: a long, ragged robe of grave-grey split front and back, a deep hood over a face lost in shadow but for two green eyes, a stole the colour of old
# blood, a collar of little skulls, a skull wand in the right hand and a Skull Shield on the left arm, both glowing green where the eyes should be

PRIEST_RIG = r.HeroRig(hip_y=0.86, spine_y=0.94, chest_y=1.0, neck_y=1.44, shoulder=(0.29, 1.38), elbow_y=1.12, wrist_y=0.88, leg_x=0.1, knee_y=0.47,
                       ankle_y=0.09, toe=0.14, heel=0.07,
                       extra=(("robe_front", "hips", (0.0, 0.9, 0.17)),
                              ("robe_back", "hips", (0.0, 0.9, -0.17))))
PRIEST_GAIT = r.Gait(cycle=2.4, stance=0.36, lift=0.15, bob=0.03, hip_drop=0.1, width=0.09, lean=9.0)
PRIEST_SHIELD_AT = (-0.29, 0.83, 0.0)     # the left fist on the shield's grip
WAND_X = 0.29                              # the wand's shaft, up from the right fist


def skull(m, cx, cy, cz, s, eyes="ghost_eye"):
    """A skull facing +Z, <s> across, its middle at (cx, cy, cz): the cranium, the eye sockets (glowing), the nose, the jaw and its teeth."""
    h = s / 2
    m.box(cx - h, cy - h * 0.35, cz - h, cx + h, cy + h, cz + h * 0.9, "bone")                               # cranium
    m.box(cx - h * 0.72, cy - h * 0.95, cz - h * 0.5, cx + h * 0.72, cy - h * 0.3, cz + h * 0.8, "bone")     # the jaw
    for side in (-1.0, 1.0):
        x0, x1 = sorted((cx + side * h * 0.12, cx + side * h * 0.62))
        m.box(x0, cy - h * 0.05, cz + h * 0.9, x1, cy + h * 0.42, cz + h * 0.93, "visor")                    # the sockets
        m.box(x0 + h * 0.08, cy + h * 0.03, cz + h * 0.93, x1 - h * 0.08, cy + h * 0.34, cz + h * 0.95, eyes)
    m.box(cx - h * 0.1, cy - h * 0.3, cz + h * 0.9, cx + h * 0.1, cy - h * 0.08, cz + h * 0.93, "visor")    # the nose
    for k in range(4):
        x = cx - h * 0.45 + k * h * 0.3
        m.box(x - h * 0.1, cy - h * 0.62, cz + h * 0.8, x + h * 0.1, cy - h * 0.38, cz + h * 0.84, "bone")  # teeth


def build_priest_hero(rig):
    m = Mesh()
    j = rig.index
    for side in ("l", "r"):
        limbs(m, j, side, [("foot", 0.04, 0.16, 0.0, -0.07, 0.11, 0.14, "chest_dark"),
                           ("shin", 0.045, 0.155, 0.1, -0.065, 0.5, 0.065, "visor"),
                           ("thigh", 0.04, 0.16, 0.45, -0.07, 0.88, 0.07, "visor")])
    m.joint = j["hips"]
    m.box(-0.25, 0.7, -0.17, 0.25, 0.95, 0.17, "hood_dark")
    m.box(-0.26, 0.85, -0.18, 0.26, 0.92, 0.18, "bone")                                  # a cord of knucklebones
    m.joint = j["robe_front"]
    m.box(-0.26, 0.12, 0.15, 0.26, 0.9, 0.2, "hood_dark")
    m.box(-0.07, 0.2, 0.2, 0.07, 0.9, 0.21, "ghoul_robe")                                # the stole hanging down the front
    for k, x in enumerate((-0.2, -0.08, 0.05, 0.18)):                                    # a ragged hem
        m.box(x - 0.05, 0.04 + 0.03 * (k % 2), 0.15, x + 0.05, 0.12, 0.2, "hood_dark")
    m.joint = j["robe_back"]
    m.box(-0.26, 0.12, -0.2, 0.26, 0.9, -0.15, "hood_dark")
    for k, x in enumerate((-0.19, -0.06, 0.07, 0.2)):
        m.box(x - 0.05, 0.03 + 0.03 * (k % 2), -0.2, x + 0.05, 0.12, -0.15, "hood_dark")
    m.joint = j["chest"]
    m.box(-0.23, 0.9, -0.15, 0.23, 1.42, 0.15, "hood_dark")
    m.box(-0.07, 0.92, 0.15, 0.07, 1.4, 0.16, "ghoul_robe")                              # the stole
    for k in range(5):                                                                   # a collar of little skulls
        x = -0.2 + k * 0.1
        m.box(x - 0.035, 1.33, 0.13, x + 0.035, 1.4, 0.19, "bone")
        m.box(x - 0.02, 1.36, 0.19, x + 0.02, 1.38, 0.195, "visor")
    for side in ("l", "r"):
        limbs(m, j, side, [("upper_arm", 0.22, 0.36, 1.1, -0.08, 1.42, 0.08, "hood_dark"),
                           ("upper_arm", 0.2, 0.38, 1.32, -0.1, 1.46, 0.1, "bone"),                  # bone pauldrons
                           ("forearm", 0.22, 0.37, 0.86, -0.09, 1.14, 0.09, "hood_dark"),
                           ("forearm", 0.215, 0.375, 0.84, -0.1, 0.92, 0.1, "ghoul_robe"),           # a ragged cuff
                           ("hand", 0.25, 0.35, 0.78, -0.05, 0.88, 0.05, "ghoul_skin")])
    # The hood, deep and peaked, the face inside it shadow but for two green eyes
    m.joint = j["head"]
    m.box(-0.11, 1.44, -0.1, 0.11, 1.66, 0.12, "visor")
    m.box(-0.08, 1.56, 0.12, -0.025, 1.6, 0.125, "ghost_eye")
    m.box(0.025, 1.56, 0.12, 0.08, 1.6, 0.125, "ghost_eye")
    m.box(-0.17, 1.42, -0.18, 0.17, 1.74, -0.1, "hood_dark")
    m.box(-0.17, 1.66, -0.18, 0.17, 1.76, 0.18, "hood_dark")
    m.box(-0.175, 1.42, -0.1, -0.12, 1.7, 0.18, "hood_dark")
    m.box(0.12, 1.42, -0.1, 0.175, 1.7, 0.18, "hood_dark")
    m.pyramid(-0.17, 1.76, -0.18, 0.17, 1.76, 0.1, (0.0, 1.98, -0.2), "hood_dark")
    # The skull wand, up from the right fist: a dark shaft bound in bone, and a skull with green eyes on top
    m.joint = j["hand_r"]
    m.box(WAND_X - 0.022, 0.74, -0.022, WAND_X + 0.022, 1.24, 0.022, "chest_dark")
    m.box(WAND_X - 0.03, 1.04, -0.03, WAND_X + 0.03, 1.08, 0.03, "bone")
    m.box(WAND_X - 0.03, 1.19, -0.03, WAND_X + 0.03, 1.24, 0.03, "bone")
    skull(m, WAND_X, 1.33, 0.0, 0.16)
    # The Skull Shield, gripped at its middle and faced forward from the fist: a rim of dark iron, a field of bone plates, a great skull in the middle
    m.joint = j["hand_l"]
    cx, cy, cz = PRIEST_SHIELD_AT
    m.box(cx - 0.28, cy - 0.34, cz + 0.08, cx + 0.28, cy + 0.34, cz + 0.12, "iron")
    m.box(cx - 0.34, cy - 0.22, cz + 0.08, cx + 0.34, cy + 0.22, cz + 0.12, "iron")
    m.box(cx - 0.25, cy - 0.3, cz + 0.12, cx + 0.25, cy + 0.3, cz + 0.14, "bomb_black")
    m.box(cx - 0.31, cy - 0.19, cz + 0.12, cx + 0.31, cy + 0.19, cz + 0.14, "bomb_black")
    for dx, dy in ((-0.2, 0.2), (0.2, 0.2), (-0.2, -0.2), (0.2, -0.2)):
        m.box(cx + dx - 0.04, cy + dy - 0.04, cz + 0.14, cx + dx + 0.04, cy + dy + 0.04, cz + 0.16, "bone")   # bone studs
    skull(m, cx, cy + 0.02, cz + 0.28, 0.3)
    m.box(cx - 0.05, cy - 0.03, cz + 0.02, cx + 0.05, cy + 0.03, cz + 0.08, "leather")    # the strap the fist holds
    return m


def _priest_arms(pose, shield_at, wand_at, wand_tilt, shield_tilt=-4.0, wand_roll=10.0):
    """The Skull Shield held out in front on the left arm, faced ahead; the wand in the right hand, its skull tipped forward <wand_tilt> degrees."""
    r.arm_ik(pose, "l", shield_at, elbow_pole=(-1.0, -0.6, -0.2))
    face(pose, "hand_l", (0.12, 0.0, 1.0), tilt=shield_tilt, roll=-4.0)
    r.arm_ik(pose, "r", wand_at, elbow_pole=(1.0, -0.5, -0.4))
    face(pose, "hand_r", (0.15, 0.0, 1.0), tilt=wand_tilt, roll=wand_roll)


def priest_idle(pose, u):
    t = u * IDLE_SECONDS
    breath = math.sin(2 * math.pi * t / 2.0)
    shift = math.sin(2 * math.pi * t / IDLE_SECONDS)
    r.stand_legs(pose, breath, 0.5 * shift, width=0.12)
    spine_upright(pose, lean=6.0 + breath, turn=-3.0 * shift)
    pose.set("chest", r.q_euler(x=2.0 - 1.0 * breath))
    pose.set_world("head", r.q_euler(x=8.0, y=12.0 * math.sin(2 * math.pi * t / IDLE_SECONDS + 0.9)))
    skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
    _priest_arms(pose, (-0.2, 1.0 + 0.008 * breath, 0.42), (0.33, 1.0 + 0.01 * breath, 0.3), 14.0 + 3.0 * math.sin(2 * math.pi * t / 2.0))


def priest_run(pose, phase, heading):
    turn, swing = r.run_legs(pose, PRIEST_GAIT, phase, heading)
    bounce = math.cos(4 * math.pi * phase)
    spine_upright(pose, lean=9.0, turn=-3.0 * swing)
    pose.set("chest", r.q_euler(x=1.0 * bounce))
    pose.set_world("head", r.q_euler(x=2.0))
    skirt_flaps(pose, "robe_front", "robe_back", margin=3.0, give=0.6)
    # The shield stays up in front, bobbing with the stride; the wand carried forward at a slant
    _priest_arms(pose, (-0.2, 1.02 + 0.02 * bounce, 0.42), (0.32, 1.0 + 0.02 * bounce, 0.32 + 0.06 * swing), 36.0 + 4.0 * swing, wand_roll=18.0)


def priest_air(pose, u):
    r.airborne_legs(pose)
    spine_upright(pose, lean=4.0)
    skirt_flaps(pose, "robe_front", "robe_back", margin=3.0, give=0.6)
    _priest_arms(pose, (-0.22, 1.1, 0.4), (0.42, 1.15, 0.25), 20.0, wand_roll=25.0)


def priest_hex(pose, u):
    """A skull loosed: the wand drawn back beside the hood, then thrust out ahead, its skull first, as the skull leaves it; the shield braced before."""
    r.stand_legs(pose, 0.0, 0.0, width=0.14)
    at = attack_moment(u, 0.2, 0.62, 0.95)
    spine_upright(pose, lean=attack_value(at, 6.0, 0.0, 12.0))
    pose.set("chest", r.q_euler(y=attack_value(at, 0.0, 18.0, -10.0)))
    pose.set_world("head", r.q_euler(x=attack_value(at, 8.0, 0.0, 10.0)))
    skirt_flaps(pose, "robe_front", "robe_back", margin=1.0)
    _priest_arms(pose, attack_value(at, (-0.2, 1.0, 0.42), (-0.22, 1.04, 0.38), (-0.2, 0.98, 0.46)),
                 attack_value(at, (0.33, 1.0, 0.3), (0.34, 1.45, -0.02), (0.2, 1.25, 0.62)), attack_value(at, 14.0, -18.0, 80.0),
                 wand_roll=attack_value(at, 10.0, 14.0, 4.0))


PRIEST = Hero("priest", "priest_hero.glb", PRIEST_RIG, build_priest_hero, PRIEST_GAIT, priest_idle, priest_run, priest_air, {"Hex": (1.0, priest_hex)})
HEROES["priest"] = PRIEST
