"""Golden Hour v3 art: visible treatment props and first-person gloved hands.

Run (background mode, no UI):
  blender.exe --background --factory-startup --python generate_v3_assets.py

New files only (TreatmentProps.fbx, FirstPersonHands.fbx, Source/GoldenHour_V3.blend); the v1/v2 exports and
sources are never touched. Same conventions as generate_v2_assets.py: coordinates are written in Unity space
(x = right, up, z = forward) and converted with to_blender(); V2ModelPostprocessor imports these files with
"Bake Axis Conversion" so Unity (x, y, z) = Blender (x, z, y). Every mesh has its transform applied, so only
vertex positions matter.

TreatmentProps children (positions are in the local frame of the HumanoidBody joint they attach to):
  Dressing_Limb   gauze band + pad around a limb segment (joint local, limb along -Y, front +Z), radius 0.065
  Dressing_Head   gauze band around the head (Head joint local)
  Dressing_Torso  dressing pad with tape on the front of the trunk (Spine joint local)
  Blanket_Lying   crumpled foil sheet over a lying body (Hips joint local: body along +Y, front +Z)
  Blanket_Shawl   foil wrapped round the back and shoulders of a sitting person (Chest joint local)
FirstPersonHands children: Hand.L and Hand.R in the Viewmodel arm frame (arm along +Z, palm down), with
Sleeve, Glove_Cuff, Glove_Palm, Glove_Back, 4 two-segment fingers and a two-segment thumb.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(ROOT, "Blender", "Exports")
SOURCE_DIR = os.path.join(ROOT, "Blender", "Source")
os.makedirs(EXPORT_DIR, exist_ok=True)
os.makedirs(SOURCE_DIR, exist_ok=True)
for name in ("TreatmentProps.fbx", "FirstPersonHands.fbx"):
    target = os.path.join(EXPORT_DIR, name)
    if os.path.exists(target):
        print("REPLACING_GENERATED", name)

bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(4242)


def to_blender(v):
    x, up, z = v
    return Vector((x, z, up))


MATERIALS = {}


def material(name, color, metallic=0.0, roughness=0.7):
    if name in MATERIALS:
        return MATERIALS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    MATERIALS[name] = mat
    return mat


GAUZE = material("GH_Gauze", (0.93, 0.93, 0.9), 0.0, 0.95)
PAD = material("GH_DressingPad", (0.98, 0.98, 0.97), 0.0, 0.9)
TAPE = material("GH_Tape", (0.86, 0.8, 0.66), 0.0, 0.8)
FOIL = material("GH_FoilGold", (0.88, 0.68, 0.22), 0.85, 0.28)
NITRILE = material("GH_Nitrile", (0.27, 0.47, 0.84), 0.0, 0.45)
SLEEVE = material("GH_SleeveNavy", (0.12, 0.17, 0.24), 0.0, 0.85)
STRIPE = material("GH_SleeveReflective", (0.85, 0.86, 0.82), 0.1, 0.25)


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.05
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def mesh_object(name, verts_unity, faces, mat, parent, smooth=True, solidify=0.0):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([to_blender(v) for v in verts_unity], [], faces)
    mesh.update()
    # Unity's axis mapping is a reflection, so face winding flips: recalculate normals outward.
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if smooth:
        for polygon in mesh.polygons:
            polygon.use_smooth = True
    if solidify:
        modifier = obj.modifiers.new("Thickness", "SOLIDIFY")
        modifier.thickness = solidify
        modifier.offset = 0.0
    obj.parent = parent
    return obj


def primitive(kind, name, centre, size, mat, parent, rotate_to=None, **kwargs):
    """Unit primitive scaled in Unity axes (size = x, up, z) then oriented so its Unity +up axis follows rotate_to."""
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=kwargs.get("segments", 14), ring_count=kwargs.get("rings", 9), radius=0.5)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=kwargs.get("vertices", 14), radius=0.5, depth=1.0)
    obj = bpy.context.object
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rotate_to is not None:
        up = Vector((0.0, 0.0, 1.0))
        direction = to_blender(rotate_to).normalized()
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = up.rotation_difference(direction)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    if kind != "cube":
        bpy.ops.object.shade_smooth()
    if kwargs.get("bevel"):
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = kwargs["bevel"]
        modifier.segments = 2
    obj.name = name
    obj.data.name = name
    obj.data.materials.append(mat)
    obj.location = to_blender(centre)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    obj.parent = parent
    return obj


def segment(name, start, end, radius, mat, parent):
    """Capsule-like rod between two Unity-space points (cylinder + rounded tip)."""
    a = Vector(start)
    b = Vector(end)
    centre = (a + b) * 0.5
    length = (b - a).length
    primitive("cylinder", name, tuple(centre), (radius * 2, length, radius * 2), mat, parent, rotate_to=tuple(b - a), vertices=10)
    primitive("sphere", name + "_Tip", tuple(b), (radius * 2, radius * 2, radius * 2), mat, parent, segments=10, rings=6)


def band(name, radius, height, y, mat, parent, segments=20, wobble=0.0, z_bias=0.0):
    """Open tube around the local Y axis (a bandage wrap), with slight irregularity."""
    verts = []
    faces = []
    for ring, offset in enumerate((-height * 0.5, height * 0.5)):
        for i in range(segments):
            angle = i * 2 * math.pi / segments
            r = radius * (1 + random.uniform(-wobble, wobble))
            verts.append((math.cos(angle) * r, y + offset + random.uniform(-0.004, 0.004), math.sin(angle) * r + z_bias))
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((i, j, segments + j, segments + i))
    return mesh_object(name, verts, faces, mat, parent, solidify=0.008)


# ------------------------------------------------------------------ treatment props


def build_treatment_props():
    root = empty("TreatmentProps")

    limb = empty("Dressing_Limb", root)
    band("Dressing_Limb_Wrap", 0.07, 0.11, 0.0, GAUZE, limb, wobble=0.04)
    band("Dressing_Limb_Wrap2", 0.072, 0.05, -0.045, GAUZE, limb, wobble=0.05)
    primitive("cube", "Dressing_Limb_Pad", (0.0, 0.0, 0.068), (0.06, 0.08, 0.018), PAD, limb, bevel=0.006)
    primitive("cube", "Dressing_Limb_Knot", (0.05, 0.035, 0.05), (0.03, 0.025, 0.02), GAUZE, limb, bevel=0.006)

    head = empty("Dressing_Head", root)
    band("Dressing_Head_Wrap", 0.118, 0.06, 0.15, GAUZE, head, segments=24, wobble=0.03, z_bias=0.005)
    primitive("cube", "Dressing_Head_Pad", (0.0, 0.15, 0.118), (0.07, 0.05, 0.016), PAD, head, bevel=0.006)

    torso = empty("Dressing_Torso", root)
    primitive("cube", "Dressing_Torso_Pad", (0.0, 0.06, 0.118), (0.15, 0.12, 0.018), PAD, torso, bevel=0.008)
    primitive("cube", "Dressing_Torso_TapeA", (0.0, 0.1, 0.122), (0.2, 0.022, 0.006), TAPE, torso)
    primitive("cube", "Dressing_Torso_TapeB", (0.0, 0.02, 0.122), (0.2, 0.022, 0.006), TAPE, torso)

    # Foil sheet over a lying body: along +Y (feet at -0.95, chest at +0.55), front +Z, sides draped towards -Z.
    lying = empty("Blanket_Lying", root)
    columns, rows = 12, 26
    verts = []
    for r in range(rows + 1):
        y = -0.95 + 1.5 * r / rows
        for c in range(columns + 1):
            u = -1 + 2 * c / columns
            x = u * 0.42
            drop = max(0.0, abs(u) - 0.45) * 0.3
            z = 0.15 - drop - (0.02 if y < -0.6 else 0.0) + random.uniform(-0.012, 0.012)
            x *= 1 - drop * 0.6
            verts.append((x, y + random.uniform(-0.008, 0.008), z))
    faces = []
    for r in range(rows):
        for c in range(columns):
            i = r * (columns + 1) + c
            faces.append((i, i + 1, i + columns + 2, i + columns + 1))
    mesh_object("Blanket_Lying_Sheet", verts, faces, FOIL, lying, solidify=0.004)

    # Foil wrapped round the back and shoulders (open at the front), Chest joint local.
    shawl = empty("Blanket_Shawl", root)
    segments, levels = 20, 8
    verts = []
    for level in range(levels + 1):
        t = level / levels
        y = -0.24 + 0.62 * t
        radius = 0.27 - 0.07 * t
        for i in range(segments + 1):
            angle = math.radians(-150 + 300 * i / segments) - math.pi / 2
            r = radius * (1 + random.uniform(-0.04, 0.04))
            verts.append((math.cos(angle) * r * 1.05, y, math.sin(angle) * r * 0.85 - 0.01))
    faces = []
    for level in range(levels):
        for i in range(segments):
            a = level * (segments + 1) + i
            faces.append((a, a + 1, a + segments + 2, a + segments + 1))
    mesh_object("Blanket_Shawl_Wrap", verts, faces, FOIL, shawl, solidify=0.004)
    return root


# ------------------------------------------------------------------ first-person hands


def build_hand(parent, sign):
    side = "L" if sign < 0 else "R"
    hand = empty("Hand." + side, parent)
    primitive("cylinder", "Sleeve_" + side, (0.0, 0.0, -0.17), (0.1, 0.27, 0.095), SLEEVE, hand, rotate_to=(0, 0, 1), vertices=16)
    primitive("cylinder", "Sleeve_Stripe_" + side, (0.0, 0.0, -0.11), (0.103, 0.025, 0.098), STRIPE, hand, rotate_to=(0, 0, 1), vertices=16)
    primitive("cylinder", "Glove_Cuff_" + side, (0.0, 0.0, -0.025), (0.078, 0.07, 0.07), NITRILE, hand, rotate_to=(0, 0, 1), vertices=16)
    primitive("cube", "Glove_Palm_" + side, (0.0, -0.004, 0.045), (0.088, 0.03, 0.1), NITRILE, hand, bevel=0.012)
    primitive("sphere", "Glove_Back_" + side, (0.0, 0.008, 0.04), (0.09, 0.03, 0.1), NITRILE, hand)
    # Fingers from little finger (outside) to index (towards the thumb), slight natural curl.
    xs = (-0.033, -0.011, 0.011, 0.033)
    lengths = (0.042, 0.052, 0.056, 0.05)
    for index, (x, length) in enumerate(zip(xs, lengths)):
        x *= sign
        base = Vector((x, -0.004, 0.094))
        mid = base + Vector((x * 0.08, -0.008, length * 0.55))
        tip = mid + Vector((x * 0.05, -0.018, length * 0.42))
        radius = 0.0105 if index != 0 else 0.0092
        segment("Glove_Finger%d_A_%s" % (index + 1, side), tuple(base), tuple(mid), radius, NITRILE, hand)
        segment("Glove_Finger%d_B_%s" % (index + 1, side), tuple(mid), tuple(tip), radius * 0.92, NITRILE, hand)
    thumb_base = Vector((-sign * 0.04, -0.008, 0.035))
    thumb_mid = thumb_base + Vector((-sign * 0.022, -0.006, 0.03))
    thumb_tip = thumb_mid + Vector((-sign * 0.006, -0.01, 0.028))
    segment("Glove_Thumb_A_" + side, tuple(thumb_base), tuple(thumb_mid), 0.0125, NITRILE, hand)
    segment("Glove_Thumb_B_" + side, tuple(thumb_mid), tuple(thumb_tip), 0.0115, NITRILE, hand)
    return hand


def build_hands():
    root = empty("FirstPersonHands")
    build_hand(root, -1)
    build_hand(root, 1)
    return root


def export_hierarchy(root, filename):
    bpy.ops.object.select_all(action="DESELECT")
    objects = [root] + list(root.children_recursive)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(EXPORT_DIR, filename),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = sum(len(obj.data.polygons) for obj in meshes)
    print("EXPORTED", filename, "objects:", len(objects), "meshes:", len(meshes), "faces:", triangles)


props = build_treatment_props()
export_hierarchy(props, "TreatmentProps.fbx")
hands = build_hands()
export_hierarchy(hands, "FirstPersonHands.fbx")
names = [obj.name for obj in list(props.children_recursive) + list(hands.children_recursive)]
for required in ("Dressing_Limb", "Dressing_Head", "Dressing_Torso", "Blanket_Lying", "Blanket_Shawl", "Hand.L", "Hand.R"):
    if required not in names:
        raise RuntimeError("missing " + required)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_DIR, "GoldenHour_V3.blend"))
print("GOLDEN_HOUR_V3_ASSETS_OK")
