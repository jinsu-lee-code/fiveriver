# 적(인민군) 리그 공통 상수 · 경로. 아군(rigify_rok_20261118)의 도구(rok_rig · rok_run · rok_timeline · rok_render)를
# 그대로 쓰려고 모듈 이름은 같게 둔다. 값만 적 모델 · 모신나강에 맞춘다.
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, ".."))
REPO = os.path.normpath(os.path.join(OUT, "..", "..", ".."))
ART = os.path.join(REPO, "five_river", "Assets", "_Project", "Art")

MODEL_FBX = os.path.join(ART, "Models", "Characters", "KPA_Rifleman", "SK_CH_KPA_Rifleman.fbx")   # 게임 프리팹 PF_CH_KPA_Rifleman이 쓰는 모델
RIFLE_FBX = os.path.join(ART, "Models", "Weapons", "Mosin", "SM_WP_Mosin.fbx")
CLIP_DIR = os.path.join(ART, "Animations", "KPA")

WORK = os.path.join(OUT, "work")
EXPORT = os.path.join(OUT, "export")
PREVIEW = os.path.join(OUT, "preview")
RIGDIR = os.path.join(OUT, "rig")
BASE_BLEND = os.path.join(WORK, "kpa_rig_base.blend")
REF_JSON = os.path.join(WORK, "ref_poses.json")      # 적은 쓰지 않음(기존 자세 추출 없음)

RIG_NAME = "KPA_Rig"
RIFLE_MESH = "Mosin"
PREFIX = "A_KPA_"

# 게임 프리팹(PF_CH_KPA_Rifleman)이 모델에 거는 스케일. 리그는 게임 크기로 만든다.
SCALE = 0.84313995
FPS = 30

TRENCH_DEPTH = 0.9
PARAPET = 0.3
PARAPET_TOP = TRENCH_DEPTH + PARAPET
TRENCH_WIDTH = 1.8
FRONT_WALL_Y = -0.42

# 모신나강(1.23 m, 총검 없음) 좌표: x 오른쪽, y 위, z 총구. 원점 = 방아쇠 위 총강 축 (메시 y -0.044)
RIFLE_MUZZLE_Z = 0.871
RIFLE_BUTT = (0.0, -0.11, -0.352)      # 개머리판 판 가운데(어깨에 닿는 점)
RIFLE_BORE_Y = 0.0
BAYONET_LEN = 0.43                     # 미리보기용 총검(게임 에셋에 없음): 총구 오른쪽에 붙는 4각 총검

# 원본(mixamorig) 뼈 → (내보내기 이름, Rigify 메타리그 뼈). 끝 뼈(HeadTop_End · *4 · Toe_End)는 내보내지 않는다.
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
