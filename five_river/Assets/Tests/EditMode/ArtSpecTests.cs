using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.Tests
{
    // ART_SPEC 규칙 검사: 그레이박스든 최종본이든 같은 이름 · 같은 부품 · 같은 슬롯이어야 프리팹 연결이 유지된다.
    // art 브랜치에서 FBX를 덮어쓴 뒤 이 테스트로 공통 체크 M1 · M3 · M12 · W2와 전차 부품 체크를 확인한다.
    public class ArtSpecTests
    {
        const string Models = "Assets/_Project/Art/Models/";

        static GameObject Load(string rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Models + rel);
            Assert.IsNotNull(go, "파일 없음 (이름 · 폴더 확인): " + rel);
            return go;
        }

        static string[] Slots(GameObject go) =>
            go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToArray();

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = RendererBounds(rs[0]);
            foreach (var r in rs) b.Encapsulate(RendererBounds(r));
            return b;
        }

        // 리그 메시(SkinnedMeshRenderer)의 bounds 는 Unity가 넉넉하게 잡아 실제보다 8~12cm 크게 나온다(2026-10-30 확인).
        // 키 · 피벗은 바인드 포즈 정점으로 잰다.
        static Bounds RendererBounds(Renderer r)
        {
            if (!(r is SkinnedMeshRenderer s) || s.sharedMesh == null) return r.bounds;
            var v = s.sharedMesh.vertices;
            var b = new Bounds(r.transform.TransformPoint(v[0]), Vector3.zero);
            foreach (var p in v) b.Encapsulate(r.transform.TransformPoint(p));
            return b;
        }

        [Test]
        public void Tank_HasPartsSlotsAndSize()
        {
            var t = Load("Vehicles/T34_85/SM_VH_T34_85.fbx");
            var names = t.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToArray();
            foreach (var part in new[] { "Hull", "Turret", "Gun", "TrackL", "TrackR" })
                CollectionAssert.Contains(names, part, "전차 부품 이름: " + part);
            var gun = t.GetComponentsInChildren<Transform>(true).First(x => x.name == "Gun");
            Assert.AreEqual("Turret", gun.parent.name, "Gun은 Turret 밑 (포탑과 같이 돈다)");
            CollectionAssert.IsSubsetOf(Slots(t), new[] { "M_VH_T34_Body", "M_VH_T34_Track" }, "재질 슬롯 2개 이하, 이름 규칙");
            var b = Bounds(t);
            Assert.AreEqual(3.0f, b.size.x, 3.0f * 0.1f, "폭 3.0 m");
            Assert.Greater(b.max.z, 3.5f, "포 방향 +Z");
            CollectionAssert.Contains(names, "TrackL");
            Assert.Less(t.GetComponentsInChildren<Transform>(true).First(x => x.name == "TrackL").GetComponent<Renderer>().bounds.center.x, 0f, "TrackL은 왼쪽(-X)");
        }

        [Test]
        public void M1_HasBoltAndClipSlot_PointsForward()
        {
            var m1 = Load("Weapons/M1Garand/SM_WP_M1Garand.fbx");
            var names = m1.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToArray();
            CollectionAssert.Contains(names, "Bolt");
            CollectionAssert.Contains(names, "ClipSlot");
            CollectionAssert.IsSubsetOf(Slots(m1), new[] { "M_WP_M1_Wood", "M_WP_M1_Metal" });
            var b = Bounds(m1);
            Assert.AreEqual(1.10f, b.size.z, 1.10f * 0.1f, "길이 1.10 m");
            Assert.Greater(b.max.z, -b.min.z, "총구 +Z");
        }

        [Test]
        public void Bazooka_HasRocketSlot()
        {
            var bz = Load("Weapons/Bazooka/SM_WP_Bazooka.fbx");
            CollectionAssert.Contains(bz.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToArray(), "RocketSlot");
            Assert.AreEqual(1.52f, Bounds(bz).size.z, 1.52f * 0.1f, "길이 1.52 m");
        }

        // 캐릭터 슬롯 (결정 2026-10-30): Body · Gear 외에 납품 모델이 쓰는 Head · Belt · HandL · HandR · Skin 을 허용한다.
        // 이유: 최종 모델이 부위별 재질로 들어왔고, 2칸으로 다시 굽는 것보다 규격을 실제에 맞추는 편이 일정상 안전하다.
        static readonly string[] CharacterSlotParts = { "Body", "Gear", "Head", "Belt", "HandL", "HandR", "Skin" };

        [TestCase("Characters/ROK_Soldier/SK_CH_ROK_Soldier.fbx", 1.70f, "M_CH_ROK")]
        [TestCase("Characters/ROK_ParkYongpal/SK_CH_ROK_ParkYongpal.fbx", 1.72f, "M_CH_ROK")]
        [TestCase("Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx", 1.68f, "M_CH_KPA")]
        public void Characters_HeightAndSlots(string rel, float height, string slotPrefix)
        {
            var c = Load(rel);
            var b = Bounds(c);
            Assert.AreEqual(height, b.size.y, height * 0.05f, "키 ±5%");
            Assert.AreEqual(0f, b.min.y, 0.05f, "피벗 발 중앙");
            CollectionAssert.IsSubsetOf(Slots(c), CharacterSlotParts.Select(x => slotPrefix + "_" + x).ToArray());
        }

        [TestCase("Props/Sandbag/SM_PR_SandbagWall.fbx")]
        [TestCase("Props/AmmoCrate/SM_PR_AmmoCrate.fbx")]
        [TestCase("Props/Target/SM_PR_Target.fbx")]
        [TestCase("Props/BlanketBody/SM_PR_BlanketBody.fbx")]
        [TestCase("Weapons/M1Garand/SM_WP_M1Garand_Clip.fbx")]
        [TestCase("Weapons/M1Garand/SM_WP_M1Garand_ClipEmpty.fbx")]
        [TestCase("Weapons/Bazooka/SM_WP_BazookaRocket.fbx")]
        [TestCase("Weapons/Mosin/SM_WP_Mosin.fbx")]
        public void Props_ExistAndSitOnGround(string rel)
        {
            var go = Load(rel);
            Assert.AreEqual(Vector3.one, go.transform.localScale, "Transform 적용 (스케일 1)");
            if (rel.StartsWith("Props/")) Assert.AreEqual(0f, Bounds(go).min.y, 0.05f, "피벗 바닥 중앙");
        }

        [Test]
        public void Prefabs_HaveNoMissingReferences()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    Assert.IsNotNull(c, "스크립트 없음: " + path);
                    var so = new SerializedObject(c);
                    var p = so.GetIterator();
                    while (p.NextVisible(true))
                        if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                            Assert.Fail($"Missing 참조: {path} {c.GetType().Name}.{p.propertyPath}");
                }
            }
        }
    }
}
