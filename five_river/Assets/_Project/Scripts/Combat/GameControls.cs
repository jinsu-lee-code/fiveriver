using System;
using UnityEngine.InputSystem;

namespace FiveRivers.Combat
{
    // 조작 키 (CLAUDE.md 핵심 설계 규칙 9, 변경 금지): WASD · 마우스 / 좌클릭 사격 · 우클릭 조준 / R 장전 /
    // F 보충 / 1 · 2 무기 / Z 자세 / Shift 달리기 / Esc 메뉴. 화면 안내는 KeyLabel로 실제 바인딩을 읽는다.
    public sealed class GameControls : IDisposable
    {
        public readonly InputAction Move = new InputAction("Move", InputActionType.Value);
        public readonly InputAction Look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        public readonly InputAction Fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
        public readonly InputAction Aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
        public readonly InputAction Reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
        public readonly InputAction Resupply = new InputAction("Resupply", InputActionType.Button, "<Keyboard>/f");
        public readonly InputAction Weapon1 = new InputAction("Weapon1", InputActionType.Button, "<Keyboard>/1");
        public readonly InputAction Weapon2 = new InputAction("Weapon2", InputActionType.Button, "<Keyboard>/2");
        public readonly InputAction Stance = new InputAction("Stance", InputActionType.Button, "<Keyboard>/z");
        public readonly InputAction Sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        public readonly InputAction Pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");

        InputAction[] All => new[] { Move, Look, Fire, Aim, Reload, Resupply, Weapon1, Weapon2, Stance, Sprint, Pause };

        public GameControls()
        {
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        public void Enable()
        {
            foreach (var a in All) a.Enable();
        }

        public void Dispose()
        {
            foreach (var a in All) a.Dispose();
        }

        public static string KeyLabel(InputAction action)
        {
            string s = action.GetBindingDisplayString();
            return string.IsNullOrEmpty(s) ? action.name : s;
        }
    }
}
