using System;
using UnityEngine;
using Vanguard.Juice;
using Vanguard.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vanguard.Combat
{
    /// <summary>
    /// Hệ thống tấn công cận chiến chuỗi 3 đòn (3-Hit Combo) bằng Chuột trái.
    /// Hỗ trợ Input Buffer, Combo Reset Window, quét Hitbox tối ưu 0 GC Alloc và tương thích Unity 6.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [System.Serializable]
        public struct AttackConfig
        {
            [Tooltip("Lượng sát thương gây ra")]
            public float damage;

            [Tooltip("Độ lớn lực đẩy lùi (vận tốc ban đầu)")]
            public float knockbackForce;

            [Tooltip("Thời gian đẩy lùi kéo dài (giây)")]
            public float knockbackDuration;

            [Tooltip("Thời gian làm choáng (giây, chỉ áp dụng cho đòn kết liễu)")]
            public float stunDuration;

            [Tooltip("Thời gian vung đòn (giây) trước khi có thể kết thúc hoặc chuyển đòn")]
            public float attackDuration;
        }

        [Header("Combo Configurations (Đòn 1, 2, 3)")]
        [SerializeField]
        private AttackConfig attack1 = new AttackConfig
        {
            damage = 20f,
            knockbackForce = 15f,     // ~1.5m knockback qua 0.2s
            knockbackDuration = 0.2f,
            stunDuration = 0f,
            attackDuration = 0.22f
        };

        [SerializeField]
        private AttackConfig attack2 = new AttackConfig
        {
            damage = 25f,
            knockbackForce = 15f,     // ~1.5m knockback qua 0.2s
            knockbackDuration = 0.2f,
            stunDuration = 0f,
            attackDuration = 0.22f
        };

        [SerializeField]
        private AttackConfig attack3 = new AttackConfig
        {
            damage = 40f,
            knockbackForce = 32f,     // ~4.0m knockback qua 0.25s
            knockbackDuration = 0.25f,
            stunDuration = 0.3f,      // Làm choáng 0.3s
            attackDuration = 0.3f
        };

        [Header("Combo Window & Timing")]
        [Tooltip("Khoảng thời gian tối đa để tiếp tục chuỗi combo trước khi reset về đòn 1 (0.8s)")]
        [SerializeField] private float comboResetWindow = 0.8f;

        [Tooltip("Thời gian hiệu lực của bộ đệm input (Input Buffer)")]
        [SerializeField] private float inputBufferWindow = 0.3f;

        [Tooltip("Khóa di chuyển thường của PlayerController trong khoảnh khắc vung kiếm")]
        [SerializeField] private bool lockMovementDuringAttack = true;

        [Header("Hitbox Detection")]
        [Tooltip("Điểm phát hitbox trước mặt nhân vật. Nếu để trống sẽ tự tính theo attackOffsetZ")]
        [SerializeField] private Transform attackPoint = null;

        [Tooltip("Khoảng cách offset trục Z trước mặt nhân vật nếu attackPoint là null")]
        [SerializeField] private float attackOffsetZ = 1.2f;

        [Tooltip("Bán kính hình cầu quét hitbox (mặc định 1.5m)")]
        [SerializeField] private float attackRadius = 1.5f;

        [Tooltip("LayerMask quét các đối tượng kẻ địch")]
        [SerializeField] private LayerMask enemyLayer;

        // Cached components
        private PlayerController playerController;
        private PlayerDash playerDash;
        private PlayerHealth playerHealth;

        // Runtime states
        private int currentComboStep; // 0: Idle, 1: Attack 1, 2: Attack 2, 3: Attack 3
        private bool isAttacking;
        private float attackTimer;
        private float currentAttackDuration;
        private float comboResetTimer;

        // Input buffer states
        private bool inputBuffered;
        private float bufferTimer;

        // Bộ nhớ đệm cố định cho Physics.OverlapSphereNonAlloc (0 GC Alloc)
        private const int MaxHitTargets = 16;
        private readonly Collider[] hitColliders = new Collider[MaxHitTargets];
        private readonly Transform[] hitRoots = new Transform[MaxHitTargets];

        #region Properties & Events
        /// <summary>
        /// Hệ số nhân sát thương đòn chém (1 = mặc định). Dùng cho thẻ bài nâng cấp Roguelite.
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>
        /// Hệ số tốc độ đánh (1 = mặc định, 1.2 = nhanh hơn 20%). Dùng cho thẻ bài nâng cấp Roguelite.
        /// </summary>
        public float AttackSpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// Hệ số nhân bán kính quét đòn chém (1 = mặc định). Dùng cho thẻ bài nâng cấp Roguelite.
        /// </summary>
        public float AttackRadiusMultiplier { get; set; } = 1f;

        /// <summary>
        /// Tỉ lệ chí mạng của đòn chém (0 - 1). Dùng cho thẻ bài nâng cấp Roguelite.
        /// </summary>
        public float CritChance { get; set; } = 0f;

        /// <summary>
        /// Hệ số sát thương khi chí mạng (mặc định x2).
        /// </summary>
        public float CritMultiplier { get; set; } = 2f;

        /// <summary>
        /// Bước combo hiện tại (0: Idle, 1: Đòn 1, 2: Đòn 2, 3: Đòn 3).
        /// </summary>
        public int CurrentComboStep => currentComboStep;

        /// <summary>
        /// Nhân vật có đang trong trạng thái vung đòn hay không.
        /// </summary>
        public bool IsAttacking => isAttacking;

        /// <summary>
        /// Vị trí trung tâm của hitbox chém.
        /// </summary>
        public Vector3 AttackPosition => attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * attackOffsetZ;

        /// <summary>
        /// Kích hoạt khi bắt đầu vung đòn (comboStep: 1, 2, 3) phục vụ tích Nộ (Card 1.5) và Animation/Audio.
        /// </summary>
        public event Action<int> OnAttackPerformed;

        /// <summary>
        /// Kích hoạt khi đòn đánh trúng mục tiêu (comboStep, enemyCollider) phục vụ VFX/Game Feel (Sprint 5).
        /// </summary>
        public event Action<int, Collider> OnHitLanded;
        #endregion

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerDash = GetComponent<PlayerDash>();
            playerHealth = GetComponent<PlayerHealth>();

            // Nếu enemyLayer chưa được gán trong Inspector, tự động tìm Layer "Enemy"
            if (enemyLayer.value == 0)
            {
                enemyLayer = LayerMask.GetMask("Enemy");
                if (enemyLayer.value == 0)
                {
                    // Fallback sang layer 6 (Layer Enemy mặc định)
                    enemyLayer = 1 << 6;
                }
            }
        }

        private void Start()
        {
            Debug.Log("[PlayerCombat] Khởi tạo thành công. Sẵn sàng chiến đấu (Chuột trái để tung combo)!");
        }

        private void OnDisable()
        {
            if (isAttacking)
            {
                EndCurrentAttack();
            }
            currentComboStep = 0;
            inputBuffered = false;
        }

        private void Update()
        {
            // Nếu người chơi đã chết thì hủy bỏ mọi hành động tấn công
            if (playerHealth != null && playerHealth.IsDead)
            {
                if (isAttacking) EndCurrentAttack();
                return;
            }

            HandleInput();
            UpdateAttackProgress();
            UpdateComboWindow();
        }

        /// <summary>
        /// Lắng nghe input Chuột trái và xử lý Input Buffer.
        /// </summary>
        private void HandleInput()
        {
            bool attackPressed = ReadAttackInput();

            if (attackPressed)
            {
                // Không tấn công khi đang lướt Dash (tránh đè vận tốc Dash)
                if (playerDash != null && playerDash.IsDashing)
                {
                    return;
                }

                if (isAttacking)
                {
                    // Đang vung đòn: Ghi nhận vào bộ đệm (Input Buffer)
                    inputBuffered = true;
                    bufferTimer = inputBufferWindow;
                }
                else
                {
                    // Không vung đòn: Ra đòn ngay lập tức
                    PerformNextComboStrike();
                }
            }

            // Đếm lùi thời gian sống của input trong bộ đệm
            if (inputBuffered)
            {
                bufferTimer -= Time.deltaTime;
                if (bufferTimer <= 0f)
                {
                    inputBuffered = false;
                }
            }
        }

        /// <summary>
        /// Cập nhật thời gian vung đòn và chuyển tiếp combo nếu có input đệm.
        /// </summary>
        private void UpdateAttackProgress()
        {
            if (!isAttacking) return;

            attackTimer += Time.deltaTime;

            if (attackTimer >= currentAttackDuration)
            {
                EndCurrentAttack();

                // Nếu có input đệm hợp lệ, vung đòn kế tiếp liền mạch
                if (inputBuffered)
                {
                    inputBuffered = false;
                    PerformNextComboStrike();
                }
                else
                {
                    // Nếu là đòn 3 kết thúc, reset combo ngay lập tức
                    if (currentComboStep >= 3)
                    {
                        currentComboStep = 0;
                        comboResetTimer = 0f;
                        Debug.Log("[Combat] Combo đã reset về đòn 1");
                    }
                    else
                    {
                        // Kích hoạt cửa sổ đếm ngược combo reset (0.8s)
                        comboResetTimer = comboResetWindow;
                    }
                }
            }
        }

        /// <summary>
        /// Cửa sổ duy trì chuỗi combo: sau 0.8s không đánh tiếp sẽ tự động reset về đòn 1.
        /// </summary>
        private void UpdateComboWindow()
        {
            if (isAttacking || currentComboStep == 0) return;

            if (comboResetTimer > 0f)
            {
                comboResetTimer -= Time.deltaTime;
                if (comboResetTimer <= 0f)
                {
                    currentComboStep = 0;
                    Debug.Log("[Combat] Combo đã reset về đòn 1");
                }
            }
        }

        /// <summary>
        /// Thực hiện bước kế tiếp trong chuỗi 3 đòn.
        /// </summary>
        private void PerformNextComboStrike()
        {
            // Tăng bước combo (từ 0 -> 1, 1 -> 2, 2 -> 3, hoặc 3 -> 1)
            currentComboStep++;
            if (currentComboStep > 3)
            {
                currentComboStep = 1;
            }

            int currentComboIndex = currentComboStep - 1;
            AttackConfig currentAttack = GetAttackConfig(currentComboStep);
            float currentDamage = currentAttack.damage * DamageMultiplier;

            currentAttackDuration = currentAttack.attackDuration / Mathf.Max(0.1f, AttackSpeedMultiplier);
            attackTimer = 0f;
            isAttacking = true;
            comboResetTimer = 0f;

            // In log trực quan khi người chơi vung đòn
            Debug.Log($"[Combat] Tung đòn combo {currentComboIndex + 1}! (Damage: {currentDamage})");

            // Đòn kết combo (Finisher): rung màn hình tạo độ uy lực
            if (currentComboStep == 3)
            {
                JuiceManager.Shake(0.35f, 0.2f);
            }

            // Khóa di chuyển thường của PlayerController trong lúc vung đòn
            if (lockMovementDuringAttack && playerController != null)
            {
                playerController.CanMove = false;
            }

            // Quét Hitbox và gây sát thương/hiệu ứng tức thời
            ExecuteHitDetection(currentAttack);

            // Bắn event thực hiện đòn đánh
            OnAttackPerformed?.Invoke(currentComboStep);
        }

        /// <summary>
        /// Kết thúc đòn đánh hiện tại và mở khóa di chuyển nếu an toàn.
        /// </summary>
        private void EndCurrentAttack()
        {
            isAttacking = false;

            // Mở khóa di chuyển nếu người chơi còn sống và không đang trong cú lướt
            if (lockMovementDuringAttack && playerController != null)
            {
                if ((playerHealth == null || !playerHealth.IsDead) &&
                    (playerDash == null || !playerDash.IsDashing))
                {
                    playerController.CanMove = true;
                }
            }
        }

        /// <summary>
        /// Quét Hitbox bằng Physics.OverlapSphereNonAlloc để đạt 0 GC Alloc.
        /// </summary>
        private void ExecuteHitDetection(AttackConfig attack)
        {
            Vector3 center = AttackPosition;
            int hitCount = Physics.OverlapSphereNonAlloc(center, attackRadius * AttackRadiusMultiplier, hitColliders, enemyLayer);
            int hitRootsCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider targetCollider = hitColliders[i];
                if (targetCollider == null) continue;

                // Không tự đánh trúng bản thân
                if (targetCollider.transform.root == transform.root) continue;

                // Tránh xử lý nhiều lần cho cùng một thực thể trong 1 lần vung đòn (nếu có nhiều collider con)
                Transform root = targetCollider.transform.root;
                bool alreadyHit = false;
                for (int r = 0; r < hitRootsCount; r++)
                {
                    if (hitRoots[r] == root)
                    {
                        alreadyHit = true;
                        break;
                    }
                }

                if (alreadyHit) continue;
                if (hitRootsCount < MaxHitTargets)
                {
                    hitRoots[hitRootsCount++] = root;
                }

                // In log trực quan khi chém trúng Collider
                Debug.Log($"[Combat] Chém trúng: {targetCollider.name}");

                Vector3 hitDirection = targetCollider.transform.position - transform.position;
                hitDirection.y = 0f;
                if (hitDirection.sqrMagnitude > 0.0001f)
                {
                    hitDirection.Normalize();
                }
                else
                {
                    hitDirection = transform.forward;
                }

                Vector3 hitPoint = targetCollider.ClosestPoint(center);

                // Gây sát thương nếu đối tượng có component IDamageable
                IDamageable damageable = targetCollider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    float finalDamage = attack.damage * DamageMultiplier;
                    if (CritChance > 0f && UnityEngine.Random.value < CritChance)
                    {
                        finalDamage *= CritMultiplier;
                        Debug.Log($"[Combat] CHÍ MẠNG! {finalDamage} DMG");
                    }

                    damageable.TakeDamage(finalDamage, hitPoint, hitDirection);
                }

                // Áp dụng lực đẩy lùi qua IKnockbackable
                IKnockbackable knockbackable = targetCollider.GetComponentInParent<IKnockbackable>();
                if (knockbackable != null)
                {
                    knockbackable.ApplyKnockback(hitDirection, attack.knockbackForce, attack.knockbackDuration);
                }

                // Áp dụng làm choáng qua IStunnable (chỉ có ở đòn 3)
                if (attack.stunDuration > 0f)
                {
                    IStunnable stunnable = targetCollider.GetComponentInParent<IStunnable>();
                    if (stunnable != null)
                    {
                        stunnable.ApplyStun(attack.stunDuration);
                    }
                }

                // LUÔN ĐƯỢC GỌI mỗi khi chém trúng Collider thuộc Layer Enemy (kể cả khi chưa có IDamageable)
                OnHitLanded?.Invoke(currentComboStep, targetCollider);

                // Khựng hình ngắn khi chém trúng để tạo cảm giác đầm tay
                JuiceManager.HitStop(0.06f);
            }
        }

        /// <summary>
        /// Lấy cấu hình chỉ số tương ứng theo bước combo.
        /// </summary>
        private AttackConfig GetAttackConfig(int step)
        {
            switch (step)
            {
                case 1: return attack1;
                case 2: return attack2;
                case 3: return attack3;
                default: return attack1;
            }
        }

        /// <summary>
        /// Đọc input Chuột trái (Mouse 0).
        /// Hỗ trợ cả New Input System và Legacy Input.
        /// </summary>
        private bool ReadAttackInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
#endif

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Vẽ hình cầu dây màu đỏ biểu diễn vùng chém trong Scene view
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AttackPosition, attackRadius * AttackRadiusMultiplier);

            // Vẽ điểm tâm phát hitbox
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(AttackPosition, 0.08f);
        }
#endif
    }
}
