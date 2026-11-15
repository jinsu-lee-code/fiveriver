using System;
using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 사망 동작이 끝날 때 표시 모델만 지형에 내려놓는다. 적의 이동 기준과 소총 물리는 그대로 사용한다.
    [DefaultExecutionOrder(140)]
    sealed class EnemyDeathGrounding : MonoBehaviour
    {
        const float Clearance = .004f, CellSize = .16f;
        const int CellCount = 24, CellTotal = CellCount * CellCount;
        static readonly float[] MeasureAt = { .74f, .87f, .96f, 1f };

        Animator animator;
        Terrain terrain;
        TerrainData terrainData;
        Transform hips, chest, head, leftShin, rightShin, leftFoot, rightFoot;
        Vector3 restPosition;
        Quaternion restRotation;
        SkinnedMeshRenderer skin;
        Mesh baked;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly Vector3[] contactPoints = new Vector3[CellTotal * 3];
        readonly float[] contactGaps = new float[CellTotal * 3];
        readonly List<int> contacts = new List<int>();
        int measurement;
        float targetHeight, currentHeight;
        Quaternion targetFit = Quaternion.identity, currentFit = Quaternion.identity;
        Quaternion headBefore, leftShinBefore, rightShinBefore;
        bool adjustedProne;

        public void Bind(Animator target)
        {
            animator = target;
            terrain = Terrain.activeTerrain;
            terrainData = terrain != null ? terrain.terrainData : null;
            if (terrainData == null || !animator.isHuman) { enabled = false; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            enabled = hips != null && chest != null && skin != null && skin.sharedMesh != null;
        }

        void Update()
        {
            if (!adjustedProne) return;
            // 최종 프레임이 반복될 때도 고개와 정강이 보정을 누적하지 않는다.
            if (head != null) head.localRotation = headBefore;
            if (leftShin != null) leftShin.localRotation = leftShinBefore;
            if (rightShin != null) rightShin.localRotation = rightShinBefore;
            adjustedProne = false;
        }

        void LateUpdate()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool prone = state.IsName("Death_03");
            if ((!state.IsName("Death_01") && !state.IsName("Death_02") && !prone) ||
                animator.IsInTransition(0) || state.normalizedTime < .65f)
                return;

            transform.SetLocalPositionAndRotation(restPosition, restRotation);
            Vector3 pivot = hips.position;
            if (!GroundCoordinates(pivot, out float u, out float v)) return;
            Vector3 plane = new Vector3(pivot.x, Height(pivot), pivot.z);
            Vector3 normal = terrain.transform.TransformDirection(terrainData.GetInterpolatedNormal(u, v)).normalized;
            if (pivot.y - plane.y > .9f || normal.y < .7f) return;

            float progress = state.normalizedTime;
            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, .91f, progress));
            Vector3 torso = chest.position - pivot;
            var alongGround = Vector3.ProjectOnPlane(torso, normal);
            if (alongGround.sqrMagnitude > .01f)
            {
                // Preserve the side-lying/prone roll; tilt only the torso's length onto the slope.
                var turn = Quaternion.FromToRotation(torso, alongGround);
                var limited = Quaternion.RotateTowards(Quaternion.identity, turn, 28f);
                var tilt = Quaternion.Slerp(Quaternion.identity, limited, settle);
                transform.SetPositionAndRotation(pivot + tilt * (transform.position - pivot), tilt * transform.rotation);
            }
            if (prone) SettleProneLimbs(normal, alongGround, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.79f, .96f, progress)));
            if (measurement < MeasureAt.Length && progress >= MeasureAt[measurement])
            {
                if (TryFitSurface(pivot, plane, normal, chest.position - pivot, prone, out var fit, out float gap))
                {
                    targetFit = fit;
                    targetHeight = Mathf.Clamp(Clearance - gap, -.55f, .30f);
                }
                do measurement++; while (measurement < MeasureAt.Length && progress >= MeasureAt[measurement]);
                if (measurement == MeasureAt.Length && baked != null)
                {
                    Destroy(baked);
                    baked = null;
                    vertices.Clear();
                    vertices.Capacity = 0;
                }
            }

            currentFit = Quaternion.RotateTowards(currentFit, Quaternion.Slerp(Quaternion.identity, targetFit, settle),
                220f * Time.deltaTime);
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight * settle, 2.6f * Time.deltaTime);
            transform.SetPositionAndRotation(pivot + currentFit * (transform.position - pivot) + Vector3.up * currentHeight,
                currentFit * transform.rotation);
        }

        void SettleProneLimbs(Vector3 normal, Vector3 forward, float weight)
        {
            if (weight <= 0f || forward.sqrMagnitude < .01f) return;
            adjustedProne = true;
            if (head != null)
            {
                headBefore = head.localRotation;
                // 숙인 모자 끝이 가슴을 받치지 않도록 고개를 조금 편다.
                var sideways = Vector3.Cross(normal, forward).normalized;
                head.rotation = Quaternion.AngleAxis(-14f * weight, sideways) * head.rotation;
            }
            if (leftShin != null) leftShinBefore = leftShin.localRotation;
            if (rightShin != null) rightShinBefore = rightShin.localRotation;
            SettleShin(leftShin, leftFoot, normal, weight);
            SettleShin(rightShin, rightFoot, normal, weight);
        }

        static void SettleShin(Transform shin, Transform foot, Vector3 normal, float weight)
        {
            if (shin == null || foot == null) return;
            var direction = foot.position - shin.position;
            if (Vector3.Dot(direction, normal) <= .035f) return;
            var alongGround = Vector3.ProjectOnPlane(direction, normal);
            if (alongGround.sqrMagnitude < .01f) return;
            var turn = Quaternion.FromToRotation(direction, alongGround);
            var limited = Quaternion.RotateTowards(Quaternion.identity, turn, 12f);
            shin.rotation = Quaternion.Slerp(Quaternion.identity, limited, weight) * shin.rotation;
        }

        bool TryFitSurface(Vector3 pivot, Vector3 plane, Vector3 normal, Vector3 torso, bool prone, out Quaternion fit, out float gap)
        {
            baked ??= new Mesh { name = "Enemy corpse contact sample" };
            Array.Fill(contactGaps, float.PositiveInfinity);
            float torsoLength = torso.magnitude;
            var torsoAxis = torso.normalized;
            var rollAxis = Vector3.ProjectOnPlane(torsoAxis, normal).normalized;
            var pitchAxis = Vector3.Cross(normal, rollAxis).normalized;
            // 이 모델의 비균일 스케일은 BakeMesh(true)의 로컬 정점에 행렬을 적용해야 실제 렌더 크기와 일치한다.
            skin.BakeMesh(baked, true);
            baked.GetVertices(vertices);
            var localToWorld = skin.transform.localToWorldMatrix;
            foreach (var local in vertices)
            {
                var point = localToWorld.MultiplyPoint3x4(local);
                Vector3 fromHip = point - pivot;
                int cell = Cell(fromHip);
                if (cell < 0) continue;
                float distance = Vector3.Dot(normal, point - plane);
                SaveContact(cell, point, distance);
                // 골반과 가슴의 실제 표면을 팔다리 최저점과 따로 모은다.
                float along = Vector3.Dot(fromHip, torsoAxis);
                if (along < -.12f || along > torsoLength + .10f ||
                    (fromHip - along * torsoAxis).sqrMagnitude > .28f * .28f) continue;
                int core = along < torsoLength * .5f ? 1 : 2;
                SaveContact(core * CellTotal + cell, point, distance);
            }
            contacts.Clear();
            for (int cell = 0; cell < contactGaps.Length; cell++)
                if (float.IsFinite(contactGaps[cell])) contacts.Add(cell);

            fit = Quaternion.identity;
            float best = float.PositiveInfinity;
            int rollStep = prone ? 5 : 10;
            // 몸통의 두 지지점이 바닥과 가까워지는 범위에서 기울기를 고른다.
            for (int pitch = -14; pitch <= 14; pitch += 7)
                for (int roll = -30; roll <= 30; roll += rollStep)
                {
                    var turn = Quaternion.AngleAxis(roll, rollAxis) * Quaternion.AngleAxis(pitch, pitchAxis);
                    float floor = GroundGap(turn, pivot, out float pelvis, out float ribs);
                    float score = Mathf.Max(pelvis, ribs) - floor + .20f * (pelvis + ribs - 2f * floor)
                        + .00035f * (Mathf.Abs(pitch) + Mathf.Abs(roll));
                    if (!float.IsFinite(score) || score >= best) continue;
                    best = score;
                    fit = turn;
                }
            // 기울어진 뒤 구역별 최저점이 달라진다. 전체 정점을 다시 분류해서 높이를 정한다.
            Array.Fill(contactGaps, float.PositiveInfinity, 0, CellTotal);
            foreach (var local in vertices)
            {
                var offset = fit * (localToWorld.MultiplyPoint3x4(local) - pivot);
                int cell = Cell(offset);
                if (cell < 0) continue;
                var point = pivot + offset;
                SaveContact(cell, point, Vector3.Dot(normal, point - plane));
            }
            gap = float.PositiveInfinity;
            for (int cell = 0; cell < CellTotal; cell++)
            {
                var point = contactPoints[cell];
                if (float.IsFinite(contactGaps[cell]) && GroundCoordinates(point, out _, out _))
                    gap = Mathf.Min(gap, point.y - Height(point));
            }
            return float.IsFinite(gap);
        }

        static int Cell(Vector3 offset)
        {
            int x = Mathf.FloorToInt(offset.x / CellSize) + CellCount / 2;
            int z = Mathf.FloorToInt(offset.z / CellSize) + CellCount / 2;
            return (uint)x < CellCount && (uint)z < CellCount ? z * CellCount + x : -1;
        }

        void SaveContact(int cell, Vector3 point, float distance)
        {
            if (distance >= contactGaps[cell]) return;
            contactGaps[cell] = distance;
            contactPoints[cell] = point;
        }

        float GroundGap(Quaternion turn, Vector3 pivot, out float pelvis, out float ribs)
        {
            float gap = float.PositiveInfinity;
            pelvis = ribs = float.PositiveInfinity;
            foreach (int cell in contacts)
            {
                var point = pivot + turn * (contactPoints[cell] - pivot);
                if (!GroundCoordinates(point, out _, out _)) continue;
                float distance = point.y - Height(point);
                gap = Mathf.Min(gap, distance);
                if (cell >= 2 * CellTotal) ribs = Mathf.Min(ribs, distance);
                else if (cell >= CellTotal) pelvis = Mathf.Min(pelvis, distance);
            }
            return gap;
        }

        bool GroundCoordinates(Vector3 world, out float u, out float v)
        {
            Vector3 local = terrain.transform.InverseTransformPoint(world);
            u = local.x / terrainData.size.x;
            v = local.z / terrainData.size.z;
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        float Height(Vector3 world) => terrain.SampleHeight(world) + terrain.transform.position.y;
        void OnDestroy() { if (baked != null) Destroy(baked); }
    }
}
