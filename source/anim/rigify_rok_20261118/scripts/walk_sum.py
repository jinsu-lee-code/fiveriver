# measure.json 요약 출력: python walk_sum.py <measure.json> ...
import json
import sys

for path in sys.argv[1:]:
    d = json.load(open(path, encoding="utf-8"))
    print("==", path)
    for k in ("source", "student", "regular"):
        if k not in d:
            continue
        m = d[k]
        for s in "LR":
            x = m[s]
            print("%-8s %s stance %2s kneeStance %s kneeSwingMax %s ankleMax %s ballMax %s heelMax %s stanceSpd %s slip %s" % (
                k, s, x["stance_frames"], x["knee_stance_min_max"], x["knee_swing_max"], x["ankle_z_max"], x["ball_z_max"],
                x["heel_z_max"], x["stance_speed"], x.get("game_slip_mps")))
        print("%-8s hips %s clipSpd %s rateNoSlip %s" % (k, m["hips_z"], m.get("clip_speed_game_scale"), m.get("rate_for_no_slip")))
