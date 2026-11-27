using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 바주카 허리 자세에서 손이 보이게 (2026-11-27, 사용자 지시 "바주카 손도 고쳐봐"). 허리 자세 위치 · 각도 두 값만 바꾼다.
    // 전(0.20, −0.16, 0.24 · 8, −4, 10)에는 방아쇠를 쥔 오른손이 화면 아래 밖에 있었고(측정: 손 중심이 화면 밖), 보이는 손은
    // 앞 손잡이를 받친 왼손뿐이었다. 관을 조금 올리고 오른쪽 · 앞으로 내고 안쪽으로 돌려, 앞 손잡이를 감싼 왼손과 뒤 권총
    // 손잡이를 쥔 오른손이 함께 화면 오른쪽 아래에 들어오게 한다(관은 화면의 약 30 %). 조준 · 장전 · 달리기 값은 그대로.
    // 여러 번 실행해도 같다. 되돌리기: Revert 메뉴(전 값).
    public static class BazookaHipHands20261127
    {
        public static readonly Vector3 HipPosition = new Vector3(0.24f, -0.14f, 0.34f), HipEuler = new Vector3(7f, -10f, 8f);
        public static readonly Vector3 BeforePosition = new Vector3(0.20f, -0.16f, 0.24f), BeforeEuler = new Vector3(8f, -4f, 10f);

        [MenuItem("FiveRivers/Setup/Bazooka Hip Hands 20261127")]
        public static string Apply() => Set(HipPosition, HipEuler);

        [MenuItem("FiveRivers/Setup/Bazooka Hip Hands 20261127 Revert")]
        public static string Revert() => Set(BeforePosition, BeforeEuler);

        static string Set(Vector3 position, Vector3 euler)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "edit mode only";
            var launcher = Object.FindFirstObjectByType<BazookaPresentation>(FindObjectsInactive.Include);
            if (launcher == null) return "no BazookaPresentation in the open scene";
            var so = new SerializedObject(launcher);
            var p = so.FindProperty("hipViewPosition"); var e = so.FindProperty("hipViewEuler");
            string log = $"hip {p.vector3Value} {e.vector3Value} -> {position} {euler}";
            if (p.vector3Value == position && e.vector3Value == euler) return log + " (nothing to change)";
            p.vector3Value = position; e.vector3Value = euler;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(launcher.gameObject.scene);
            EditorSceneManager.SaveScene(launcher.gameObject.scene);
            return log;
        }
    }
}
