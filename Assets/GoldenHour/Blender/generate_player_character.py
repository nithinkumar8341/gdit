"""Golden Hour main character: a skinned, animated first responder (new file; no existing asset is touched).

Run (background mode, no UI):
  blender.exe --background --factory-startup --python generate_player_character.py

Builds one smooth body mesh (Skin + Subdivision modifiers on a skeleton graph), a deforming armature with named bones
(Hips, Spine, Chest, Neck, Head, UpperArm/LowerArm/Hand, UpperLeg/LowerLeg/Foot/Toe .L/.R), automatic skin weights,
face and hair parts parented to the Head bone, a responder uniform (red jacket with white reflective bands, navy
trousers, black boots) and four baked actions: Idle, Walk, Run, Kneel.
Blender axes here: x = right, y = forward (the character faces +Y), z = up. Exported like the v2 assets
(forward -Z, up Y, no baked space transform); Unity imports it with "Bake Axis Conversion" so Unity (x, y, z) =
Blender (x, z, y): the character stands on Unity +Y and faces Unity +Z, left side on -X.
Outputs: Exports/PlayerCharacter.fbx, Source/GoldenHour_PlayerCharacter.blend.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(ROOT, "Blender", "Exports")
SOURCE_DIR = os.path.join(ROOT, "Blender", "Source")
os.makedirs(EXPORT_DIR, exist_ok=True)
os.makedirs(SOURCE_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30


def material(name, color, metallic=0.0, roughness=0.7):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


SKIN = material("PC_Skin", (0.74, 0.53, 0.4), 0.0, 0.55)
JACKET = material("PC_JacketRed", (0.66, 0.06, 0.07), 0.0, 0.6)
STRIPE = material("PC_ReflectiveWhite", (0.92, 0.93, 0.9), 0.15, 0.25)
TROUSERS = material("PC_TrousersNavy", (0.07, 0.09, 0.15), 0.0, 0.8)
BOOTS = material("PC_Boots", (0.03, 0.03, 0.035), 0.1, 0.4)
HAIR = material("PC_Hair", (0.12, 0.08, 0.05), 0.0, 0.8)
EYES = material("PC_Eyes", (0.05, 0.05, 0.06), 0.0, 0.2)
GLOVES = material("PC_Gloves", (0.2, 0.36, 0.72), 0.0, 0.45)

# ------------------------------------------------------------------ body mesh (skeleton graph + Skin modifier)

# The head is a separate rigid skull/jaw/hair on the Head bone; the skin graph ends at the top of the neck.
nodes = [
    ("pelvis", (0.0, 0.0, 0.97), (0.165, 0.115)),
    ("spine", (0.0, 0.0, 1.13), (0.152, 0.106)),
    ("chest", (0.0, 0.0, 1.3), (0.19, 0.122)),
    ("upperchest", (0.0, 0.0, 1.42), (0.198, 0.115)),
    ("neck", (0.0, 0.006, 1.52), (0.056, 0.058)),
    ("necktop", (0.0, 0.012, 1.6), (0.052, 0.055)),
]
for side, sx in (("L", -1.0), ("R", 1.0)):
    nodes += [
        ("shoulder" + side, (sx * 0.205, 0.0, 1.43), (0.07, 0.068)),
        ("elbow" + side, (sx * 0.26, 0.0, 1.16), (0.049, 0.049)),
        ("wrist" + side, (sx * 0.285, 0.02, 0.93), (0.035, 0.03)),
        ("hand" + side, (sx * 0.295, 0.03, 0.83), (0.042, 0.022)),
        ("thumb" + side, (sx * 0.27, 0.065, 0.875), (0.016, 0.016)),
        ("hip" + side, (sx * 0.095, 0.0, 0.92), (0.092, 0.092)),
        ("knee" + side, (sx * 0.105, 0.01, 0.52), (0.064, 0.064)),
        ("ankle" + side, (sx * 0.11, -0.01, 0.1), (0.047, 0.047)),
        ("toe" + side, (sx * 0.115, 0.15, 0.04), (0.048, 0.032)),
    ]
index = {name: i for i, (name, _, _) in enumerate(nodes)}
edges = [("pelvis", "spine"), ("spine", "chest"), ("chest", "upperchest"), ("upperchest", "neck"), ("neck", "necktop")]
for side in ("L", "R"):
    edges += [("upperchest", "shoulder" + side), ("shoulder" + side, "elbow" + side), ("elbow" + side, "wrist" + side),
              ("wrist" + side, "hand" + side), ("wrist" + side, "thumb" + side), ("pelvis", "hip" + side),
              ("hip" + side, "knee" + side), ("knee" + side, "ankle" + side), ("ankle" + side, "toe" + side)]

mesh = bpy.data.meshes.new("PlayerBody")
mesh.from_pydata([p for _, p, _ in nodes], [(index[a], index[b]) for a, b in edges], [])
body = bpy.data.objects.new("PlayerBody", mesh)
scene.collection.objects.link(body)
bpy.context.view_layer.objects.active = body
body.select_set(True)
skin = body.modifiers.new("Skin", "SKIN")
skin.use_smooth_shade = True
for i, (name, _, radius) in enumerate(nodes):
    mesh.skin_vertices[0].data[i].radius = radius
    mesh.skin_vertices[0].data[i].use_root = name == "pelvis"
subdivision = body.modifiers.new("Smooth", "SUBSURF")
subdivision.levels = 2
subdivision.render_levels = 2
bpy.ops.object.modifier_apply(modifier="Skin")
bpy.ops.object.modifier_apply(modifier="Smooth")
for polygon in mesh.polygons:
    polygon.use_smooth = True

# Uniform by region (rest pose): skin on head/neck and hands, red jacket with reflective bands, trousers, boots.
for mat in (SKIN, JACKET, STRIPE, TROUSERS, BOOTS):
    mesh.materials.append(mat)
SLOT = {"skin": 0, "jacket": 1, "stripe": 2, "trousers": 3, "boots": 4}
for polygon in mesh.polygons:
    x, y, z = polygon.center
    if z > 1.545:
        slot = "skin"
    elif abs(x) > 0.215 and z < 0.95:
        slot = "skin"
    elif z > 0.9:
        # Bands, collar, cuffs, belt and boot tops are rigid rings (below), which also hide these seams.
        slot = "jacket"
    elif z > 0.13:
        slot = "trousers"
    else:
        slot = "boots"
    polygon.material_index = SLOT[slot]

# ------------------------------------------------------------------ armature

armature = bpy.data.armatures.new("PlayerRig")
rig = bpy.data.objects.new("PlayerRig", armature)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
BONES = {}


def bone(name, head, tail, parent=None, connect=False, deform=True):
    b = armature.edit_bones.new(name)
    b.head = head
    b.tail = tail
    b.roll = 0.0
    b.use_deform = deform
    if parent:
        b.parent = BONES[parent]
        b.use_connect = connect
    BONES[name] = b
    return b


bone("Hips", (0, 0, 0.93), (0, 0, 1.1))
bone("Spine", (0, 0, 1.1), (0, 0, 1.3), "Hips", True)
bone("Chest", (0, 0, 1.3), (0, 0, 1.5), "Spine", True)
bone("Neck", (0, 0, 1.5), (0, 0.012, 1.6), "Chest", True)
bone("Head", (0, 0.012, 1.6), (0, 0.0, 1.84), "Neck", True)
for side, sx in (("L", -1.0), ("R", 1.0)):
    bone("UpperArm." + side, (sx * 0.19, 0, 1.44), (sx * 0.25, 0, 1.17), "Chest")
    bone("LowerArm." + side, (sx * 0.25, 0, 1.17), (sx * 0.28, 0.02, 0.93), "UpperArm." + side, True)
    bone("Hand." + side, (sx * 0.28, 0.02, 0.93), (sx * 0.29, 0.03, 0.8), "LowerArm." + side, True)
    bone("UpperLeg." + side, (sx * 0.095, 0, 0.93), (sx * 0.105, 0.01, 0.52), "Hips")
    bone("LowerLeg." + side, (sx * 0.105, 0.01, 0.52), (sx * 0.11, -0.01, 0.1), "UpperLeg." + side, True)
    bone("Foot." + side, (sx * 0.11, -0.01, 0.1), (sx * 0.115, 0.15, 0.04), "LowerLeg." + side, True)
    bone("Toe." + side, (sx * 0.115, 0.15, 0.04), (sx * 0.115, 0.2, 0.04), "Foot." + side, True, deform=False)
bpy.ops.object.mode_set(mode="OBJECT")

# Skin weights: heat-map automatic weights, envelope weights as a fallback.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
unweighted = sum(1 for v in mesh.vertices if not any(g.weight > 0.01 for g in v.groups))
if unweighted > len(mesh.vertices) * 0.02:
    print("AUTO_WEIGHTS_INCOMPLETE", unweighted, "of", len(mesh.vertices), "- using envelope weights")
    for group in list(body.vertex_groups):
        body.vertex_groups.remove(group)
    bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")
    unweighted = sum(1 for v in mesh.vertices if not any(g.weight > 0.01 for g in v.groups))
print("SKIN_WEIGHTS vertices", len(mesh.vertices), "unweighted", unweighted, "groups", len(body.vertex_groups))
if unweighted > len(mesh.vertices) * 0.02:
    raise RuntimeError("Skinning failed: %d unweighted vertices" % unweighted)

# ------------------------------------------------------------------ face, hair, gloves (rigid, parented to bones)


def attached(name, kind, location, scale, mat, bone_name, rotation=(0, 0, 0)):
    if kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=16, radius=1.0, location=location)
    elif kind == "ring":
        bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=1.0, depth=2.0, end_fill_type="NOTHING", location=location)
    else:
        bpy.ops.mesh.primitive_cube_add(size=2.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name
    obj.scale = scale
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if kind in ("sphere", "ring"):
        bpy.ops.object.shade_smooth()
    if kind == "ring":
        thickness = obj.modifiers.new("Thickness", "SOLIDIFY")
        thickness.thickness = 0.006
    obj.data.materials.append(mat)
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world
    return obj


# Head: skull + jaw + hair on a short neck.
attached("Skull", "sphere", (0, 0.012, 1.705), (0.091, 0.103, 0.115), SKIN, "Head")
attached("Jaw", "sphere", (0, 0.04, 1.648), (0.072, 0.078, 0.058), SKIN, "Head")
attached("Hair", "sphere", (0, -0.006, 1.75), (0.096, 0.108, 0.082), HAIR, "Head")
attached("HairBack", "sphere", (0, -0.035, 1.705), (0.088, 0.075, 0.09), HAIR, "Head")
attached("Nose", "sphere", (0, 0.113, 1.692), (0.015, 0.024, 0.026), SKIN, "Head")
attached("Mouth", "cube", (0, 0.104, 1.646), (0.022, 0.004, 0.0035), EYES, "Head")
for side, sx in (("L", -1.0), ("R", 1.0)):
    attached("Eye." + side, "sphere", (sx * 0.034, 0.098, 1.718), (0.011, 0.008, 0.011), EYES, "Head")
    attached("Brow." + side, "cube", (sx * 0.035, 0.103, 1.741), (0.019, 0.006, 0.005), HAIR, "Head")
    attached("Ear." + side, "sphere", (sx * 0.092, 0.005, 1.7), (0.012, 0.026, 0.034), SKIN, "Head")
    # Reflective arm band, cuff and boot top (these also hide the material seams of the skinned body).
    attached("ArmBand." + side, "ring", (sx * 0.233, 0.0, 1.3), (0.062, 0.062, 0.018), STRIPE, "UpperArm." + side)
    attached("Cuff." + side, "ring", (sx * 0.282, 0.018, 0.955), (0.043, 0.04, 0.02), JACKET, "LowerArm." + side)
    attached("BootTop." + side, "ring", (sx * 0.11, -0.008, 0.135), (0.056, 0.056, 0.02), BOOTS, "LowerLeg." + side)
# Hi-vis bands on the jacket, collar and belt.
attached("ChestBand", "ring", (0, 0, 1.27), (0.198, 0.13, 0.02), STRIPE, "Spine")
attached("WaistBand", "ring", (0, 0, 1.06), (0.168, 0.118, 0.02), STRIPE, "Hips")
attached("Collar", "ring", (0, 0.007, 1.53), (0.066, 0.068, 0.034), JACKET, "Chest")
attached("Belt", "ring", (0, 0, 0.905), (0.178, 0.124, 0.022), BOOTS, "Hips")

# ------------------------------------------------------------------ animation

bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
pose = rig.pose.bones
for pb in pose:
    pb.rotation_mode = "XYZ"


def posed_point(bone_name, use_tail=True):
    bpy.context.view_layer.update()
    pb = pose[bone_name]
    return rig.matrix_world @ (pb.tail if use_tail else pb.head)


def reset_pose():
    for pb in pose:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)


# Measure which X-rotation sign swings a hanging limb forward (+Y) and leans an upright bone forward.
reset_pose()
pose["UpperLeg.L"].rotation_euler = (0.5, 0, 0)
LIMB_FORWARD = 1.0 if posed_point("UpperLeg.L").y > 0.05 else -1.0
reset_pose()
pose["Spine"].rotation_euler = (0.4, 0, 0)
LEAN_FORWARD = 1.0 if posed_point("Chest").y > 0.02 else -1.0
reset_pose()
print("AXIS_SIGNS limb", LIMB_FORWARD, "lean", LEAN_FORWARD)


def key_pose(frame, angles, hips_drop=0.0):
    """angles: bone -> degrees; positive = forward swing for limbs / forward lean for the spine chain."""
    reset_pose()
    for name, degrees in angles.items():
        sign = LEAN_FORWARD if name in ("Spine", "Chest", "Neck", "Head") else LIMB_FORWARD
        pose[name].rotation_euler = (math.radians(degrees) * sign, 0, 0)
    # Hips bone points up, so its local Y is world up.
    pose["Hips"].location = (0, -hips_drop, 0)
    for pb in pose:
        pb.keyframe_insert("rotation_euler", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def make_action(name, frames, builder):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    builder()
    for fcurve in getattr(action, "fcurves", []):
        for point in fcurve.keyframe_points:
            point.interpolation = "BEZIER"
    scene.frame_start = 1
    scene.frame_end = frames
    return action


def cycle(frames, leg, knee, arm, elbow, lean, bob):
    def build():
        steps = 8
        for k in range(steps + 1):
            t = k / steps
            phase = t * 2 * math.pi
            swing = math.sin(phase)
            # Knee bends most while the leg swings forward (recovery), less on the stance leg.
            knee_l = knee * max(0.0, math.sin(phase + math.pi * 0.5)) + knee * 0.15
            knee_r = knee * max(0.0, math.sin(phase + math.pi * 1.5)) + knee * 0.15
            key_pose(1 + round(t * (frames - 1)), {
                "UpperLeg.L": leg * swing, "UpperLeg.R": -leg * swing,
                "LowerLeg.L": -knee_l, "LowerLeg.R": -knee_r,
                "Foot.L": knee_l * 0.25, "Foot.R": knee_r * 0.25,
                "UpperArm.L": -arm * swing, "UpperArm.R": arm * swing,
                "LowerArm.L": elbow, "LowerArm.R": elbow,
                "Spine": lean, "Chest": lean * 0.4, "Head": -lean * 0.6,
            }, hips_drop=bob * (1 - abs(math.cos(phase))))
    return build


def idle():
    for frame, breath in ((1, 0.0), (31, 1.0), (60, 0.0)):
        key_pose(frame, {"Spine": -1.0 * breath, "Chest": -1.5 * breath, "Head": 1.0 * breath,
                         "UpperArm.L": 2.0 * breath, "UpperArm.R": 2.0 * breath, "LowerArm.L": 8.0, "LowerArm.R": 8.0})


def kneel():
    for frame in (1, 10):
        key_pose(frame, {"UpperLeg.L": 85.0, "LowerLeg.L": -85.0, "Foot.L": 0.0,
                         "UpperLeg.R": -5.0, "LowerLeg.R": -95.0, "Foot.R": 40.0,
                         "Spine": 12.0, "Chest": 6.0, "Head": -8.0,
                         "UpperArm.L": 25.0, "UpperArm.R": 25.0, "LowerArm.L": 35.0, "LowerArm.R": 35.0}, hips_drop=0.42)


actions = [
    make_action("Idle", 60, idle),
    make_action("Walk", 31, cycle(31, 28.0, 45.0, 22.0, 18.0, 3.0, 0.025)),
    make_action("Run", 21, cycle(21, 42.0, 85.0, 40.0, 80.0, 10.0, 0.06)),
    make_action("Kneel", 10, kneel),
]

# Self-check on the Walk action: at a quarter cycle the left foot is ahead of the hips; kneeling lowers the hips.
rig.animation_data.action = bpy.data.actions["Walk"]
scene.frame_set(8)
left_foot_forward = posed_point("Foot.L", use_tail=False).y
rig.animation_data.action = bpy.data.actions["Kneel"]
scene.frame_set(1)
kneel_hips = posed_point("Hips", use_tail=False).z
print("POSE_CHECK walk left ankle y %.3f, kneel hips z %.3f" % (left_foot_forward, kneel_hips))
if left_foot_forward < 0.05 or kneel_hips > 0.65:
    raise RuntimeError("Animation self-check failed")
rig.animation_data.action = bpy.data.actions["Idle"]
scene.frame_set(1)
bpy.ops.object.mode_set(mode="OBJECT")

# ------------------------------------------------------------------ export

bpy.ops.object.select_all(action="DESELECT")
for obj in [rig] + list(rig.children_recursive):
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=os.path.join(EXPORT_DIR, "PlayerCharacter.fbx"),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    bake_space_transform=False,
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    armature_nodetype="NULL",
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
)
triangles = sum(len(p.vertices) - 2 for p in mesh.polygons)
print("EXPORTED PlayerCharacter.fbx body vertices", len(mesh.vertices), "triangles", triangles, "bones", len(armature.bones),
      "actions", [a.name for a in actions])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_DIR, "GoldenHour_PlayerCharacter.blend"))
print("GOLDEN_HOUR_PLAYER_CHARACTER_OK")
