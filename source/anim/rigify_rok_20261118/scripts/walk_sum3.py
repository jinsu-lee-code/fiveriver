# walk_10_make.py 의 SUMMARY 줄 요약 (표준 입력) — 11-18 이어받은 판: 단계별 무릎 · 발바닥 각 · 메시 미끄러짐
import json
import sys

for line in sys.stdin:
    if line.startswith("FINGER_SIGN") or line.startswith("FOOT_VERTS"):
        print(line.rstrip()[:200])
        continue
    if not line.startswith("SUMMARY"):
        if "Error" in line or "Traceback" in line or line.startswith("  File"):
            print(line.rstrip())
        continue
    d = json.loads(line.split(" ", 1)[1])
    print("frames %s (%.2fs) speed %.2f | hips %s | reach drop %.3f | mesh low %s | loop pos %s m vel %s | hands z %s" % (
        d["frames"], d["seconds"], d["speed_mps"], d["hips_z"], d["reach_drop_max"], d["mesh_low"], d["loop_pos_m"],
        d.get("loop_velocity_gap"), d["hand_z"]))
    for s in ("L", "R"):
        x = d[s]
        print("  %s knee contact %5.1f load %5.1f mid %5.1f toeoff %5.1f swing %5.1f | sole contact %5.1f toeoff %5.1f swingUp %5.1f swingDown %5.1f"
              " | tip midswing %.3f mesh midswing %s | slip contact %s mesh %s (planted %s)" % (
                  s, x["knee_contact"], x["knee_load_peak"], x["knee_mid_min"], x["knee_toeoff"], x["knee_swing_max"],
                  x["sole_contact"], x["sole_toeoff"], x["sole_swing_up_max"], x["sole_swing_down_min"],
                  x["tip_z_midswing_min"], x.get("mesh_midswing_low"), x.get("contact_slip_mps"), x.get("mesh_slip_mps"),
                  x.get("mesh_planted_frames")))
        print("     flat slip %s | stance foot low max %s" % (x.get("mesh_slip_flat_mps"), x.get("mesh_stance_low_max")))
    if "loop_second_diff" in d:
        print("  loop 2nd diff", d["loop_second_diff"])
