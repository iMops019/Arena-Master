"""The Ranger's Trapper tree: its traps, venom and hawk. Written by make_placeholder_models.py (write_all).

  snare_open.glb        a snare lying armed: a flat iron jaw trap about 0.8 m across - a round base plate, the two toothed jaws laid open flat either side of a
                        pressure plate, the springs, and a chain to a stake
  snare_shut.glb        the same snare sprung: its jaws stood up and clamped together over the plate, teeth locked
  snare_burst.glb       a flat ring of radius 1 (iron-brown with a bright inner edge), scaled as a snare bursts
  caltrops.glb          caltrops scattered over a flat circle of radius 1: little four-pointed iron spikes, one point always up
  venom_drip.glb        a drop of venom, radius 1 (the game draws it tiny): bright green, drawn glowing
  toxic_puff.glb        a puff of a toxic cloud, radius about 1: a lumpy green ball with paler swirls, drawn glowing
  hawk_up.glb, hawk_down.glb
                        the hawk, wings up and wings down (drawn in turn as it flaps), about 1 m across, facing +Z: brown with a pale breast, a hooked gold
                        beak and a barred tail
  hawk_mark.glb         Hawk's Mark: a small glowing arrowhead pointing down, about 0.3 m, hung over the marked enemy

Uses only colours already in make_placeholder_models.PALETTE: adding one changes every model's texture.
"""

import math

from make_placeholder_models import Mesh, sphere, write_glb, _unit_noise


def _ring(m, inner, outer, colour, y=0.0, segments=40):
    """A flat ring (annulus) facing up."""
    for i in range(segments):
        a0, a1 = math.tau * i / segments, math.tau * (i + 1) / segments
        m.facing([(math.cos(a0) * inner, y, math.sin(a0) * inner), (math.cos(a0) * outer, y, math.sin(a0) * outer),
                  (math.cos(a1) * outer, y, math.sin(a1) * outer), (math.cos(a1) * inner, y, math.sin(a1) * inner)], (0.0, 1.0, 0.0), colour)


def _jaw(m, side, raised):
    """One jaw: a half-ring of iron with teeth along its inner edge. Laid open flat on its <side> of the trap (x < 0 or x > 0), or <raised> upright over the
    middle to meet the other."""
    r_in, r_out, segments = 0.3, 0.36, 10
    for i in range(segments):
        a0, a1 = math.pi * i / segments, math.pi * (i + 1) / segments
        # A half-ring in the (u, v) plane: u along Z, v away from the hinge line (x = 0).
        pts = [(math.cos(a0) * r_in, math.sin(a0) * r_in), (math.cos(a0) * r_out, math.sin(a0) * r_out),
               (math.cos(a1) * r_out, math.sin(a1) * r_out), (math.cos(a1) * r_in, math.sin(a1) * r_in)]

        def place(u, v, lift=0.0):
            if raised:
                return (side * 0.02 + side * lift, 0.05 + v, u)   # standing up over the middle
            return (side * v, 0.05 + lift, u)                    # lying flat, reaching out to its side

        top = [place(u, v, 0.03) for u, v in pts]
        bottom = [place(u, v) for u, v in pts]
        out = (side, 0.0, 0.0) if raised else (0.0, 1.0, 0.0)
        m.facing(top, out, "iron")
        m.facing(bottom, (-out[0], -out[1], -out[2]), "iron")
        # A tooth on the inner edge, pointing in toward the middle.
        mid = (a0 + a1) / 2
        u0, v0 = math.cos(a0) * r_in, math.sin(a0) * r_in
        u1, v1 = math.cos(a1) * r_in, math.sin(a1) * r_in
        tip = (math.cos(mid) * (r_in - 0.07), math.sin(mid) * (r_in - 0.07))
        m.facing([place(u0, v0, 0.03), place(u1, v1, 0.03), place(tip[0], tip[1], 0.015)], out, "steel")
        m.facing([place(u0, v0), place(tip[0], tip[1], 0.015), place(u1, v1)], (-out[0], -out[1], -out[2]), "steel")


