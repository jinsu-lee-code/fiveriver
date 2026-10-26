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
                t.approachSpeed = 3.5f; t.turretTurnSpeed = 25; t.fireInterval = 9;
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
            Ensure<TutorialConfig>("TutorialConfig", t =>
            {
                t.highlightAfter = 15f; t.hintAfter = 30f; t.lookDegrees = 90f; t.moveMeters = 2f;
            });
            Ensure<GameText>("GameText", SeedText);
            AssetDatabase.SaveAssets();
        }

        // 화면 문구 1차본 (2026-10-27). 자막은 SOUND_LIST "D 확정 전 제안 문구" 그대로. D가 확정한다 (TASKS 1-6).
        static void SeedText(GameText g)
        {
            g.gameTitle = "다섯 개의 강";
            g.gameSubtitle = "1950년 8월 · 다부동";
            g.startPrompt = "시작하려면 Enter";
            g.operatorHint = "운영자 재시작  Ctrl + Shift + R";
            g.pauseTitle = "일시정지";
            g.resumeLabel = "계속하기";
            g.restartLabel = "처음으로 (운영자)";

            g.tutorialSteps = new[]
            {
                new GameText.Step { prompt = "주변을 둘러본다", hint = "마우스를 움직이면 시점이 돌아갑니다." },
                new GameText.Step { prompt = "이동한다", hint = "W A S D 키로 참호 안을 걸어 보세요." },
                new GameText.Step { prompt = "자세를 낮춘다", hint = "자세를 낮추면 모래주머니 뒤에 몸이 숨습니다. 한 번 더 누르면 일어섭니다." },
                new GameText.Step { prompt = "조준해서 표적을 맞힌다", hint = "오른쪽 버튼을 누른 채 가늠쇠를 참호 앞 표적에 맞추고, 왼쪽 버튼으로 쏘세요." },
                new GameText.Step { prompt = "장전한다", hint = "M1은 8발을 다 쏘면 클립이 튀어나옵니다. 남은 탄이 있어도 장전할 수 있습니다." },
            };
            g.tutorialRemainingHeader = "익혀 둘 조작";
            g.mouseKeyLabel = "마우스";

            g.objectiveLabel = "목표";
            g.objectiveTutorial = "조작을 익혀라";
            g.objectiveScout = "정찰병을 막아라";
            g.objectiveAssault = "진지를 지켜라 · 밝을 때 엄폐해서 쏴라";
            g.objectiveSilence = "탄약 상자에서 탄약을 보충하라";
            g.objectiveTankMove = "왼쪽 둔덕으로 이동하라";
            g.objectiveTankFire = "바주카로 전차 옆면을 노려라";
            g.objectiveAllySupport = "엎드려라";

            g.resupplyPrompt = "탄약 보충";
            g.bazookaPrompt = "바주카 꺼내기";
            g.m1Name = "M1 개런드";
            g.bazookaName = "M20 바주카";

            g.scoutSpotted = Line("분대장", "앞에 움직임 있다! 쏴!", 2.5f, "VO_SQD_Scout_01");
            g.flareUp = Line("분대장", "조명탄이다! 엄폐하고 쏴!", 2.8f, "VO_SQD_Flare_01");
            g.flareDown = Line("분대장", "어두워진다. 지금 움직여!", 2.8f, "VO_SQD_Flare_02");
            g.retreat = Line("분대장", "물러간다… 탄약 챙겨!", 3f, "VO_SQD_Retreat_01");
            g.parkCall1 = Line("박용팔", "만수야.", 2.5f, "VO_PARK_Call_01");
            g.parkCall2 = Line("박용팔", "최만수! …만수 못 봤어?", 3.5f, "VO_PARK_Call_02");
            g.tankArrives = Line("분대장", "전차다! 정면은 안 돼, 옆으로 돌아!", 3.5f, "VO_SQD_Tank_01");
            g.tankRicochet = Line("분대장", "옆구리! 옆구리를 노려!", 2.5f, "VO_SQD_Tank_02");
            g.allySupport = Line("분대장", "엎드려! 지원 온다!", 3f, "VO_SQD_Ally_01");

            g.resultDate = "1950년 8월 · 다부동";
            g.resultName = "최만수";
            g.resultLetter = "어머니께.\n저는 잘 있습니다. 밤이 길어도 아침은 꼭 옵니다.\n추석에는 집에 가서 어머니가 빚은 송편을 먹고 싶습니다.\n그때까지 아프지 마십시오.\n\n만수 올림";
            g.resultTitle = "다섯 개의 강";
        }

        static GameText.Line Line(string speaker, string text, float duration, string voice) =>
            new GameText.Line { speaker = speaker, text = text, duration = duration, voice = voice };

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
