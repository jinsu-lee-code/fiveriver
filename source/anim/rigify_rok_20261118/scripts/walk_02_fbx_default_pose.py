# 걷기 진단 2: FBX 파일에 저장된 뼈 노드의 기본값(Lcl Rotation)과 바인드 포즈(Pose 노드)를 직접 읽는다.
# 메시 없는 동작 FBX는 Unity가 아바타(Humanoid 기준 자세)를 이 "노드 기본값"으로 만든다. 그것이 T자가 아니면
# 리타깃에서 그 차이만큼 모든 프레임이 틀어진다.
# 실행: blender -b --factory-startup --python scripts/walk_02_fbx_default_pose.py -- <a.fbx> [<b.fbx> ...]
import math
import sys

import addon_utils

addon_utils.enable("io_scene_fbx")
from io_scene_fbx import parse_fbx  # noqa: E402

files = sys.argv[sys.argv.index("--") + 1:]
WANT = ("Hips", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        "Spine", "LeftArm", "LeftForeArm")


def props(elem):
    out = {}
    for e in elem.elems:
        if e.id == b"Properties70":
            for p in e.elems:
                name = p.props[0].decode("utf-8", "replace")
                out[name] = p.props[4:]
    return out


for path in files:
    root, version = parse_fbx.parse(path)
    print("FILE", path, "version", version)
    objects = next(e for e in root.elems if e.id == b"Objects")
    models = {}
    poses = []
    for e in objects.elems:
        if e.id == b"Model":
            name = e.props[1].split(b"\x00")[0].decode("utf-8", "replace")
            models[e.props[0]] = name
            short = name.split(":")[-1]
            if short in WANT:
                p = props(e)
                rot = p.get("Lcl Rotation", (0, 0, 0))
                pre = p.get("PreRotation", (0, 0, 0))
                tr = p.get("Lcl Translation", (0, 0, 0))
                print("  NODE %-14s Lcl Rotation (%.1f, %.1f, %.1f)  PreRotation (%.1f, %.1f, %.1f)  Lcl Translation (%.2f, %.2f, %.2f)" % (
                    (short,) + tuple(float(v) for v in rot) + tuple(float(v) for v in pre) + tuple(float(v) for v in tr)))
        elif e.id == b"Pose":
            poses.append(e)
    print("  POSE NODES (bind pose):", len(poses), [p.props[1].split(b"\x00")[0] for p in poses])
