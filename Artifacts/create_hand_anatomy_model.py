import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "Assets" / "Models" / "HandAnatomy"
FBX_PATH = OUT_DIR / "realistic_hand_anatomy_model.fbx"
BLEND_PATH = OUT_DIR / "realistic_hand_anatomy_model.blend"
PREVIEW_PATH = OUT_DIR / "realistic_hand_anatomy_preview.png"


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_material(name, color, roughness=0.55, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Alpha"].default_value = alpha
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0
    mat.diffuse_color = color
    if alpha < 1.0:
        output = nodes.get("Material Output")
        transparent = nodes.new(type="ShaderNodeBsdfTransparent")
        transparent.inputs["Color"].default_value = (color[0], color[1], color[2], 1)
        mix = nodes.new(type="ShaderNodeMixShader")
        mix.inputs["Fac"].default_value = alpha
        links.new(transparent.outputs["BSDF"], mix.inputs[1])
        links.new(bsdf.outputs["BSDF"], mix.inputs[2])
        links.new(mix.outputs["Shader"], output.inputs["Surface"])
        mat.blend_method = "BLEND"
        mat.use_screen_refraction = True
        mat.show_transparent_back = True
    return mat


def parent_empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def assign_material(obj, mat):
    obj.data.materials.append(mat)


def shade_smooth(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)


def align_to_vector(obj, start, end):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    obj.location = (start_v + end_v) * 0.5
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()


def add_ellipsoid(name, loc, scale, mat, parent=None, rot=(0, 0, 0), segments=32):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=max(12, segments // 2),
        radius=1,
        location=loc,
        rotation=rot,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    assign_material(obj, mat)
    shade_smooth(obj)
    if parent:
        obj.parent = parent
    return obj


def add_cylinder_between(name, start, end, radius, mat, parent=None, vertices=24):
    start_v = Vector(start)
    end_v = Vector(end)
    length = (end_v - start_v).length
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length)
    obj = bpy.context.object
    obj.name = name
    align_to_vector(obj, start, end)
    assign_material(obj, mat)
    shade_smooth(obj)
    if parent:
        obj.parent = parent
    return obj


def add_capsule(name, start, end, radius, mat, parent=None, vertices=24):
    cyl = add_cylinder_between(f"{name}_shaft", start, end, radius, mat, parent, vertices)
    add_ellipsoid(f"{name}_proximal_head", start, (radius, radius, radius), mat, parent, segments=vertices)
    add_ellipsoid(f"{name}_distal_head", end, (radius, radius, radius), mat, parent, segments=vertices)
    return cyl


def add_curve_tube(name, points, radius, mat, parent=None, resolution=5):
    curve = bpy.data.curves.new(name, type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = 5
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, coords in zip(spline.points, points):
        point.co = (coords[0], coords[1], coords[2], 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, mat)
    if parent:
        obj.parent = parent
    return obj


def add_text(name, text, loc, size, mat, parent=None):
    font_curve = bpy.data.curves.new(name, "FONT")
    font_curve.body = text
    font_curve.align_x = "CENTER"
    font_curve.align_y = "CENTER"
    font_curve.size = size
    font_curve.extrude = 0.0004
    obj = bpy.data.objects.new(name, font_curve)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (math.radians(72), 0, 0)
    assign_material(obj, mat)
    if parent:
        obj.parent = parent
    return obj


def build_hand():
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"

    root = parent_empty("Realistic_Hand_Anatomy_Model")
    skin_layer = parent_empty("rand_0_skin", root)
    muscle_layer = parent_empty("rand_0_muscle", root)
    bone_layer = parent_empty("rand_0_bone", root)
    nerve_layer = parent_empty("rand_0_nerve", root)
    annotation_layer = parent_empty("anatomy_labels", root)

    bone_mat = make_material("warm_ivory_bone", (0.88, 0.80, 0.62, 1), 0.62)
    joint_mat = make_material("slightly_glossy_cartilage", (0.92, 0.88, 0.78, 1), 0.38)
    muscle_mat = make_material("deep_red_muscle", (0.58, 0.035, 0.025, 1), 0.72)
    muscle_dark = make_material("striated_muscle_shadow", (0.28, 0.012, 0.01, 1), 0.8)
    tendon_mat = make_material("pale_tendon", (0.86, 0.78, 0.60, 1), 0.5)
    nerve_mat = make_material("clinical_yellow_nerves", (1.0, 0.73, 0.05, 1), 0.44)
    skin_mat = make_material("transparent_skin_shell", (0.78, 0.47, 0.34, 0.10), 0.5, 0.10)
    label_mat = make_material("dark_label_text", (0.04, 0.035, 0.03, 1), 0.4)

    finger_specs = [
        ("index", -0.034, 0.118, [0.043, 0.033, 0.024], 0.0062),
        ("middle", -0.011, 0.126, [0.050, 0.037, 0.026], 0.0067),
        ("ring", 0.012, 0.118, [0.045, 0.034, 0.024], 0.0063),
        ("little", 0.034, 0.101, [0.036, 0.028, 0.020], 0.0055),
    ]

    metacarpal_start_y = -0.025
    metacarpal_end_y = 0.083
    for idx, (name, x, y_base, segments, radius) in enumerate(finger_specs, start=2):
        add_capsule(f"{name}_metacarpal", (x * 0.76, metacarpal_start_y, 0), (x, metacarpal_end_y, 0.006), radius * 0.92, bone_mat, bone_layer)
        y = y_base
        z = 0.009
        r = radius
        prev = (x, metacarpal_end_y, z)
        for s, length in enumerate(segments):
            end = (x + (s * 0.0015), y + sum(segments[:s + 1]), z + s * 0.002)
            add_capsule(f"{name}_phalange_{s + 1}", prev, end, r * (0.92 - s * 0.14), bone_mat, bone_layer)
            add_ellipsoid(f"{name}_joint_{s + 1}", prev, (r * 1.25, r * 1.08, r * 0.85), joint_mat, bone_layer, segments=20)
            prev = end

    thumb_chain = [
        ("thumb_metacarpal", (-0.055, 0.004, 0.003), (-0.098, 0.050, 0.008), 0.0067),
        ("thumb_proximal_phalanx", (-0.098, 0.050, 0.008), (-0.128, 0.088, 0.012), 0.0063),
        ("thumb_distal_phalanx", (-0.128, 0.088, 0.012), (-0.151, 0.119, 0.014), 0.0055),
    ]
    for name, start, end, radius in thumb_chain:
        add_capsule(name, start, end, radius, bone_mat, bone_layer)
        add_ellipsoid(f"{name}_joint_cap", start, (radius * 1.25, radius, radius), joint_mat, bone_layer, segments=20)

    carpal_positions = [
        (-0.041, -0.052, 0.001), (-0.022, -0.058, 0.004), (-0.003, -0.055, 0.005), (0.018, -0.052, 0.002),
        (-0.034, -0.031, 0.004), (-0.013, -0.033, 0.006), (0.009, -0.031, 0.005), (0.031, -0.028, 0.002),
    ]
    for i, loc in enumerate(carpal_positions, start=1):
        add_ellipsoid(f"carpal_bone_{i}", loc, (0.012, 0.010, 0.008), bone_mat, bone_layer, rot=(0.2 * i, 0.13 * i, 0), segments=24)

    add_ellipsoid("thenar_muscle_mass", (-0.066, 0.027, 0.024), (0.022, 0.047, 0.011), muscle_mat, muscle_layer, rot=(0.2, 0.0, -0.72))
    add_ellipsoid("hypothenar_muscle_mass", (0.051, 0.018, 0.022), (0.018, 0.042, 0.010), muscle_mat, muscle_layer, rot=(-0.08, 0.0, 0.28))
    add_ellipsoid("central_palm_muscle_sheet", (-0.004, 0.039, 0.020), (0.043, 0.062, 0.007), muscle_mat, muscle_layer, rot=(0.04, 0, -0.02))

    for i, (name, x, y_base, segments, radius) in enumerate(finger_specs):
        add_curve_tube(f"{name}_flexor_tendon", [(x * 0.88, -0.055, 0.029), (x * 0.96, 0.06, 0.030), (x, y_base + sum(segments) - 0.005, 0.023)], 0.0026, tendon_mat, muscle_layer)
        add_curve_tube(f"{name}_lumbrical_muscle", [(x * 0.42, 0.014, 0.032), (x * 0.75, 0.067, 0.034), (x, 0.105, 0.031)], 0.0042, muscle_mat, muscle_layer)
        add_curve_tube(f"{name}_muscle_striation_a", [(x * 0.85 - 0.004, 0.018, 0.038), (x - 0.002, 0.096, 0.035)], 0.00055, muscle_dark, muscle_layer)
        add_curve_tube(f"{name}_muscle_striation_b", [(x * 0.78 + 0.004, 0.022, 0.039), (x + 0.003, 0.092, 0.034)], 0.00055, muscle_dark, muscle_layer)

    for offset in [-0.012, 0.0, 0.012]:
        add_curve_tube(f"palmar_aponeurosis_fiber_{offset:+.3f}", [(offset, -0.035, 0.037), (offset * 0.5, 0.04, 0.038), (offset * 1.8, 0.116, 0.034)], 0.0011, tendon_mat, muscle_layer)

    add_curve_tube("median_nerve_main", [(-0.009, -0.086, 0.043), (-0.008, -0.030, 0.047), (-0.011, 0.035, 0.050), (-0.018, 0.072, 0.049)], 0.0031, nerve_mat, nerve_layer)
    median_branches = {
        "median_thumb_branch": [(-0.018, 0.072, 0.049), (-0.065, 0.069, 0.052), (-0.131, 0.108, 0.040)],
        "median_index_branch": [(-0.018, 0.072, 0.049), (-0.037, 0.119, 0.048), (-0.033, 0.200, 0.035)],
        "median_middle_branch": [(-0.018, 0.072, 0.049), (-0.012, 0.129, 0.049), (-0.010, 0.226, 0.036)],
        "median_ring_branch": [(-0.014, 0.066, 0.048), (0.011, 0.124, 0.047), (0.014, 0.207, 0.035)],
    }
    for name, points in median_branches.items():
        add_curve_tube(name, points, 0.0019, nerve_mat, nerve_layer)

    add_curve_tube("ulnar_nerve_main", [(0.041, -0.084, 0.041), (0.047, -0.024, 0.045), (0.044, 0.045, 0.048)], 0.0027, nerve_mat, nerve_layer)
    add_curve_tube("ulnar_little_branch", [(0.044, 0.045, 0.048), (0.039, 0.105, 0.049), (0.038, 0.183, 0.034)], 0.0018, nerve_mat, nerve_layer)
    add_curve_tube("ulnar_ring_branch", [(0.044, 0.045, 0.048), (0.024, 0.103, 0.048), (0.014, 0.189, 0.034)], 0.0016, nerve_mat, nerve_layer)

    for loc in [(-0.011, 0.035, 0.050), (-0.018, 0.072, 0.049), (0.044, 0.045, 0.048)]:
        add_ellipsoid("nerve_branch_node", loc, (0.004, 0.004, 0.004), nerve_mat, nerve_layer, segments=16)

    add_ellipsoid("palm_skin_translucent_shell", (-0.005, 0.030, 0.020), (0.081, 0.096, 0.022), skin_mat, skin_layer, rot=(0.02, 0, 0), segments=48)
    for name, x, y_base, segments, radius in finger_specs:
        add_capsule(f"{name}_skin_envelope", (x, 0.085, 0.023), (x + 0.003, y_base + sum(segments) + 0.008, 0.029), radius * 1.78, skin_mat, skin_layer, vertices=32)
    add_capsule("thumb_skin_envelope", (-0.057, 0.021, 0.024), (-0.157, 0.126, 0.029), 0.0122, skin_mat, skin_layer, vertices=32)
    add_capsule("wrist_skin_envelope", (-0.005, -0.091, 0.020), (-0.005, -0.045, 0.021), 0.040, skin_mat, skin_layer, vertices=40)

    add_text("label_bones", "Bones", (0.080, 0.142, 0.060), 0.012, label_mat, annotation_layer)
    add_text("label_muscles", "Muscles", (-0.091, 0.034, 0.068), 0.012, label_mat, annotation_layer)
    add_text("label_nerves", "Median + ulnar nerves", (0.005, -0.093, 0.071), 0.010, label_mat, annotation_layer)
    add_curve_tube("label_line_bones", [(0.064, 0.132, 0.056), (0.019, 0.115, 0.020)], 0.00045, label_mat, annotation_layer)
    add_curve_tube("label_line_muscles", [(-0.075, 0.033, 0.064), (-0.050, 0.027, 0.030)], 0.00045, label_mat, annotation_layer)
    add_curve_tube("label_line_nerves", [(0.000, -0.079, 0.066), (-0.008, -0.033, 0.048)], 0.00045, label_mat, annotation_layer)

    root.rotation_euler = (0, 0, 0)
    root.location = (0, 0, 0)

    return root


def setup_camera_and_lights():
    bpy.ops.object.light_add(type="AREA", location=(0.0, -0.28, 0.42))
    key = bpy.context.object
    key.name = "large_softbox_key"
    key.data.energy = 380
    key.data.size = 0.48

    bpy.ops.object.light_add(type="POINT", location=(-0.20, 0.17, 0.18))
    fill = bpy.context.object
    fill.name = "warm_fill_light"
    fill.data.energy = 45

    bpy.ops.object.camera_add(location=(0.0, -0.44, 0.26), rotation=(math.radians(61), 0, 0))
    cam = bpy.context.object
    bpy.context.scene.camera = cam
    cam.data.lens = 62
    cam.data.dof.use_dof = True
    cam.data.dof.focus_distance = 0.46
    cam.data.dof.aperture_fstop = 7.5

    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.cycles.samples = 96
    bpy.context.scene.world.color = (0.78, 0.80, 0.82)
    bpy.context.scene.render.resolution_x = 1600
    bpy.context.scene.render.resolution_y = 1200
    bpy.context.scene.view_settings.view_transform = "Filmic"
    bpy.context.scene.view_settings.look = "Medium Low Contrast"
    bpy.context.scene.view_settings.exposure = -0.85
    bpy.context.scene.view_settings.gamma = 1


def convert_non_mesh_geometry_for_fbx():
    for obj in bpy.context.scene.objects:
        obj.select_set(False)

    convert_types = {"CURVE", "FONT"}
    to_convert = [obj for obj in bpy.context.scene.objects if obj.type in convert_types]
    for obj in to_convert:
        obj.select_set(True)

    if to_convert:
        bpy.context.view_layer.objects.active = to_convert[0]
        bpy.ops.object.convert(target="MESH")
        for obj in bpy.context.selected_objects:
            shade_smooth(obj)


def export_assets():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    convert_non_mesh_geometry_for_fbx()
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=False,
        apply_unit_scale=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        path_mode="AUTO",
        add_leaf_bones=False,
    )
    bpy.context.scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)


def main():
    build_hand()
    setup_camera_and_lights()
    export_assets()
    print(f"Wrote {FBX_PATH}")
    print(f"Wrote {BLEND_PATH}")
    print(f"Wrote {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
