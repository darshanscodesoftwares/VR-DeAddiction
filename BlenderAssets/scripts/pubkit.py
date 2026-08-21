"""Shared helpers for the pub asset scripts.

Everything is built from explicit vertex/face data rather than bpy.ops, because
operators depend on selection state and context, which is fragile in background
mode. Building meshes directly is deterministic and reproducible.

Conventions (see BlenderAssets/README.md):
  - Built in **Blender coordinates: Z up**. The FBX exporter converts to
    Unity's Y-up. Building Y-up here would double-rotate every asset.
  - metres
  - origin at the base of the object, centred in X/Y
  - no materials: Unity assigns the existing flat Standard palette
"""

import math
import os

import bpy


# --------------------------------------------------------------------- scene


def reset_scene():
    """Empty the file. Background Blender starts with a default cube."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def new_mesh(name, verts, faces):
    """Create a mesh object from raw geometry and link it to the scene."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.validate(verbose=False)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


# ------------------------------------------------------------------ builders


def _section_scale(angle, section, flutes, flute_depth):
    """Radius multiplier at a given angle, for non-circular cross-sections.

    Real premium bottles are rarely plain cylinders -- fluting and square
    sections are most of what makes one brand distinguishable from another at a
    glance, which matters more here than any label text.
    """
    if section == "round":
        return 1.0

    if section == "fluted":
        # Cosine ripple: shallow vertical ribs running the height of the bottle.
        return 1.0 - flute_depth * (0.5 - 0.5 * math.cos(flutes * angle))

    if section == "square":
        # Superellipse: square with softened corners. Exponent 4 is close to a
        # rounded rectangle; higher would be a hard box.
        c, s = abs(math.cos(angle)), abs(math.sin(angle))
        return (c ** 4 + s ** 4) ** -0.25

    raise ValueError(f"unknown section: {section}")


def lathe(name, profile, segments=16, close_bottom=True, close_top=True,
          section="round", flutes=12, flute_depth=0.10):
    """Revolve a 2D profile around the vertical (Z) axis.

    profile: list of (height, radius) from bottom to top.
    section: "round", "fluted" (vertical ribs) or "square" (superellipse).
    """
    verts = []
    faces = []

    rings = []
    for height, radius in profile:
        ring = []
        for s in range(segments):
            a = (s / segments) * math.tau
            r = radius * _section_scale(a, section, flutes, flute_depth)
            ring.append(len(verts))
            verts.append((math.cos(a) * r, math.sin(a) * r, height))
        rings.append(ring)

    for r in range(len(rings) - 1):
        lower, upper = rings[r], rings[r + 1]
        for s in range(segments):
            n = (s + 1) % segments
            faces.append([lower[s], lower[n], upper[n], upper[s]])

    if close_bottom:
        faces.append(list(reversed(rings[0])))
    if close_top:
        faces.append(list(rings[-1]))

    return new_mesh(name, verts, faces)


def lathe_ring_faces(profile_len, segments, ring_from, ring_to):
    """Face indices for the side quads between two rings of a lathe().

    lathe() emits side quads ring by ring, so the faces between profile points
    r and r+1 occupy a contiguous block. That lets a label band be selected by
    height without touching the mesh.
    """
    ring_from = max(0, ring_from)
    ring_to = min(profile_len - 1, ring_to)
    return list(range(ring_from * segments, ring_to * segments))


def set_material_slots(obj, slot_names):
    """Create named material slots on an object.

    Blender materials here carry no shading -- they exist purely as *names*, so
    Unity can map each submesh onto one of the project's existing flat
    materials. Keeping the look in Unity is what stops the 41-material palette
    fragmenting (see BlenderAssets/README.md).
    """
    obj.data.materials.clear()
    for name in slot_names:
        mat = bpy.data.materials.get(name)
        if mat is None:
            mat = bpy.data.materials.new(name)
        obj.data.materials.append(mat)


