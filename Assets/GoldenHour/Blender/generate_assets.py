"""Generate Golden Hour low-poly props in Blender background mode.

Run:
  blender.exe --background --factory-startup --python generate_assets.py

All layout coordinates below are written as (x, up, depth) in metres, matching
Unity's Y-up convention. Helpers convert them into Blender's Z-up space
(Blender X = x, Blender Y = depth, Blender Z = up) before export, and the FBX
exporter converts back to Y-up for Unity.
"""
import math
import os

import bpy


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
EXPORT_DIR = os.path.join(ROOT, "Blender", "Exports")
SOURCE_DIR = os.path.join(ROOT, "Blender", "Source")
os.makedirs(EXPORT_DIR, exist_ok=True)
os.makedirs(SOURCE_DIR, exist_ok=True)


def to_blender(vector):
    x, up, depth = vector
    return (x, depth, up)


def material(name, color, metallic=0.0, roughness=0.72):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


SKIN = material("GH_Skin", (0.72, 0.52, 0.38))
SHIRT = material("GH_Clothing", (0.12, 0.25, 0.38))
TROUSERS = material("GH_Trousers", (0.12, 0.15, 0.18))
RED = material("GH_EmergencyRed", (0.72, 0.055, 0.07))
WHITE = material("GH_CleanWhite", (0.91, 0.92, 0.88))
METAL = material("GH_Metal", (0.35, 0.39, 0.42), 0.75)
ORANGE = material("GH_SafetyOrange", (0.95, 0.27, 0.04))
REFLECTIVE = material("GH_Reflective", (0.95, 0.82, 0.45), 0.0, 0.35)
GREEN = material("GH_SupplyGreen", (0.12, 0.48, 0.29))
BLUE = material("GH_SupplyBlue", (0.11, 0.35, 0.67))
CAR_PAINT = material("GH_CarPaint", (0.42, 0.16, 0.12), 0.3, 0.45)
GLASS = material("GH_Glass", (0.35, 0.47, 0.55), 0.1, 0.15)
TYRE = material("GH_Tyre", (0.05, 0.05, 0.06))


def finish(obj, name, mat, parent):
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


def cube(name, location, dimensions, mat, parent=None, bevel=0.03, tilt=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=to_blender(location))
    obj = bpy.context.object
    obj.dimensions = to_blender(dimensions)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if tilt:
        obj.rotation_euler = to_blender(tilt)
    if bevel:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
    return finish(obj, name, mat, parent)


def sphere(name, location, dimensions, mat, segments=10, rings=6, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings,
                                         radius=1, location=to_blender(location))
    obj = bpy.context.object
    obj.dimensions = to_blender(dimensions)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, parent)


