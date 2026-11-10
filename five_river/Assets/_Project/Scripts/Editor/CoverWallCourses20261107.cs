using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Closes the gap between the courses of the hill cover walls (CoverWalls20261107). The scanned round-bag row is
    // a crescent whose ends curl up, so seating the upper course on the lower one's bounding box left it hovering
    // over the middle (a light band showed between the courses; on the CP12 slope they split apart). Each course
    // now follows the lower one's own tilt and is pressed 17 cm into it, and a third course brings the wall back to
    // about 0.95 m, matching the 1.0 m cover collider.
    public static class CoverWallCourses20261107
    {
        const float RowHeight = 0.44f, Press = 0.17f;

        [MenuItem("FiveRivers/Map/Fix Cover Wall Courses 20261107")]
        public static void Apply()
        {
            var root = GameObject.Find("/[Map]/CoverWalls20261107");
            if (root == null) throw new System.InvalidOperationException("Run Low Sandbag Cover Walls 20261107 first.");
            var round = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Megascans20261107/SandbagRound/PF_MS_SandbagRound.prefab");
            int fixedWalls = 0;
            foreach (Transform wall in root.transform)
            {
                var lower = wall.GetChild(0);
                if (wall.Find("Course3") != null) continue; // already fixed
                var rng = new System.Random(wall.name.GetHashCode() ^ 77);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                Undo.RecordObject(wall.GetChild(1), "Seat course");
                Seat(wall.GetChild(1), lower, new Vector3(.95f, 1.03f, .95f), R(-2.5f, 2.5f));
                var third = (GameObject)PrefabUtility.InstantiatePrefab(round, wall);
                Undo.RegisterCreatedObjectUndo(third, "Third course");
                third.name = "Course3";
                third.transform.SetSiblingIndex(2);
                Seat(third.transform, wall.GetChild(1), new Vector3(.94f, 1.0f, .93f), R(-2.5f, 2.5f));
                fixedWalls++;
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[Cover wall courses] Seated courses on " + fixedWalls + " walls.");
        }

        // upper sits on lower: same tilt, a touch smaller, pressed into it along the lower course's own up axis
        static void Seat(Transform upper, Transform lower, Vector3 shrink, float yaw)
        {
            upper.rotation = lower.rotation * Quaternion.Euler(0f, yaw, 0f);
            upper.localScale = Vector3.Scale(lower.localScale, shrink);
            upper.position = lower.position + lower.up * (RowHeight * lower.lossyScale.y - Press);
        }
    }
}
