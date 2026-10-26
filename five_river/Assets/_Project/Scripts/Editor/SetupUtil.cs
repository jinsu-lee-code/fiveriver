using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Setup 메뉴 공용 도구. 몇 번 실행해도 같은 결과가 나오도록 "있으면 쓰고 없으면 만든다".
    static class SetupUtil
    {
        public static GameObject Root(string name)
        {
            var go = GameObject.Find("/" + name);
            if (go == null) go = new GameObject(name);
            return go;
        }

        public static GameObject Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[Setup] {target.GetType().Name}.{field} 필드를 찾지 못함");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError("[Setup] 에셋 없음: " + path);
            return asset;
        }
    }
}
