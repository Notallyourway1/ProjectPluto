"""Build the Project Pluto (SLAM) model and texture with Pillow and taniwha's mu.py.

Usage: python3 -I Source/BuildPluto.py MU_DIR ASSET_DIR FONT.ttf
  MU_DIR     folder holding io_object_mu's mu.py (tools/io_object_mu)
  ASSET_DIR  output folder (GameData/ProjectPluto/Parts/Assets)

Coordinates are Blender's (Z is the missile's long axis, nose +Z); mu.py converts to Unity on write,
so +Z here is the part's +Y in KSP. The belly is +Y here (Unity +Z, the vessel's "down"); the top,
where the warhead hatches are, is -Y.
"""
import argparse
import math
import random
import sys
from pathlib import Path

LENGTH = 26.8
RADIUS = 0.75
BASE = -LENGTH / 2
TOP = LENGTH / 2
NOSE_START = TOP - 9.8

# Body profile (height, radius): nozzle, boat-tail, straight body, ogive nose.
BODY = [(BASE, 0.56), (BASE + 0.35, 0.66), (BASE + 1.0, RADIUS)]
BODY += [(h, RADIUS) for h in (BASE + 4.0, BASE + 8.0, 0.0, NOSE_START - 4.0)]
for i in range(13):
    f = i / 12
    BODY.append((NOSE_START + f * (TOP - NOSE_START), max(0.015, RADIUS * (1 - f * f) ** 0.62)))

# Angles around the body, degrees from Blender +X. 90 = belly (+Y), 270 = top (-Y).
FIN_ANGLES = (270, 30, 150)
BOOSTER_ANGLES = (90, 210, 330)
BOOSTER_RADIUS = 0.36
BOOSTER_LENGTH = 7.2

# Texture: rows 0..SWATCH_ROWS hold flat colour swatches; the body uses the rest.
TEX = 1024
SWATCH_V = 0.012  # UV v of the swatch strip centre
SWATCHES = {"white": 0.125, "dark": 0.375, "steel": 0.625, "gold": 0.875}
BODY_V0 = 0.03
STEEL = (150, 154, 158)
GOLD = (201, 160, 62)
WHITE = (226, 226, 220)
DARK = (46, 46, 50)


def body_v(height):
    return BODY_V0 + (1 - BODY_V0) * (height - BASE) / (TOP - BASE)


def quat_z(degrees):
    half = math.radians(degrees) / 2
    return (math.cos(half), 0, 0, math.sin(half))


def new_mesh(Mu):
    mesh = Mu.MuMesh()
    mesh.verts, mesh.normals, mesh.uvs, mesh.submeshes = [], [], [], [[]]
    return mesh


def add_lathe(mesh, profile, segments, centre=(0.0, 0.0), uv=None, caps=False):
    """Revolve (height, radius) points around the Z axis through `centre`."""
    cx, cy = centre
    start = len(mesh.verts)
    faces = mesh.submeshes[0]
    for row, (height, radius) in enumerate(profile):
        low = profile[max(0, row - 1)]
        high = profile[min(len(profile) - 1, row + 1)]
        dr = (high[1] - low[1]) / ((high[0] - low[0]) or 1e-6)
        n = math.sqrt(1 + dr * dr)
        for s in range(segments + 1):
            t = math.tau * s / segments
            x, y = math.cos(t), math.sin(t)
            mesh.verts.append((cx + radius * x, cy + radius * y, height))
            mesh.normals.append((x / n, y / n, -dr / n))
            mesh.uvs.append(uv if uv else (s / segments, body_v(height)))
    for row in range(len(profile) - 1):
        for s in range(segments):
            lo = start + row * (segments + 1) + s
            hi = lo + segments + 1
            faces.extend(((lo, lo + 1, hi), (lo + 1, hi + 1, hi)))
    if caps:
        for end, direction in ((0, -1), (len(profile) - 1, 1)):
            height, radius = profile[end]
            centre_index = len(mesh.verts)
            mesh.verts.append((cx, cy, height))
            mesh.normals.append((0, 0, direction))
            mesh.uvs.append(uv if uv else (0.5, body_v(height)))
            for s in range(segments):
                t = math.tau * s / segments
                mesh.verts.append((cx + radius * math.cos(t), cy + radius * math.sin(t), height))
                mesh.normals.append((0, 0, direction))
                mesh.uvs.append(uv if uv else (s / segments, body_v(height)))
            for s in range(segments):
                a, b = centre_index + s + 1, centre_index + (s + 1) % segments + 1
                faces.append((centre_index, a, b) if direction > 0 else (centre_index, b, a))