def assign_faces_to_slot(obj, face_indices, slot_index):
    """Put specific faces in a material slot. Everything else keeps slot 0."""
    for fi in face_indices:
        if 0 <= fi < len(obj.data.polygons):
            obj.data.polygons[fi].material_index = slot_index
    obj.data.update()


def box(name, size, centre=(0.0, 0.0, 0.0)):
    """Axis-aligned box in Blender coordinates: (x, y, z) with **z up**.

    Blender is Z-up; the FBX exporter converts to Unity's Y-up at export time.
    Building in Y-up here would double-rotate and lay every asset on its side.

    The box is centred on `centre`, so pass centre=(0, 0, h/2) to sit it on the
    ground plane.
    """
    sx, sy, sz = (c * 0.5 for c in size)
    cx, cy, cz = centre

    verts = [
        (cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz),
        (cx + sx, cy - sy, cz + sz), (cx - sx, cy - sy, cz + sz),
        (cx - sx, cy + sy, cz - sz), (cx + sx, cy + sy, cz - sz),
        (cx + sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz + sz),
    ]
    faces = [
        [0, 3, 2, 1],  # bottom
        [4, 5, 6, 7],  # top
        [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
    ]
    return new_mesh(name, verts, faces)


def taper_box(name, bottom_size, top_size, height, centre=(0.0, 0.0, 0.0)):
    """Box with different cross-sections at bottom and top.

    Used for moulded-plastic legs and concrete pedestals, which are never
    perfectly prismatic. bottom_size/top_size are the (x, y) footprint; the
    taper runs upward along **z**, and `centre` is the base centre.
    """
    bx, by = (c * 0.5 for c in bottom_size)
    tx, ty = (c * 0.5 for c in top_size)
    cx, cy, cz = centre

    verts = [
        (cx - bx, cy - by, cz), (cx + bx, cy - by, cz),
        (cx + bx, cy + by, cz), (cx - bx, cy + by, cz),
        (cx - tx, cy - ty, cz + height), (cx + tx, cy - ty, cz + height),
        (cx + tx, cy + ty, cz + height), (cx - tx, cy + ty, cz + height),
    ]
    faces = [
        [0, 3, 2, 1], [4, 5, 6, 7],
        [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
    ]
    return new_mesh(name, verts, faces)


def curved_panel(name, width, height, thickness, curve_depth,
                 segments=6, centre=(0.0, 0.0, 0.0), lean_degrees=0.0):
    """A panel bowed along its width — a chair back, essentially.

    Width runs along x, height upward along **z**, thickness along y.
    curve_depth is how far the centre bows backward (+y) relative to the edges.
    lean_degrees reclines the panel backwards about its base.
    """
    cx, cy, cz = centre
    half_t = thickness * 0.5
    lean = math.radians(lean_degrees)

    verts = []
    front_rows, back_rows = [], []

    for row in range(2):
        z_local = row * height
        front_idx, back_idx = [], []

        for s in range(segments + 1):
            u = s / segments
            x = (u - 0.5) * width
            # Parabolic bow, deepest at the centre.
            bow = curve_depth * (1.0 - (2.0 * u - 1.0) ** 2)

            for y_off, store in ((-half_t + bow, front_idx), (half_t + bow, back_idx)):
                # Recline about the x axis at the panel base.
                y = y_off * math.cos(lean) - z_local * math.sin(lean)
                z = y_off * math.sin(lean) + z_local * math.cos(lean)
                store.append(len(verts))
                verts.append((cx + x, cy + y, cz + z))

        front_rows.append(front_idx)
        back_rows.append(back_idx)

    faces = []
    for s in range(segments):
        f0, f1 = front_rows[0][s], front_rows[0][s + 1]
        f2, f3 = front_rows[1][s + 1], front_rows[1][s]
        faces.append([f0, f1, f2, f3])

        b0, b1 = back_rows[0][s], back_rows[0][s + 1]
        b2, b3 = back_rows[1][s + 1], back_rows[1][s]
        faces.append([b3, b2, b1, b0])

    # Cap the four edges.
    faces.append([front_rows[0][0], back_rows[0][0], back_rows[1][0], front_rows[1][0]])
    faces.append([front_rows[1][-1], back_rows[1][-1], back_rows[0][-1], front_rows[0][-1]])
    for s in range(segments):
        faces.append([front_rows[1][s], front_rows[1][s + 1],
                      back_rows[1][s + 1], back_rows[1][s]])
        faces.append([back_rows[0][s], back_rows[0][s + 1],
                      front_rows[0][s + 1], front_rows[0][s]])

    return new_mesh(name, verts, faces)


def lean_about(objects, degrees, pivot_y=0.0, pivot_z=0.0):
    """Rotate objects together about the x axis through a shared pivot.

    Leaning each panel about its own base makes a chair back splay apart,
    because parts at different heights rotate about different points. Rotating
    the assembled back about one pivot keeps everything parallel.
    """
    a = math.radians(degrees)
    ca, sa = math.cos(a), math.sin(a)

    for obj in objects:
        mesh = obj.data
        for v in mesh.vertices:
            y = v.co.y - pivot_y
            z = v.co.z - pivot_z
            v.co.y = pivot_y + y * ca - z * sa
            v.co.z = pivot_z + y * sa + z * ca
        mesh.update()


def grime(obj, floor_dirt=0.55, crevice=0.35, mottle=0.14, seed=7):
    """Bake wear into vertex colours, read by the VertexGrime shader.

    Three effects, each matching something visible in the reference photos:

      floor_dirt  darkens geometry near the ground, where splashes and
                  scuffing collect -- chair legs and table feet
      crevice     darkens vertices with many neighbours, approximating the
                  ambient occlusion that gathers in joints and under rims
      mottle      low-frequency noise so no two faces are identically clean,
                  which is what stops flat colour looking like plastic CAD

    Vertex colours multiply the base colour, so 1.0 = untouched.
    """
    import random

    mesh = obj.data
    if not mesh.vertex_colors:
        mesh.vertex_colors.new(name="Col")
    layer = mesh.vertex_colors.active

    zs = [v.co.z for v in mesh.vertices]
    z_min, z_max = min(zs), max(zs)
    z_span = max(z_max - z_min, 1e-4)

    # Valence stands in for occlusion: vertices shared by many faces sit in
    # corners and joints.
    valence = [0] * len(mesh.vertices)
    for poly in mesh.polygons:
        for vi in poly.vertices:
            valence[vi] += 1
    max_val = max(valence) if valence else 1

    rng = random.Random(seed)
    jitter = [rng.uniform(-1.0, 1.0) for _ in range(len(mesh.vertices))]

    for poly in mesh.polygons:
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            v = mesh.vertices[vi]

            # Ground-up dirt gradient, strongest in the lowest 25% of the object.
            h = (v.co.z - z_min) / z_span
            dirt = floor_dirt * max(0.0, 1.0 - h / 0.25)

            occ = crevice * (valence[vi] / max_val) ** 2
            noise = mottle * jitter[vi]

            shade = 1.0 - dirt - occ + noise
            shade = min(1.0, max(0.25, shade))

            # Grime is slightly warm/brown, not neutral grey.
            layer.data[li].color = (shade, shade * 0.985, shade * 0.955, 1.0)

    mesh.update()


def export_fbx_multi(objects, out_dir, name):
    """Export several objects as one FBX, preserving their parent hierarchy.

    Hands must stay segmented: a single rigid mesh cannot curl its fingers, and
    both the controller hand and the tracked hand need per-joint articulation.
    """
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"{name}.fbx")

    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH', 'EMPTY'},
        use_mesh_modifiers=True,
        mesh_smooth_type='EDGE',
        add_leaf_bones=False,
        path_mode='COPY',
    )

    tris = sum(tri_count(o) for o in objects if o.type == 'MESH')
    print(f"[pubkit] {name}: {len(objects)} parts, {tris} tris -> {path}")
    return path


