"""Draws a skinned hero's clips as contact sheets, to look at poses without the game: each row is one clip, each column a moment in it, seen from the side
(+X), the front (+Z) or three-quarters. Needs Pillow (pip install pillow); the model script itself needs nothing.

    python tools/preview_hero.py ranger [out.png] [--view side|front|three|back] [--frames 8]
"""

import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import hero_rig as rig_math  # noqa: E402
import make_placeholder_models as models  # noqa: E402

CELL = 220
SCALE = 95          # pixels per metre
LIGHT = rig_math.v_norm((0.5, 0.8, 0.6))


def skin(mesh, rig, local):
    """Each vertex and normal of <mesh> moved by its joint in the pose <local> ({joint: (rotation, translation[, scale])})."""
    frame = {name: (value[0], value[1]) for name, value in local.items()}
    scales = {name: value[2] for name, value in local.items() if len(value) > 2 and value[2] is not None}
    world = rig.world(frame)
    positions, normals = [], []
    for p, n, j in zip(mesh.positions, mesh.normals, mesh.joints):
        name = rig.names[j]
        rotation, at = world[name]
        offset = rig_math.v_sub(p, rig.bind[name])
        s = scales.get(name)
        if s is not None:
            offset = (offset[0] * s[0], offset[1] * s[1], offset[2] * s[2])
        positions.append(rig_math.v_add(at, rig_math.q_rotate(rotation, offset)))
        normals.append(rig_math.q_rotate(rotation, n))
    return positions, normals


def project(view, p):
    """Screen x, y and depth (larger is nearer) for a model-space point."""
    x, y, z = p
    if view == "side":            # looking at the hero's right side (+X), its front to the right of the picture
        return z, y, x
    if view == "front":
        return -x, y, z
    if view == "back":
        return x, y, -z
    a = math.radians(40)           # three-quarters: from front-right
    return -x * math.cos(a) + z * math.sin(a), y, x * math.sin(a) + z * math.cos(a)


def draw(image, mesh, positions, normals, view, origin):
    painter = ImageDraw.Draw(image)
    ox, oy = origin
    faces = []
    for t in range(0, len(mesh.indices), 3):
        ids = mesh.indices[t:t + 3]
        pts = [project(view, positions[i]) for i in ids]
        depth = sum(p[2] for p in pts) / 3
        n = normals[ids[0]]
        u = mesh.uvs[ids[0]][0]
        colour = models.PALETTE[models.COLOURS[min(len(models.COLOURS) - 1, int(u * len(models.COLOURS)))]]
        faces.append((depth, pts, n, colour))
    faces.sort(key=lambda f: f[0])
    for _, pts, n, colour in faces:
        shade = 0.45 + 0.55 * max(0.0, rig_math.v_dot(n, LIGHT))
        fill = tuple(int(c * shade) for c in colour)
        painter.polygon([(ox + p[0] * SCALE, oy - p[1] * SCALE) for p in pts], fill=fill)
    painter.line([(ox - CELL / 2 + 8, oy), (ox + CELL / 2 - 8, oy)], fill=(90, 90, 90))


def sheet(mesh, rig, clips, path, view="side", frames=8):
    """<clips>: [(label, seconds, pose function)]. Writes a contact sheet of each clip at <frames> moments."""
    image = Image.new("RGB", (CELL * frames, CELL * len(clips) + 4), (236, 232, 222))
    painter = ImageDraw.Draw(image)
    for row, (label, seconds, pose) in enumerate(clips):
        for column in range(frames):
            t = seconds * column / frames
            positions, normals = skin(mesh, rig, pose(t))
            draw(image, mesh, positions, normals, view, (CELL * column + CELL / 2, CELL * row + CELL - 14))
            painter.text((CELL * column + 4, CELL * row + 4), f"{label} {t:.2f}", fill=(20, 20, 20))
    image.save(path)
    return path


if __name__ == "__main__":
    import hero_models

    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    options = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    hero = hero_models.HEROES[args[0] if args else "ranger"]
    out = Path(args[1]) if len(args) > 1 else Path(f"{hero.name}_{options.get('view', 'side')}.png")
    wanted = options.get("clips")
    clips = [(name, seconds, pose) for name, (seconds, pose) in hero.clips().items() if not wanted or name in wanted.split(",")]
    print(sheet(hero.mesh(), hero.rig, clips, out, options.get("view", "side"), int(options.get("frames", 8))))
