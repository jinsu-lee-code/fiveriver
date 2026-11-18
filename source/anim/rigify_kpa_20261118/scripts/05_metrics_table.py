# work/metrics.json → README용 표(마크다운)를 출력한다.
import io
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
M = json.load(io.open(os.path.join(here, "..", "work", "metrics.json"), encoding="utf-8"))
order = sys.argv[1:] or list(M)


def rng(r):
    return ", ".join("%d~%d" % (a, b) for a, b in r) if r else "없음"


print("| 클립 | 프레임(초) | 반복 첫·끝 차 | 엉덩이 높이 | 머리 꼭대기 최고(흉장 1.2 m 대비) | 눈 최고 | 손-총 최대(왼/오) | 소총 최저점 · 머리와 간격 | 꿇은 무릎 최저 | 발 |")
print("|---|---|---|---|---|---|---|---|---|---|")
for n in order:
    d = M[n]
    feet = d["feet"]
    if "ball_speed_mean" in feet["L"]:
        f = "접지 발볼 속도 왼 %.2f(%.2f~%.2f) · 오 %.2f(%.2f~%.2f) m/s, 접지 중 발볼 뜸 최대 %.1f mm" % (
            feet["L"]["ball_speed_mean"], feet["L"]["ball_speed_min"], feet["L"]["ball_speed_max"],
            feet["R"]["ball_speed_mean"], feet["R"]["ball_speed_min"], feet["R"]["ball_speed_max"],
            1000 * max(feet["L"]["ball_height_in_contact_max"], feet["R"]["ball_height_in_contact_max"]))
    else:
        f = "디딘 발 이동 최대 왼 %.1f · 오 %.1f mm" % (1000 * feet["L"]["planted_drift_max"], 1000 * feet["R"]["planted_drift_max"])
    hl = "-" if d["hand_rifle_L_max"] is None else "%.1f mm" % (1000 * d["hand_rifle_L_max"])
    hr = "-" if d["hand_rifle_R_max"] is None else "%.1f mm" % (1000 * d["hand_rifle_R_max"])
    loop = ("%.2f° / %.1f mm" % (d["first_last_bone_deg"], 1000 * d["first_last_pos_m"])) if d["loop"] else "(전환)"
    print("| %s | %d (%.2f) | %s | %.3f~%.3f | %.3f (%+.3f) | %.3f | %s / %s | %.3f · %.3f | %.3f | %s |" % (
        n, d["frames"], d["seconds"], loop, d["hips_z"][0], d["hips_z"][1], d["head_top_max"], d["head_vs_parapet_max"],
        d["eye_max"], hl, hr, d["rifle_lowest_point"], d["rifle_head_clear_min"], d["knee_lowest"]["R"], f))
print()
print("| 클립 | 왼손이 총에 붙은 프레임 | 오른손이 총에 붙은 프레임 | 시작 자세 차 | 끝 자세 차 |")
print("|---|---|---|---|---|")
for n in order:
    d = M[n]
    c = d["connect"]
    s = "%s: %.1f° (%s)" % (c["start"]["vs"], c["start"]["deg"], c["start"]["bone"]) if "start" in c else "-"
    e = "%s: %.1f° (%s)" % (c["end"]["vs"], c["end"]["deg"], c["end"]["bone"]) if "end" in c else "-"
    print("| %s | %s | %s | %s | %s |" % (n, rng(d["grip_frames"]["L"]), rng(d["grip_frames"]["R"]), s, e))