def skin_tone(obj, seed=61, tip_warmth=0.16, crease=0.26, mottle=0.05):
    """Bake skin variation into vertex colours.

    Flat-coloured skin is the main thing that makes a hand read as plastic.
    Three effects, all things real skin does:

      tip_warmth  fingertips and knuckles run redder, because they are
                  thin-skinned and better perfused
      crease      darkens vertices with many neighbours, standing in for the
                  shadowing in knuckle creases and between the fingers
      mottle      fine noise, so no two patches are identically toned

    Multiplies the base colour, so 1.0 leaves it untouched. Read by the
    VertexGrime shader -- Unity's Standard shader ignores vertex colours.
    """
    import random

    mesh = obj.data
    if not mesh.vertex_colors:
        mesh.vertex_colors.new(name="Col")
    layer = mesh.vertex_colors.active

    zs = [v.co.z for v in mesh.vertices]
    z_min, z_max = min(zs), max(zs)
    z_span = max(z_max - z_min, 1e-4)

    valence = [0] * len(mesh.vertices)
    for poly in mesh.polygons:
        for vi in poly.vertices:
            valence[vi] += 1
    max_val = max(valence) if valence else 1

    rng = random.Random(seed)
    jitter = [rng.uniform(-1.0, 1.0) for _ in range(len(mesh.vertices))]

    for poly in mesh.polygons:
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            v = mesh.vertices[vi]

            # Toward the far end of the segment = toward the fingertip.
            h = (v.co.z - z_min) / z_span
            warm = tip_warmth * h

            occ = crease * (valence[vi] / max_val) ** 2
            noise = mottle * jitter[vi]

            base = min(1.0, max(0.35, 1.0 - occ + noise))

            # Redder toward the tips: red holds, green and blue fall away.
            layer.data[li].color = (
                min(1.0, base + warm * 0.55),
                base * (1.0 - warm * 0.45),
                base * (1.0 - warm * 0.70),
                1.0,
            )

    mesh.update()


