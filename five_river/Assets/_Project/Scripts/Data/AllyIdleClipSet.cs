using System;
using UnityEngine;

namespace FiveRivers.Data
{
    // 참호 아군이 싸우지 않을 때 대기 자세 위에 얹는 동작 (2026-11-18, VARCO 대기 · 경계 동작 클립에서 구운 것).
    // 클립을 통째로 재생하지 않는다: 받은 클립은 뼈대 · 선 방향 · 발 간격 · 소총 든 모양이 지금의 대기 자세와 달라
    // 그대로 섞으면 몸이 옆으로 돌고 발이 끌렸다. 클립 첫 프레임에 대한 몸통 · 목 · 머리 · 골반의 "변화량"만
    // 아바타별로 구워 두고(메뉴 FiveRivers/Setup/ROK Idle Clips), Combat/AllyIdleLife 가 지금 자세에 더한다.
    // 반복 클립은 첫 · 끝 자세가 같으므로 변화량은 0에서 시작해 0으로 끝난다.
    // 표시 전용: 판정 · 진행 · 수치와 무관하다. 화면에서 어색했던 클립은 메뉴의 표에서 뺀다.
    public sealed class AllyIdleClipSet : ScriptableObject
    {
        public const string ResourceName = "AllyIdleClips";

        [Serializable]
        public sealed class Track
        {
            public string clip;      // A_ROK_… (클립 이름 = 파일명)
            public string avatar;    // 이 변화량을 구운 아바타 이름 (뼈의 축이 아바타마다 다르다)
            public bool kneeling;    // 무릎 대기(Idle_Trench) 위에 얹는 것, 아니면 서서 대기(Idle_Stand)
            public float length;     // 초
            public float rate;       // 초당 표본 수
            public float gain = 1f;  // 변화량에 곱하는 배율 (숨쉬기 · 체중 이동뿐인 잔잔한 클립은 화면에서 보이도록 키운다)
            // 각 뼈 자신의 축 기준 회전 변화량(첫 프레임 대비). 그 뼈가 없는 아바타는 빈 배열.
            public Quaternion[] hips, spine, chest, upperChest, neck, head;
            // 머리가 몸 전체 기준으로 돈 양(머리 자신의 축 기준): 다른 코드가 머리 방향을 붙들어 둔 아군에게 쓴다.
            public Quaternion[] headTotal;
            // 골반 이동(m, 모델 크기 1 기준): x = 그의 오른쪽, y = 위, z = 그의 앞.
            public Vector3[] hipsMove;

            public int Count => hipsMove != null ? hipsMove.Length : 0;
        }

        public Track[] tracks = new Track[0];
    }
}
