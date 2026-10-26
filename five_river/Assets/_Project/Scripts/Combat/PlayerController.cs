using System;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 1인칭 이동 · 시점 · 자세. 몸(이 오브젝트)이 좌우, 카메라 축이 상하를 돈다.
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerConfig config;
        [SerializeField] Transform cameraPivot;
        [SerializeField] InputGate gate;

        CharacterController body;
        float pitch;
        float recoil;
        float verticalSpeed;
        float eye;

        public GameControls Controls { get; private set; }
        public InputGate Gate => gate;
        public Transform CameraPivot => cameraPivot;
        public bool Crouched { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Aiming { get; set; }
        public Vector2 MoveInput { get; private set; }

        // 튜토리얼 행동 판정용 누적 값 (회차 시작 · 복구 때 0)
        public float LookedDegrees { get; private set; }
        public float MovedDistance { get; private set; }

        public event Action StanceChanged;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            Controls = new GameControls();
            Controls.Enable();
            eye = config.standEyeHeight;
            ApplyEye();
        }

        void OnDestroy()
        {
            Controls.Dispose();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (gate.CanLook)
            {
                var d = Controls.Look.ReadValue<Vector2>() * config.lookSensitivity * (Aiming ? config.aimLookScale : 1f);
                transform.Rotate(0f, d.x, 0f);
                pitch = Mathf.Clamp(pitch - d.y, -config.pitchLimit, config.pitchLimit);
                LookedDegrees += d.magnitude;
            }

            recoil = Mathf.MoveTowards(recoil, 0f, dt * config.recoilRecovery);
            cameraPivot.localRotation = Quaternion.Euler(pitch - recoil, 0f, 0f);

            MoveInput = gate.CanAct ? Controls.Move.ReadValue<Vector2>() : Vector2.zero;
            if (gate.CanAct && Controls.Stance.WasPressedThisFrame()) SetCrouched(!Crouched);
            Sprinting = gate.CanAct && Controls.Sprint.IsPressed() && MoveInput.y > 0.1f && !Aiming;
            if (Sprinting && Crouched) SetCrouched(false);

            float speed = Crouched ? config.crouchSpeed : Sprinting ? config.sprintSpeed : config.walkSpeed;
            if (Aiming) speed *= config.aimSpeedScale;
            var planar = transform.TransformDirection(new Vector3(MoveInput.x, 0f, MoveInput.y)) * speed;

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed -= config.gravity * dt;
            var before = transform.position;
            body.Move((planar + Vector3.up * verticalSpeed) * dt);
            var moved = transform.position - before;
            moved.y = 0f;
            MovedDistance += moved.magnitude;

            float targetEye = Crouched ? config.crouchEyeHeight : config.standEyeHeight;
            eye = Mathf.MoveTowards(eye, targetEye, config.stanceChangeSpeed * dt);
            ApplyEye();
        }

        void ApplyEye()
        {
            cameraPivot.localPosition = new Vector3(0f, eye, 0f);
            body.height = eye + 0.15f;
            body.center = new Vector3(0f, body.height / 2f, 0f);
        }

        public void SetCrouched(bool crouched)
        {
            if (Crouched == crouched) return;
            Crouched = crouched;
            StanceChanged?.Invoke();
        }

        public void AddRecoil(float degrees) => recoil += degrees;

        public void Teleport(Transform point)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(point.position, Quaternion.Euler(0f, point.eulerAngles.y, 0f));
            body.enabled = true;
            pitch = 0f;
            recoil = 0f;
            verticalSpeed = 0f;
            Crouched = false;
            eye = config.standEyeHeight;
            ApplyEye();
        }

        public void ResetTutorialCounters()
        {
            LookedDegrees = 0f;
            MovedDistance = 0f;
        }
    }
}
