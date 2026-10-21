using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vanguard.Player
{
    /// <summary>
    /// Hệ thống lướt (Dash) tích trữ 2 lượt với cơ chế bất tử (i-Frames).
    /// Tương thích Unity 6 URP và phối hợp chặt chẽ với PlayerController.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDash : MonoBehaviour
    {
        [Header("Dash Settings")]
        [Tooltip("Số lượt Dash tích trữ tối đa")]
        [SerializeField] private int maxCharges = 2;

        [Tooltip("Thời gian hồi lại 1 lượt Dash (giây), hồi tuần tự từng lượt")]
        [SerializeField] private float chargeCooldown = 2.0f;

        [Tooltip("Quãng đường lướt (mét)")]
        [SerializeField] private float dashDistance = 5.0f;

        [Tooltip("Thời gian thực hiện cú lướt dứt khoát (giây)")]
        [SerializeField] private float dashDuration = 0.2f;

        [Tooltip("Thời gian duy trì trạng thái bất tử i-Frames tính từ lúc bắt đầu lướt (giây)")]
        [SerializeField] private float iFrameDuration = 0.18f;

        [Header("Visual Effects")]
        [Tooltip("Hiệu ứng vệt mờ khi lướt (TrailRenderer). Tự động bật khi dash và tắt khi hết lướt")]
        [SerializeField] private TrailRenderer dashTrail;

        // Cached components & runtime states
        private Rigidbody rb;
        private PlayerController playerController;

        private int currentCharges;
        private float cooldownTimer;
        private bool isDashing;
        private float dashTimer;
        private Vector3 dashVelocity;

        /// <summary>
        /// Cờ bất tử (i-Frames): Bật true trong 0.18s đầu của cú lướt, sau đó tự động tắt về false.
        /// </summary>
        public bool IsInvulnerable { get; private set; }

        /// <summary>
        /// Trạng thái nhân vật đang trong quá trình lướt.
        /// </summary>
        public bool IsDashing => isDashing;

        /// <summary>
        /// Số lượt dash hiện có.
        /// </summary>
        public int CurrentCharges => currentCharges;

        /// <summary>
        /// Số lượt dash tối đa.
        /// </summary>
        public int MaxCharges => maxCharges;

        /// <summary>
        /// Tiến độ hồi chiêu của charge hiện tại (từ 0.0 đến 1.0).
        /// </summary>
        public float CooldownProgress => currentCharges >= maxCharges ? 1f : Mathf.Clamp01(cooldownTimer / chargeCooldown);

        // Sự kiện thông báo cho UI HUD, Audio, VFX
        public event Action OnDashStarted;
        public event Action OnDashEnded;
        public event Action<int, int> OnDashChargesChanged;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();

            if (dashTrail == null)
            {
                dashTrail = GetComponent<TrailRenderer>();
            }

            if (dashTrail != null)
            {
                dashTrail.emitting = false;
            }

            currentCharges = maxCharges;
        }

        private void Start()
        {
            // Bắn event khởi tạo để UI nhận thông tin charges ban đầu
            OnDashChargesChanged?.Invoke(currentCharges, maxCharges);
        }

        private void OnDisable()
        {
            // Đảm bảo không bị kẹt cờ khóa khi component hoặc GameObject bị vô hiệu hóa
            if (isDashing)
            {
                EndDash();
            }
        }

        private void Update()
        {
            UpdateCooldown();

            if (isDashing)
            {
                UpdateDashProgress();
            }
            else if (ReadDashInput())
            {
                TryDash();
            }
        }

        private void FixedUpdate()
        {
            if (isDashing)
            {
                ApplyDashMovement();
            }
        }

        /// <summary>
        /// Hồi phục charges tuần tự (2.0s hồi 1 charge).
        /// </summary>
        private void UpdateCooldown()
        {
            if (currentCharges < maxCharges)
            {
                cooldownTimer += Time.deltaTime;
                if (cooldownTimer >= chargeCooldown)
                {
                    cooldownTimer = 0f;
                    currentCharges++;
                    OnDashChargesChanged?.Invoke(currentCharges, maxCharges);
                }
            }
            else
            {
                cooldownTimer = 0f;
            }
        }

        /// <summary>
        /// Quản lý thời gian lướt và cờ bất tử i-Frames theo từng frame.
        /// </summary>
        private void UpdateDashProgress()
        {
            dashTimer += Time.deltaTime;

            // Tắt cờ bất tử sau đúng iFrameDuration (0.18s)
            if (IsInvulnerable && dashTimer >= iFrameDuration)
            {
                IsInvulnerable = false;
            }

            // Kết thúc cú lướt sau khi đạt đủ thời gian dash (~0.2s)
            if (dashTimer >= dashDuration)
            {
                EndDash();
            }
        }

        /// <summary>
        /// Thử kích hoạt Dash nếu còn charge và không đang trong một cú dash khác.
        /// </summary>
        public bool TryDash()
        {
            if (isDashing || currentCharges <= 0)
            {
                return false;
            }

            StartDash();
            return true;
        }

        /// <summary>
        /// Bắt đầu thực hiện cú lướt: trừ charge, kích hoạt i-Frames, khóa di chuyển thường.
        /// </summary>
        private void StartDash()
        {
            currentCharges--;
            OnDashChargesChanged?.Invoke(currentCharges, maxCharges);

            isDashing = true;
            IsInvulnerable = true;
            dashTimer = 0f;

            // Khóa di chuyển thường của PlayerController
            if (playerController != null)
            {
                playerController.CanMove = false;
            }

            // Bật vệt khói/trail
            if (dashTrail != null)
            {
                dashTrail.emitting = true;
            }

            // Xác định hướng lướt: Ưu tiên hướng WASD, nếu đứng yên lướt theo transform.forward
            Vector3 direction = GetDashDirection();
            float speed = dashDistance / dashDuration;
            dashVelocity = direction * speed;

            // Áp dụng vận tốc ngay lập tức
            ApplyDashMovement();

            OnDashStarted?.Invoke();
        }

        /// <summary>
        /// Kết thúc cú lướt: tắt cờ bất tử, mở lại di chuyển thường, triệt tiêu quán tính ngang.
        /// </summary>
        private void EndDash()
        {
            isDashing = false;
            IsInvulnerable = false;

            // Mở khóa di chuyển thường của PlayerController
            if (playerController != null)
            {
                playerController.CanMove = true;
            }

            // Tắt vệt trail
            if (dashTrail != null)
            {
                dashTrail.emitting = false;
            }

            // Dừng vận tốc ngang để dừng lại dứt khoát tại đích (giữ nguyên trọng lực trục Y)
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
#else
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
#endif

            OnDashEnded?.Invoke();
        }

        /// <summary>
        /// Áp dụng vận tốc lướt vật lý trong FixedUpdate tương thích Unity 6.
        /// </summary>
        private void ApplyDashMovement()
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(dashVelocity.x, rb.linearVelocity.y, dashVelocity.z);
#else
            rb.velocity = new Vector3(dashVelocity.x, rb.velocity.y, dashVelocity.z);
#endif
        }

        /// <summary>
        /// Xác định hướng lướt:
        /// Ưu tiên hướng input WASD hiện tại; nếu đang đứng yên thì lướt theo transform.forward.
        /// </summary>
        private Vector3 GetDashDirection()
        {
            Vector2 moveInput = Vector2.zero;

            if (playerController != null)
            {
                moveInput = playerController.CurrentMoveInput;
            }
            else
            {
                moveInput = ReadFallbackMoveInput();
            }

            // Nếu người chơi đang bấm WASD (kể cả đi chéo)
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
                return inputDir.normalized;
            }

            // Đang đứng yên -> Lướt theo hướng mặt nhân vật (đang nhìn chuột)
            Vector3 forwardDir = transform.forward;
            forwardDir.y = 0f; // Triệt tiêu Y

            return forwardDir.sqrMagnitude > 0.0001f ? forwardDir.normalized : Vector3.forward;
        }

        /// <summary>
        /// Nhận phím Space hoặc Chuột phải (Mouse 1) để kích hoạt Dash.
        /// Hỗ trợ cả New Input System và Legacy Input.
        /// </summary>
        private bool ReadDashInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
            {
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Đọc input dự phòng trong trường hợp không có PlayerController trên cùng GameObject.
        /// </summary>
        private Vector2 ReadFallbackMoveInput()
        {
            Vector2 rawInput = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawInput.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawInput.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawInput.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawInput.x += 1f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (rawInput == Vector2.zero)
            {
                rawInput.x = Input.GetAxisRaw("Horizontal");
                rawInput.y = Input.GetAxisRaw("Vertical");
            }
#endif

            return rawInput.sqrMagnitude > 0.0001f ? rawInput.normalized : Vector2.zero;
        }
    }
}
