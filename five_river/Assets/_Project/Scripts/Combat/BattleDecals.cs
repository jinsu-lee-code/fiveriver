using System.Collections.Generic;
using FiveRivers.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Combat
{
    // 전투 흔적 데칼 (부족한 점 5번, 2026-11-26). 얇은 어댑터: 효과가 생기는 자리(Effects.SpawnedAt)에 총알 자국 · 그을음을,
    // 쓰러진 몸이 멈춘 자리(EnemyAgent.BodySettled)에 몸 아래 흙에 스민 핏자국을 URP 데칼로 남긴다.
    // 데칼은 효과 컨테이너 밑이라 체크포인트 복구 · 새 회차(Effects.ClearAll)에 함께 지워진다(핵심 규칙 3 · 4).
    // 종류마다 상한(BattleTraceConfig)을 넘으면 가장 오래된 것부터 지운다. 점수 · 처치 피드백이 아니라 땅에 남는 흔적일 뿐(규칙 6).
    // 메뉴 FiveRivers/Setup/Battle Decals 가 재질 · 렌더러 기능 · 이 컴포넌트를 연결한다.
    public sealed class BattleDecals : MonoBehaviour
    {
        [SerializeField] BattleTraceConfig config;
        [SerializeField] Effects effects;
        [SerializeField] Material bulletMaterial, scorchMaterial, bloodMaterial;

        readonly Queue<DecalProjector> bullets = new Queue<DecalProjector>(), scorches = new Queue<DecalProjector>(), bloods = new Queue<DecalProjector>();
        Transform eye;
        int seed;

        public int Count => bullets.Count + scorches.Count + bloods.Count;

        public void Configure(BattleTraceConfig traceConfig, Effects owner, Material bullet, Material scorch, Material blood)
        {
            config = traceConfig; effects = owner; bulletMaterial = bullet; scorchMaterial = scorch; bloodMaterial = blood;
        }

        void OnEnable()
        {
            if (effects == null || config == null) { enabled = false; return; }
            effects.SpawnedAt += OnEffect;
            EnemyAgent.BodySettled += OnBody;
        }

        void OnDisable()
        {
            if (effects != null) effects.SpawnedAt -= OnEffect;
            EnemyAgent.BodySettled -= OnBody;
        }

        bool InRange(Vector3 p)
        {
            if (eye == null) { var player = FindFirstObjectByType<PlayerController>(); if (player != null) eye = player.CameraPivot; }
            return eye == null || (eye.position - p).sqrMagnitude <= config.range * config.range;
        }

        void OnEffect(EffectKind kind, Vector3 position, Quaternion rotation)
        {
            if (!InRange(position)) return;
            switch (kind)
            {
                case EffectKind.ImpactDirt:
                    // the impact faces out of the surface (LookRotation(normal)): project back into it — but only into
                    // ground, sandbags and walls. A round that struck a man (M1Rifle spawns the same puff) leaves no hole
                    // floating where he stood.
                    var surfaceOut = rotation * Vector3.forward;
                    if (!Physics.Raycast(position + surfaceOut * 0.1f, -surfaceOut, out var under, 0.3f, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
                        || under.collider.GetComponentInParent<IDamageable>() != null || under.collider.GetComponentInParent<Animator>() != null || under.collider.attachedRigidbody != null || under.collider.GetComponentInParent<TankController>() != null) return;
                    Place(bullets, config.bulletMost, bulletMaterial, position, surfaceOut, config.bulletSize, config.bulletOpacity);
                    break;
                case EffectKind.RocketExplosion:
                    Place(scorches, config.scorchMost, scorchMaterial, position, Vector3.up, config.rocketScorchSize, config.scorchOpacity);
                    break;
                case EffectKind.CannonBlast:
                    Place(scorches, config.scorchMost, scorchMaterial, position, Vector3.up, config.shellScorchSize, config.scorchOpacity);
                    break;
            }
        }

        void OnBody(EnemyAgent body)
        {
            if (body == null) return;
            var anim = body.GetComponentInChildren<Animator>();
            var under = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Spine) : body.transform;
            var p = under != null ? under.position : body.transform.position;
            if (!InRange(p)) return;
            var ground = Terrain.activeTerrain;
            if (ground != null) p.y = ground.SampleHeight(p) + ground.transform.position.y;
            Place(bloods, config.bloodMost, bloodMaterial, p, Vector3.up, config.bloodSize, config.bloodOpacity);
        }

        void Place(Queue<DecalProjector> ring, int most, Material material, Vector3 at, Vector3 surfaceOut, Vector2 sizeRange, float opacity)
        {
            if (material == null || most <= 0) return;
            // the frame of a retry / new run: a body settling or a round landing in that same frame must not leave a
            // trace behind the clear (one was left once, 2026-11-26)
            if (Time.frameCount == effects.LastClearFrame) return;
            DecalProjector decal;
            if (ring.Count >= most) { decal = ring.Dequeue(); if (decal == null) decal = Make(material); }
            else decal = Make(material);
            if (decal == null) return;
            float roll = Hash(seed++) * 360f;
            float size = Mathf.Lerp(sizeRange.x, sizeRange.y, Hash(seed++));
            if (surfaceOut.sqrMagnitude < 1e-4f) surfaceOut = Vector3.up;
            // the projector looks into the surface; its depth straddles it so a slightly uneven ground still takes it
            decal.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-surfaceOut.normalized) * Quaternion.Euler(0f, 0f, roll));
            decal.size = new Vector3(size, size, config.depth);
            decal.pivot = Vector3.zero;
            decal.fadeFactor = opacity;
            decal.gameObject.SetActive(true);
            ring.Enqueue(decal);
        }

        DecalProjector Make(Material material)
        {
            var go = new GameObject("BattleDecal_" + material.name);
            go.transform.SetParent(effects.Container, false);
            var d = go.AddComponent<DecalProjector>();
            d.material = material;
            d.drawDistance = config.range;
            d.fadeScale = 0.9f;
            return d;
        }

        // a fixed scatter (the same run looks the same)
        static float Hash(int i)
        {
            uint h = (uint)(i + 1) * 2654435761u; h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        // the effect container was emptied (retry / new run): the rings hold destroyed projectors
        void LateUpdate()
        {
            if (bullets.Count > 0 && bullets.Peek() == null) bullets.Clear();
            if (scorches.Count > 0 && scorches.Peek() == null) scorches.Clear();
            if (bloods.Count > 0 && bloods.Peek() == null) bloods.Clear();
        }
    }
}
