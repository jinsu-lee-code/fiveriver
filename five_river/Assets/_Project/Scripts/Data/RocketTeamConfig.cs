using UnityEngine;

namespace FiveRivers.Data
{
    // 로켓포반(사수 = 플레이어 + 탄약수 = 아군) 수치 (2026-11-24). 동작 클립의 시간표는 source/anim/rigify_rok_20261118 README 11절.
    // 장전 시간 자체는 WeaponConfig.bazookaReloadTime 이다 (여기서 바꾸지 않는다): 클립 재생 속도를 그 시간에 맞춘다.
    [CreateAssetMenu(fileName = "RocketTeamConfig", menuName = "FiveRivers/Rocket Team Config")]
    public sealed class RocketTeamConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/RocketTeamConfig.asset";

        [Header("탄약수")]
        [Tooltip("탄약수가 될 아군 오브젝트 이름 (씬 Ally_3). 없으면 둔덕에 가장 가까운 일반 병사")]
        public string loaderName = "Ally_3";

        [Header("자리 (사수 기준, 전차 쪽을 같이 본다. x 오른쪽, y 앞(음수 = 뒤), m)")]
        public Vector2 slotOffset = new Vector2(0.42f, -0.80f);
        [Tooltip("이 거리(m) 안이면 자리에 도착한 것")] public float arriveGap = 0.35f;
        [Tooltip("사수가 움직여 자리가 이만큼 벗어나면 따라온다")] public float followGap = 1.2f;
        [Tooltip("이 거리 밖으로 사수가 벗어나 있으면 장전을 시작하지 않는다 · 진행 중이면 멈추고 기다린다")] public float loadReach = 1.5f;
        [Tooltip("장전 중 이만큼 벗어나면 그 장전을 접고 따라간다 (사수가 크게 자리를 떴다)")] public float abortGap = 2.0f;
        [Tooltip("따라올 수 있는 최대 거리. 이보다 멀면 따라오지 않고 손짓")] public float followMax = 8f;
        [Tooltip("사수가 이 거리(m) 밖이면 손짓")] public float waveBeyond = 3.5f;
        public float waveCooldown = 8f;
        [Tooltip("장전 요청 뒤 탄약수가 준비되지 못한 채 이만큼 기다리면 이번 장전은 사수 혼자 한다 (사수가 숙여 있는 시간은 세지 않는다)")]
        public float giveUpSeconds = 6f;
        [Tooltip("둔덕(안내 자리)에 사수가 이 거리(m) 안으로 한 번 들어오면, 그 뒤로는 사수를 따라 자리를 잡는다")]
        public float joinRadius = 3f;

        [Header("사수 자세 · 시선 (장전이 멈추는 조건)")]
        [Tooltip("사수가 Z로 몸을 낮춘 동안은 장전이 멈춘다 (사수가 서 있는 시간만 장전 시간에 센다)")] public bool pauseWhileCrouched = true;
        [Tooltip("사수 시선이 전차 쪽에서 이 각도(도) 넘게 돌아가 있으면 장전이 멈춘다 (발사관 뒤 끝이 탄약수 손에서 멀다)")] public float maxLookAngle = 65f;

        [Header("이동")]
        public float moveSpeed = 2.2f;
        public float turnRate = 260f;

        [Header("장전 클립 시간표 (클립 시간 초, 기본값 = 납품 클립)")]
        public float loadClipSeconds = 2.2f;
        [Tooltip("로켓을 다 밀어 넣고 사수에게 신호를 보내는 때 = 발사관 장전 완료. 1.56 걸쇠 이후, 1.92 두 번째 두드림")] public float readyClipSeconds = 1.92f;
        public float insertClipSeconds = 0.80f;
        public float[] tapClipSeconds = { 1.78f, 1.92f };
        public float kneelInSeconds = 2.633f;
        public float reachSeconds = 0.933f;
        public float braceSeconds = 1.233f;
        public float waveSeconds = 2.433f;
        public float toRifleSeconds = 2.433f;
        public float minClipSpeed = 0.6f;
        public float maxClipSpeed = 1.6f;

        [Header("신호 (1인칭에는 탄약수가 안 보이므로 이것으로 '장전 중'을 느끼게 한다)")]
        [Tooltip("어깨 두드림 때 화면만 아주 약하게 흔들린다 (조준은 그대로, 도)")] public float tapShakeDegrees = 0.35f;
        public float tapShakeSeconds = 0.16f;
        [Tooltip("두드림 소리 (SoundLibrary id). 없으면 소리 없음 — SOUND_LIST 에 새 소리가 필요하다")] public string tapSoundId = "SFX_Loader_Tap";
        [Tooltip("로켓이 발사관에 들어가는 소리 (기존 효과음). 발사관 장전 완료 때도 같은 소리가 한 번 더 난다(AudioDirector)")] public string insertSoundId = "SFX_Bazooka_Load";

        [Header("로켓 소품 (탄약수 손의 로켓)")]
        [Tooltip("로켓 모델 원점 보정(모델 좌표, m): 로켓 축이 FBX 원점에서 위로 0.0696 m")] public Vector3 propModelOffset = new Vector3(0f, -0.0696f, 0f);
        [Tooltip("로켓 모델의 축을 궤적의 앞(탄두 쪽)에 맞추는 회전(도). 처음 확인에서 맞춘다")] public Vector3 propModelEuler = Vector3.zero;
        [Tooltip("옆에 세워 둔 예비 로켓을 보여 준다")] public bool showPile = true;
    }
}