def _snare(raised):
    m = Mesh()
    # The base plate and the pressure plate in its middle.
    for i in range(16):
        a0, a1 = math.tau * i / 16, math.tau * (i + 1) / 16
        m.facing([(0.0, 0.03, 0.0), (math.cos(a0) * 0.16, 0.03, math.sin(a0) * 0.16), (math.cos(a1) * 0.16, 0.03, math.sin(a1) * 0.16)], (0.0, 1.0, 0.0), "dark")
        m.facing([(0.0, 0.045, 0.0), (math.cos(a0) * 0.11, 0.045, math.sin(a0) * 0.11), (math.cos(a1) * 0.11, 0.045, math.sin(a1) * 0.11)], (0.0, 1.0, 0.0),
                 "iron")
    m.box(-0.42, 0.0, -0.035, 0.42, 0.04, 0.035, "iron")          # the bar the jaws hinge on, and the springs at its ends
    for side in (-1, 1):
        m.box(side * 0.46 - 0.05, 0.0, -0.06, side * 0.46 + 0.05, 0.07, 0.06, "dark")
        m.box(side * 0.46 - 0.035, 0.07, -0.045, side * 0.46 + 0.035, 0.09, 0.045, "steel")
        _jaw(m, side, raised)
    # A chain from one spring to a stake.
    for k in range(5):
        z = -0.1 - 0.07 * k
        m.box(0.44 - 0.015, 0.0, z - 0.03, 0.44 + 0.015, 0.025, z + 0.03, "steel" if k % 2 == 0 else "iron")
    m.prism(0.44, -0.47, 0.025, 0.0, 0.12, "wood", sides=6, top="wood")
    return m


def build_snare_open():
    return _snare(raised=False)


def build_snare_shut():
    return _snare(raised=True)


def build_snare_burst():
    m = Mesh()
    _ring(m, 0.8, 1.0, "earth_light")
    _ring(m, 0.74, 0.8, "shock", y=0.005)
    for i in range(12):                                            # splinters of iron flung out on it
        a = math.tau * i / 12 + 0.2
        r0, r1 = 0.82, 1.08
        m.facing([(math.cos(a - 0.03) * r0, 0.01, math.sin(a - 0.03) * r0), (math.cos(a) * r1, 0.01, math.sin(a) * r1),
                  (math.cos(a + 0.03) * r0, 0.01, math.sin(a + 0.03) * r0)], (0.0, 1.0, 0.0), "iron")
    return m


def _caltrop(m, x, z, s, turn):
    """A caltrop: four iron points from its middle, one straight up and three on the ground."""
    centre = (x, s * 0.35, z)
    points = [(x, s * 1.1, z)] + [(x + math.cos(turn + k * math.tau / 3) * s, 0.0, z + math.sin(turn + k * math.tau / 3) * s) for k in range(3)]
    for p in points:
        axis = (p[0] - centre[0], p[1] - centre[1], p[2] - centre[2])
        length = math.sqrt(sum(a * a for a in axis))
        side = (axis[2] / length, 0.0, -axis[0] / length) if abs(axis[1]) < 0.9 * length else (1.0, 0.0, 0.0)
        w = s * 0.12
        base = [(centre[0] + side[0] * w, centre[1], centre[2] + side[2] * w), (centre[0], centre[1] + w, centre[2]),
                (centre[0] - side[0] * w, centre[1], centre[2] - side[2] * w), (centre[0], centre[1] - w, centre[2])]
        for i in range(4):
            a, b = base[i], base[(i + 1) % 4]
            out = ((a[0] + b[0]) / 2 - centre[0], (a[1] + b[1]) / 2 - centre[1], (a[2] + b[2]) / 2 - centre[2])
            m.facing([a, b, p], out, "steel" if i % 2 == 0 else "iron")


def build_caltrops():
    m = Mesh()
    for i in range(26):
        a = math.tau * _unit_noise(i, 3.1)
        d = 0.95 * math.sqrt(_unit_noise(i, 6.7))
        _caltrop(m, math.cos(a) * d, math.sin(a) * d, 0.07 + 0.03 * _unit_noise(i, 1.9), math.tau * _unit_noise(i, 4.4))
    return m


def build_venom_drip():
    """A drop, radius 1: rounded above, drawn out to a point below."""
    m = Mesh()
    top, bottom = (0.0, 0.8, 0.0), (0.0, -1.6, 0.0)
    ring = [(math.cos(k * math.tau / 6), 0.0, math.sin(k * math.tau / 6)) for k in range(6)]
    for i in range(6):
        a, b = ring[i], ring[(i + 1) % 6]
        m.facing([a, b, top], ((a[0] + b[0]) / 2, 0.4, (a[2] + b[2]) / 2), "venom_light")
        m.facing([b, a, bottom], ((a[0] + b[0]) / 2, -0.4, (a[2] + b[2]) / 2), "venom")
    return m


def build_toxic_puff():
    """A puff of a toxic cloud, radius about 1: a lumpy ball, paler swirls on its top."""
    m = Mesh()
    sphere(m, 0.0, 0.0, 0.0, 1.0, lambda i, k: "venom_light" if i < 2 and (k + i) % 3 == 0 else "venom", stacks=5, slices=8)
    sphere(m, 0.45, 0.25, 0.2, 0.55, lambda i, k: "venom" if (i + k) % 2 else "venom_light", stacks=4, slices=6)
    sphere(m, -0.4, 0.15, -0.3, 0.6, lambda i, k: "venom", stacks=4, slices=6)
    return m


