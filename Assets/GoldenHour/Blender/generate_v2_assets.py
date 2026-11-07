"""Golden Hour v2 art: jointed humanoid rigs, emergency vehicles and props.

Run (background mode, no UI):
  blender.exe --background --factory-startup --python generate_v2_assets.py

Coordinates are written in Unity space: (x = right, up, z = forward), metres.
The FBX export (forward -Z, up Y, baked space transform) followed by Unity's
import turns Blender's +Y depth into Unity -Z and Blender +X into Unity -X
(the v1 kit hinge at Blender depth -0.46 landed on Unity +Z), so to_blender()
negates x and z to compensate. GoldenHourProjectBuilder validates the result
(left arm on -X, nose on +Z) after import.

Rigs are hierarchies of named empties (Hips, Spine, Chest, Neck, Head,
UpperArm/LowerArm/UpperLeg/LowerLeg .L/.R) with rigid mesh segments parented to
them. Unity drives the joints procedurally (breathing, restless, limp, walk,
kneel, recovery position), so no baked animation clips are exported.
"""
import math
import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(ROOT, "Blender", "Exports")
SOURCE_DIR = os.path.join(ROOT, "Blender", "Source")
os.makedirs(EXPORT_DIR, exist_ok=True)
os.makedirs(SOURCE_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)


def to_blender(v):
    x, up, z = v
    return (-x, z, up)


def size_to_blender(v):
    x, up, z = v
    return (x, z, up)


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


SKIN = material("GH_Skin", (0.72, 0.52, 0.38))
CLOTHING = material("GH_Clothing", (0.8, 0.8, 0.8))
TROUSERS = material("GH_Trousers", (0.11, 0.13, 0.17))
SHOES = material("GH_Shoes", (0.04, 0.04, 0.045))
HAIR = material("GH_Hair", (0.16, 0.1, 0.06))
HELMET = material("GH_HelmetShell", (0.05, 0.05, 0.07), 0.2, 0.35)
VISOR = material("GH_Visor", (0.18, 0.26, 0.34), 0.3, 0.1)
HARDHAT = material("GH_HardHat", (0.95, 0.75, 0.08))
VEST = material("GH_HiVis", (0.8, 0.92, 0.18))
REFLECT = material("GH_Reflective", (0.85, 0.86, 0.82), 0.1, 0.25)
CAP = material("GH_CapNavy", (0.08, 0.1, 0.18))
WHITE = material("GH_VehicleWhite", (0.9, 0.9, 0.87), 0.2, 0.45)
AMB_GREEN = material("GH_AmbulanceGreen", (0.18, 0.62, 0.22))
AMB_YELLOW = material("GH_AmbulanceYellow", (0.95, 0.85, 0.1))
RED = material("GH_EmergencyRed", (0.72, 0.05, 0.06), 0.2, 0.4)
BLUE = material("GH_PoliceBlue", (0.08, 0.2, 0.6), 0.2, 0.4)
GLASS = material("GH_Glass", (0.2, 0.28, 0.34), 0.2, 0.1)
TYRE = material("GH_Tyre", (0.04, 0.04, 0.045))
METAL = material("GH_Metal", (0.5, 0.52, 0.55), 0.8, 0.35)
DARK = material("GH_DarkTrim", (0.08, 0.09, 0.1))
BUS_PAINT = material("GH_BusPaint", (0.86, 0.66, 0.12), 0.2, 0.5)
LAMP_RED = material("GH_LampRed", (0.9, 0.08, 0.06))
LAMP_BLUE = material("GH_LampBlue", (0.1, 0.25, 0.95))
LAMP_AMBER = material("GH_LampAmber", (1.0, 0.6, 0.1))
MATTRESS = material("GH_StretcherMattress", (0.75, 0.12, 0.1))


def link(obj, parent):
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
    return obj


