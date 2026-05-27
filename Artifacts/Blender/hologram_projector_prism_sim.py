import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
BLEND_PATH = ROOT / "four_projector_trapezoid_prism_sim.blend"
RENDER_PATH = ROOT / "four_projector_trapezoid_prism_preview.png"


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_mat(name, color, roughness=0.4, alpha=1.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Roughness"].default_value = roughness
        if emission:
            bsdf.inputs["Emission Color"].default_value = emission
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    mat.blend_method = "BLEND"
    mat.use_screen_refraction = True
    mat.show_transparent_back = True
    return mat


def cube_obj(name, location, scale, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    return obj


def cylinder_between(name, start, end, radius, mat):
    start = Vector(start)
    end = Vector(end)
    mid = (start + end) * 0.5
    direction = end - start
    length = direction.length
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius, depth=length, location=mid)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    if mat:
        obj.data.materials.append(mat)
    return obj


def cone_arrow(name, start, end, radius, mat):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    length = direction.length
    shaft_end = start + direction * 0.82
    shaft = cylinder_between(f"{name} shaft", start, shaft_end, radius, mat)
    bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=radius * 3.2, radius2=0, depth=length * 0.18, location=(shaft_end + end) * 0.5)
    head = bpy.context.object
    head.name = f"{name} head"
    head.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    if mat:
        head.data.materials.append(mat)
    return shaft, head


def add_label(text, location, rotation=(math.radians(65), 0, math.radians(0)), size=0.18):
    bpy.ops.object.text_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = f"Label - {text}"
    obj.data.body = text
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.materials.append(label_mat)
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def make_prism_faces():
    # Upside-down square trapezoidal prism/frustum: large square opening at top,
    # smaller square opening at bottom. Faces are separate transparent panels.
    z_low = 1.55
    z_high = 4.0
    small = 2.45
    large = 4.55
    s = small / 2
    l = large / 2

    corners_low = {
        "NE": Vector((s, s, z_low)),
        "NW": Vector((-s, s, z_low)),
        "SW": Vector((-s, -s, z_low)),
        "SE": Vector((s, -s, z_low)),
    }
    corners_high = {
        "NE": Vector((l, l, z_high)),
        "NW": Vector((-l, l, z_high)),
        "SW": Vector((-l, -l, z_high)),
        "SE": Vector((l, -l, z_high)),
    }
    faces = {
        "North beam-splitter face": ["NW", "NE"],
        "East beam-splitter face": ["NE", "SE"],
        "South beam-splitter face": ["SE", "SW"],
        "West beam-splitter face": ["SW", "NW"],
    }

    for name, keys in faces.items():
        k1, k2 = keys
        verts = [corners_low[k1], corners_low[k2], corners_high[k2], corners_high[k1]]
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in verts], [], [(0, 1, 2, 3)])
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(glass_mat)

        solid = obj.modifiers.new("thin panel thickness", "SOLIDIFY")
        solid.thickness = 0.025
        solid.offset = 0

    # Thin edge outline for readability.
    edge_points = [
        ((-l, -l, z_high), (l, -l, z_high)),
        ((l, -l, z_high), (l, l, z_high)),
        ((l, l, z_high), (-l, l, z_high)),
        ((-l, l, z_high), (-l, -l, z_high)),
        ((-s, -s, z_low), (s, -s, z_low)),
        ((s, -s, z_low), (s, s, z_low)),
        ((s, s, z_low), (-s, s, z_low)),
        ((-s, s, z_low), (-s, -s, z_low)),
        ((-s, -s, z_low), (-l, -l, z_high)),
        ((s, -s, z_low), (l, -l, z_high)),
        ((s, s, z_low), (l, l, z_high)),
        ((-s, s, z_low), (-l, l, z_high)),
    ]
    for idx, (a, b) in enumerate(edge_points):
        cylinder_between(f"prism edge {idx + 1}", a, b, 0.012, edge_mat)