def add_box(mesh, lo, hi, uv):
    """Axis-aligned box, faces wound both ways so it renders from any side."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    corners = [(x, y, z) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
    quads = ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3))
    centre = tuple((a + b) / 2 for a, b in zip(lo, hi))
    faces = mesh.submeshes[0]
    for quad in quads:
        base = len(mesh.verts)
        for i in quad:
            c = corners[i]
            mesh.verts.append(c)
            d = [c[k] - centre[k] for k in range(3)]
            length = math.sqrt(sum(v * v for v in d)) or 1
            mesh.normals.append(tuple(v / length for v in d))
            mesh.uvs.append(uv)
        faces.extend(((base, base + 1, base + 2), (base, base + 2, base + 3)))
        faces.extend(((base, base + 2, base + 1), (base, base + 3, base + 2)))


def add_plate(mesh, root_chord, tip_chord, span, sweep, thickness, uv):
    """Trapezoid fin in its local frame: span along +X, chord along Z, centred on the hinge."""
    half_t = thickness / 2
    outline = [(0, -root_chord / 2), (0, root_chord / 2),
               (span, sweep + tip_chord / 2), (span, sweep - tip_chord / 2)]
    faces = mesh.submeshes[0]
    for side in (-1, 1):
        base = len(mesh.verts)
        for x, z in outline:
            mesh.verts.append((x, side * half_t, z))
            mesh.normals.append((0, side, 0))
            mesh.uvs.append(uv)
        tri = ((base, base + 1, base + 2), (base, base + 2, base + 3))
        for a, b, c in tri:
            faces.append((a, b, c) if side > 0 else (a, c, b))
    # Edge strip so the plate has visible thickness.
    for i in range(4):
        x0, z0 = outline[i]
        x1, z1 = outline[(i + 1) % 4]
        base = len(mesh.verts)
        for x, z, y in ((x0, z0, -half_t), (x1, z1, -half_t), (x1, z1, half_t), (x0, z0, half_t)):
            mesh.verts.append((x, y, z))
            mesh.normals.append((z1 - z0, 0, x0 - x1))
            mesh.uvs.append(uv)
        faces.extend(((base, base + 1, base + 2), (base, base + 2, base + 3)))
        faces.extend(((base, base + 2, base + 1), (base, base + 3, base + 2)))


def build_model(Mu, asset_dir):
    def node(name, position=(0, 0, 0), rotation=(1, 0, 0, 0)):
        obj = Mu.MuObject(name)
        obj.transform = Mu.MuTransform()
        obj.transform.name = name
        obj.transform.localPosition = position
        obj.transform.localRotation = rotation
        obj.transform.localScale = (1, 1, 1)
        obj.tag_and_layer = Mu.MuTagLayer()
        obj.tag_and_layer.tag, obj.tag_and_layer.layer = "Untagged", 0
        return obj

    def visible(obj, mesh, material_index=0):
        obj.shared_mesh = mesh
        obj.renderer = Mu.MuRenderer()
        obj.renderer.materials = [material_index]

    def convex_collider(name, mesh, position=(0, 0, 0)):
        col = node(name, position)
        col.collider = Mu.MuColliderMesh(True)
        col.collider.isTrigger = 0
        col.collider.convex = 1
        col.collider.mesh = mesh
        return col

    swatch = {k: (u, SWATCH_V) for k, u in SWATCHES.items()}

    mu = Mu.Mu("PlutoExterior")
    root = node("PlutoExterior")
    mu.obj = root

    body = node("Body")
    body_mesh = new_mesh(Mu)
    add_lathe(body_mesh, BODY, 40, caps=True)
    visible(body, body_mesh)
    root.children.append(body)
    # Nozzle insert with its own emissive material: the plugin makes it glow with exhaust temperature.
    nozzle = node("NozzleGlow")
    nozzle_mesh = new_mesh(Mu)
    add_lathe(nozzle_mesh, [(BASE - 0.02, 0.0), (BASE - 0.01, 0.5), (BASE + 0.3, 0.5)], 24, uv=swatch["dark"])
    visible(nozzle, nozzle_mesh, 1)
    root.children.append(nozzle)
    collider_mesh = new_mesh(Mu)
    add_lathe(collider_mesh, [BODY[0], BODY[2], (NOSE_START, RADIUS), (TOP - 3, 0.45), BODY[-1]], 12, caps=True)
    root.children.append(convex_collider("BodyCollider", collider_mesh))

    # Ventral scoop intake under the forward body, facing the nose.
    scoop = node("Scoop")
    scoop_mesh = new_mesh(Mu)
    add_box(scoop_mesh, (-0.42, RADIUS - 0.08, 1.5), (0.42, RADIUS + 0.38, 5.0), swatch["steel"])
    add_box(scoop_mesh, (-0.34, RADIUS + 0.02, 4.9), (0.34, RADIUS + 0.30, 5.02), swatch["dark"])
    visible(scoop, scoop_mesh)
    root.children.append(scoop)
    # Intake transform: forward (Unity +Z) turned to face the nose (Unity +Y).
    root.children.append(node("Intake", (0, RADIUS + 0.2, 5.0), (0.70710678, 0.70710678, 0, 0)))

    # Tail fins (fixed) and canards (moving), each built in a frame whose local X points outward.
    for i, angle in enumerate(FIN_ANGLES, start=1):
        t = math.radians(angle)
        fin = node(f"Fin{i}", (RADIUS * math.cos(t), RADIUS * math.sin(t), BASE + 2.1), quat_z(angle))
        mesh = new_mesh(Mu)
        add_plate(mesh, 3.0, 1.4, 1.25, -0.6, 0.08, swatch["steel"])
        visible(fin, mesh)
        root.children.append(fin)

        canard = node(f"Canard{i}", (RADIUS * 0.8 * math.cos(t), RADIUS * 0.8 * math.sin(t), NOSE_START + 1.2),
                      quat_z(angle))
        mesh = new_mesh(Mu)
        add_plate(mesh, 1.3, 0.6, 0.62, -0.25, 0.06, swatch["gold"])
        visible(canard, mesh)
        root.children.append(canard)

    # Three strap-on boosters around the tail, each with its own collider and thrust transform.
    booster_profile = [(0.0, 0.26), (0.25, BOOSTER_RADIUS), (BOOSTER_LENGTH - 1.2, BOOSTER_RADIUS),
                       (BOOSTER_LENGTH - 0.6, 0.27), (BOOSTER_LENGTH - 0.15, 0.10), (BOOSTER_LENGTH, 0.02)]
    for i, angle in enumerate(BOOSTER_ANGLES, start=1):
        t = math.radians(angle)
        offset = RADIUS + BOOSTER_RADIUS + 0.02
        position = (offset * math.cos(t), offset * math.sin(t), BASE + 0.1)
        booster = node(f"Booster{i}", position)
        mesh = new_mesh(Mu)
        add_lathe(mesh, booster_profile, 20, uv=swatch["white"], caps=True)
        add_lathe(mesh, [(-0.25, 0.30), (0.0, 0.24)], 16, uv=swatch["dark"])
        # Strut back to the body.
        add_box(mesh, (-0.06, -0.06, 1.0), (0.06, 0.06, 1.3), swatch["steel"])
        visible(booster, mesh)
        col_mesh = new_mesh(Mu)
        add_lathe(col_mesh, [(0.0, BOOSTER_RADIUS), (BOOSTER_LENGTH, BOOSTER_RADIUS)], 8, caps=True)
        booster.children.append(convex_collider(f"Booster{i}Col", col_mesh))
        # Thrust transform: forward turned to point aft (Unity -Y), at the nozzle.
        booster.children.append(node("boostThrust", (0, 0, -0.25), (0.70710678, -0.70710678, 0, 0)))
        root.children.append(booster)

    root.children.append(node("ramjetThrust", (0, 0, BASE - 0.05), (0.70710678, -0.70710678, 0, 0)))
    root.children.append(node("warheadHatch", (0, -RADIUS - 0.3, -1.0)))

    texture = Mu.MuTexture()
    texture.name, texture.type = "PlutoSkin.png", 0
    mu.textures = [texture]
    material = Mu.MuMaterial()
    material.name = "Pluto skin"
    material.shaderName = "KSP/Diffuse"
    tex = Mu.MuMatTex()
    tex.index, tex.scale, tex.offset = 0, (1, 1), (0, 0)
    material.textureProperties["_MainTex"] = tex
    glow = Mu.MuMaterial()
    glow.name = "Pluto nozzle glow"
    glow.shaderName = "KSP/Emissive/Diffuse"
    glow_tex = Mu.MuMatTex()
    glow_tex.index, glow_tex.scale, glow_tex.offset = 0, (1, 1), (0, 0)
    glow.textureProperties["_MainTex"] = glow_tex
    glow.colorProperties["_EmissiveColor"] = (0.0, 0.0, 0.0, 1.0)
    mu.materials = [material, glow]
    mu.write(str(asset_dir / "PlutoExterior.mu"))


def build_texture(path, font_path):
    from PIL import Image, ImageDraw, ImageFont

    rng = random.Random(1964)
    image = Image.new("RGB", (TEX, TEX))
    pixels = image.load()
    gold_from = body_v(NOSE_START)
    for y in range(TEX):
        v = 1 - (y + 0.5) / TEX
        for x in range(TEX):
            u = (x + 0.5) / TEX
            if v < BODY_V0:
                colour = (WHITE if u < 0.25 else DARK if u < 0.5 else STEEL if u < 0.75 else GOLD)
                pixels[x, y] = colour
                continue
            base = GOLD if v >= gold_from else STEEL
            seam = -16 if (x % 64) < 2 else 0
            noise = rng.randrange(-5, 6)
            pixels[x, y] = tuple(max(0, min(255, c + seam + noise)) for c in base)

    draw = ImageDraw.Draw(image)
    # Panel lines around the body.
    for height in (BASE + 1.0, BASE + 7.5, -1.0, NOSE_START):
        y = int((1 - body_v(height)) * TEX)
        draw.line((0, y, TEX, y), fill=(92, 92, 96), width=2)
    # Sixteen warhead hatches along the top (u = 0.75 is -Y, the top).
    hatch_x = int(0.75 * TEX)
    for i in range(16):
        h0 = -10.5 + i * 0.85
        y0 = int((1 - body_v(h0 + 0.75)) * TEX)
        y1 = int((1 - body_v(h0)) * TEX)
        draw.rectangle((hatch_x - 22, y0, hatch_x + 22, y1), outline=(70, 70, 74), width=2)

    font = ImageFont.truetype(str(font_path), 60)
    small = ImageFont.truetype(str(font_path), 22)
    lettering = Image.new("RGBA", (520, 100))
    text = ImageDraw.Draw(lettering)
    text.text((4, 0), "PLUTO", font=font, fill=(32, 36, 40, 255))
    text.text((8, 66), "SLAM  /  TORY-IIC", font=small, fill=(40, 44, 48, 255))
    stencil = lettering.crop(lettering.getbbox()).rotate(90, expand=True)
    for u in (0.0, 0.5):
        x = int(u * TEX) + 20
        y = int((1 - body_v(1.0)) * TEX)
        image.paste(stencil, (x, y), stencil)
    image.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mu_dir", type=Path)
    parser.add_argument("assets", type=Path)
    parser.add_argument("font", type=Path)
    args = parser.parse_args()
    sys.path.insert(0, str(args.mu_dir.resolve()))
    import mu as Mu  # noqa: E402  (path supplied at run time)

    args.assets.mkdir(parents=True, exist_ok=True)
    build_model(Mu, args.assets)
    build_texture(args.assets / "PlutoSkin.png", args.font)


if __name__ == "__main__":
    main()
