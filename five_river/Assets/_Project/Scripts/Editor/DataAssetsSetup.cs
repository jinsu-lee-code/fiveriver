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
