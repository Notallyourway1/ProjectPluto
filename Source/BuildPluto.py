"""Build the Project Pluto (SLAM) model and texture with Pillow and taniwha's mu.py.

Writes PlutoExterior.mu, PlutoLaunchPad.mu and PlutoSkin.png.

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
# Shape follows Vought's SLAM cutaway and 1/10 scale model: a long, nearly straight cone nose with a
# terrain-avoidance antenna probe, warhead bay on top amidships, a ventral intake pod with a centre
# cone that fairs back into the belly ahead of the reactor, and three swept tail fins.
NOSE_LENGTH = 8.6
NOSE_START = TOP - NOSE_LENGTH


def nose_radius(height):
    """Nearly conical nose, eased into the body so the shoulder has no hard kink."""
    s = min(1.0, max(0.0, (TOP - height) / NOSE_LENGTH))
    return max(0.015, RADIUS * s * (2 - s) ** 0.35)


# Body profile (height, radius): nozzle, boat-tail, straight body, cone nose.
BODY = [(BASE, 0.56), (BASE + 0.35, 0.66), (BASE + 1.0, RADIUS)]
BODY += [(h, RADIUS) for h in (BASE + 4.0, BASE + 8.0, 0.0, NOSE_START - 2.0)]
for i in range(17):
    h = NOSE_START + i / 16 * NOSE_LENGTH
    BODY.append((h, nose_radius(h)))

# Ventral intake pod: a lathe about an axis below the body's, so it bulges out of the belly.
# Profile (height, radius); its bottom meets the body's again where it ends.
POD_CENTRE_Y = 0.68
POD = [(1.6, 0.56), (1.42, 0.66), (-1.2, 0.66), (-3.6, 0.48), (-5.2, 0.12), (-5.3, 0.0)]
INTAKE_LIP = 1.6
# Copper forward body, white aft (as on the Vought model).
WHITE_FROM = -5.6
WARHEAD_BAY = (2.7, -3.1)

# Angles around the body, degrees from Blender +X. 90 = belly (+Y), 270 = top (-Y).
FIN_ANGLES = (270, 30, 150)
BOOSTER_ANGLES = (90, 210, 330)
BOOSTER_RADIUS = 0.36
BOOSTER_LENGTH = 7.2

# Texture: rows 0..SWATCH_ROWS hold flat colour swatches; the body uses the rest.
TEX = 1024
TEX_H = 2048  # along the body; around it, 1024 px already covers the much shorter circumference
SWATCH_V = 0.012  # UV v of the swatch strip centre
SWATCHES = {"white": 0.125, "dark": 0.375, "steel": 0.625, "copper": 0.875}
BODY_V0 = 0.03
STEEL = (150, 154, 158)
COPPER = (192, 132, 108)
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
    """Revolve (height, radius) points around the Z axis through `centre`.

    uv: None maps the body texture by height and angle; a (u, v) pair is one flat colour; a function
    of height returns the (u, v) for that height."""
    if callable(uv):
        uv_at = uv
    else:
        uv_at = lambda height: uv  # noqa: E731

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
            mesh.uvs.append(uv_at(height) if uv else (s / segments, body_v(height)))
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
            mesh.uvs.append(uv_at(height) if uv else (0.5, body_v(height)))
            for s in range(segments):
                t = math.tau * s / segments
                mesh.verts.append((cx + radius * math.cos(t), cy + radius * math.sin(t), height))
                mesh.normals.append((0, 0, direction))
                mesh.uvs.append(uv_at(height) if uv else (s / segments, body_v(height)))
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
    # Terrain-avoidance antenna probe ahead of the nose.
    probe_mesh = new_mesh(Mu)
    add_lathe(probe_mesh, [(TOP - 0.3, 0.035), (TOP + 1.2, 0.025), (TOP + 1.25, 0.06), (TOP + 1.35, 0.06),
                           (TOP + 1.45, 0.0)], 8, uv=swatch["steel"])
    probe = node("Probe")
    visible(probe, probe_mesh)
    root.children.append(probe)

    collider_mesh = new_mesh(Mu)
    add_lathe(collider_mesh, [BODY[0], BODY[2], (NOSE_START, RADIUS), (TOP - 3, nose_radius(TOP - 3)), BODY[-1]],
              12, caps=True)
    add_lathe(collider_mesh, [POD[1], POD[2], POD[3], POD[4]], 10, centre=(0.0, POD_CENTRE_Y))
    root.children.append(convex_collider("BodyCollider", collider_mesh))

    # Ventral intake pod under the middle of the body: an open lip with a dark centre cone, faired back
    # into the belly ahead of the reactor. Copper forward, white aft, like the body.
    def skin(height):
        return swatch["copper"] if height > WHITE_FROM else swatch["white"]

    scoop = node("Scoop")
    scoop_mesh = new_mesh(Mu)
    add_lathe(scoop_mesh, POD, 28, centre=(0.0, POD_CENTRE_Y), uv=skin)
    add_lathe(scoop_mesh, [(INTAKE_LIP - 0.4, 0.0), (INTAKE_LIP - 0.02, 0.55), (INTAKE_LIP, 0.56)], 28,
              centre=(0.0, POD_CENTRE_Y), uv=swatch["dark"])
    add_lathe(scoop_mesh, [(INTAKE_LIP - 0.3, 0.34), (INTAKE_LIP + 0.25, 0.30), (INTAKE_LIP + 1.05, 0.0)], 20,
              centre=(0.0, POD_CENTRE_Y), uv=swatch["dark"])
    visible(scoop, scoop_mesh)
    root.children.append(scoop)
    # Intake transform: forward (Unity +Z) turned to face the nose (Unity +Y).
    root.children.append(node("Intake", (0, POD_CENTRE_Y, INTAKE_LIP), (0.70710678, 0.70710678, 0, 0)))

    # Tail fins (fixed) and canards (moving), each built in a frame whose local X points outward.
    for i, angle in enumerate(FIN_ANGLES, start=1):
        t = math.radians(angle)
        fin = node(f"Fin{i}", (RADIUS * math.cos(t), RADIUS * math.sin(t), BASE + 4.3), quat_z(angle))
        mesh = new_mesh(Mu)
        add_plate(mesh, 2.6, 1.0, 1.5, -0.9, 0.08, swatch["white"])
        visible(fin, mesh)
        root.children.append(fin)

        # Small all-moving control fins on the nose cone, about a sixth of the length back from the tip.
        canard_h = TOP - 4.6
        canard_r = nose_radius(canard_h) * 0.9
        canard = node(f"Canard{i}", (canard_r * math.cos(t), canard_r * math.sin(t), canard_h), quat_z(angle))
        mesh = new_mesh(Mu)
        add_plate(mesh, 1.0, 0.35, 0.6, -0.3, 0.05, swatch["copper"])
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
    root.children.append(node("warheadHatch", (0, -RADIUS - 0.3, sum(WARHEAD_BAY) / 2)))

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


def build_launch_pad(Mu, asset_dir):
    """The ground launcher. Blender +Z here is the pad's Unity +Y, pointing up into the missile's belly.

    Rail: the cradle under the belly; the plugin turns it to lie along the missile.
    Stand: pivot under the cradle; the plugin keeps it upright. Its Tower (built 1 m long) is stretched
    to reach the ground and its Foot is moved to the tower's bottom.
    """
    swatch = {k: (u, SWATCH_V) for k, u in SWATCHES.items()}

    def node(name, position=(0, 0, 0)):
        obj = Mu.MuObject(name)
        obj.transform = Mu.MuTransform()
        obj.transform.name = name
        obj.transform.localPosition = position
        obj.transform.localRotation = (1, 0, 0, 0)
        obj.transform.localScale = (1, 1, 1)
        obj.tag_and_layer = Mu.MuTagLayer()
        obj.tag_and_layer.tag, obj.tag_and_layer.layer = "Untagged", 0
        return obj

    def visible(obj, mesh):
        obj.shared_mesh = mesh
        obj.renderer = Mu.MuRenderer()
        obj.renderer.materials = [0]

    mu = Mu.Mu("PlutoLaunchPad")
    root = node("PlutoLaunchPad")
    mu.obj = root

    rail = node("Rail")
    mesh = new_mesh(Mu)
    add_box(mesh, (-0.18, -7.0, -0.6), (0.18, 7.0, -0.28), swatch["steel"])
    for x0, x1 in ((-0.55, -0.41), (0.41, 0.55)):
        add_box(mesh, (x0, -7.0, -0.3), (x1, 7.0, 0.12), swatch["dark"])
    for y in range(-6, 7, 2):
        add_box(mesh, (-0.55, y - 0.1, -0.45), (0.55, y + 0.1, -0.28), swatch["steel"])
    visible(rail, mesh)
    # Lets the cradle be clicked in the editor; the plugin turns it off in flight so it cannot catch
    # on the missile as it slides off.
    rail_collider = node("RailCollider")
    rail_collider.collider = Mu.MuColliderMesh(True)
    rail_collider.collider.isTrigger = 0
    rail_collider.collider.convex = 1
    rail_mesh = new_mesh(Mu)
    add_box(rail_mesh, (-0.55, -7.0, -0.6), (0.55, 7.0, 0.0), swatch["steel"])
    rail_collider.collider.mesh = rail_mesh
    rail.children.append(rail_collider)
    root.children.append(rail)

    stand = node("Stand", (0, 0, -0.6))
    hinge = new_mesh(Mu)
    add_box(hinge, (-0.5, -0.5, -0.5), (0.5, 0.5, 0.0), swatch["dark"])
    visible(stand, hinge)

    tower = node("Tower")
    mesh = new_mesh(Mu)
    for x in (-0.35, 0.35):
        for y in (-0.35, 0.35):
            add_box(mesh, (x - 0.06, y - 0.06, -1.0), (x + 0.06, y + 0.06, 0.0), swatch["steel"])
    add_box(mesh, (-0.13, -0.13, -1.0), (0.13, 0.13, 0.0), swatch["dark"])
    visible(tower, mesh)
    stand.children.append(tower)

    foot = node("Foot", (0, 0, -4.0))
    mesh = new_mesh(Mu)
    add_box(mesh, (-2.2, -3.0, -0.4), (2.2, 3.0, 0.0), swatch["white"])
    add_box(mesh, (-0.6, -0.6, 0.0), (0.6, 0.6, 0.5), swatch["dark"])
    visible(foot, mesh)
    collider = node("FootCollider")
    collider.collider = Mu.MuColliderMesh(True)
    collider.collider.isTrigger = 0
    collider.collider.convex = 1
    col_mesh = new_mesh(Mu)
    add_box(col_mesh, (-2.2, -3.0, -0.4), (2.2, 3.0, 0.0), swatch["white"])
    collider.collider.mesh = col_mesh
    foot.children.append(collider)
    stand.children.append(foot)
    root.children.append(stand)

    texture = Mu.MuTexture()
    texture.name, texture.type = "PlutoSkin.png", 0
    mu.textures = [texture]
    material = Mu.MuMaterial()
    material.name = "Pluto launcher"
    material.shaderName = "KSP/Diffuse"
    tex = Mu.MuMatTex()
    tex.index, tex.scale, tex.offset = 0, (1, 1), (0, 0)
    material.textureProperties["_MainTex"] = tex
    mu.materials = [material]
    mu.write(str(asset_dir / "PlutoLaunchPad.mu"))


def build_texture(path, font_path):
    from PIL import Image, ImageDraw, ImageFont

    rng = random.Random(1964)
    image = Image.new("RGB", (TEX, TEX_H))
    pixels = image.load()
    white_from = body_v(WHITE_FROM)
    for y in range(TEX_H):
        v = 1 - (y + 0.5) / TEX_H
        row_tone = rng.randrange(-4, 5)  # faint banding along the polished copper
        for x in range(TEX):
            u = (x + 0.5) / TEX
            if v < BODY_V0:
                pixels[x, y] = (WHITE if u < 0.25 else DARK if u < 0.5 else STEEL if u < 0.75 else COPPER)
                continue
            copper = v >= white_from
            base = COPPER if copper else WHITE
            seam = -14 if (x % 128) < 2 else 0
            noise = rng.randrange(-4, 5) + (row_tone if copper else 0)
            pixels[x, y] = tuple(max(0, min(255, c + seam + noise)) for c in base)

    px_per_m_around = TEX / (math.tau * RADIUS)
    px_per_m_along = TEX_H * (1 - BODY_V0) / LENGTH

    def y_at(height):
        return int((1 - body_v(height)) * TEX_H)

    def x_at(u):
        return int(u * TEX)

    draw = ImageDraw.Draw(image)
    # Panel lines around the body.
    for height in (BASE + 1.0, BASE + 7.5, WHITE_FROM, *WARHEAD_BAY, NOSE_START):
        draw.line((0, y_at(height), TEX, y_at(height)), fill=(96, 84, 78), width=2)

    # Warhead bay on top (u = 0.75 is -Y, the top): two rows of eight hatches.
    hatch_w = int(0.28 * px_per_m_around)
    bay_top, bay_bottom = WARHEAD_BAY
    pitch = (bay_top - bay_bottom) / 8
    for row in (-1, 1):
        cx = x_at(0.75) + row * (hatch_w // 2 + 2)
        for i in range(8):
            h0 = bay_bottom + i * pitch
            draw.rectangle((cx - hatch_w // 2, y_at(h0 + pitch - 0.04), cx + hatch_w // 2, y_at(h0 + 0.04)),
                           outline=(78, 62, 56), width=2)

    def decal(art, centre_height, along_m, around_m):
        """Paste art (drawn with the body's axis left to right) on both sides, upright and reading
        left to right to a viewer on each side, squashed to the texture's scale."""
        size = (int(around_m * px_per_m_around), int(along_m * px_per_m_along))
        for u, turn in ((0.0, 90), (0.5, 270)):
            stamp = art.rotate(turn, expand=True, resample=Image.BICUBIC).resize(size, Image.LANCZOS)
            x = x_at(u) - size[0] // 2
            y = y_at(centre_height) - size[1] // 2
            for dx in (0, TEX) if x < 0 else (0,):
                image.paste(stamp, (x + dx, y), stamp)

    # USAF star-and-bar insignia, then "USAF" further aft, as on the Vought model.
    r = 100
    star = Image.new("RGBA", (5 * r, 2 * r + 8), (0, 0, 0, 0))
    art = ImageDraw.Draw(star)
    cx, cy = star.width // 2, star.height // 2
    blue, red, white = (0, 40, 104, 255), (178, 34, 52, 255), (245, 245, 240, 255)
    art.rectangle((cx - 2 * r - 6, cy - r // 2 - 6, cx + 2 * r + 6, cy + r // 2 + 6), fill=blue)
    art.rectangle((cx - 2 * r, cy - r // 2, cx + 2 * r, cy + r // 2), fill=white)
    art.rectangle((cx - 2 * r, cy - r // 6, cx + 2 * r, cy + r // 6), fill=red)
    art.ellipse((cx - r, cy - r, cx + r, cy + r), fill=blue)
    points = []
    for k in range(10):
        a = -math.pi / 2 + k * math.pi / 5
        rr = r * (0.95 if k % 2 == 0 else 0.36)
        points.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    art.polygon(points, fill=white)
    decal(star, -1.6, 1.75, 0.82)

    font = ImageFont.truetype(str(font_path), 200)
    word = Image.new("RGBA", (620, 240), (0, 0, 0, 0))
    ImageDraw.Draw(word).text((10, 0), "USAF", font=font, fill=(24, 24, 28, 255))
    word = word.crop(word.getbbox())
    decal(word, -3.9, 2.0, 0.42)
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
    build_launch_pad(Mu, args.assets)
    build_texture(args.assets / "PlutoSkin.png", args.font)


if __name__ == "__main__":
    main()