def empty(name, local, parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.05
    bpy.context.collection.objects.link(obj)
    link(obj, parent)
    obj.location = to_blender(local)
    return obj


def _finish(obj, name, mat, parent, local, bevel):
    obj.name = name
    obj.data.name = name
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    link(obj, parent)
    obj.location = to_blender(local)
    return obj


def box(name, local, size, mat, parent=None, bevel=0.02, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.scale = size_to_blender(size)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    _finish(obj, name, mat, parent, local, bevel)
    if rot:
        obj.rotation_euler = rot
    return obj


def ball(name, local, size, mat, parent=None, segments=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=0.5)
    obj = bpy.context.object
    obj.scale = size_to_blender(size)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()
    return _finish(obj, name, mat, parent, local, 0)


def rod(name, local, radius, length, mat, parent=None, axis="up", vertices=12, smooth=True):
    """Cylinder along Unity 'up' (default), 'x' or 'z'."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length)
    obj = bpy.context.object
    if axis == "x":
        obj.rotation_euler = (0, math.radians(90), 0)
    elif axis == "z":
        obj.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if smooth:
        bpy.ops.object.shade_smooth()
    return _finish(obj, name, mat, parent, local, 0)


def limb(name, local, top_radius, bottom_radius, length, mat, parent, vertices=10):
    """Tapered limb hanging down from the joint (Unity -up)."""
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=bottom_radius, radius2=top_radius, depth=length)
    obj = bpy.context.object
    bpy.ops.object.shade_smooth()
    return _finish(obj, name, mat, parent, (local[0], local[1] - length * 0.5, local[2]), 0)


# ------------------------------------------------------------------ humanoid


def build_human(prefix, head_scale=1.0, limb_scale=1.0, torso_scale=1.0):
    """Joint layout matches HumanoidBody.BuildPrimitiveBody (1.75 m adult at scale 1)."""
    root = empty(prefix, (0, 0, 0))
    hips = empty("Hips", (0, 0.95 * limb_scale, 0), root)
    box(prefix + "_Pelvis", (0, 0, 0), (0.34 * torso_scale, 0.2, 0.21), TROUSERS, hips, 0.04)
    spine = empty("Spine", (0, 0.1, 0), hips)
    box(prefix + "_Abdomen_Clothing", (0, 0.06, 0), (0.33 * torso_scale, 0.18, 0.2), CLOTHING, spine, 0.05)
    chest = empty("Chest", (0, 0.18 * torso_scale, 0), spine)
    box(prefix + "_Torso_Clothing", (0, 0.13 * torso_scale, 0), (0.4 * torso_scale, 0.42 * torso_scale, 0.23), CLOTHING, chest, 0.07)
    box(prefix + "_Collar_Clothing", (0, 0.35 * torso_scale, 0), (0.24, 0.05, 0.16), CLOTHING, chest, 0.02)
    box(prefix + "_Vest", (0, 0.13 * torso_scale, 0), (0.43 * torso_scale, 0.36 * torso_scale, 0.255), VEST, chest, 0.05)
    box(prefix + "_Vest_StripeA", (0, 0.06 * torso_scale, 0), (0.44 * torso_scale, 0.04, 0.262), REFLECT, chest, 0.0)
    box(prefix + "_Vest_StripeB", (0, 0.2 * torso_scale, 0), (0.44 * torso_scale, 0.04, 0.262), REFLECT, chest, 0.0)
    neck = empty("Neck", (0, 0.38 * torso_scale, 0), chest)
    rod(prefix + "_NeckSkin", (0, 0.03, 0), 0.05, 0.08, SKIN, neck)
    head = empty("Head", (0, 0.08, 0), neck)
    hs = head_scale
    ball(prefix + "_HeadSkin", (0, 0.12 * hs, 0.01), (0.21 * hs, 0.25 * hs, 0.23 * hs), SKIN, head)
    ball(prefix + "_Nose", (0, 0.11 * hs, 0.125 * hs), (0.035 * hs, 0.05 * hs, 0.04 * hs), SKIN, head, 8, 6)
    ball(prefix + "_EarL", (-0.105 * hs, 0.12 * hs, 0), (0.03, 0.06 * hs, 0.045 * hs), SKIN, head, 8, 6)
    ball(prefix + "_EarR", (0.105 * hs, 0.12 * hs, 0), (0.03, 0.06 * hs, 0.045 * hs), SKIN, head, 8, 6)
    ball(prefix + "_Hair", (0, 0.18 * hs, -0.02 * hs), (0.225 * hs, 0.16 * hs, 0.235 * hs), HAIR, head)
    ball(prefix + "_Helmet", (0, 0.14 * hs, 0), (0.3 * hs, 0.32 * hs, 0.31 * hs), HELMET, head, 18, 12)
    box(prefix + "_Helmet_Visor", (0, 0.12 * hs, 0.145 * hs), (0.2 * hs, 0.08 * hs, 0.03), VISOR, head, 0.01)
    ball(prefix + "_HardHat", (0, 0.22 * hs, 0), (0.28 * hs, 0.14 * hs, 0.3 * hs), HARDHAT, head)
    box(prefix + "_HardHat_Brim", (0, 0.2 * hs, 0.02), (0.3 * hs, 0.02, 0.34 * hs), HARDHAT, head, 0.0)
    rod(prefix + "_Cap", (0, 0.26 * hs, 0), 0.13 * hs, 0.05, CAP, head)
    box(prefix + "_Cap_Peak", (0, 0.245 * hs, 0.13 * hs), (0.2 * hs, 0.015, 0.09), CAP, head, 0.0)

    for side in (-1, 1):
        suffix = ".L" if side < 0 else ".R"
        tag = "L" if side < 0 else "R"
        upper_arm = empty("UpperArm" + suffix, (side * 0.26 * torso_scale, 0.32 * torso_scale, 0), chest)
        ball(prefix + "_Shoulder" + tag + "_Clothing", (0, 0, 0), (0.13, 0.12, 0.14), CLOTHING, upper_arm, 10, 8)
        limb(prefix + "_UpperArm" + tag + "_Clothing", (0, 0, 0), 0.065, 0.055, 0.31 * limb_scale, CLOTHING, upper_arm)
        lower_arm = empty("LowerArm" + suffix, (0, -0.31 * limb_scale, 0), upper_arm)
        limb(prefix + "_LowerArm" + tag + "_Clothing", (0, 0, 0), 0.052, 0.045, 0.26 * limb_scale, CLOTHING, lower_arm)
        box(prefix + "_Hand" + tag, (0, -0.31 * limb_scale, 0.01), (0.07, 0.1, 0.035), SKIN, lower_arm, 0.015)
        upper_leg = empty("UpperLeg" + suffix, (side * 0.1 * torso_scale, -0.08, 0), hips)
        limb(prefix + "_Thigh" + tag, (0, 0, 0), 0.085, 0.065, 0.42 * limb_scale, TROUSERS, upper_leg)
        lower_leg = empty("LowerLeg" + suffix, (0, -0.42 * limb_scale, 0), upper_leg)
        limb(prefix + "_Shin" + tag, (0, 0, 0), 0.062, 0.05, 0.4 * limb_scale, TROUSERS, lower_leg)
        box(prefix + "_Shoe" + tag, (0, -0.43 * limb_scale, 0.045), (0.11, 0.08, 0.25), SHOES, lower_leg, 0.025)
    return root


# ------------------------------------------------------------------ vehicles


def wheels(prefix, parent, positions, radius, width):
    for i, (x, z) in enumerate(positions):
        rod(prefix + "_Wheel" + str(i), (x, radius, z), radius, width, TYRE, parent, axis="x", vertices=16)
        rod(prefix + "_Hub" + str(i), (x + (0.01 if x > 0 else -0.01), radius, z), radius * 0.45, width + 0.02, METAL, parent, axis="x", vertices=10)


def build_ambulance():
    root = empty("Ambulance", (0, 0, 0))
    box("Ambulance_Box", (0, 1.55, -0.6), (2.3, 2.3, 4.2), WHITE, root, 0.06)
    box("Ambulance_Cab", (0, 1.15, 2.3), (2.2, 1.5, 1.8), WHITE, root, 0.08)
    box("Ambulance_Bonnet", (0, 0.85, 3.05), (2.1, 0.7, 0.5), WHITE, root, 0.08)
    box("Ambulance_Windscreen", (0, 1.55, 3.21), (1.9, 0.62, 0.04), GLASS, root, 0.0)
    box("Ambulance_SideWindowL", (-1.11, 1.5, 2.4), (0.03, 0.5, 1.1), GLASS, root, 0.0)
    box("Ambulance_SideWindowR", (1.11, 1.5, 2.4), (0.03, 0.5, 1.1), GLASS, root, 0.0)
    for side, name in ((-1, "L"), (1, "R")):
        box("Ambulance_StripeG" + name, (side * 1.16, 1.0, 0.3), (0.02, 0.22, 6.1), AMB_GREEN, root, 0.0)
        box("Ambulance_StripeY" + name, (side * 1.16, 1.22, 0.3), (0.02, 0.22, 6.1), AMB_YELLOW, root, 0.0)
        box("Ambulance_CrossV" + name, (side * 1.17, 2.0, -0.8), (0.02, 0.6, 0.2), RED, root, 0.0)
        box("Ambulance_CrossH" + name, (side * 1.17, 2.0, -0.8), (0.02, 0.2, 0.6), RED, root, 0.0)
    box("Ambulance_RearDoorL", (-0.56, 1.5, -2.71), (1.08, 2.0, 0.04), WHITE, root, 0.01)
    box("Ambulance_RearDoorR", (0.56, 1.5, -2.71), (1.08, 2.0, 0.04), WHITE, root, 0.01)
    box("Ambulance_RearChevrons", (0, 0.55, -2.73), (2.2, 0.25, 0.02), AMB_YELLOW, root, 0.0)
    box("Ambulance_Bumper", (0, 0.45, 3.32), (2.2, 0.22, 0.14), DARK, root, 0.03)
    box("Ambulance_LightBar", (0, 2.78, 1.7), (1.4, 0.12, 0.3), DARK, root, 0.02)
    box("Ambulance_LampL", (-0.45, 2.88, 1.7), (0.4, 0.1, 0.24), LAMP_BLUE, root, 0.02)
    box("Ambulance_LampR", (0.45, 2.88, 1.7), (0.4, 0.1, 0.24), LAMP_BLUE, root, 0.02)
    box("Ambulance_Headlights", (0, 0.95, 3.31), (1.7, 0.14, 0.03), REFLECT, root, 0.0)
    wheels("Ambulance", root, [(-1.05, 2.1), (1.05, 2.1), (-1.05, -1.9), (1.05, -1.9)], 0.42, 0.3)
    empty("Ambulance_Front", (0, 1.0, 3.4), root)
    return root


def build_police_car():
    root = empty("PoliceCar", (0, 0, 0))
    box("Police_Body", (0, 0.65, 0), (1.85, 0.7, 4.5), WHITE, root, 0.12)
    box("Police_Cabin", (0, 1.25, -0.2), (1.65, 0.55, 2.3), WHITE, root, 0.12)
    box("Police_Windscreen", (0, 1.25, 0.97), (1.5, 0.45, 0.04), GLASS, root, 0.0)
    box("Police_RearScreen", (0, 1.25, -1.37), (1.5, 0.42, 0.04), GLASS, root, 0.0)
    for side, name in ((-1, "L"), (1, "R")):
        box("Police_Windows" + name, (side * 0.83, 1.26, -0.2), (0.03, 0.4, 2.0), GLASS, root, 0.0)
        box("Police_Stripe" + name, (side * 0.935, 0.7, 0), (0.02, 0.22, 4.3), BLUE, root, 0.0)
        box("Police_Check" + name, (side * 0.94, 0.47, 0), (0.02, 0.12, 4.3), AMB_YELLOW, root, 0.0)
    box("Police_LightBar", (0, 1.58, -0.2), (1.2, 0.1, 0.28), DARK, root, 0.02)
    box("Police_LampL", (-0.35, 1.67, -0.2), (0.45, 0.1, 0.22), LAMP_BLUE, root, 0.02)
    box("Police_LampR", (0.35, 1.67, -0.2), (0.45, 0.1, 0.22), LAMP_BLUE, root, 0.02)
    box("Police_Headlights", (0, 0.72, 2.25), (1.5, 0.12, 0.03), REFLECT, root, 0.0)
    box("Police_Bumper", (0, 0.38, 2.27), (1.8, 0.18, 0.1), DARK, root, 0.03)
    wheels("Police", root, [(-0.85, 1.45), (0.85, 1.45), (-0.85, -1.45), (0.85, -1.45)], 0.34, 0.24)
    empty("PoliceCar_Front", (0, 0.7, 2.35), root)
    return root


def build_fire_truck():
    root = empty("FireTruck", (0, 0, 0))
    box("Fire_Cab", (0, 1.55, 3.1), (2.5, 2.1, 1.9), RED, root, 0.08)
    box("Fire_Windscreen", (0, 2.0, 4.06), (2.2, 0.75, 0.04), GLASS, root, 0.0)
    box("Fire_Body", (0, 1.65, -0.9), (2.5, 2.3, 6.1), RED, root, 0.06)
    for side, name in ((-1, "L"), (1, "R")):
        for i in range(3):
            box("Fire_Locker" + name + str(i), (side * 1.26, 1.6, -2.9 + i * 1.95), (0.03, 1.6, 1.7), METAL, root, 0.0)
        box("Fire_Stripe" + name, (side * 1.27, 0.75, 0.6), (0.02, 0.18, 8.9), AMB_YELLOW, root, 0.0)
    box("Fire_LadderRailL", (-0.45, 2.95, -0.8), (0.08, 0.08, 6.4), METAL, root, 0.0)
    box("Fire_LadderRailR", (0.45, 2.95, -0.8), (0.08, 0.08, 6.4), METAL, root, 0.0)
    for i in range(13):
        box("Fire_LadderRung" + str(i), (0, 2.95, -3.8 + i * 0.5), (0.9, 0.04, 0.05), METAL, root, 0.0)
    box("Fire_LightBar", (0, 2.68, 3.4), (1.8, 0.12, 0.3), DARK, root, 0.02)
    box("Fire_LampL", (-0.55, 2.78, 3.4), (0.5, 0.1, 0.24), LAMP_BLUE, root, 0.02)
    box("Fire_LampR", (0.55, 2.78, 3.4), (0.5, 0.1, 0.24), LAMP_BLUE, root, 0.02)
    box("Fire_Bumper", (0, 0.55, 4.1), (2.5, 0.3, 0.16), METAL, root, 0.03)
    wheels("Fire", root, [(-1.12, 3.0), (1.12, 3.0), (-1.12, -1.6), (1.12, -1.6), (-1.12, -2.9), (1.12, -2.9)], 0.5, 0.36)
    empty("FireTruck_Front", (0, 1.0, 4.2), root)
    return root


def build_bus():
    """School/coach bus lying on its right side: roof faces -x, floor faces +x, side windows face up."""
    root = empty("Bus", (0, 0, 0))
    box("Bus_Body", (0, 1.25, 0), (3.0, 2.5, 11.5), BUS_PAINT, root, 0.12)
    for i in range(6):
        box("Bus_WindowUp" + str(i), (0.25, 2.51, -4.5 + i * 1.8), (1.6, 0.04, 1.3), GLASS, root, 0.0)
        box("Bus_WindowCrack" + str(i), (0.2 + 0.3 * (i % 2), 2.535, -4.6 + i * 1.8), (0.6, 0.02, 0.05), REFLECT, root, 0.0,
            rot=(0, 0, math.radians(25 + i * 17)))
    box("Bus_Windscreen", (0.2, 1.25, 5.76), (2.2, 2.0, 0.04), GLASS, root, 0.0)
    box("Bus_RoofEdge", (-1.52, 1.25, 0), (0.04, 2.2, 11.3), DARK, root, 0.0)
    box("Bus_Stripe", (0.4, 2.52, 0), (0.25, 0.02, 11.2), DARK, root, 0.0)
    box("Bus_Bumper", (0.2, 1.25, 5.82), (2.4, 2.2, 0.12), DARK, root, 0.03)
    for i, z in enumerate((-3.8, 3.6)):
        for j, y in enumerate((0.55, 1.95)):
            rod("Bus_Wheel" + str(i) + str(j), (1.62, y, z), 0.5, 0.36, TYRE, root, axis="x", vertices=16)
    empty("Bus_Front", (0, 1.25, 5.9), root)
    return root


def build_motorbike():
    """Motorbike lying on its left side."""
    root = empty("Motorbike", (0, 0, 0))
    frame = empty("Motorbike_Frame", (0, 0.35, 0), root)
    frame.rotation_euler = (0, math.radians(78), 0)
    rod("Motorbike_WheelF", (0, 0, 0.75), 0.33, 0.12, TYRE, frame, axis="x", vertices=18)
    rod("Motorbike_WheelR", (0, 0, -0.7), 0.33, 0.14, TYRE, frame, axis="x", vertices=18)
    box("Motorbike_Tank", (0, 0.42, 0.15), (0.3, 0.22, 0.55), RED, frame, 0.06)
    box("Motorbike_Seat", (0, 0.42, -0.35), (0.26, 0.1, 0.6), DARK, frame, 0.04)
    box("Motorbike_Engine", (0, 0.15, 0.0), (0.3, 0.3, 0.45), METAL, frame, 0.04)
    rod("Motorbike_Fork", (0, 0.32, 0.65), 0.025, 0.7, METAL, frame)
    rod("Motorbike_Bars", (0, 0.68, 0.55), 0.02, 0.7, METAL, frame, axis="x")
    box("Motorbike_Headlamp", (0, 0.55, 0.78), (0.14, 0.12, 0.08), REFLECT, frame, 0.02)
    rod("Motorbike_Exhaust", (0.18, 0.1, -0.35), 0.04, 0.6, METAL, frame, axis="z")
    return root


def build_stretcher():
    root = empty("Stretcher", (0, 0, 0))
    box("Stretcher_Mattress", (0, 0.62, 0), (0.6, 0.1, 1.95), MATTRESS, root, 0.04)
    box("Stretcher_FrameL", (-0.31, 0.55, 0), (0.04, 0.04, 2.0), METAL, root, 0.0)
    box("Stretcher_FrameR", (0.31, 0.55, 0), (0.04, 0.04, 2.0), METAL, root, 0.0)
    for z in (-0.8, 0.8):
        for x in (-0.25, 0.25):
            rod("Stretcher_Leg", (x, 0.3, z), 0.02, 0.5, METAL, root)
            rod("Stretcher_Castor", (x, 0.06, z), 0.06, 0.04, TYRE, root, axis="x")
    box("Stretcher_Pillow", (0, 0.7, 0.75), (0.45, 0.07, 0.3), WHITE, root, 0.03)
    return root


def build_props():
    root = empty("V2Props", (0, 0, 0))
    box("Prop_Suitcase", (0, 0.2, 0), (0.45, 0.4, 0.2), BLUE, root, 0.03)
    box("Prop_Rubble1", (1.0, 0.15, 0), (0.6, 0.3, 0.4), METAL, root, 0.08, rot=(0.3, 0.2, 0.5))
    box("Prop_Rubble2", (1.8, 0.12, 0.2), (0.4, 0.24, 0.5), DARK, root, 0.06, rot=(0.1, 0.6, 0.2))
    rod("Prop_JerryCan", (2.6, 0.22, 0), 0.14, 0.44, RED, root, vertices=8, smooth=False)
    rod("Prop_FireExtinguisher", (3.2, 0.3, 0), 0.08, 0.6, RED, root)
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
        bake_space_transform=True,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    print("EXPORTED", filename, "objects:", len(objects))


builders = [
    (lambda: build_human("RiggedHuman"), "RiggedHuman.fbx"),
    (lambda: build_human("RiggedChild", head_scale=1.18, limb_scale=0.92, torso_scale=0.9), "RiggedChild.fbx"),
    (build_ambulance, "Ambulance.fbx"),
    (build_police_car, "PoliceCar.fbx"),
    (build_fire_truck, "FireTruck.fbx"),
    (build_bus, "Bus.fbx"),
    (build_motorbike, "Motorbike.fbx"),
    (build_stretcher, "Stretcher.fbx"),
    (build_props, "V2Props.fbx"),
]

offset = 0.0
for build, filename in builders:
    root = build()
    export_hierarchy(root, filename)
    root.location = (offset, 30.0, 0.0)
    offset += 14.0

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_DIR, "GoldenHour_V2.blend"))
print("GOLDEN_HOUR_V2_ASSETS_OK", len(builders))