def cylinder(name, location, radius, depth, mat, vertices=8, tilt=None, parent=None):
    """Cylinder whose length runs along the 'up' axis unless tilted."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                        location=to_blender(location))
    obj = bpy.context.object
    if tilt:
        obj.rotation_euler = to_blender(tilt)
    return finish(obj, name, mat, parent)


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
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    print("EXPORTED", filename, "objects:", len(selected))


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

# --- Low-poly standing mannequin (origin at the feet, ~1.75 m tall) ---------
cube("Mannequin_Torso", (0, 1.08, 0), (0.46, 0.62, 0.26), SHIRT)
cube("Mannequin_Hips", (0, 0.74, 0), (0.42, 0.18, 0.24), TROUSERS)
sphere("Mannequin_Head", (0, 1.62, 0), (0.24, 0.29, 0.26), SKIN)
cylinder("Mannequin_Neck", (0, 1.45, 0), 0.07, 0.12, SKIN)
for side in (-1, 1):
    cylinder("Mannequin_Arm", (side * 0.3, 1.04, 0), 0.075, 0.6, SHIRT,
             tilt=(0, 0, side * 0.12))
    sphere("Mannequin_Hand", (side * 0.34, 0.7, 0), (0.1, 0.13, 0.1), SKIN)
    cylinder("Mannequin_Leg", (side * 0.11, 0.38, 0), 0.085, 0.66, TROUSERS)
    cube("Mannequin_Shoe", (side * 0.11, 0.05, 0.05), (0.13, 0.1, 0.27), TROUSERS)

# --- First-aid kit: base plus a hinged lid under an empty pivot -------------
# Hinge sits on the back top edge (depth -0.46); the lid extends toward +depth.
hinge = bpy.data.objects.new("KitHinge", None)
bpy.context.collection.objects.link(hinge)
hinge.location = to_blender((0, 0.52, -0.46))
cube("Kit_Base", (0, 0.26, 0), (1.25, 0.52, 0.92), RED)
cube("Kit_Lid", (0, 0.6, 0), (1.27, 0.16, 0.94), RED, parent=hinge, bevel=0.05)
cube("Kit_Cross_Vertical", (0, 0.685, 0), (0.2, 0.03, 0.56), WHITE, parent=hinge, bevel=0.01)
cube("Kit_Cross_Horizontal", (0, 0.685, 0), (0.56, 0.03, 0.2), WHITE, parent=hinge, bevel=0.01)
for side in (-1, 1):
    cube("Kit_Latch", (side * 0.42, 0.5, 0.475), (0.12, 0.2, 0.06), METAL)
    cube("Kit_Handle", (side * 0.66, 0.32, 0), (0.06, 0.08, 0.34), METAL)
cube("Kit_Gauze", (-0.34, 0.5, 0.0), (0.24, 0.1, 0.3), WHITE)
cube("Kit_Dressing", (-0.08, 0.5, 0.0), (0.22, 0.1, 0.3), GREEN)
cube("Kit_Bandage", (0.16, 0.5, 0.0), (0.2, 0.1, 0.3), REFLECTIVE)
cube("Kit_Gloves", (0.38, 0.5, 0.0), (0.18, 0.08, 0.3), BLUE)

# --- Road barrier ------------------------------------------------------------
cube("Barrier_Beam", (0, 0.78, 0), (2.4, 0.26, 0.1), ORANGE)
for side in (-1, 1):
    cube("Barrier_Foot", (side * 0.95, 0.06, 0), (0.18, 0.12, 0.7), METAL)
    cube("Barrier_Leg", (side * 0.95, 0.45, 0), (0.12, 0.8, 0.12), METAL)
for index in range(4):
    cube("Barrier_Stripe", (-0.9 + index * 0.6, 0.78, -0.055), (0.24, 0.27, 0.02),
         WHITE, bevel=0.0)

# --- Supply bundles (inventory dressing) -------------------------------------
cube("Supply_Gauze", (0, 0.1, 0), (0.3, 0.2, 0.22), WHITE)
cylinder("Supply_Bandage", (0.45, 0.1, 0), 0.1, 0.2, REFLECTIVE, vertices=10)
cube("Supply_Gloves", (0.85, 0.06, 0), (0.24, 0.12, 0.3), BLUE)
cube("Supply_Dressing", (1.25, 0.05, 0), (0.28, 0.1, 0.28), GREEN)
cube("Supply_Blanket", (1.7, 0.1, 0), (0.4, 0.2, 0.3), REFLECTIVE)

# --- Accident props: cone and debris ----------------------------------------
cube("Prop_Cone_Base", (0, 0.025, 0), (0.42, 0.05, 0.42), ORANGE, bevel=0.0)
bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=0.17, radius2=0.03,
                                depth=0.62, location=to_blender((0, 0.36, 0)))
finish(bpy.context.object, "Prop_Cone_Body", ORANGE, None)
cylinder("Prop_Cone_Stripe", (0, 0.4, 0), 0.115, 0.08, WHITE, vertices=10)

# --- Damaged car (origin centred on the ground, front toward +x) -----------
cube("Car_Body", (0, 0.55, 0), (4.0, 0.6, 1.75), CAR_PAINT, bevel=0.08)
cube("Car_Cabin", (-0.35, 1.08, 0), (2.0, 0.55, 1.55), GLASS, bevel=0.06)
cube("Car_Roof", (-0.4, 1.38, 0), (1.8, 0.08, 1.5), CAR_PAINT, bevel=0.03)
cube("Car_Hood_Crumpled", (1.55, 0.9, 0.1), (1.0, 0.12, 1.6), CAR_PAINT,
     tilt=(0.18, 0.0, 0.22), bevel=0.04)
cube("Car_Bumper", (2.05, 0.38, 0.15), (0.14, 0.2, 1.7), METAL, tilt=(0, 0.25, 0))
for axle in (-1.25, 1.3):
    for side in (-1, 1):
        cylinder("Car_Wheel", (axle, 0.34, side * 0.82), 0.34, 0.24, TYRE,
                 vertices=12, tilt=(math.pi / 2, 0, 0))
cube("Car_Glass_Shards", (2.6, 0.02, -0.6), (0.6, 0.02, 0.5), GLASS, bevel=0.0,
     tilt=(0, 0.6, 0))

bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE_DIR, "GoldenHour_Props.blend"))
export_named("Mannequin_", "LowPolyMannequin.fbx")
# "Kit" also matches the KitHinge empty, keeping the lid hierarchy intact.
export_named("Kit", "FirstAidKit.fbx")
export_named("Barrier_", "RoadBarrier.fbx")
export_named("Supply_", "SupplyBundles.fbx")
export_named("Prop_", "AccidentProps.fbx")
export_named("Car_", "DamagedCar.fbx")
print("GOLDEN_HOUR_BLENDER_ASSETS_OK")
