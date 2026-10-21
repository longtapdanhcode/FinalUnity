using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vanguard.Player
{
    /// <summary>
    /// Xử lý di chuyển nhân vật WASD (8 hướng) và xoay nhìn theo chuột (Mouse Look) trên mặt phẳng Y = 0.
    /// Tương thích hoàn toàn với Unity 6 (sử dụng Rigidbody.linearVelocity).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Tốc độ di chuyển cố định (m/s)")]
        [SerializeField] private float moveSpeed = 7f;

        [Header("Mouse Look Settings")]
        [Tooltip("Camera chính dùng để tạo raycast theo vị trí con trỏ chuột")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("Xoay mượt mà (true) hoặc trực tiếp nhìn về điểm trỏ chuột tức thì (false)")]
        [SerializeField] private bool smoothRotation = false;

        [Tooltip("Tốc độ xoay (độ/giây) khi bật smoothRotation")]
        [SerializeField] private float rotationSpeed = 720f;

        // Thành phần được cache để tránh gọi GetComponent trong Update/FixedUpdate
        private Rigidbody rb;
        private Vector2 moveInput;
        private Vector3 currentAimPoint;
        private Plane groundPlane;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }

        public Vector3 CurrentAimPoint => currentAimPoint;

        /// <summary>
        /// Cho phép hoặc tạm khóa di chuyển thường (ví dụ: khi đang Dash hoặc bị choáng).
        /// </summary>
        public bool CanMove { get; set; } = true;

        /// <summary>
        /// Input di chuyển WASD hiện tại (đã chuẩn hóa).
        /// </summary>
        public Vector2 CurrentMoveInput => moveInput;

        private void Reset()
        {
            ConfigureRigidbody();
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Mặt phẳng toán học Y = 0 với vector pháp tuyến hướng lên trên (Vector3.up)
            groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Update()
        {
            ReadMovementInput();
            HandleMouseLook();
        }

        private void FixedUpdate()
        {
            ApplyMovement();
        }

        /// <summary>
        /// Tự động cấu hình Rigidbody constraints (Freeze Rotation X, Z) trong Awake/Reset nếu chưa set.
        /// </summary>
        private void ConfigureRigidbody()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (rb != null)
            {
                // Khóa trục xoay X và Z để giữ nhân vật thẳng đứng, tránh bị ngã/lật khi va chạm
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                // Tối ưu độ mượt của chuyển động vật lý giữa các frame render
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        /// <summary>
        /// Nhận input di chuyển WASD 8 hướng và chuẩn hóa vector để tránh đi chéo nhanh hơn.
        /// Hỗ trợ cả New Input System và Legacy Input Manager.
        /// </summary>
        private void ReadMovementInput()
        {
            Vector2 rawInput = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var keyboard = Keyboard.current;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) rawInput.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) rawInput.y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) rawInput.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) rawInput.x += 1f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (rawInput == Vector2.zero)
            {
                rawInput.x = Input.GetAxisRaw("Horizontal");
                rawInput.y = Input.GetAxisRaw("Vertical");
            }
#endif

            // Chuẩn hóa vector hướng nếu có input để tốc độ 8 hướng luôn đồng đều
            moveInput = rawInput.sqrMagnitude > 0.0001f ? rawInput.normalized : Vector2.zero;
        }

        /// <summary>
        /// Xử lý di chuyển bằng Rigidbody trong FixedUpdate tương thích Unity 6.
        /// Sử dụng rb.linearVelocity và giữ nguyên vận tốc trục Y do trọng lực.
        /// </summary>
        private void ApplyMovement()
        {
            // Nếu bị khóa di chuyển (ví dụ: đang lướt Dash), không ghi đè vận tốc vật lý
            if (!CanMove) return;

            // Chuyển đổi input 2D (X, Y) thành vector hướng thế giới 3D (X, Z)
            Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 horizontalVelocity = moveDirection * moveSpeed;

#if UNITY_6000_0_OR_NEWER
            // Unity 6: rb.linearVelocity thay thế cho rb.velocity đã deprecated
            rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
#else
            rb.velocity = new Vector3(horizontalVelocity.x, rb.velocity.y, horizontalVelocity.z);
#endif
        }

        /// <summary>
        /// Bắn Raycast từ Main Camera qua vị trí chuột xuống mặt phẳng Plane(Vector3.up, Vector3.zero).
        /// Tính vector hướng từ nhân vật tới điểm giao nhau và chỉ xoay quanh trục Y (giữ nguyên X = 0, Z = 0).
        /// </summary>
        private void HandleMouseLook()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Vector2 mouseScreenPosition = GetMouseScreenPosition();
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPosition);

            if (groundPlane.Raycast(ray, out float enterDistance))
            {
                Vector3 hitPoint = ray.GetPoint(enterDistance);
                currentAimPoint = hitPoint;

                Vector3 lookDirection = hitPoint - transform.position;
                lookDirection.y = 0f; // Triệt tiêu thành phần Y để chỉ xoay quanh trục Y

                if (lookDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

                    if (smoothRotation)
                    {
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            targetRotation,
                            rotationSpeed * Time.deltaTime
                        );
                    }
                    else
                    {
                        transform.rotation = targetRotation;
                    }
                }
            }
        }

        /// <summary>
        /// Đọc vị trí con trỏ chuột trên màn hình theo pixel.
        /// </summary>
        private Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#endif

            return Vector2.zero;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Vẽ điểm ngắm và đường nối trong Scene view khi đối tượng được chọn
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentAimPoint, 0.2f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentAimPoint);
        }
#endif
    }
}
