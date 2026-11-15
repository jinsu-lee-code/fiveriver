# jpg sequence -> H.264 mp4 via Blender's sequencer.  blender -b --python to_mp4.py -- <folder> <out.mp4> <fps> <w> <h>
import bpy, glob, os, sys
a = sys.argv[sys.argv.index("--") + 1:]
src, out, fps, w, h = a[0], a[1], int(a[2]), int(a[3]), int(a[4])
files = sorted(os.path.basename(f) for f in glob.glob(os.path.join(src, "*.jpg")))
sc = bpy.context.scene
sc.render.fps = fps
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = w, h, 100
se = sc.sequence_editor_create()
coll = se.strips if hasattr(se, "strips") else se.sequences
strip = coll.new_image(name="seq", filepath=os.path.join(src, files[0]), channel=1, frame_start=1)
for f in files[1:]:
    strip.elements.append(f)
sc.frame_start, sc.frame_end = 1, len(files)
ims = sc.render.image_settings
if hasattr(ims, "media_type"):
    ims.media_type = 'VIDEO'
ims.file_format = 'FFMPEG'
sc.render.ffmpeg.format = 'MPEG4'
sc.render.ffmpeg.codec = 'H264'
sc.render.ffmpeg.constant_rate_factor = 'HIGH'
sc.render.ffmpeg.audio_codec = 'NONE'
sc.render.filepath = out
sc.render.use_sequencer = True
bpy.ops.render.render(animation=True)
print("MP4_OK", out, len(files))