def make_hologram():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=0.42, location=(0, 0, 2.55))
    core = bpy.context.object
    core.name = "reflected hologram - glowing core"
    core.scale = (1.0, 1.0, 1.35)
    core.data.materials.append(hologram_mat)

    # Simple orbit rings to make the reflected image visually distinct.
    for i, rot in enumerate([(0, 0, 0), (math.radians(65), 0, 0), (0, math.radians(65), 0)]):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.62, minor_radius=0.012, major_segments=96, minor_segments=8, location=(0, 0, 2.55), rotation=rot)
        ring = bpy.context.object
        ring.name = f"reflected hologram ring {i + 1}"
        ring.data.materials.append(hologram_mat)


def make_projector_module(name, side, x, y, rotation_z):
    projector = cube_obj(f"{name} bottom projector", (x, y, 0.2), (0.7, 0.45, 0.28), projector_mat)
    projector.rotation_euler[2] = rotation_z

    lens_offset = Vector((0, 0.33, 0.03))
    lens_offset.rotate(projector.rotation_euler)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.12, location=Vector((x, y, 0.25)) + lens_offset)
    lens = bpy.context.object
    lens.name = f"{name} projector lens"
    lens.scale = (1.0, 1.0, 0.45)
    lens.data.materials.append(lens_mat)

    # Horizontal rear-projection diffuser panel above each projector.
    screen = cube_obj(f"{name} rear-projection screen", (x * 0.55, y * 0.55, 1.02), (1.65, 1.0, 0.045), screen_mat)
    screen.rotation_euler[2] = rotation_z

    # Bright content patch on each diffuser.
    patch = cube_obj(f"{name} displayed image patch", (x * 0.55, y * 0.55, 1.055), (1.0, 0.5, 0.012), content_mat)
    patch.rotation_euler[2] = rotation_z

    screen_point = Vector((x * 0.55, y * 0.55, 1.12))
    face_point = Vector((x * 0.23, y * 0.23, 2.4))
    viewer_point = Vector((x * 0.92, y * 0.92, 3.0))
    cone_arrow(f"{name} projector beam", (x, y, 0.38), screen_point, 0.025, cyan_mat)
    cone_arrow(f"{name} reflected beam to prism", screen_point, face_point, 0.018, cyan_mat)
    cone_arrow(f"{name} viewer ray", face_point, viewer_point, 0.018, cyan_mat)

    label_pos = Vector((x, y, 0.55))
    add_label("bottom projector", label_pos, size=0.16)
    add_label("rear-projection screen", (x * 0.55, y * 0.55, 1.35), size=0.15)


clear_scene()

# Materials.
base_mat = make_mat("matte black enclosure", (0.015, 0.017, 0.02, 1), roughness=0.85)
projector_mat = make_mat("dark projector body", (0.03, 0.035, 0.04, 1), roughness=0.5)
lens_mat = make_mat("projector lens glass", (0.02, 0.08, 0.12, 0.9), roughness=0.1, alpha=0.9, emission=(0.0, 0.45, 1.0, 1), emission_strength=0.6)
screen_mat = make_mat("rear-projection diffuser", (0.85, 0.9, 0.95, 0.72), roughness=0.2, alpha=0.72, emission=(0.15, 0.35, 0.8, 1), emission_strength=0.25)
content_mat = make_mat("bright projected content", (0.1, 0.75, 1.0, 0.95), roughness=0.2, alpha=0.9, emission=(0.0, 0.8, 1.0, 1), emission_strength=1.7)
glass_mat = make_mat("transparent beam-splitter prism", (0.28, 0.85, 1.0, 0.22), roughness=0.02, alpha=0.22, emission=(0.0, 0.25, 0.45, 1), emission_strength=0.08)
edge_mat = make_mat("prism edge outline", (0.55, 0.85, 1.0, 0.75), roughness=0.15, alpha=0.75, emission=(0.1, 0.45, 0.8, 1), emission_strength=0.25)
cyan_mat = make_mat("cyan optical path arrows", (0.0, 0.85, 1.0, 0.62), roughness=0.25, alpha=0.62, emission=(0.0, 0.9, 1.0, 1), emission_strength=1.4)
hologram_mat = make_mat("floating reflected hologram", (0.0, 0.95, 1.0, 0.55), roughness=0.08, alpha=0.55, emission=(0.0, 0.95, 1.0, 1), emission_strength=1.8)
label_mat = make_mat("label text bright", (1, 1, 1, 1), roughness=0.5, emission=(1, 1, 1, 1), emission_strength=0.35)

