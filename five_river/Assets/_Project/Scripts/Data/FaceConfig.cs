using System;
using FiveRivers.Narrative;
using UnityEngine;

namespace FiveRivers.Data
{
    // 대사 립싱크 · 눈 깜빡임 수치 (부족한 점 1번, 2026-11-26). 블렌드 셰이프는 메뉴 FiveRivers/Setup/Face Shapes 가 얹는다.
    [CreateAssetMenu(fileName = "FaceConfig", menuName = "FiveRivers/Face Config")]
    public sealed class FaceConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/FaceConfig.asset";

        [Serializable]
        public struct Speaker
        {
            [Tooltip("자막 화자 이름 (GameText Line.speaker)")] public string speaker;
            [Tooltip("그 사람 오브젝트 이름 (씬 · 생성 이름)")] public string actor;
        }

        [Tooltip("자막 화자 → 입을 움직일 인물")]
        public Speaker[] speakers =
        {
            new Speaker { speaker = "박용팔", actor = "ParkYongpal" },
            new Speaker { speaker = "분대장", actor = "Ally_2" },
            new Speaker { speaker = "최만수", actor = "Ally_1" },
            new Speaker { speaker = "탄약수", actor = "Ally_3" },
            new Speaker { speaker = "윤태석", actor = "학도병 윤태석" },
        };

        [Tooltip("음성이 없을 때 자막 표시 시간 중 말하는 데 쓰는 비율 (끝에 읽을 틈을 남김)")]
        [Range(0.3f, 1f)] public float speechFill = 0.85f;

        public LipSyncSettings lipSync = new LipSyncSettings();

        [Header("모델별 입 모양 세기 상한 (0~1, 모든 입 모양에 곱함)")]
        [Range(0f, 1f)] public float parkMouth = 1f;
        [Range(0f, 1f)] public float regularMouth = 1f;
        [Tooltip("학도병 메시는 아랫입술 가운데 원본 UV 찢김이 입을 벌리면 커져 보여 낮게 묶는다 (renders/Soldier_combo_*.png)")]
        [Range(0f, 1f)] public float studentMouth = 0.45f;

        [Header("눈 깜빡임 (박용팔 · 일반 국군만, 학도병 메시는 감은 눈 모양 없음)")]
        public Vector2 blinkInterval = new Vector2(2f, 6f);
        public Vector2 blinkLength = new Vector2(0.1f, 0.15f);

        public float MouthLimit(FaceModel model)
        {
            switch (model)
            {
                case FaceModel.Park: return parkMouth;
                case FaceModel.Student: return studentMouth;
                default: return regularMouth;
            }
        }

        public string ActorFor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker) || speakers == null) return null;
            foreach (var s in speakers) if (s.speaker == speaker) return s.actor;
            return null;
        }
    }

    // 값은 끝에만 추가한다 (프리팹에 정수로 저장)
    public enum FaceModel { Park, Regular, Student }
}
