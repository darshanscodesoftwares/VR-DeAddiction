"""
Generates the REVERSE-side normal map for a sheet material.

The back of a corrugated sheet is the NEGATIVE of its front: a groove wherever
the front has a ridge. In tangent space that is (x, y) -> (-x, -y), which in
0-1 encoding is R' = 1-R, G' = 1-G. Reusing the front map on the back lights
both faces as if the ridges bulged toward the viewer from either side, which is
impossible and reads as a printed sticker rather than metal.

A box carries ONE material, so anything using this needs a second thin skin on
its reverse driven by the map this produces.

Run:  blender --background --python back_normal.py -- <in.png> <out.png>
"""
import bpy, numpy as np, sys

src, dst = sys.argv[-2], sys.argv[-1]

img = bpy.data.images.load(src)
img.colorspace_settings.name = 'Non-Color'      # a normal map is data, not colour
w, h = img.size
a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)

a[..., 0] = 1.0 - a[..., 0]
a[..., 1] = 1.0 - a[..., 1]

out = bpy.data.images.new("back", width=w, height=h, alpha=True, float_buffer=False)
out.colorspace_settings.name = 'Non-Color'
out.pixels = a.ravel().tolist()
out.file_format = 'PNG'
out.filepath_raw = dst
out.save()
print(f"[back_normal] {w}x{h}  {src.split('/')[-1]} -> {dst.split('/')[-1]}")
