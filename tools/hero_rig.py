"""The heroes' shared skeleton and the maths their clips are made with: quaternions, forward kinematics, two-bone leg IK, and the run cycle's feet.
Each class's model and clips are in its own build (make_placeholder_models.py); this file is only the tool that poses them. Plain Python, no dependencies.

A hero's skeleton (HeroRig) has world-axis bind frames, like the quartermaster's: a joint's rest pose has no turn, so a rotation in a clip turns it about its
own point. The joints:
    root                  at the feet; never moves
      hips                the pelvis; its translation is the body's height and bob
        spine             a pivot above the pelvis (bound to nothing): it undoes the hips' turn when running to the side, so the chest faces the aim
          chest           the torso; the upper-body layer's root (an attack is laid over the chest and all under it)
            head
            upper_arm_l, forearm_l, hand_l     the -X arm
            upper_arm_r, forearm_r, hand_r     the +X arm
            + any props a class adds under a hand (a bow's string, an arrow)
        thigh_l, shin_l, foot_l                the -X leg
        thigh_r, shin_r, foot_r                the +X leg

The run is posed by where the feet are, not by joint angles: each foot is planted on the ground and slides back under the body exactly as far as the body goes
forward (the game drives the clip's phase by the distance the hero covers, so a planted foot stays put), then swings forward through the air. The legs reach
their feet by IK and the feet are kept flat on the ground while planted.
"""

import math


# ---------------------------------------------------------------------------------------------------------------------------------------------------------
# Quaternions (x, y, z, w) and vectors

