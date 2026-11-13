using FiveRivers.Combat;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    [CustomEditor(typeof(NpcRifleGrip))]
    public sealed class NpcRifleGripEditor : Editor
    {
        static readonly string[] HandNames = { "오른손 · 방아쇠", "왼손 · 받침" };
        static readonly Color[] HandColors = { new Color(1f, 0.65f, 0.25f), new Color(0.3f, 0.8f, 1f) };
        int selectedHand;

        [MenuItem("CONTEXT/AllyCombatant/소총 파지 조정 추가")]
        static void AddAllyGrip(MenuCommand command)
        {
            var ally = (AllyCombatant)command.context;
            AddGrip((Transform)new SerializedObject(ally).FindProperty("weapon").objectReferenceValue, ally);
        }

        [MenuItem("CONTEXT/EnemyAgent/소총 파지 조정 추가")]
        static void AddEnemyGrip(MenuCommand command)
        {
            var enemy = (EnemyAgent)command.context;
            AddGrip(enemy.transform.Find("Socket_Rifle/Rifle"), enemy);
        }

        [MenuItem("CONTEXT/AllyCombatant/소총 파지 조정 추가", true)]
        [MenuItem("CONTEXT/EnemyAgent/소총 파지 조정 추가", true)]
        static bool CanAddGrip(MenuCommand command) => !EditorApplication.isPlayingOrWillChangePlaymode;

        static void AddGrip(Transform rifle, Component actor)
        {
            if (rifle == null)
            {
                Debug.LogWarning("NPC에 연결된 소총을 찾을 수 없습니다.", actor);
                return;
            }
            if (!rifle.TryGetComponent<NpcRifleGrip>(out _))
                Undo.AddComponent<NpcRifleGrip>(rifle.gameObject);
            Selection.activeGameObject = rifle.gameObject;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUI.BeginChangeCheck();
            selectedHand = GUILayout.Toolbar(selectedHand, HandNames);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            EditorGUILayout.HelpBox("Scene에서 W로 이동, E로 회전합니다. 각 손은 소총의 로컬 축을 기준으로 보정됩니다.",
                MessageType.Info);
            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play에서 맞춘 값은 이 컴포넌트를 복사하고, Play를 종료한 뒤 편집 대상에 값을 붙여넣어 저장하세요.",
                    MessageType.Info);
        }

        void OnSceneGUI()
        {
            var grip = (NpcRifleGrip)target;
            var rifle = grip.transform;
            serializedObject.Update();
            for (int i = 0; i < HandNames.Length; i++)
            {
                bool right = i == 0;
                var position = serializedObject.FindProperty(right ? "rightPositionOffset" : "leftPositionOffset");
                var rotation = serializedObject.FindProperty(right ? "rightRotationOffset" : "leftRotationOffset");
                var reference = ReferencePose(grip, right);
                var worldPosition = rifle.TransformPoint(reference.position + position.vector3Value);
                var worldRotation = rifle.rotation * Quaternion.Euler(rotation.vector3Value) * reference.rotation;
                float size = HandleUtility.GetHandleSize(worldPosition) * 0.08f;
                using (new Handles.DrawingScope(HandColors[i]))
                {
                    Handles.Label(worldPosition + Vector3.up * size, HandNames[i]);
                    if (i != selectedHand)
                    {
                        if (Handles.Button(worldPosition, Quaternion.identity, size, size, Handles.DotHandleCap))
                        {
                            selectedHand = i;
                            Repaint();
                        }
                        continue;
                    }

                    EditorGUI.BeginChangeCheck();
                    if (Tools.current == Tool.Rotate)
                    {
                        var rotated = Handles.RotationHandle(worldRotation, worldPosition);
                        if (EditorGUI.EndChangeCheck())
                            rotation.vector3Value = (Quaternion.Inverse(rifle.rotation) * rotated
                                * Quaternion.Inverse(reference.rotation)).eulerAngles;
                    }
                    else
                    {
                        var moved = Handles.PositionHandle(worldPosition, rifle.rotation);
                        if (EditorGUI.EndChangeCheck())
                            position.vector3Value = rifle.InverseTransformPoint(moved) - reference.position;
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        static Pose ReferencePose(NpcRifleGrip grip, bool right)
        {
            if (EditorApplication.isPlaying && grip.TryGetReference(right, out var live)) return live;
            var rifle = grip.transform;
            // shortcut: 편집 중에는 대표 접점을 표시한다. 현재 애니메이션의 손목 기준점은 Play에서 확인한다.
            var ally = rifle.GetComponentInParent<AllyCombatant>();
            if (ally != null)
            {
                var fit = ally.GetComponentInChildren<AlliedRifleFit>(true);
                if (fit != null && fit.enabled && fit.correctCarryPose && AlliedRifleFit.IsParkAvatar(fit.GetComponent<Animator>()))
                    return new Pose(right ? AlliedRifleFit.ParkRightGrip : AlliedRifleFit.ParkAimedLeftGrip, Quaternion.identity);
                if (fit != null && fit.enabled && (fit.correctShoulderedHands || fit.correctCarryPose))
                    return new Pose(right ? fit.ShoulderedTriggerWristGrip : fit.supportWristGrip, Quaternion.identity);
                return new Pose(right ? new Vector3(0f, -.035f, -.16f) : new Vector3(0f, -.015f, .22f),
                    Quaternion.identity);
            }
            var mesh = rifle.GetComponentInChildren<MeshFilter>(true);
            var centre = mesh != null && mesh.sharedMesh != null
                ? rifle.InverseTransformPoint(mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center))
                : Vector3.zero;
            var offset = right
                ? Vector3.back * EnemyAgent.GripBehindCentre + rifle.InverseTransformVector(-rifle.forward * EnemyAgent.GripForward)
                : Vector3.forward * .2f + rifle.InverseTransformVector(-rifle.up * EnemyHandIK.Below);
            return new Pose(centre + offset, Quaternion.identity);
        }
    }
}
