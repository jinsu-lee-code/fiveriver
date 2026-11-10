using System;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 1인칭 이동 · 시점 · 자세. 몸(이 오브젝트)이 좌우, 카메라 축이 상하를 돈다.
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerConfig config;
        [SerializeField] Transform cameraPivot;
        [SerializeField] InputGate gate;

        [Header("정면 참호 통과 제한 (측면 통로는 유지)")]
        [SerializeField] bool restrictFrontLine;
        [SerializeField] bool allowSidePassage = true;
        [SerializeField] Vector2 frontLineX = new Vector2(-20f, 20f);
        [SerializeField] float frontLineZ = 0.57f;

        // The dawn counterattack opens a single crossing at the east end of the trench.
        float? hillExitX;
        public void SetHillExit(float? x) => hillExitX = x;

        CharacterController body;
        float pitch;
        float recoil;
        float recoilVelocity;
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
            // Raycasts in WeaponHolder.Update must see this frame's mouse pitch.
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
            var movement = (planar + Vector3.up * verticalSpeed) * dt;
            // 플레이어의 정면 통과만 제한한다. 탄환/로켓/적의 물리 충돌은 바꾸지 않는다.
            if (restrictFrontLine && movement.z > 0f && before.z <= frontLineZ && before.z + movement.z > frontLineZ)
            {
                float t = (frontLineZ - before.z) / movement.z;
                float crossingX = before.x + movement.x * t;
                bool inHillExit = hillExitX.HasValue && Mathf.Abs(crossingX - hillExitX.Value) <= 4f;
                if (!inHillExit && (!allowSidePassage || (crossingX >= frontLineX.x && crossingX <= frontLineX.y)))
                    movement.z = frontLineZ - before.z;
            }
            body.Move(movement);
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

        public void AddRecoil(float degrees)
        {
            recoil = Mathf.Min(recoil + degrees * 0.18f, 12f);
            recoilVelocity += degrees * 50f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float omega = Mathf.Max(16f, config.recoilRecovery * 2.5f);
                float impulse = recoilVelocity + omega * recoil;
                float decay = Mathf.Exp(-omega * dt);
                recoil = (recoil + impulse * dt) * decay;
                recoilVelocity = (recoilVelocity - omega * impulse * dt) * decay;
            }
            cameraPivot.localRotation = Quaternion.Euler(pitch - recoil, 0f, 0f);
        }

        public void Teleport(Transform point)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(point.position, Quaternion.Euler(0f, point.eulerAngles.y, 0f));
            body.enabled = true;
            pitch = 0f;
            recoil = 0f;
            recoilVelocity = 0f;
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