def v_add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def v_sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def v_scale(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def v_dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def v_cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def v_len(a):
    return math.sqrt(v_dot(a, a))


def v_norm(a):
    length = v_len(a)
    return (a[0] / length, a[1] / length, a[2] / length) if length > 1e-9 else (0.0, 0.0, 0.0)


def v_lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


Q_IDENTITY = (0.0, 0.0, 0.0, 1.0)


def q_axis(axis, degrees):
    half = math.radians(degrees) / 2
    x, y, z = v_norm(axis)
    s = math.sin(half)
    return (x * s, y * s, z * s, math.cos(half))


def q_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def q_inv(q):
    return (-q[0], -q[1], -q[2], q[3])


def q_rotate(q, v):
    """v turned by q."""
    qv = (v[0], v[1], v[2], 0.0)
    x, y, z, _ = q_mul(q_mul(q, qv), q_inv(q))
    return (x, y, z)


def q_euler(x=0.0, y=0.0, z=0.0):
    """A turn of x degrees about X, then z about Z, then y about Y (each about the joint's own point). For a joint facing +Z with a limb hanging down (-Y):
    x < 0 swings the limb forward (+Z), x > 0 back; z > 0 swings it toward +X; y > 0 turns the front (+Z) toward +X."""
    return q_mul(q_mul(q_axis((0, 1, 0), y), q_axis((0, 0, 1), z)), q_axis((1, 0, 0), x))


def q_slerp(a, b, t):
    dot = sum(a[i] * b[i] for i in range(4))
    if dot < 0:
        b, dot = tuple(-c for c in b), -dot
    if dot > 0.9995:
        out = tuple(a[i] + (b[i] - a[i]) * t for i in range(4))
    else:
        theta = math.acos(dot)
        sa, sb = math.sin((1 - t) * theta), math.sin(t * theta)
        out = tuple((a[i] * sa + b[i] * sb) / math.sin(theta) for i in range(4))
    n = math.sqrt(sum(c * c for c in out))
    return tuple(c / n for c in out)


def q_from_basis(x_axis, y_axis, z_axis):
    """The rotation that takes X, Y, Z to the given orthonormal axes."""
    m00, m10, m20 = x_axis
    m01, m11, m21 = y_axis
    m02, m12, m22 = z_axis
    trace = m00 + m11 + m22
    if trace > 0:
        s = math.sqrt(trace + 1.0) * 2
        return ((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s)
    if m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        return (0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s)
    if m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        return ((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s)
    s = math.sqrt(1.0 + m22 - m00 - m11) * 2
    return ((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s)


def q_aim(down, forward):
    """The rotation that points a limb's rest direction (-Y, hanging) along <down>, keeping its front (+Z) as near <forward> as it can."""
    y_axis = v_norm(v_scale(down, -1.0))
    z_axis = v_sub(forward, v_scale(y_axis, v_dot(forward, y_axis)))
    if v_len(z_axis) < 1e-6:
        z_axis = v_sub((0.0, 0.0, 1.0), v_scale(y_axis, y_axis[2]))
    z_axis = v_norm(z_axis)
    x_axis = v_cross(y_axis, z_axis)
    return q_from_basis(x_axis, y_axis, z_axis)


def q_look(forward, up=(0.0, 1.0, 0.0)):
    """The rotation that points +Z along <forward>, keeping +Y as near <up> as it can."""
    z_axis = v_norm(forward)
    x_axis = v_cross(up, z_axis)
    if v_len(x_axis) < 1e-6:
        x_axis = v_cross((0.0, 0.0, 1.0) if abs(z_axis[2]) < 0.9 else (1.0, 0.0, 0.0), z_axis)
    x_axis = v_norm(x_axis)
    return q_from_basis(x_axis, v_cross(z_axis, x_axis), z_axis)


def smooth(t, t0, t1):
    """0 before t0, 1 after t1, eased in between."""
    if t <= t0:
        return 0.0
    if t >= t1:
        return 1.0
    u = (t - t0) / (t1 - t0)
    return u * u * (3 - 2 * u)


def ease(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


# ---------------------------------------------------------------------------------------------------------------------------------------------------------
# The skeleton

class HeroRig:
    """A hero's joints, placed for its build. <extra> adds a class's own joints: (name, parent name, bind position)."""

    def __init__(self, hip_y=0.86, chest_y=1.02, neck_y=1.44, shoulder=(0.29, 1.38), elbow_y=1.12, wrist_y=0.88, leg_x=0.11, knee_y=0.47,
                 ankle_y=0.1, spine_y=0.94, toe=0.12, heel=0.08, extra=()):
        sx, sy = shoulder
        self.leg_x, self.hip_y, self.ankle_y, self.toe, self.heel = leg_x, hip_y, ankle_y, toe, heel
        self.thigh_y = hip_y - 0.04
        self.thigh = self.thigh_y - knee_y
        self.shin = knee_y - ankle_y
        joints = [
            ("root", None, (0.0, 0.0, 0.0)),
            ("hips", "root", (0.0, hip_y, 0.0)),
            ("spine", "hips", (0.0, spine_y, 0.0)),
            ("chest", "spine", (0.0, chest_y, 0.0)),
            ("head", "chest", (0.0, neck_y, 0.0)),
            ("upper_arm_l", "chest", (-sx, sy, 0.0)),
            ("forearm_l", "upper_arm_l", (-sx, elbow_y, 0.0)),
            ("hand_l", "forearm_l", (-sx, wrist_y, 0.0)),
            ("upper_arm_r", "chest", (sx, sy, 0.0)),
            ("forearm_r", "upper_arm_r", (sx, elbow_y, 0.0)),
            ("hand_r", "forearm_r", (sx, wrist_y, 0.0)),
            ("thigh_l", "hips", (-leg_x, self.thigh_y, 0.0)),
            ("shin_l", "thigh_l", (-leg_x, knee_y, 0.0)),
            ("foot_l", "shin_l", (-leg_x, ankle_y, 0.0)),
            ("thigh_r", "hips", (leg_x, self.thigh_y, 0.0)),
            ("shin_r", "thigh_r", (leg_x, knee_y, 0.0)),
            ("foot_r", "shin_r", (leg_x, ankle_y, 0.0)),
        ]
        joints += list(extra)
        self.names = [name for name, _, _ in joints]
        self.index = {name: i for i, name in enumerate(self.names)}
        self.parent = {name: parent for name, parent, _ in joints}
        self.bind = {name: position for name, _, position in joints}

    def gltf_joints(self):
        """As write_skinned_glb wants them: (name, parent index or None, bind position), parents first."""
        return tuple((name, None if self.parent[name] is None else self.index[self.parent[name]], self.bind[name]) for name in self.names)

    def child_of(self, name):
        """The first joint hanging from <name>."""
        return next(child for child in self.names if self.parent[child] == name)

    def offset(self, name):
        parent = self.parent[name]
        return v_sub(self.bind[name], self.bind[parent]) if parent else self.bind[name]

    def world(self, local):
        """Forward kinematics: {joint: (world rotation, world position)} from {joint: (local rotation, translation or None)}; joints not given keep their rest."""
        out = {}
        for name in self.names:
            rotation, translation = local.get(name, (Q_IDENTITY, None))
            parent = self.parent[name]
            if parent is None:
                out[name] = (rotation, translation or self.bind[name])
                continue
            p_rot, p_pos = out[parent]
            step = translation if translation is not None else self.offset(name)
            out[name] = (q_mul(p_rot, rotation), v_add(p_pos, q_rotate(p_rot, step)))
        return out


class Pose:
    """A pose being built: the hips' place and turn, world rotations for the legs (from IK) and local rotations for the rest.
    <local> is what a clip writes: {joint: (local rotation, translation or None)}."""

    def __init__(self, rig):
        self.rig = rig
        self.local = {}

    def set(self, name, rotation, translation=None):
        self.local[name] = (rotation, translation)

    def world(self):
        return self.rig.world(self.local)

    def set_world(self, name, world_rotation):
        """Gives <name> this world rotation, whatever its parents are doing (they must be set first)."""
        parent_rotation = self.world()[self.rig.parent[name]][0]
        self.local[name] = (q_mul(q_inv(parent_rotation), world_rotation), self.local.get(name, (None, None))[1])

    def frame(self):
        """{joint: (rotation, translation or None)} for every joint, as write_skinned_glb samples it. The hips always carry a translation."""
        out = {}
        for name in self.rig.names[1:]:
            rotation, translation = self.local.get(name, (Q_IDENTITY, None))
            if name == "hips" and translation is None:
                translation = self.rig.bind["hips"]
            out[name] = (rotation, translation)
        return out


def two_bone(pose, upper, lower, target, pole, front):
    """Reaches the <upper>-<lower> limb's end (the next joint down from <lower>) to <target> in model space, the middle joint (knee, elbow) bending toward
    <pole>, each bone's front (+Z at rest) kept as near <front> as it can be. Returns how far short it fell (0 if it reached)."""
    rig = pose.rig
    root = pose.world()[upper][1]
    end = rig.child_of(lower)
    a = v_len(rig.offset(lower))
    b = v_len(rig.offset(end))
    to_target = v_sub(target, root)
    dist = v_len(to_target)
    reach = (a + b) * 0.999
    short = max(0.0, dist - reach)
    dist = min(max(dist, abs(a - b) + 1e-3), reach)
    d = v_norm(to_target)
    cos_root = (a * a + dist * dist - b * b) / (2 * a * dist)
    sin_root = math.sqrt(max(0.0, 1 - cos_root * cos_root))
    bend = v_sub(pole, v_scale(d, v_dot(pole, d)))
    bend = v_norm(bend) if v_len(bend) > 1e-6 else v_norm(v_cross(d, (1.0, 0.0, 0.0)))
    middle = v_add(root, v_add(v_scale(d, a * cos_root), v_scale(bend, a * sin_root)))
    reached = v_add(root, v_scale(d, dist))
    pose.set_world(upper, q_aim(v_sub(middle, root), front))
    pose.set_world(lower, q_aim(v_sub(reached, middle), front))
    return short


def leg_ik(pose, side, ankle, forward, toe_pitch=0.0, foot_forward=None):
    """Reaches the <side> ("l"/"r") leg's ankle to <ankle>, the knee bending toward <forward>, and sets the foot level along <foot_forward> (default
    <forward>, flattened) with its toe pitched down by <toe_pitch> degrees (up if negative). Returns how far short it fell (0 if it reached)."""
    short = two_bone(pose, f"thigh_{side}", f"shin_{side}", ankle, forward, forward)
    flat = foot_forward or forward
    flat = v_norm((flat[0], 0.0, flat[2]))
    yaw = math.degrees(math.atan2(flat[0], flat[2]))
    pose.set_world(f"foot_{side}", q_mul(q_axis((0, 1, 0), yaw), q_axis((1, 0, 0), toe_pitch)))
    return short


def arm_ik(pose, side, wrist, elbow_pole, front=(0.0, 1.0, 0.0)):
    """Reaches the <side> arm's wrist to <wrist>, the elbow bending toward <elbow_pole>."""
    return two_bone(pose, f"upper_arm_{side}", f"forearm_{side}", wrist, elbow_pole, front)


def aim_arm(pose, side, direction, bend=0.0, front=(0.0, 1.0, 0.0), elbow_pole=None):
    """Points the <side> arm along <direction> (model space), the elbow bent <bend> degrees (toward <elbow_pole>, default the arm's front)."""
    rig = pose.rig
    shoulder = pose.world()[f"upper_arm_{side}"][1]
    a = v_len(rig.offset(f"forearm_{side}"))
    b = v_len(rig.offset(f"hand_{side}"))
    reach = math.sqrt(a * a + b * b + 2 * a * b * math.cos(math.radians(bend)))
    return arm_ik(pose, side, v_add(shoulder, v_scale(v_norm(direction), reach * 0.9995)), elbow_pole or front, front)


# ---------------------------------------------------------------------------------------------------------------------------------------------------------
# The run cycle's feet

class Gait:
    """One running stride, two steps, over <cycle> metres of ground. Each foot is planted for <stance> of the cycle (the rest it swings, and for
    0.5 - stance of each step both feet are off the ground), lifted <lift> m at the most, and the hips bob <bob> m about <hip_drop> below standing."""

    def __init__(self, cycle=2.6, stance=0.34, lift=0.2, bob=0.035, hip_drop=0.1, width=0.1, lean=9.0):
        self.cycle, self.stance, self.lift, self.bob, self.hip_drop, self.width, self.lean = cycle, stance, lift, bob, hip_drop, width, lean

    def foot(self, phase):
        """A foot's place along the way of travel (metres ahead of the body) and its height, <phase> (0 to 1) into its own cycle, 0 = just planted.
        Also the toe's pitch in degrees."""
        stance, cycle = self.stance, self.cycle
        half = stance * cycle / 2
        phase %= 1.0
        if phase < stance:
            along = half - phase * cycle
            pitch = -10.0 * (1 - phase / stance) ** 3 + 22.0 * smooth(phase / stance, 0.7, 1.0)   # heel lands toe-up, rolls onto the toe
            return along, 0.0, pitch
        u = (phase - stance) / (1 - stance)
        along = -half + 2 * half * ease(u)
        height = self.lift * math.sin(math.pi * min(1.0, u ** 0.75))      # the heel kicks up early, then the foot reaches forward low
        pitch = 22.0 * (1 - smooth(u, 0.0, 0.45)) - 10.0 * smooth(u, 0.6, 1.0)
        return along, height, pitch

    def hips(self, phase):
        """The hips' drop below standing (lowest mid-stance, highest mid-flight) and their sway and roll toward the planted foot."""
        low = self.stance / 2
        drop = self.hip_drop + self.bob * math.cos(4 * math.pi * (phase - low))
        sway = math.cos(2 * math.pi * (phase - low))                        # +1 over the left foot, -1 over the right
        return drop, sway


def sole_lift(rig, pitch):
    """How far the ankle must rise for a foot tipped <pitch> degrees (toe down if positive) to rest on its toe or heel rather than sink into the ground."""
    p = math.radians(pitch)
    reach = rig.toe if p > 0 else rig.heel
    return max(0.0, rig.ankle_y * math.cos(p) + reach * math.sin(abs(p)) - rig.ankle_y)


def run_legs(pose, gait, phase, heading_deg, hips_turn_deg=None, drop_scale=1.0):
    """Poses the hips and legs <phase> into a run toward <heading_deg> (0 = ahead, +Z; positive toward +X). The hips turn part of the way toward the heading
    (<hips_turn_deg>, default 60% of it), and the feet land along the heading, a little apart. Returns the hips' turn (for the spine to undo) and the arms' swing, +1 when the right arm is
    forward (the left foot leads)."""
    rig = pose.rig
    turn = 0.6 * heading_deg if hips_turn_deg is None else hips_turn_deg
    drop, sway = gait.hips(phase)
    heading = math.radians(heading_deg)
    ahead = (math.sin(heading), 0.0, math.cos(heading))
    side = (math.cos(heading), 0.0, -math.sin(heading))
    twist = 7.0 * math.cos(2 * math.pi * phase)                            # the leading leg's hip forward: the left at 0, the right at 0.5
    hips_rotation = q_mul(q_axis((0, 1, 0), turn + twist), q_mul(q_axis((0, 0, 1), -3.0 * sway), q_axis((1, 0, 0), gait.lean)))
    hips_at = v_add((0.0, rig.hip_y - drop * drop_scale, 0.0), v_scale(side, -0.025 * sway))
    pose.set("hips", hips_rotation, hips_at)
    facing = q_rotate(q_axis((0, 1, 0), turn), (0.0, 0.0, 1.0))
    short = 0.0
    for name, sign, offset in (("l", -1.0, 0.0), ("r", 1.0, 0.5)):
        along, height, pitch = gait.foot(phase + offset)
        height = max(height, sole_lift(rig, pitch))
        ankle = v_add(v_add(v_scale(ahead, along), v_scale(side, sign * gait.width)), (0.0, rig.ankle_y + height, 0.0))
        short = max(short, leg_ik(pose, name, ankle, facing, pitch, facing))
    pose.shortfall = max(getattr(pose, "shortfall", 0.0), short)
    return turn, math.cos(2 * math.pi * phase)


def stand_legs(pose, breath=0.0, weight=0.0, width=None, crouch=0.0):
    """Standing: the feet planted under the hips, <weight> shifting onto one (+ onto the right), <crouch> m of knee bend."""
    rig = pose.rig
    w = rig.leg_x + 0.02 if width is None else width
    pose.set("hips", q_mul(q_axis((0, 0, 1), 1.5 * weight), q_axis((0, 1, 0), 2.0 * weight)),
             (0.03 * weight, rig.hip_y - 0.015 - crouch - 0.004 * breath, 0.0))
    for name, sign in (("l", -1.0), ("r", 1.0)):
        ankle = (sign * w, rig.ankle_y, 0.02 * sign)
        leg_ik(pose, name, ankle, (0.12 * sign, 0.0, 1.0), 0.0, (0.15 * sign, 0.0, 1.0))


def airborne_legs(pose, tuck=1.0):
    """In the air: knees up, one leg ahead of the other."""
    rig = pose.rig
    pose.set("hips", q_axis((1, 0, 0), 6.0), (0.0, rig.hip_y + 0.02, 0.0))
    for name, sign, ahead in (("l", -1.0, 0.22), ("r", 1.0, -0.12)):
        ankle = (sign * rig.leg_x, rig.ankle_y + 0.28 * tuck, ahead * tuck)
        leg_ik(pose, name, ankle, (0.0, 0.0, 1.0), 20.0 * tuck, (0.0, 0.0, 1.0))


def check_feet(rig, frames, planted):
    """For tests of a clip: the lowest foot sole's height in each frame (should be ~0 while a foot is planted)."""
    lows = []
    for local in frames:
        world = rig.world(local)
        lows.append(min(world[f"foot_{s}"][1][1] - rig.ankle_y for s in "lr"))
    return lows
