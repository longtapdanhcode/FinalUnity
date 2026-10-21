using System;
using System.Collections;
using UnityEngine;
using Vanguard.Combat;

namespace Vanguard.Player
{
    /// <summary>
    /// Quản lý lượng máu, nhận sát thương, hiệu ứng đẩy lùi (Knockback) và trạng thái tử vong của Player.
    /// Tương tác với PlayerDash (i-Frames) và PlayerController.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour, IDamageable, IKnockbackable
    {
        [Header("Health Settings")]
        [Tooltip("Lượng máu tối đa ban đầu theo GDD")]
        [SerializeField] private float maxHealth = 100f;

        // Trạng thái runtime
        private float currentHealth;
        private bool isDead;

        // Cached components
        private Rigidbody rb;
        private PlayerController playerController;
        private PlayerDash playerDash;

        // Coroutine quản lý knockback để tránh trùng lặp và rác bộ nhớ
        private Coroutine knockbackCoroutine;
        private readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        #region Properties
        /// <summary>
        /// Lượng máu hiện tại.
        /// </summary>
        public float CurrentHealth => currentHealth;

        /// <summary>
        /// Lượng máu tối đa hiện tại (có thể tăng qua thẻ bài nâng cấp).
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// Tỷ lệ phần trăm máu (0.0 đến 1.0) phục vụ UI thanh máu.
        /// </summary>
        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        /// <summary>
        /// Cờ trạng thái đã tử vong (IDamageable).
        /// </summary>
        public bool IsDead => isDead;
        #endregion

        #region Events
        /// <summary>
        /// Kích hoạt khi máu thay đổi (currentHealth, maxHealth) phục vụ HUD UI.
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Kích hoạt khi nhận sát thương thành công (damageTaken) phục vụ hiệu ứng rung màn hình, âm thanh, flash đỏ.
        /// </summary>
        public event Action<float> OnDamaged;

        /// <summary>
        /// Kích hoạt khi đòn đánh bị chặn nhờ i-Frames của PlayerDash.
        /// </summary>
        public event Action OnDamageBlocked;

        /// <summary>
        /// Kích hoạt một lần khi lượng máu tụt về 0.
        /// </summary>
        public event Action OnPlayerDeath;
        #endregion

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();
            playerDash = GetComponent<PlayerDash>();

            currentHealth = maxHealth;
            isDead = false;
        }

        private void Start()
        {
            // Bắn event khởi tạo để UI thanh máu HUD cập nhật ngay khi vào game
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void OnDisable()
        {
            if (knockbackCoroutine != null)
            {
                StopCoroutine(knockbackCoroutine);
                knockbackCoroutine = null;
            }
        }

        #region IDamageable Implementation
        /// <summary>
        /// Nhận sát thương từ nguồn tấn công.
        /// Tự động kiểm tra cờ i-Frames của PlayerDash để miễn nhiễm sát thương.
        /// </summary>
        public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (isDead || damageAmount <= 0f) return;

            // Kiểm tra trạng thái bất tử (i-Frames) khi đang lướt Dash
            if (playerDash != null && playerDash.IsInvulnerable)
            {
                Debug.Log("[PlayerHealth] Sát thương bị chặn hoàn toàn bởi i-Frames của Dash!");
                OnDamageBlocked?.Invoke();
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
            OnDamaged?.Invoke(damageAmount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }
        #endregion

        #region IKnockbackable Implementation
        /// <summary>
        /// Áp dụng lực đẩy lùi lên Player thông qua Rigidbody.
        /// Miễn nhiễm knockback nếu đang trong trạng thái i-Frames của Dash.
        /// </summary>
        public void ApplyKnockback(Vector3 forceDirection, float forceMagnitude, float duration)
        {
            if (isDead || forceMagnitude <= 0f || duration <= 0f) return;

            // Bỏ qua đẩy lùi nếu đang có i-Frames
            if (playerDash != null && playerDash.IsInvulnerable) return;

            if (knockbackCoroutine != null)
            {
                StopCoroutine(knockbackCoroutine);
            }

            knockbackCoroutine = StartCoroutine(KnockbackRoutine(forceDirection, forceMagnitude, duration));
        }

        private IEnumerator KnockbackRoutine(Vector3 forceDirection, float forceMagnitude, float duration)
        {
            // Tạm thời khóa di chuyển thường của người chơi trong lúc bị giật lùi
            if (playerController != null)
            {
                playerController.CanMove = false;
            }

            Vector3 direction = forceDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.fixedDeltaTime;
                float currentSpeed = Mathf.Lerp(forceMagnitude, 0f, elapsed / duration);
                Vector3 knockbackVelocity = direction * currentSpeed;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = new Vector3(knockbackVelocity.x, rb.linearVelocity.y, knockbackVelocity.z);
#else
                rb.velocity = new Vector3(knockbackVelocity.x, rb.velocity.y, knockbackVelocity.z);
#endif
                yield return waitForFixedUpdate;
            }

            // Trả lại quyền di chuyển nếu người chơi còn sống và không đang trong cú dash
            if (playerController != null && (playerDash == null || !playerDash.IsDashing) && !isDead)
            {
                playerController.CanMove = true;
            }

            knockbackCoroutine = null;
        }
        #endregion

        #region Roguelite Card Upgrade Methods
        /// <summary>
        /// Hồi máu trực tiếp cho Player (không vượt quá maxHealth).
        /// </summary>
        /// <param name="healAmount">Lượng máu hồi phục.</param>
        public void Heal(float healAmount)
        {
            if (isDead || healAmount <= 0f) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Tăng lượng máu tối đa (dùng cho các thẻ bài nâng cấp / buff Roguelite).
        /// </summary>
        /// <param name="additionalMaxHealth">Lượng máu tối đa cộng thêm.</param>
        /// <param name="healCurrent">Có hồi lượng máu hiện tại tương ứng hay không.</param>
        public void IncreaseMaxHealth(float additionalMaxHealth, bool healCurrent = false)
        {
            if (additionalMaxHealth <= 0f) return;

            maxHealth += additionalMaxHealth;

            if (healCurrent && !isDead)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + additionalMaxHealth);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
        #endregion

        /// <summary>
        /// Xử lý khi máu về 0: Đánh dấu tử vong, khóa điều khiển và thông báo sự kiện.
        /// </summary>
        private void Die()
        {
            if (isDead) return;

            isDead = true;

            // Khóa di chuyển của Player
            if (playerController != null)
            {
                playerController.CanMove = false;
            }

            // Dừng vận tốc ngang
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
#else
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
#endif

            Debug.Log("[PlayerHealth] Player đã tử vong.");
            OnPlayerDeath?.Invoke();
        }
    }
}
