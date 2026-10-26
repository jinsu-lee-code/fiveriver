using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // TextMeshPro 필수 리소스(설정 · 셰이더)를 창 없이 가져온다. 이미 있으면 아무것도 하지 않는다.
    static class TmpEssentials
    {
        const string Package = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        public const string Folder = "Assets/TextMesh Pro";

        public static bool Present => AssetDatabase.IsValidFolder(Folder);

        [MenuItem("FiveRivers/Setup/Import TMP Essentials")]
        public static void Ensure()
        {
            if (Present) return;
            AssetDatabase.ImportPackage(Package, false);
            Debug.Log("[Setup] TMP 필수 리소스 가져오기 요청");
        }
    }
}
