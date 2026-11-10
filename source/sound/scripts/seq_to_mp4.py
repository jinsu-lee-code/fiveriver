# Image sequence (C:/sk_assets/capture/run/f_0000.jpg ...) -> H.264 mp4 at 30 fps, via Blender's video sequencer.
import bpy, os, glob, sys
src = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "C:/sk_assets/capture/run"
out = sys.argv[sys.argv.index("--") + 2] if "--" in sys.argv else "C:/sk_assets/capture/run.mp4"
files = sorted(os.path.basename(f) for f in glob.glob(os.path.join(src, "*.jpg")))
sc = bpy.context.scene
sc.render.fps = 30
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 1280, 720, int(os.environ.get("SEQ_PCT", "100"))
se = sc.sequence_editor_create()
coll = se.strips if hasattr(se, "strips") else se.sequences
strip = coll.new_image(name="run", filepath=os.path.join(src, files[0]), channel=1, frame_start=1)
for f in files[1:]:
    strip.elements.append(f)
sc.frame_start, sc.frame_end = 1, len(files)
ims = sc.render.image_settings
if hasattr(ims, "media_type"):
    ims.media_type = 'VIDEO'
ims.file_format = 'FFMPEG'
sc.render.ffmpeg.format = 'MPEG4'
sc.render.ffmpeg.codec = 'H264'
sc.render.ffmpeg.constant_rate_factor = os.environ.get("SEQ_CRF", "HIGH")
sc.render.ffmpeg.audio_codec = 'NONE'
sc.render.filepath = out
sc.render.use_sequencer = True
bpy.ops.render.render(animation=True)
print("MP4_OK", out, len(files))
