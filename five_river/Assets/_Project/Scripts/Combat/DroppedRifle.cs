using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 쓰러진 적이 놓친 소총의 마무리 (2026-11-27, 검수 캡처 hitdir/front_head_0.7 · 2.0).
    // 예전: EnemyAgent.DropRifle 이 크기를 맞추지 않은 BoxCollider(모델이 자식에 있으면 1 m 정육면체)를 붙여 던지고,
    // 시신이 굳는 순간(EnemyAgent.Settle, 쓰러진 뒤 5초) 강체를 지워 그때 자세 그대로 얼렸다 → 땅에 비스듬히 선 채 굳음.
    // 이제: 충돌 상자를 모델 크기에 맞추고(DropRifle), 이 컴포넌트가 강체가 잠들거나 멈추거나 상한 시간이 지날 때 직접 굳힌다
    // (시신이 굳는 때와 무관). 굳힐 때 25° 넘게 서 있으면 지면 법선에 맞춰 0.3초에 걸쳐 눕힌다(눕힐 자리가 막히면 그대로).
    // 시신 래그돌 몸 조각과는 부딪히지 않는다(충돌 행렬상 Ignore Raycast ↔ Ragdoll 도 꺼져 있지만, 넘기는 순간에도 확실히).
    // 판단은 순수 계산 DroppedRifleRest. 소총은 EnemyDirector 밑에 있어 체크포인트 복구 · 새 회차에 시신과 함께 지워진다
    // (EnemyAgent.Remove · EnemyDirector.ClearAll, 핵심 규칙 3 · 4). 정적 상태 없음. 표시 전용(판정 · 진행 무관).
    [DisallowMultipleComponent]
    public sealed class DroppedRifle : MonoBehaviour
    {
        Rigidbody body;
        BoxCollider box;
        EnemyRagdoll ragdoll;
        DroppedRifleRest.Rule rule;
        float droppedAt, stillFor, layAt;
        bool ragdollIgnored, frozen, laying;
        Vector3 layFromPosition, layToPosition;
        Quaternion layFromRotation, layToRotation;

        public bool Frozen => frozen;
        public bool Laid { get; private set; }

        static readonly RaycastHit[] hits = new RaycastHit[8];
        static readonly Collider[] overlaps = new Collider[16];
        static readonly float[] LayYaws = { 0f, 60f, -60f, 120f, -120f, 180f };

        public static DroppedRifle Attach(GameObject rifle, Rigidbody rigidbody, BoxCollider collider, EnemyRagdoll fromBody)
        {
            var rest = rifle.GetComponent<DroppedRifle>();
            if (rest == null) rest = rifle.AddComponent<DroppedRifle>();
            rest.body = rigidbody;
            rest.box = collider;
            rest.ragdoll = fromBody;
            rest.rule = DroppedRifleRest.Rule.From(fromBody != null ? fromBody.Config : null);
            rest.droppedAt = Time.time;
            rest.stillFor = 0f;
            rest.frozen = rest.laying = rest.ragdollIgnored = false;
            rest.Laid = false;
            rest.enabled = true;
            rest.IgnoreRagdoll(true);
            return rest;
        }

        // The collider fitted to the rifle's mesh, in the rifle's own space (an AddComponent<BoxCollider> on an object
        // whose mesh sits on a child is a 1 m cube at its origin: the rifle stood on end on it).
        public static void FitBox(BoxCollider collider, Transform rifle, MeshFilter mesh)
        {
            if (collider == null || rifle == null || mesh == null || mesh.sharedMesh == null) return;
            var local = mesh.sharedMesh.bounds;
            var toRifle = rifle.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            var fitted = new Bounds(toRifle.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                fitted.Encapsulate(toRifle.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f))));
            var size = fitted.size;
            // never thinner than 3 cm, so it cannot tunnel into the ground or roll on an edge forever
            collider.center = fitted.center;
            collider.size = new Vector3(Mathf.Max(size.x, 0.03f), Mathf.Max(size.y, 0.03f), Mathf.Max(size.z, 0.03f));
        }

        // The bodies of the man who dropped it: never lean on them. The ragdoll's colliders are off until it is handed
        // to physics, and a pair is only ignored between enabled colliders, so it is done again on the first physics
        // step after the handover (the collision matrix already keeps Ignore Raycast and Ragdoll apart: this is the
        // second guard, in case a layer changes).
        void IgnoreRagdoll(bool atDrop)
        {
            if (box == null || !box.enabled || ragdoll == null || ragdoll.Bodies == null) return;
            if (!atDrop && (ragdollIgnored || !ragdoll.Simulating)) return;
            foreach (var part in ragdoll.Bodies)
            {
                if (part == null) continue;
                var c = part.GetComponent<Collider>();
                if (c != null && c.enabled && c.gameObject.activeInHierarchy) Physics.IgnoreCollision(box, c, true);
            }
            if (!atDrop) ragdollIgnored = true;
        }

        void FixedUpdate()
        {
            if (frozen) return;
            if (body == null) { Freeze(); return; }
            IgnoreRagdoll(false);
            stillFor = DroppedRifleRest.StillFor(stillFor, body.linearVelocity.magnitude, body.angularVelocity.magnitude, Time.fixedDeltaTime, rule);
            if (DroppedRifleRest.ReadyToFreeze(Time.time - droppedAt, body.IsSleeping(), stillFor, rule)) Freeze();
        }

        void Update()
        {
            if (!laying) { if (frozen) enabled = false; return; }
            float k = DroppedRifleRest.LayProgress(Time.time - layAt, rule);
            transform.SetPositionAndRotation(Vector3.Lerp(layFromPosition, layToPosition, k), Quaternion.Slerp(layFromRotation, layToRotation, k));
            if (k < 1f) return;
            laying = false;
            Laid = true;
            enabled = false;
        }

        // No more physics: it lies as it is, or is laid down flat on the ground under it.
        public void Freeze()
        {
            if (frozen) return;
            frozen = true;
            Vector3 centreLocal = box != null ? box.center : Vector3.zero;
            Vector3 size = box != null ? Vector3.Scale(box.size, transform.lossyScale) : new Vector3(0.06f, 0.1f, 1.2f);
            if (body != null) Destroy(body);
            if (box != null) Destroy(box);
            body = null;
            var centre = transform.TransformPoint(centreLocal);
            if (!GroundUnder(centre, out var groundPoint, out var normal, out var groundCollider)) return;
            float tilt = DroppedRifleRest.Tilt(transform.forward, normal);
            if (!DroppedRifleRest.NeedsLay(tilt, rule)) return;

            float halfLength = size.z * 0.5f, halfThick = Mathf.Min(size.x, size.y) * 0.5f;
            // the lower end stays where it touches the ground; the rest comes down
            var along = transform.forward;
            var low = Vector3.Dot(along, normal) > 0f ? centre - along * halfLength : centre + along * halfLength;
            foreach (float yaw in LayYaws)
            {
                var rotation = DroppedRifleRest.Lay(transform.rotation, Vector3.forward, normal, yaw);
                var newAlong = rotation * Vector3.forward;
                // the lower end was the one behind along the long axis (or ahead): keep it on the same end
                var newCentre = low + (Vector3.Dot(along, normal) > 0f ? newAlong : -newAlong) * halfLength;
                if (GroundUnder(newCentre, out var under, out var underNormal, out _)) newCentre = under + underNormal * halfThick;
                else newCentre = groundPoint + normal * halfThick + (newCentre - groundPoint - normal * Vector3.Dot(newCentre - groundPoint, normal));
                if (Blocked(newCentre, rotation, size, groundCollider)) continue;
                layFromPosition = transform.position;
                layFromRotation = transform.rotation;
                layToRotation = rotation;
                // the rifle's origin is not its centre: carry the origin with the centre
                layToPosition = newCentre - rotation * Vector3.Scale(centreLocal, transform.lossyScale);
                layAt = Time.time;
                laying = true;
                enabled = true;
                return;
            }
            // nowhere clear to lay it (against cover on every side): it stays as it came to rest
        }

        // The ground right under p: the nearest surface below that is not a person, a body part or a loose object.
        static bool GroundUnder(Vector3 p, out Vector3 point, out Vector3 normal, out Collider ground)
        {
            point = p; normal = Vector3.up; ground = null;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.6f, Vector3.down, hits, 2.5f, WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || IsLoose(c)) continue;
                if (hits[i].distance >= best) continue;
                best = hits[i].distance;
                point = hits[i].point; normal = hits[i].normal; ground = c;
            }
            if (ground != null) return true;
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return false;
            var local = p - terrain.transform.position;
            var size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return false;
            point = new Vector3(p.x, terrain.SampleHeight(p) + terrain.transform.position.y, p.z);
            normal = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
            return true;
        }

        static bool IsLoose(Collider c) => c.attachedRigidbody != null || c.GetComponentInParent<EnemyAgent>() != null
            || c.GetComponentInParent<AllyCombatant>() != null || c.GetComponentInParent<CharacterController>() != null;

        // Laid there, would it pass into cover, a wall or a tree? (the ground itself does not count)
        bool Blocked(Vector3 centre, Quaternion rotation, Vector3 size, Collider ground)
        {
            var half = size * 0.45f;
            int n = Physics.OverlapBoxNonAlloc(centre, half, overlaps, rotation, WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlaps[i];
                if (c == null || c == ground || c is TerrainCollider || IsLoose(c) || c.transform.IsChildOf(transform)) continue;
                return true;
            }
            // the art rocks and sandbags with no collider of their own (the one it already touches does not count)
            var along = rotation * Vector3.forward * (size.z * 0.5f);
            return CoverVisuals.Blocks(centre + along, 0.05f, transform.position) || CoverVisuals.Blocks(centre - along, 0.05f, transform.position);
        }
    }
}
