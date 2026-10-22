using System.Collections.Generic;
using UnityEngine;
using Vanguard.Combat;

namespace Vanguard.Environment
{
    /// <summary>
    /// Quản lý bẫy lưỡi xoay tử thần tại tâm võ đài (Sprint 3 - Card 3.1).
    /// Xoay liên tục quanh trục Y, gây sát thương và đẩy văng mọi mục tiêu (Player & Enemy).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CenterRotatingTrap : MonoBehaviour
    {
        [Header("Trap Configuration")]
        [Tooltip("Tốc độ xoay quanh trục Y (độ/giây)")]
        [SerializeField] private float rotationSpeed = 90f;

        [Tooltip("Sát thương va quẹt (DMG)")]
        [SerializeField] private float damage = 20f;

        [Tooltip("Lực đẩy văng ra xa")]
        [SerializeField] private float knockbackForce = 12f;

        [Tooltip("Thời gian hiệu lực đẩy lùi (giây)")]
        [SerializeField] private float knockbackDuration = 0.25f;

        [Tooltip("Thời gian hồi sát thương trên cùng 1 mục tiêu (giây)")]
        [SerializeField] private float hitCooldown = 0.6f;

        [Header("Activation Settings")]
        [Tooltip("Mặc định bật để test, có hàm kích hoạt theo Wave")]
        [SerializeField] private bool isActive = true;

        [Tooltip("Tự kích hoạt từ Wave chỉ định trở đi")]
        [SerializeField] private int activateFromWave = 8;

        [Header("Optional Component References")]
        [Tooltip("Danh sách Collider của lưỡi bẫy (tự tìm kiếm nếu để trống)")]
        [SerializeField] private Collider[] bladeColliders = System.Array.Empty<Collider>();

        [Tooltip("Danh sách Visual GameObjects của lưỡi bẫy (tự tìm kiếm nếu để trống)")]
        [SerializeField] private GameObject[] bladeVisuals = System.Array.Empty<GameObject>();

        // Bộ nhớ đệm kiểm soát cooldown từng Collider riêng biệt (0 GC Alloc)
        private readonly Dictionary<Collider, float> lastHitTime = new Dictionary<Collider, float>(32);

        private Rigidbody rb;

        /// <summary>Phát khi bẫy gây sát thương lên một mục tiêu (dùng cho VFX); tham số là vị trí mục tiêu.</summary>
        public event System.Action<Vector3> OnTargetHit;

        #region Properties
        public bool IsActive => isActive;
        public float RotationSpeed => rotationSpeed;
        public float Damage => damage;
        public float KnockbackForce => knockbackForce;
        public float KnockbackDuration => knockbackDuration;
        public float HitCooldown => hitCooldown;
        public int ActivateFromWave => activateFromWave;
        #endregion

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Tự động tìm kiếm các Collider lưỡi dao ở đối tượng con nếu chưa gán
            if (bladeColliders == null || bladeColliders.Length == 0)
            {
                bladeColliders = GetComponentsInChildren<Collider>(true);
            }

            // Tự động tìm kiếm visual con nếu chưa gán
            if (bladeVisuals == null || bladeVisuals.Length == 0)
            {
                bladeVisuals = new GameObject[transform.childCount];
                for (int i = 0; i < transform.childCount; i++)
                {
                    bladeVisuals[i] = transform.GetChild(i).gameObject;
                }
            }

            // Áp dụng trạng thái ban đầu
            SetTrapActive(isActive);
        }

        private void Update()
        {
            if (!isActive) return;

            // Xoay đều quanh trục Y
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            ProcessHit(other);
        }

        /// <summary>
        /// Xử lý va chạm quét mục tiêu mang IDamageable và áp dụng sát thương/đẩy lùi theo cooldown.
        /// </summary>
        /// <param name="other">Collider đối tượng va chạm với lưỡi bẫy.</param>
        private void ProcessHit(Collider other)
        {
            if (!isActive || other == null) return;

            // Bỏ qua va chạm với chính các bộ phận thuộc bẫy
            if (other.transform.root == transform.root) return;

            // Kiểm tra xem đối tượng có nhận sát thương hay không
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            // Kiểm tra Cooldown độc lập cho từng mục tiêu (0 GC Alloc)
            float currentTime = Time.time;
            if (lastHitTime.TryGetValue(other, out float lastTime))
            {
                if (currentTime - lastTime < hitCooldown)
                {
                    return;
                }
            }

            // Cập nhật mốc thời gian chém trúng gần nhất
            lastHitTime[other] = currentTime;

            // Tính vector đẩy lùi: hướng từ tâm bẫy (0, 0, 0) văng ra ngoài theo mặt phẳng phẳng nằm ngang (flatDir.y = 0)
            Vector3 knockbackDir = other.transform.position - transform.position;
            knockbackDir.y = 0f;
            if (knockbackDir.sqrMagnitude > 0.0001f)
            {
                knockbackDir.Normalize();
            }
            else
            {
                // Fallback nếu mục tiêu đứng trùng tâm tuyệt đối
                knockbackDir = transform.forward;
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude > 0.0001f)
                {
                    knockbackDir.Normalize();
                }
                else
                {
                    knockbackDir = Vector3.forward;
                }
            }

            // 1. Gây sát thương
            damageable.TakeDamage(damage, other.transform.position, knockbackDir);

            // 2. Áp dụng lực đẩy lùi nếu đối tượng hỗ trợ IKnockbackable
            IKnockbackable knockbackable = other.GetComponentInParent<IKnockbackable>();
            if (knockbackable != null)
            {
                knockbackable.ApplyKnockback(knockbackDir, knockbackForce, knockbackDuration);
            }

            OnTargetHit?.Invoke(other.transform.position);

            // 3. Ghi log chuẩn theo yêu cầu GDD
            Debug.Log($"[CenterRotatingTrap] Bẫy chém trúng {other.name}! Gây {damage} DMG và đẩy lùi.");
        }

        /// <summary>
        /// Bật/tắt trạng thái hoạt động của bẫy.
        /// Khi tắt: dừng xoay, tắt collider hoặc ẩn lưỡi dao.
        /// </summary>
        /// <param name="active">Trạng thái kích hoạt mới.</param>
        public void SetTrapActive(bool active)
        {
            isActive = active;

            // Bật/tắt các collider để không nhận va chạm khi tắt
            if (bladeColliders != null)
            {
                for (int i = 0; i < bladeColliders.Length; i++)
                {
                    if (bladeColliders[i] != null)
                    {
                        bladeColliders[i].enabled = active;
                    }
                }
            }

            // Bật/tắt visuals nếu được cấu hình
            if (bladeVisuals != null)
            {
                for (int i = 0; i < bladeVisuals.Length; i++)
                {
                    if (bladeVisuals[i] != null)
                    {
                        bladeVisuals[i].SetActive(active);
                    }
                }
            }
        }

        /// <summary>
        /// Nhận sự kiện chuyển Wave từ hệ thống WaveManager.
        /// Tự kích hoạt từ activateFromWave trở đi.
        /// </summary>
        /// <param name="currentWave">Chỉ số wave hiện tại.</param>
        public void OnWaveChanged(int currentWave)
        {
            if (currentWave >= activateFromWave)
            {
                SetTrapActive(true);
            }
            else
            {
                SetTrapActive(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActive ? new Color(1f, 0.3f, 0f, 0.4f) : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 6f);
        }
    }
}
