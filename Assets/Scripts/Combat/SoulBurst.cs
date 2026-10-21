using System;
using System.Collections;
using UnityEngine;
using Vanguard.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vanguard.Combat
{
    /// <summary>
    /// Kỹ năng Nộ tối thượng: Soul Burst (Phím Q).
    /// Quản lý tích Nộ qua các đòn đánh trúng đích và giải phóng sóng xung kích Hư Vô diện rộng (AOE) kèm hiệu ứng trực quan (VFX).
    /// Hỗ trợ phím T để debug nạp đầy nộ tức thì.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class SoulBurst : MonoBehaviour
    {
        [Header("Fury Settings")]
        [Tooltip("Lượng nộ tối đa cần để kích hoạt Soul Burst")]
        [SerializeField] private float maxFury = 100f;

        [Tooltip("Lượng nộ tích được cho mỗi đòn đánh trúng kẻ địch")]
        [SerializeField] private float furyPerHit = 10f;

        [Header("Ultimate Stats")]
        [Tooltip("Sát thương diện rộng của Soul Burst")]
        [SerializeField] private float damage = 120f;

        [Tooltip("Bán kính phát sóng xung kích (mét)")]
        [SerializeField] private float blastRadius = 6.0f;

        [Tooltip("Độ lớn lực hất văng (knockback) cực mạnh")]
        [SerializeField] private float knockbackMagnitude = 35f;

        [Tooltip("Thời gian hất văng kéo dài (giây)")]
        [SerializeField] private float knockbackDuration = 0.35f;

        [Header("Visual Effects (VFX)")]
        [Tooltip("ParticleSystem hiệu ứng sóng xung kích. Nếu để trống sẽ tự động tạo procedural shockwave ring")]
        [SerializeField] private ParticleSystem shockwaveVFX = null;

        [Header("Layer Detection")]
        [Tooltip("LayerMask quét các mục tiêu kẻ địch")]
        [SerializeField] private LayerMask enemyLayer;

        // Cached components
        private PlayerCombat playerCombat;
        private PlayerHealth playerHealth;

        // Runtime states
        private float currentFury;

        // Bộ nhớ đệm cố định cho Physics.OverlapSphereNonAlloc (0 GC Alloc)
        private const int MaxTargets = 32;
        private readonly Collider[] hitColliders = new Collider[MaxTargets];
        private readonly IDamageable[] damagedEntities = new IDamageable[MaxTargets];

        #region Properties
        /// <summary>
        /// Lượng Nộ hiện tại.
        /// </summary>
        public float CurrentFury => currentFury;

        /// <summary>
        /// Lượng Nộ tối đa.
        /// </summary>
        public float MaxFury => maxFury;

        /// <summary>
        /// Tỷ lệ phần trăm thanh Nộ (từ 0.0 đến 1.0) phục vụ UI HUD.
        /// </summary>
        public float FuryNormalized => maxFury > 0f ? Mathf.Clamp01(currentFury / maxFury) : 0f;

        /// <summary>
        /// Trạng thái thanh Nộ đã đầy 100% hay chưa.
        /// </summary>
        public bool IsFuryFull => currentFury >= maxFury;
        #endregion

        #region Events
        /// <summary>
        /// Kích hoạt khi lượng Nộ thay đổi (currentFury, maxFury) phục vụ thanh UI HUD.
        /// </summary>
        public event Action<float, float> OnFuryChanged;

        /// <summary>
        /// Kích hoạt khi thanh Nộ đạt 100% sẵn sàng kích hoạt Ultimate.
        /// </summary>
        public event Action OnFuryFull;

        /// <summary>
        /// Kích hoạt khi phóng sóng xung kích Soul Burst phục vụ VFX nổ và Camera Shake (Sprint 5).
        /// </summary>
        public event Action OnSoulBurstFired;
        #endregion

        private void Awake()
        {
            if (playerCombat == null)
            {
                playerCombat = GetComponent<PlayerCombat>() ?? GetComponentInParent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>();
            }
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? GetComponentInChildren<PlayerHealth>();
            }

            currentFury = 0f;

            // Tự động tìm Layer "Enemy" nếu chưa được cấu hình trong Inspector
            if (enemyLayer.value == 0)
            {
                enemyLayer = LayerMask.GetMask("Enemy");
                if (enemyLayer.value == 0)
                {
                    enemyLayer = 1 << 6;
                }
            }
        }

        private void OnEnable()
        {
            if (playerCombat == null)
            {
                playerCombat = GetComponent<PlayerCombat>() ?? GetComponentInParent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>();
            }

            if (playerCombat != null)
            {
                // Tránh đăng ký sự kiện trùng lặp
                playerCombat.OnHitLanded -= HandleHitLanded;
                playerCombat.OnHitLanded += HandleHitLanded;
            }
        }

        private void OnDisable()
        {
            if (playerCombat != null)
            {
                playerCombat.OnHitLanded -= HandleHitLanded;
            }
        }

        private void Start()
        {
            // Bắn event khởi tạo để UI đồng bộ thanh Nộ lúc bắt đầu
            OnFuryChanged?.Invoke(currentFury, maxFury);
        }

        private void Update()
        {
            // Phím Debug T: Bơm ngay 100% Nộ
            if (ReadDebugFuryInput())
            {
                currentFury = maxFury;
                OnFuryChanged?.Invoke(currentFury, maxFury);
                Debug.Log("[SoulBurst Debug] Đã nạp đầy 100% Nộ bằng phím T!");
                OnFuryFull?.Invoke();
            }

            // Không kích hoạt chiêu nếu người chơi đã chết
            if (playerHealth != null && playerHealth.IsDead) return;

            if (ReadUltimateInput())
            {
                TryActivateSoulBurst();
            }
        }

        /// <summary>
        /// Lắng nghe sự kiện chém trúng quái từ PlayerCombat để tích Nộ.
        /// </summary>
        private void HandleHitLanded(int comboStep, Collider enemyCollider)
        {
            AddFury(furyPerHit);
            Debug.Log($"[SoulBurst] Tích +{furyPerHit} Nộ! Hiện tại: {currentFury}/{maxFury} ({(FuryNormalized * 100f):F0}%)");
        }

        /// <summary>
        /// Tích lũy Nộ (giới hạn tối đa maxFury).
        /// </summary>
        /// <param name="amount">Lượng nộ cộng thêm.</param>
        public void AddFury(float amount)
        {
            if (amount <= 0f) return;

            bool wasFull = IsFuryFull;
            currentFury = Mathf.Min(maxFury, currentFury + amount);

            OnFuryChanged?.Invoke(currentFury, maxFury);

            // Bắn event và thông báo khi nộ vừa chạm mốc 100%
            if (!wasFull && IsFuryFull)
            {
                Debug.Log("[Ultimate] Nộ đã đầy 100%! Sẵn sàng kích hoạt Soul Burst (Phím Q)!");
                OnFuryFull?.Invoke();
            }
        }

        /// <summary>
        /// Thử kích hoạt Soul Burst nếu Nộ đã đầy 100%.
        /// </summary>
        public bool TryActivateSoulBurst()
        {
            if (!IsFuryFull)
            {
                Debug.Log($"[Ultimate] Chưa đủ Nộ để kích hoạt Soul Burst! ({currentFury:F0}/{maxFury:F0})");
                return false;
            }

            ExecuteSoulBurst();
            return true;
        }

        /// <summary>
        /// Giải phóng sóng xung kích Soul Burst: tiêu hao nộ, tạo hiệu ứng VFX, gây 120 DMG và knockback 35f trong bán kính 6m.
        /// </summary>
        private void ExecuteSoulBurst()
        {
            // Tiêu hao 100% nộ
            currentFury = 0f;
            OnFuryChanged?.Invoke(currentFury, maxFury);

            Debug.Log("[Ultimate] Kích hoạt Soul Burst - Sóng xung kích Hư Vô!");

            // Kích hoạt hiệu ứng sóng xung kích trực quan (VFX)
            PlayShockwaveVFX();

            Vector3 origin = transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(origin, blastRadius, hitColliders, enemyLayer);
            int damagedCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitColliders[i];
                if (col == null) continue;

                // Không tự tác động lên chính người chơi
                if (col.transform.root == transform.root) continue;

                // Tránh xử lý trùng lặp trên cùng một thực thể (nếu quái có nhiều collider)
                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    bool alreadyDamaged = false;
                    for (int d = 0; d < damagedCount; d++)
                    {
                        if (damagedEntities[d] == damageable)
                        {
                            alreadyDamaged = true;
                            break;
                        }
                    }

                    if (alreadyDamaged) continue;

                    if (damagedCount < MaxTargets)
                    {
                        damagedEntities[damagedCount++] = damageable;
                    }
                }

                // Tính vector hướng nổ trên mặt phẳng phẳng nằm ngang:
                Vector3 flatDir = (col.transform.position - transform.position);
                flatDir.y = 0f;
                flatDir = flatDir.sqrMagnitude > 0.001f ? flatDir.normalized : transform.forward;

                // QUAN TRỌNG: Gọi knockbackable.ApplyKnockback TRƯỚC KHI gọi damageable.TakeDamage()
                IKnockbackable knockbackable = col.GetComponentInParent<IKnockbackable>();
                if (knockbackable != null)
                {
                    knockbackable.ApplyKnockback(flatDir, knockbackMagnitude, knockbackDuration);
                }
                else
                {
                    // Dự phòng (Fallback): Nếu KHÔNG có IKnockbackable nhưng có Rigidbody thông thường
                    Rigidbody targetRb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody>();
                    if (targetRb != null && !targetRb.isKinematic)
                    {
                        Vector3 dir = flatDir;
                        dir.y = 0.5f;
                        dir.Normalize();

#if UNITY_6000_0_OR_NEWER
                        // Chuẩn Unity 6: trực tiếp gán linearVelocity 15 m/s
                        targetRb.linearVelocity = dir * 15f;
#else
                        targetRb.AddForce(dir * (knockbackMagnitude * 1.5f), ForceMode.Impulse);
#endif
                        Debug.Log($"[Ultimate] Hất tung vật thể (Rigidbody fallback): {col.name}");
                    }
                }

                // Gây sát thương diện rộng qua IDamageable (SAU KHI gọi ApplyKnockback)
                if (damageable != null && !damageable.IsDead)
                {
                    Vector3 hitPoint = col.ClosestPoint(origin);
                    damageable.TakeDamage(damage, hitPoint, flatDir);
                    Debug.Log($"[Ultimate] Soul Burst gây sát thương lên: {col.name} ({damage} DMG)");
                }
            }

            // Kích hoạt event phục vụ hiệu ứng hình ảnh (VFX), âm thanh và rung màn hình
            OnSoulBurstFired?.Invoke();
        }

        /// <summary>
        /// Kích hoạt hiệu ứng sóng xung kích: chạy ParticleSystem được gán hoặc tự động tạo procedural shockwave ring.
        /// </summary>
        private void PlayShockwaveVFX()
        {
            if (shockwaveVFX != null)
            {
                shockwaveVFX.transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
                shockwaveVFX.Play();
            }
            else
            {
                StartCoroutine(SpawnProceduralShockwave(transform.position, blastRadius, 0.25f));
            }
        }

        /// <summary>
        /// Tự động sinh hiệu ứng sóng xung kích hình vành tròn nở rộng từ 0 đến blastRadius trong 0.25s rồi mờ dần biến mất.
        /// </summary>
        private IEnumerator SpawnProceduralShockwave(Vector3 center, float targetRadius, float duration)
        {
            GameObject ringObj = new GameObject("ProceduralShockwave_Ring");
            ringObj.transform.position = new Vector3(center.x, 0.05f, center.z);

            LineRenderer lineRenderer = ringObj.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;

            const int segments = 48;
            lineRenderer.positionCount = segments;
            Vector3[] points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            lineRenderer.SetPositions(points);

            // Tìm Shader tương thích URP (Sprites/Default hỗ trợ transparency hoàn hảo trên mọi render pipeline)
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            Material mat = new Material(shader);
            Color cyanColor = new Color(0f, 0.95f, 1f, 1f); // Neon Cyan Hư Vô
            mat.color = cyanColor;
            lineRenderer.material = mat;

            lineRenderer.startWidth = 0.45f;
            lineRenderer.endWidth = 0.45f;
            lineRenderer.startColor = cyanColor;
            lineRenderer.endColor = cyanColor;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Mở rộng bán kính mượt mà theo hàm Sin (OutQuad)
                float currentRadius = Mathf.Lerp(0.1f, targetRadius, Mathf.Sin(t * Mathf.PI * 0.5f));
                ringObj.transform.localScale = new Vector3(currentRadius, 1f, currentRadius);

                // Mờ dần độ trong suốt (alpha)
                float alpha = Mathf.Lerp(1f, 0f, t);
                Color currentColor = new Color(cyanColor.r, cyanColor.g, cyanColor.b, alpha);
                lineRenderer.startColor = currentColor;
                lineRenderer.endColor = currentColor;

                // Thu nhỏ dần độ dày của vành sóng
                float currentWidth = Mathf.Lerp(0.5f, 0.05f, t);
                lineRenderer.startWidth = currentWidth;
                lineRenderer.endWidth = currentWidth;

                yield return null;
            }

            Destroy(mat);
            Destroy(ringObj);
        }

        /// <summary>
        /// Bắt phím kích hoạt Ultimate (Phím Q).
        /// Hỗ trợ cả New Input System và Legacy Input.
        /// </summary>
        private bool ReadUltimateInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Q))
            {
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Bắt phím debug bơm đầy Nộ (Phím T).
        /// Hỗ trợ cả New Input System và Legacy Input.
        /// </summary>
        private bool ReadDebugFuryInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T))
            {
                return true;
            }
#endif

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Vẽ hình cầu dây màu Cyan biểu diễn phạm vi sóng xung kích 6m trong Scene view
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
#endif
    }
}
