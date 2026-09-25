"""Writes blocky stand-in models to assets/models/ until real ones are made. Plain Python, no dependencies:
  ranger_placeholder.glb  the Ranger (tunic, hood, cape, bow, quiver)
  arrow_placeholder.glb   an arrow, centred on its middle, pointing +Z
  ghoul_placeholder.glb   the first enemy: a hunched ghoul with long arms and red eyes
  xp_gem_placeholder.glb  an experience gem: a small glowing-blue crystal, centred on its middle

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
    for name, build in (("ranger_placeholder.glb", build_ranger), ("arrow_placeholder.glb", build_arrow), ("ghoul_placeholder.glb", build_ghoul), ("xp_gem_placeholder.glb", build_xp_gem)):
        mesh = build()
        write_glb(mesh, MODELS / name)
        print(f"Wrote {MODELS / name} ({len(mesh.positions)} vertices, {len(mesh.indices) // 3} triangles)")