# Base enclosure and screens.
cube_obj("matte black lower enclosure", (0, 0, -0.06), (6.4, 6.4, 0.12), base_mat)
cube_obj("central black light baffle", (0, 0, 0.65), (1.25, 1.25, 1.2), base_mat)

make_prism_faces()
make_hologram()

make_projector_module("north", "north", 0, 2.85, math.radians(180))
make_projector_module("south", "south", 0, -2.85, math.radians(0))
make_projector_module("east", "east", 2.85, 0, math.radians(90))
make_projector_module("west", "west", -2.85, 0, math.radians(-90))

# Main explanatory labels.
add_label("upside-down trapezoidal prism", (0, -3.15, 4.35), rotation=(math.radians(63), 0, 0), size=0.2)
add_label("beam-splitter face", (2.55, 0, 3.25), rotation=(math.radians(64), 0, math.radians(90)), size=0.17)
add_label("reflected hologram", (0, 0, 3.45), rotation=(math.radians(63), 0, 0), size=0.18)
add_label("viewer", (0, -4.6, 3.05), rotation=(math.radians(68), 0, 0), size=0.18)

# Viewer direction arrow.
cone_arrow("viewer sightline", (0, -4.25, 2.9), (0, -2.05, 2.65), 0.022, edge_mat)

# Simple top-view inset diagram mounted to the side.
inset_mat = make_mat("inset white panel", (1, 1, 1, 0.82), roughness=0.35, alpha=0.82)
inset = cube_obj("top view inset panel", (-4.2, 3.6, 2.7), (2.2, 0.05, 1.6), inset_mat)
add_label("top view", (-4.2, 3.54, 3.38), rotation=(math.radians(90), 0, 0), size=0.13)
for px, pz in [(-4.2, 2.4), (-4.2, 3.0), (-4.8, 2.7), (-3.6, 2.7)]:
    cube_obj("inset projector/screen mark", (px, 3.52, pz), (0.32, 0.035, 0.08), content_mat)
cube_obj("inset prism mark", (-4.2, 3.515, 2.7), (0.55, 0.035, 0.55), glass_mat)

# Lighting and camera.
bpy.ops.object.light_add(type="AREA", location=(0, -5.0, 6.0))
key = bpy.context.object
key.name = "large softbox"
key.data.energy = 450
key.data.size = 5

bpy.ops.object.camera_add(location=(6.7, -8.4, 5.25))
camera = bpy.context.object
bpy.context.scene.camera = camera
look_at(camera, (0, 0, 2.05))
camera.data.lens = 25
camera.data.dof.use_dof = True
camera.data.dof.focus_distance = 7.5
camera.data.dof.aperture_fstop = 8

# Render settings.
bpy.context.scene.render.engine = "CYCLES"
bpy.context.scene.cycles.samples = 96
bpy.context.scene.cycles.use_denoising = True
bpy.context.scene.view_settings.view_transform = "Filmic"
bpy.context.scene.view_settings.look = "Medium High Contrast"
bpy.context.scene.render.resolution_x = 1600
bpy.context.scene.render.resolution_y = 1100
bpy.context.scene.world.color = (1, 1, 1)

bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
bpy.context.scene.render.filepath = str(RENDER_PATH)
bpy.ops.render.render(write_still=True)

print(f"Saved Blender scene: {BLEND_PATH}")
print(f"Saved preview render: {RENDER_PATH}")