def _hawk(wing_lift):
    """The hawk facing +Z, about 1 m across: <wing_lift> raises (or with a minus, drops) its wingtips."""
    m = Mesh()
    m.box(-0.06, -0.05, -0.2, 0.06, 0.06, 0.16, "feather")               # body
    m.box(-0.05, -0.07, -0.12, 0.05, -0.04, 0.14, "fletching")           # pale breast
    m.box(-0.045, -0.01, 0.16, 0.045, 0.08, 0.26, "feather")             # head
    m.box(-0.04, -0.03, 0.17, 0.04, 0.0, 0.25, "fletching")              # pale throat
    m.pyramid(-0.02, 0.0, 0.26, 0.02, 0.04, 0.29, (0.0, -0.01, 0.34), "gold")   # hooked beak
    m.box(-0.047, 0.035, 0.21, -0.043, 0.055, 0.235, "dark")             # eyes
    m.box(0.043, 0.035, 0.21, 0.047, 0.055, 0.235, "dark")
    for k in range(3):                                                  # the tail, fanned and barred
        x = (k - 1) * 0.045
        colour = "fletching" if k == 1 else "feather"
        m.facing([(x - 0.03, 0.0, -0.2), (x + 0.03, 0.0, -0.2), (x * 2.2 + 0.035, 0.0, -0.42), (x * 2.2 - 0.035, 0.0, -0.42)], (0.0, 1.0, 0.0), colour)
        m.facing([(x - 0.03, -0.005, -0.2), (x * 2.2 - 0.035, -0.005, -0.42), (x * 2.2 + 0.035, -0.005, -0.42), (x + 0.03, -0.005, -0.2)], (0.0, -1.0, 0.0),
                 "leather")
    for side in (-1, 1):                                                # the wings: an inner and an outer panel, the tips lifted or dropped
        root_front, root_back = (side * 0.06, 0.03, 0.1), (side * 0.06, 0.03, -0.12)
        elbow_front, elbow_back = (side * 0.28, 0.03 + wing_lift * 0.45, 0.08), (side * 0.28, 0.03 + wing_lift * 0.45, -0.16)
        tip = (side * 0.52, 0.03 + wing_lift, -0.06)
        for quad, colour in (([root_front, elbow_front, elbow_back, root_back], "feather"),):
            m.facing(quad, (0.0, 1.0, 0.0), colour)
            m.facing(list(reversed(quad)), (0.0, -1.0, 0.0), "fletching")
        m.facing([elbow_front, tip, elbow_back], (0.0, 1.0, 0.0), "feather")
        m.facing([elbow_back, tip, elbow_front], (0.0, -1.0, 0.0), "fletching")
        for f in range(3):                                              # dark primaries fingered out past the tip
            back = (tip[0] - side * 0.05 * f, tip[1] - wing_lift * 0.08 * f, tip[2] - 0.05 - 0.03 * f)
            end = (tip[0] + side * (0.06 - 0.02 * f), tip[1] + wing_lift * 0.05, tip[2] - 0.12 - 0.04 * f)
            m.facing([tip, end, back], (0.0, 1.0, 0.0), "dark")
            m.facing([tip, back, end], (0.0, -1.0, 0.0), "dark")
    return m


def build_hawk_up():
    return _hawk(0.26)


def build_hawk_down():
    return _hawk(-0.16)


def build_hawk_mark():
    """An arrowhead about 0.3 m, pointing down: an upside-down four-sided pyramid of ember, a pale gold cap over it."""
    m = Mesh()
    top, tip = 0.15, -0.15
    ring = [(math.cos(k * math.tau / 4) * 0.11, top, math.sin(k * math.tau / 4) * 0.11) for k in range(4)]
    for i in range(4):
        a, b = ring[i], ring[(i + 1) % 4]
        m.facing([a, b, (0.0, tip, 0.0)], ((a[0] + b[0]) / 2, -0.05, (a[2] + b[2]) / 2), "ember")
        m.facing([b, a, (0.0, top + 0.05, 0.0)], ((a[0] + b[0]) / 2, 0.1, (a[2] + b[2]) / 2), "legendary_light")
    return m


TRAPPER_MODELS = (("snare_open.glb", build_snare_open), ("snare_shut.glb", build_snare_shut), ("snare_burst.glb", build_snare_burst),
                  ("caltrops.glb", build_caltrops), ("venom_drip.glb", build_venom_drip), ("toxic_puff.glb", build_toxic_puff),
                  ("hawk_up.glb", build_hawk_up), ("hawk_down.glb", build_hawk_down), ("hawk_mark.glb", build_hawk_mark))


def write_all(models_dir):
    """Writes this tree's models into <models_dir>."""
    for name, build in TRAPPER_MODELS:
        mesh = build()
        write_glb(mesh, models_dir / name)
        print(f"Wrote {models_dir / name} ({len(mesh.positions)} vertices)")
