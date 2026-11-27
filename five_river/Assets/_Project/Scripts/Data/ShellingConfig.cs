using System;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Data
{
    // 포격 (2026-11-18, 사용자 지시 "포격 소리도 집어넣고 흔적도 남겨서 전쟁의 비참함을 끌어 올려").
    // 구간마다 먼 포격(지평선 섬광 → 늦게 오는 포성) · 가까운 포격(연출용, 피해 없음) · 예광탄 · 이웃 진지 총성의
    // 빈도와 거리를 정한다. BALANCE.md 2-6번과 1:1. 표에 없는 구간에는 아무것도 없다.
    [CreateAssetMenu(fileName = "ShellingConfig", menuName = "FiveRivers/Shelling Config")]
    public sealed class ShellingConfig : ScriptableObject
    {
        // 값은 끝에만 추가한다.
        public enum Zone
        {
            None = 0,
            Field = 1,        // 참호 앞 들판
            Rear = 2,         // 참호 뒤편
            FieldAndRear = 3, // 둘 다 (들판이 더 잦다)
            AroundPlayer = 4, // 플레이어 둘레 (능선 · 고지)
        }

        [Serializable]
        public struct SectionRule
        {
            public FlowSection section;
            [Header("먼 포격: 지평선 섬광 → 늦게 오는 포성 (간격 초, 0이면 없음)")]
            public float distantMin;
            public float distantMax;
            [Range(0f, 1f)] public float distantLevel;
            [Header("가까운 포격 (간격 초, 0이면 없음)")]
            public float nearMin;
            public float nearMax;
            [Tooltip("구간에 들어온 뒤 첫 발까지 최소 시간")] public float nearFirstDelay;
            [Tooltip("이 구간에서 떨어지는 최대 발수 (0 = 제한 없음)")] public int nearMaxCount;
            public Zone zone;
            [Tooltip("플레이어에게서 이만큼은 떨어진다 (m)")] public float playerMin;
            [Tooltip("플레이어에게서 이보다 멀지 않다 (m)")] public float playerMax;
            [Header("능선 위 예광탄 줄기 · 이웃 진지 총성 (간격 초, 0이면 없음)")]
            public float tracerMin;
            public float tracerMax;
            public float gunfireMin;
            public float gunfireMax;
        }

        [Header("구간별 규칙 : BALANCE 2-6")]
        public SectionRule[] rules = Array.Empty<SectionRule>();
        [Tooltip("전시용 짧은 흐름(9:00 상한)에서도 가까운 포격(작은 탄막)을 쓴다. 큰 탄막과 숨 고르기는 일반판에만 있다")] public bool nearInExhibit = true;

        [Header("먼 포격")]
        [Tooltip("소리 속도 m/s: 섬광 뒤 포성이 오기까지 = 거리 / 이 값")] public float soundSpeed = 340f;
        [Tooltip("섬광이 일어나는 거리 m (가까운 능선 너머 ~ 골짜기 아래쪽)")] public float distantNear = 900f;
        public float distantFar = 3200f;
        [Range(0f, 1f)] [Tooltip("두세 발이 이어서 떨어질 확률")] public float salvoChance = 0.4f;
        public float salvoGapMin = 0.35f;
        public float salvoGapMax = 1.6f;
        [Range(0f, 1f)] [Tooltip("섬광이 적 쪽(북) 반원에서 날 비율. 나머지는 사방(뒤쪽은 아군 포대)")] public float frontShare = 0.7f;
        [Tooltip("지평선 섬광 밝기 (가까운 것 ~ 먼 것)")] public float flashBright = 0.3f;
        public float flashDim = 0.11f;
        public Color flashColor = new Color(1f, 0.72f, 0.45f);

        [Header("가까운 포격")]
        [Tooltip("날아오는 소리가 들린 뒤 터질 때까지 초")] public float incomingMin = 1.0f;
        public float incomingMax = 1.5f;
        [Tooltip("막혀 있다가(연출 컷 · 대사 · 전차 포) 풀린 뒤 이만큼은 더 기다린다")] public float holdAfterBlock = 2.5f;
        [Tooltip("전차가 포를 쏜 뒤 이 시간 안에는 떨어지지 않는다")] public float afterTankShot = 3.5f;

        [Header("안전 거리 m: 이 안에는 떨어지지 않는다")]
        public float keepAlly = 10f;
        public float keepEnemy = 7f;
        public float keepBody = 3.5f;
        public float keepCover = 5f;
        public float keepCrate = 8f;
        public float keepTrench = 7f;
        public float keepTank = 14f;
        public float keepCrater = 5f;

        [Header("떨어지는 구역 (월드 x 최소, z 최소, 너비, 깊이)")]
        public Rect fieldArea = new Rect(-42f, 10f, 86f, 82f);
        public Rect rearArea = new Rect(-34f, -34f, 70f, 23f);
        [Range(0f, 1f)] [Tooltip("들판 + 참호 뒤 구역에서 참호 뒤에 떨어질 비율")] public float rearShare = 0.3f;
        [Range(0f, 1f)] [Tooltip("플레이어가 보고 있는 쪽에 떨어뜨리려 하는 비율")] public float inViewShare = 0.6f;

        [Header("구덩이 (들판은 얕게: 이동 경로를 건드리지 않는다)")]
        public float craterRadius = 1.9f;
        public float craterDepth = 0.3f;
        public float craterRim = 0.1f;

        [Header("화면 · 소리 반응")]
        [Tooltip("이 거리 안이면 화면이 흔들린다 (m)")] public float shakeRange = 55f;
        [Tooltip("바로 곁(안전 거리 한계)에서의 흔들림 각도")] public float shakeDegrees = 1.5f;
        public float shakeSeconds = 0.45f;
        [Tooltip("이 거리 안이면 귀가 잠깐 먹먹하다 (m)")] public float deafenRange = 22f;
        public float deafenSeconds = 1.1f;
        [Tooltip("이 거리 안이면 참호 벽에서 흙이 떨어진다 (m)")] public float trenchDustRange = 38f;
        [Tooltip("폭발 섬광 점광원 밝기 · 범위 · 시간")] public float blastLight = 420f;
        public float blastLightRange = 45f;
        public float blastLightSeconds = 0.22f;
        // 2026-11-27: 연출 컷 렌즈가 폭발 몇 m 곁에 있을 때(조명탄 첫 연출) 위 섬광이 한 프레임 화면을 하얗게 날렸다
        // (225 이상 면적 13.7%). 연출 카메라가 켜진 동안 터지는 폭발만 이 배율로 줄인다. 플레이 중 폭발은 그대로. BALANCE 2-6.
        [Tooltip("연출 카메라 곁 폭발 섬광 밝기 배율")] [Range(0f, 1f)] public float lensBlastLightScale = 0.3f;
        [Tooltip("연출 카메라 곁 폭발 섬광 범위 배율")] [Range(0f, 1f)] public float lensBlastRangeScale = 0.6f;
        [Tooltip("자리에 남는 연기가 피어오르는 시간 (초)")] public float smolderSeconds = 40f;
        [Tooltip("새벽: 지난밤 포탄이 떨어진 자리 가운데 이만큼에서 가는 연기가 다시 오른다")] public int dawnSmokes = 5;
        public float dawnSmokeSeconds = 90f;

        // 2026-11-18 추가 지시 (사용자 원문 "포탄 떨어지는 거 한 발씩이 아니라 여러 발 떨구고 우리 진지 앞에도 떨어지고
        // 하면서 전쟁에 혼란을 느끼면서 동시에 살아남았다와 비참함 두 개를 확실히 보여주는 게 좋을 거 같아").
        [Header("작은 탄막: 가까운 포격은 한 발이 아니라 몇 발이 이어 떨어진다")]
        public int smallRoundsMin = 2;
        public int smallRoundsMax = 4;
        [Tooltip("발과 발 사이 초")] public float smallGapMin = 0.35f;
        public float smallGapMax = 1.1f;
        [Tooltip("첫 발에서 흩어지는 거리 m")] public float smallSpreadMin = 6f;
        public float smallSpreadMax = 15f;

        [Header("큰 탄막: 회차마다 한 번, 정찰이 끝나고 주 공세 직전 (일반판만). 들판에서 참호 쪽으로 걸어 들어온다")]
        public bool bigBarrage = true;
        [Tooltip("정찰병이 쓰러진 뒤 첫 날아오는 소리까지 초")] public float bigDelay = 1.6f;
        public int bigRoundsMin = 7;
        public int bigRoundsMax = 9;
        [Tooltip("첫 발부터 마지막 발까지 초")] public float bigSeconds = 5.5f;
        [Tooltip("첫 발이 떨어지는 곳: 흉벽에서 앞으로 m")] public float bigStartDistance = 58f;
        [Tooltip("마지막 몇 발은 우리 진지 바로 앞에 떨어진다")] public int bigCloseRounds = 3;
        [Tooltip("진지 바로 앞: 흉벽에서 앞으로 m")] public float bigCloseMin = 5f;
        public float bigCloseMax = 12f;
        [Tooltip("좌우로 흩어지는 거리 m (가까운 발은 절반)")] public float bigLateral = 15f;
        [Tooltip("탄막의 안전 거리 m: 플레이어 · 아군 · 탄약 상자 · 참호(흉벽)")] public float bigKeepPlayer = 6.5f;
        public float bigKeepAlly = 6f;
        public float bigKeepCrate = 6f;
        public float bigKeepTrench = 4.5f;
        [Tooltip("진지 바로 앞 낙탄: 화면 흔들림 각도 · 시간")] public float closeShakeDegrees = 2.6f;
        public float closeShakeSeconds = 0.5f;
        [Tooltip("진지 바로 앞 낙탄: 귀가 먹먹한 시간 (소리가 죽고 이명만 남았다가 천천히 돌아온다)")] public float closeDeafenSeconds = 3.2f;
        [Tooltip("먼지로 앞이 흐려졌다가 걷히는 시간 · 짙기(안개 밀도) · 색")] public float dustSeconds = 4f;
        public float dustDensity = 0.07f;
        public Color dustColor = new Color(0.17f, 0.145f, 0.12f);
        [Tooltip("탄막이 끝난 뒤의 숨 고르기 초: 포격 · 적 사격 · 새 목표 문구가 없다")] public float breatherSeconds = 4.5f;
        [Range(0f, 1f)] [Tooltip("숨 고르기 동안 플레이어 숨소리 크기")] public float breatherBreath = 0.85f;
        public GameObject dirtShowerPrefab;

        [Header("효과 프리팹 · 재질 (Setup/Shelling 메뉴가 채운다)")]
        public GameObject blastPrefab;
        public GameObject smolderPrefab;
        public GameObject trenchDustPrefab;
        public Material horizonGlow;

        [Header("예광탄 · 이웃 진지 총성")]
        public Color tracerOurs = new Color(1f, 0.42f, 0.22f);
        public Color tracerTheirs = new Color(0.45f, 1f, 0.5f);
        [Range(0f, 1f)] public float gunfireLevel = 0.3f;

        public bool TryRule(FlowSection section, out SectionRule rule)
        {
            foreach (var r in rules)
                if (r.section == section) { rule = r; return true; }
            rule = default;
            return false;
        }
    }
}
