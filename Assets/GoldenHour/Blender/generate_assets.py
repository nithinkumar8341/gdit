import os

import bpy
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(ROOT, "Blender", "Exports")
SOURCE_DIR = os.path.join(ROOT, "Blender", "Source")
os.makedirs(EXPORT_DIR, exist_ok=True)
os.makedirs(SOURCE_DIR, exist_ok=True)


def material(name, color, metallic=0.0, roughness=0.72):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


SKIN = material("Warm neutral mannequin", (0.72, 0.52, 0.38))
SHIRT = material("Blue mannequin clothing", (0.12, 0.25, 0.38))
TROUSERS = material("Charcoal mannequin clothing", (0.12, 0.15, 0.18))
RED = material("Emergency red", (0.72, 0.055, 0.07))
WHITE = material("Clean white", (0.91, 0.92, 0.88))
METAL = material("Brushed latch metal", (0.35, 0.39, 0.42), 0.75)
ORANGE = material("Safety orange", (0.95, 0.27, 0.04))
REFLECTIVE = material("Reflective ivory", (0.95, 0.82, 0.45), 0.0, 0.35)
GREEN = material("Supply green", (0.12, 0.48, 0.29))
BLUE = material("Supply blue", (0.11, 0.35, 0.67))


def cube(name, location, dimensions, mat, parent=None, bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Soft low-poly edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


def sphere(name, location, dimensions, mat, segments=10, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings,
                                         radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj


def cylinder(name, location, radius, depth, mat, vertices=8, rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                        location=location)
    obj = bpy.context.object
    obj.name = name
    if rotation:
        obj.rotation_euler = rotation
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
    return obj


def export_named(prefix, filename):
    bpy.ops.object.select_all(action="DESELECT")
    selected = [obj for obj in bpy.context.scene.objects if obj.name.startswith(prefix)]
    if not selected:
        raise RuntimeError("No Blender objects matched " + prefix)
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = selected[0]
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(EXPORT_DIR, filename),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )
    print("EXPORTED", filename, "objects:", len(selected))


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

# Separated low-poly pieces keep the mannequin readable and unanimated.
cube("Mannequin_Torso", (0, 0.98, 0), (0.52, 0.7, 0.3), SHIRT)
sphere("Mannequin_Head", (0, 1.62, 0), (0.28, 0.32, 0.28), SKIN)
cylinder("Mannequin_Neck", (0, 1.48, 0), 0.09, 0.12, SKIN)
for side in (-1, 1):
    arm = cylinder("Mannequin_Arm", (side * 0.37, 0.95, 0), 0.105, 0.62, SHIRT,
                   rotation=(0, 0, side * -0.15))
    leg = cylinder("Mannequin_Leg", (side * 0.14, 0.36, 0), 0.12, 0.66, TROUSERS)
    cube("Mannequin_Shoe", (side * 0.14, 0.07, 0.07), (0.22, 0.12, 0.34), TROUSERS)
    sphere("Mannequin_Hand", (side * 0.43, 0.6, 0), (0.14, 0.15, 0.14), SKIN)

# Rugged kit, with a separate hinged lid mesh and visible supply blocks.
hinge = bpy.data.objects.new("KitHinge", None)
bpy.context.collection.objects.link(hinge)
hinge.location = (0, 0.53, -0.46)
cube("Kit_Base", (0, 0.27, 0), (1.25, 0.5, 0.92), RED)
lid = cube("Kit_Lid", (0, 0.62, 0), (1.25, 0.16, 0.92), RED, bevel=0.055)
lid.parent = hinge
lid.location = (0, 0.09, 0.46)
cube("Kit_Cross_Vertical", (0, 0.715, 0), (0.2, 0.03, 0.56), WHITE, parent=lid, bevel=0.01)
cube("Kit_Cross_Horizontal", (0, 0.715, 0), (0.53, 0.03, 0.2), WHITE, parent=lid, bevel=0.01)
for side in (-1, 1):
    cube("Kit_Metal_Latch", (side * 0.42, 0.5, 0.48), (0.12, 0.2, 0.08), METAL)
    cube("Kit_Handle", (side * 0.31, 0.56, -0.1), (0.11, 0.08, 0.34), METAL)
cube("Kit_Gauze", (-0.29, 0.57, 0.02), (0.23, 0.12, 0.28), WHITE)
cube("Kit_Dressing", (0.0, 0.57, 0.02), (0.23, 0.12, 0.28), GREEN)
cube("Kit_Bandage", (0.29, 0.57, 0.02), (0.23, 0.12, 0.28), REFLECTIVE)

# Roadside barrier, feet, and reflective safety stripes.
cube("Barrier_Beam", (0, 0.72, 0), (2.5, 0.24, 0.25), ORANGE)
for side in (-1, 1):
    cube("Barrier_Foot", (side * 0.85, 0.14, 0), (0.62, 0.18, 0.45), METAL)
    cube("Barrier_Leg", (side * 0.85, 0.47, 0), (0.16, 0.62, 0.18), ORANGE)
    stripe = cube("Barrier_Reflective_Stripe", (side * 0.39, 0.72, -0.13),
                  (0.22, 0.23, 0.025), REFLECTIVE, bevel=0.005)

# Basic textured-looking supply bundles for low-poly dressing.
cube("Supply_Gauze", (0, 0.18, 0), (0.48, 0.34, 0.32), WHITE)
cube("Supply_Bandage", (0.58, 0.18, 0), (0.48, 0.34, 0.32), REFLECTIVE)
cylinder("Supply_Tape", (1.14, 0.16, 0), 0.19, 0.24, BLUE, vertices=10)
cube("Supply_Blanket", (1.7, 0.18, 0), (0.5, 0.34, 0.38), GREEN)

# Compact accident props: a cone, broken panel, and low-poly tree.
cylinder("Prop_Cone_Base", (0, 0.08, 0), 0.27, 0.12, ORANGE, vertices=8)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.19, radius2=0.035,
                                depth=0.62, location=(0, 0.44, 0))
bpy.context.object.name = "Prop_Cone"
bpy.context.object.data.materials.append(ORANGE)
cube("Prop_Debris", (0.65, 0.13, 0), (0.55, 0.16, 0.28), METAL)
cylinder("Prop_Tree_Trunk", (1.35, 0.8, 0), 0.11, 1.6, TROUSERS, vertices=7)
sphere("Prop_Tree_Crown", (1.35, 1.9, 0), (1.05, 1.1, 1.0), GREEN, segments=8, rings=5)

bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_DIR, "GoldenHour_Props.blend"))
export_named("Mannequin_", "LowPolyMannequin.fbx")
export_named("Kit", "FirstAidKit.fbx")
export_named("Barrier_", "RoadBarrier.fbx")
export_named("Supply_", "SupplyBundles.fbx")
export_named("Prop_", "AccidentProps.fbx")
print("GOLDEN_HOUR_BLENDER_ASSETS_OK")
