# 공통 상수 · 경로 (Blender 5.2 백그라운드에서 import)
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, ".."))
REPO = os.path.normpath(os.path.join(OUT, "..", "..", ".."))
ART = os.path.join(REPO, "five_river", "Assets", "_Project", "Art")

MODEL_FBX = os.path.join(ART, "Models", "Characters", "ROK_Soldier", "SK_CH_ROK_Soldier_Person1.fbx")
RIFLE_FBX = os.path.join(ART, "Models", "Weapons", "M1Garand", "SM_WP_M1Garand.fbx")
CLIP_DIR = os.path.join(ART, "Animations", "ROK")

WORK = os.path.join(OUT, "work")
EXPORT = os.path.join(OUT, "export")
PREVIEW = os.path.join(OUT, "preview")
RIGDIR = os.path.join(OUT, "rig")
BASE_BLEND = os.path.join(WORK, "rok_rig_base.blend")
REF_JSON = os.path.join(WORK, "ref_poses.json")

# 게임 프리팹(PF_CH_ROK_Soldier)이 모델에 거는 스케일. 리그는 게임 크기로 만든다.
SCALE = 0.77740496
FPS = 30

# 11-18 걷기: 환경 변수 ROK_MODEL=student 이면 학도병(PF_CH_ROK_StudentReinforcement의 SK_CH_ROK_Soldier.fbx, 프리팹 스케일 0.8217)으로
# 리그를 따로 만든다. 기본값(일반 국군)의 경로 · 값은 바뀌지 않는다.
MODEL_KIND = os.environ.get("ROK_MODEL", "regular")
if MODEL_KIND == "student":
    MODEL_FBX = os.path.join(ART, "Models", "Characters", "ROK_Soldier", "SK_CH_ROK_Soldier.fbx")
    SCALE = 0.82169086
    BASE_BLEND = os.path.join(WORK, "student", "rok_rig_base_student.blend")

# 참호 (FM 5-15 기준으로 다시 판 것): 깊이 0.9, 바닥 폭 1.8, 앞 흉벽 0.3 → 바닥에서 흉벽 위까지 1.2
TRENCH_DEPTH = 0.9
PARAPET = 0.3
PARAPET_TOP = TRENCH_DEPTH + PARAPET
TRENCH_WIDTH = 1.8
FRONT_WALL_Y = -0.42   # 캐릭터 원점에서 앞벽까지 (Blender -Y가 앞)

# 게임 소총(AlliedM1) 좌표: x 오른쪽, y 위, z 총구. 원점에서 총구 +0.724, 개머리판 끝 -0.381
RIFLE_MUZZLE_Z = 0.724
RIFLE_BUTT = (0.0, -0.02, -0.375)      # AllyRiflePose.Butt (어깨에 닿는 점)
RIFLE_BORE_Y = 0.026                   # Socket_Muzzle 높이

# 원본(mixamorig) 뼈 → (내보내기 이름, Rigify 메타리그 뼈)
BONES = [
    ("Hips", "Hips", "spine"),
    ("Spine", "Spine", "spine.001"),
    ("Spine1", "Chest", "spine.002"),
    ("Spine2", "UpperChest", "spine.003"),
    ("Neck", "Neck", "spine.004"),
    ("Head", "Head", "spine.006"),
]
for _s, _S in (("L", "Left"), ("R", "Right")):
    BONES += [
        (_S + "Shoulder", _S + "Shoulder", "shoulder." + _s),
        (_S + "Arm", _S + "UpperArm", "upper_arm." + _s),
        (_S + "ForeArm", _S + "LowerArm", "forearm." + _s),
        (_S + "Hand", _S + "Hand", "hand." + _s),
        (_S + "UpLeg", _S + "UpperLeg", "thigh." + _s),
        (_S + "Leg", _S + "LowerLeg", "shin." + _s),
        (_S + "Foot", _S + "Foot", "foot." + _s),
        (_S + "ToeBase", _S + "Toes", "toe." + _s),
    ]
    for _m, _h, _r in (("Thumb", "Thumb", "thumb"), ("Index", "Index", "f_index"), ("Middle", "Middle", "f_middle"),
                       ("Ring", "Ring", "f_ring"), ("Pinky", "Little", "f_pinky")):
        for _i, _seg in ((1, "Proximal"), (2, "Intermediate"), (3, "Distal")):
            BONES.append(("%sHand%s%d" % (_S, _m, _i), "%s%s%s" % (_S, _h, _seg), "%s.%02d.%s" % (_r, _i, _s)))

SRC2EXP = {"mixamorig:" + a: b for a, b, c in BONES}
EXP2META = {b: c for a, b, c in BONES}
EXP2DEF = {b: "DEF-" + c for a, b, c in BONES}


def gun(ux, uy, uz):
    """게임 소총 좌표(x 오른쪽, y 위, z 총구) → rifle 뼈 로컬(X 오른쪽, Y 총구, Z 위)."""
    from mathutils import Vector
    return Vector((ux, uz, uy))


def b2u(v):
    """Blender 위치(앞 -Y, 위 +Z) → Unity 위치(x 오른쪽, y 위, z 앞)."""
    return (-v[0], v[2], -v[1])
