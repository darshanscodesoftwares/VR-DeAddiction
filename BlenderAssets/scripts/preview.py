"""Render a preview sheet of every asset, so shapes can be checked visually.

    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --python BlenderAssets/scripts/preview.py

Writes PNGs to BlenderAssets/reference/previews/. Uses the Workbench engine:
no lighting setup, fast, and it shows silhouette and form clearly, which is
what matters when checking a model.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import build_all  # noqa: E402
import pubkit  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "reference", "previews")


def frame_object(obj, name):
    """Three-quarter view, sized to the object's bounding box."""
    bb = [obj.matrix_world @ __import__("mathutils").Vector(c)
          for c in obj.bound_box]
    xs = [v.x for v in bb]
    ys = [v.y for v in bb]
    zs = [v.z for v in bb]

    centre = ((min(xs) + max(xs)) / 2,
              (min(ys) + max(ys)) / 2,
              (min(zs) + max(zs)) / 2)
    size = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    dist = max(size * 2.3, 0.6)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam)

    # Near eye level: a high angle hides the form of furniture, which is
    # exactly what you need to judge.
    ang = math.radians(14)
    cam.location = (centre[0] + dist * math.cos(ang) * 0.75,
                    centre[1] + size * 0.55,
                    centre[2] + dist * math.sin(ang) * 1.15)

    direction = (centre[0] - cam.location[0],
                 centre[1] - cam.location[1],
                 centre[2] - cam.location[2])
    import mathutils
    rot = mathutils.Vector(direction).to_track_quat('-Z', 'Y').to_euler()
    cam.rotation_euler = rot

    scene = bpy.context.scene
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 480
    scene.render.resolution_y = 600
    scene.render.film_transparent = False
    scene.render.filepath = os.path.abspath(os.path.join(OUT, f"{name}.png"))

    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'SINGLE'
    shading.single_color = (0.62, 0.62, 0.64)
    shading.show_cavity = True

    bpy.ops.render.render(write_still=True)
    print(f"[preview] {name} -> {scene.render.filepath}")


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, builder in build_all.BUILDERS.items():
        obj = builder()
        frame_object(obj, name)
    print(f"[preview] done: {len(build_all.BUILDERS)} renders")


if __name__ == "__main__":
    main()