def join(name, objects):
    """Merge objects into one mesh. Fewer objects means fewer draw calls."""
    if not objects:
        raise ValueError("join() needs at least one object")

    for o in bpy.context.selected_objects:
        o.select_set(False)

    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    if len(objects) > 1:
        bpy.ops.object.join()

    merged = bpy.context.view_layer.objects.active
    merged.name = name
    merged.data.name = name
    return merged


def shade_smooth(obj, angle_degrees=35.0):
    """Auto-smooth: curved surfaces read smooth, hard edges stay crisp."""
    mesh = obj.data
    for poly in mesh.polygons:
        poly.use_smooth = True

    # Blender 4.1+ replaced mesh.auto_smooth_angle with a modifier, so set
    # sharp edges directly instead -- works across versions.
    threshold = math.radians(angle_degrees)
    for edge in mesh.edges:
        edge.use_edge_sharp = False

    mesh.update()
    _add_sharp_by_angle(obj, threshold)


def _add_sharp_by_angle(obj, threshold):
    import bmesh

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for edge in bm.edges:
        if len(edge.link_faces) == 2:
            if edge.calc_face_angle(0.0) > threshold:
                edge.smooth = False
    bm.to_mesh(obj.data)
    bm.free()


# -------------------------------------------------------------------- export


def tri_count(obj):
    """Triangles the mesh will occupy once Unity triangulates it."""
    return sum(max(0, len(p.vertices) - 2) for p in obj.data.polygons)


def export_fbx(obj, out_dir, name=None):
    """Export a single object as Unity-ready FBX.

    Blender is Z-up, Unity is Y-up. The FBX writer records the axis metadata
    and Unity's importer applies it -- but ONLY when bake_space_transform is
    False. With it True the conversion is baked and then skipped, and every
    asset arrives with its height along Z, lying flat in the scene.
    """
    name = name or obj.name
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"{name}.fbx")

    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='EDGE',
        add_leaf_bones=False,
        path_mode='COPY',
    )

    print(f"[pubkit] {name}: {tri_count(obj)} tris -> {path}")
    return path
