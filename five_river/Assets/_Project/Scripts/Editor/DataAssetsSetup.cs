using System;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 수치 에셋이 없을 때만 BALANCE.md 1차 값으로 만든다. 이미 있으면 건드리지 않는다 (조정 값 보존).
    // 값을 바꿀 때는 에셋을 고치고 BALANCE.md에 "→ 날짜 변경"으로 적는다. 여기 숫자는 처음 만들 때의 씨앗일 뿐이다.
    static class DataAssetsSetup
    {
        [MenuItem("FiveRivers/Setup/Create Missing Data Assets")]
        public static void Run()
        {
            Ensure<TankConfig>("TankConfig", t =>
            {
                t.sideAngle = 50; t.hitsToStop = 2;
                t.attempt1 = 75; t.attempt2 = 45; t.supportAfterFails = 2;
                t.approachSpeed = 2.5f; t.turretTurnSpeed = 25; t.fireInterval = 9;
                t.cannonDamage = 35; t.cannonRadius = 5; t.nearMissOffset = 6;
            });
            Ensure<AssistConfig>("AssistConfig", a =>
            {
                a.failThreshold = 2; a.enemyAccuracy = 0.7f; a.enemyDamage = 0.8f;
            });
            Ensure<PlayerConfig>("PlayerConfig", p =>
            {
                p.walkSpeed = 3.2f; p.sprintSpeed = 5.6f; p.crouchSpeed = 1.7f; p.aimSpeedScale = 0.6f; p.gravity = 18f;
                p.standEyeHeight = 1.6f; p.crouchEyeHeight = 0.95f; p.stanceChangeSpeed = 4f;
                p.lookSensitivity = 0.09f; p.aimLookScale = 0.6f; p.pitchLimit = 80f; p.recoilRecovery = 10f;
                p.maxHealth = 100f; p.regenDelay = 4f; p.regenPerSecond = 25f;
            });
            Ensure<WeaponConfig>("WeaponConfig", w =>
            {
                w.m1ClipSize = 8; w.m1StartReserve = 40; w.m1MaxReserve = 64;
                w.m1FireInterval = 0.22f; w.m1Damage = 100f; w.m1Range = 250f;
                w.m1HipSpread = 1.6f; w.m1AimSpread = 0.15f; w.m1RecoilPitch = 1.8f;
                w.m1ReloadEmptyTime = 2.5f; w.m1ReloadPartialTime = 3.2f; w.m1EquipTime = 0.5f; w.m1UnequipTime = 0.4f;
                w.hipFov = 70f; w.m1AimFov = 50f; w.aimBlendTime = 0.18f;
                w.bazookaRockets = 5; w.bazookaReloadTime = 2.2f; w.bazookaEquipTime = 0.7f; w.bazookaAimFov = 55f; w.bazookaRecoilPitch = 4f;
                w.rocketSpeed = 60f; w.rocketLifetime = 3f; w.rocketBlastRadius = 3f; w.rocketBlastDamage = 150f;
                w.resupplyDistance = 2.2f;
            });
            Ensure<WaveConfig>("WaveConfig", w =>
            {
                w.scouts = 2; w.mainAssault = 7; w.maxActive = 5; w.spawnInterval = 3f;
                w.health = 100f; w.moveSpeed = 3.4f; w.retreatSpeed = 4.8f; w.coverHoldMin = 5f; w.coverHoldMax = 9f;
                w.fireIntervalMin = 2.6f; w.fireIntervalMax = 4.2f;
                w.accuracyDark = 0.1f; w.accuracyLit = 0.35f; w.crouchedFactor = 0.25f; w.damage = 18f; w.sightRange = 140f;
            });
            Ensure<FlareConfig>("FlareConfig", f =>
            {
                f.scoutFlareDelay = 8f; f.assaultFirstDelay = 5f; f.assaultInterval = 24f;
                f.burnTime = 16f; f.fadeTime = 3f; f.launchHeight = 45f; f.fallSpeed = 1.6f; f.swayAmplitude = 1.5f;
                f.lightIntensity = 1500f; f.lightRange = 110f;
            });
            AssetDatabase.SaveAssets();
        }

        static void Ensure<T>(string name, Action<T> seed) where T : ScriptableObject
        {
            string path = DabudongSetup.DataDir + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            var asset = ScriptableObject.CreateInstance<T>();
            seed(asset);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log("[Setup] 수치 에셋 생성: " + path);
        }
    }
}
