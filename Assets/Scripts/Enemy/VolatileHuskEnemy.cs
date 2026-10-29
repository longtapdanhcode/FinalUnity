using System.Collections;
using UnityEngine;
using Vanguard.Combat;
using Vanguard.FX;
using Vanguard.Juice;

namespace Vanguard.Enemy
{
    /// <summary>
    /// Volatile Husk (Quái tự sát AOE): Máu rất mỏng (25 HP), tốc độ cực nhanh (6.5),
    /// lao thẳng vào người chơi. Khi áp sát trong cự ly 2m, kích hoạt ngòi nổ 0.8s (rung lắc và phát sáng cực đại)
    /// rồi phát nổ diện rộng 4m gây 50 DMG + Knockback lên cả người chơi lẫn quái vật xung quanh.
    /// </summary>
    public class VolatileHuskEnemy : EnemyBase
    {
        [Header("Explosion Settings")]
        [Tooltip("Bán kính phát nổ diện rộng AOE (mét)")]
        [SerializeField] private float explosionRadius = 4.0f;

        [Tooltip("Sát thương gây ra khi phát nổ")]
        [SerializeField] private float explosionDamage = 50f;

        [Tooltip("Thời gian đếm ngược ngòi nổ trước khi phát nổ (giây)")]
        [SerializeField] private float fuseTime = 0.8f;

        [Tooltip("Độ lớn lực hất văng (Knockback) khi nổ")]
        [SerializeField] private float knockbackForce = 25f;

        [Tooltip("Thời gian hất văng kéo dài (giây)")]
        [SerializeField] private float knockbackDuration = 0.35f;

        [Tooltip("Prefab hiệu ứng vụ nổ VFX")]
        [SerializeField] private GameObject explosionVFXPrefab = null;

        [Tooltip("LayerMask quét các đối tượng nhận sát thương nổ (Player, Enemy)")]
        [SerializeField] private LayerMask damageLayers;

        // Runtime states
        private Coroutine fuseCoroutine;
        private bool isFusing;
        private bool hasExploded;
        private TelegraphHandle fuseTelegraph;

        // Shader property IDs (0 GC Alloc)
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        // Bộ nhớ đệm cố định cho Physics.OverlapSphereNonAlloc (0 GC Alloc)
        private const int MaxHitTargets = 16;
        private readonly Collider[] hitColliders = new Collider[MaxHitTargets];
        private readonly IDamageable[] damagedEntities = new IDamageable[MaxHitTargets];

        #region Properties
        public float ExplosionRadius => explosionRadius;
        public float ExplosionDamage => explosionDamage;
        public float FuseTime => fuseTime;
        public bool IsFusing => isFusing;
        public bool HasExploded => hasExploded;
        #endregion

        private void Reset()
        {
            maxHealth = 25f;
            moveSpeed = 6.5f;
            attackRange = 2.0f;
            attackCooldown = 99f;
            explosionRadius = 4.0f;
            explosionDamage = 50f;
            fuseTime = 0.8f;
            knockbackForce = 25f;
            knockbackDuration = 0.35f;
            damageLayers = LayerMask.GetMask("Player", "Enemy");
        }

        protected override void Awake()
        {
            maxHealth = 25f;
            moveSpeed = 6.5f;
            attackRange = 2.0f;
            attackCooldown = 99f;

            base.Awake();

            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.stoppingDistance = attackRange * 0.75f;
            }

            // Đảm bảo LayerMask quét nổ bao gồm cả "Player" và "Enemy"
            damageLayers = LayerMask.GetMask("Player", "Enemy");
            if (damageLayers.value == 0)
            {
                damageLayers = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Enemy"));
            }
        }

        protected override void UpdateChaseState()
        {
            if (isFusing || hasExploded) return;

            if (target == null)
            {
                FindPlayerTarget();
                if (target == null) return;
            }

            float distance = Vector3.Distance(transform.position, target.position);

            // Khi khoảng cách tới Player <= 2.0m: bắt đầu kích hoạt ngòi nổ tự sát
            if (distance <= attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            // Lao nhanh bám theo Player
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
        }

        protected override void UpdateAttackState()
        {
            if (hasExploded) return;

            // Dừng NavMeshAgent khi đã vào cự ly kích nổ
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            // Bắt đầu đếm ngược ngòi nổ nếu chưa kích hoạt
            if (!isFusing)
            {
                PerformAttack();
            }
        }

        protected override void PerformAttack()
        {
            if (isFusing || hasExploded) return;

            if (fuseCoroutine != null) StopCoroutine(fuseCoroutine);
            fuseCoroutine = StartCoroutine(FuseCountdownRoutine());
        }

        /// <summary>
        /// Coroutine đếm ngược ngòi nổ 0.8s: quái rung lắc nhẹ, nhấp nháy phát sáng cực đại rồi nổ tung.
        /// </summary>
        private IEnumerator FuseCountdownRoutine()
        {
            isFusing = true;

            // Tạm ngắt agent để tự do rung lắc vị trí
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
            }

            Vector3 startPos = transform.position;

            // Vòng cảnh báo nổ nạp đầy đúng bằng thời gian ngòi nổ
            fuseTelegraph = TelegraphManager.ShowCircle(startPos, explosionRadius, fuseTime);
            float elapsed = 0f;

            while (elapsed < fuseTime)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fuseTime);

                // 1. Rung lắc tăng dần theo nhịp đếm ngược
                float currentShake = Mathf.Lerp(0.02f, 0.08f, progress);
                Vector3 shakeOffset = new Vector3(
                    Random.Range(-currentShake, currentShake),
                    0f,
                    Random.Range(-currentShake, currentShake)
                );
                transform.position = startPos + shakeOffset;

                // 2. Nhấp nháy màu đỏ rực rỡ và tăng dần Emission bằng MaterialPropertyBlock (0 GC Alloc)
                float flashSpeed = Mathf.Lerp(8f, 30f, progress);
                float flashFactor = Mathf.PingPong(elapsed * flashSpeed, 1f);

                Color baseColor = Color.Lerp(new Color(1f, 0.45f, 0f), Color.red, flashFactor);
                Color emissionColor = Color.Lerp(new Color(0.5f, 0.1f, 0f), new Color(2.5f, 0.2f, 0f), progress);

                if (propBlock != null && renderers != null)
                {
                    propBlock.SetColor(BaseColorPropertyId, baseColor);
                    propBlock.SetColor(EmissionColorPropertyId, emissionColor);

                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null)
                        {
                            renderers[i].SetPropertyBlock(propBlock);
                        }
                    }
                }

                yield return null;
            }

            // Hết 0.8s: Kích nổ diện rộng
            Explode();
        }

        /// <summary>
        /// Kích nổ diện rộng AOE 4m: gây 50 DMG + Knockback lên cả Player lẫn quái vật khác.
        /// </summary>
        public void Explode()
        {
            if (hasExploded) return;
            hasExploded = true;
            fuseTelegraph.Cancel();

            if (fuseCoroutine != null)
            {
                StopCoroutine(fuseCoroutine);
                fuseCoroutine = null;
            }

            Vector3 explosionCenter = transform.position;

            // Đảm bảo LayerMask quét nổ bao gồm cả "Player" và "Enemy"
            LayerMask effectiveDamageLayers = LayerMask.GetMask("Player", "Enemy");
            if (effectiveDamageLayers.value == 0 && damageLayers.value != 0)
            {
                effectiveDamageLayers = damageLayers;
            }

            // 1. Quét đối tượng trong bán kính nổ bằng Physics.OverlapSphereNonAlloc (0 GC Alloc)
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, hitColliders, effectiveDamageLayers);
            int damagedCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitColliders[i];
                if (col == null) continue;

                // ĐIỀU KIỆN LOẠI TRỪ: CHỈ bỏ qua chính bản thân Husk:
                if (col.gameObject == gameObject || col.transform.root == transform.root) continue;

                // Tìm IDamageable bằng cả GetComponent và GetComponentInParent:
                var targetDamageable = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
                if (targetDamageable != null)
                {
                    // Tránh xử lý trùng lặp trên cùng 1 thực thể nếu có nhiều collider
                    bool alreadyDamaged = false;
                    for (int d = 0; d < damagedCount; d++)
                    {
                        if (damagedEntities[d] == targetDamageable)
                        {
                            alreadyDamaged = true;
                            break;
                        }
                    }
                    if (alreadyDamaged) continue;

                    if (damagedCount < MaxHitTargets)
                    {
                        damagedEntities[damagedCount++] = targetDamageable;
                    }

                    // Áp dụng Knockback trước
                    var knockback = col.GetComponentInParent<IKnockbackable>() ?? col.GetComponent<IKnockbackable>();
                    if (knockback != null)
                    {
                        Vector3 dir = (col.transform.position - transform.position);
                        dir.y = 0;
                        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
                        knockback.ApplyKnockback(dir, knockbackForce, knockbackDuration);
                    }

                    // Gây sát thương
                    Vector3 hitDir = col.transform.position - transform.position;
                    hitDir = hitDir.sqrMagnitude > 0.001f ? hitDir.normalized : transform.forward;
                    targetDamageable.TakeDamage(explosionDamage, col.transform.position, hitDir);
                    Debug.Log($"[VolatileHusk] Nổ trúng: {col.gameObject.name} (Gây {explosionDamage} DMG)");
                }
            }

            // 2. Spawn hiệu ứng vụ nổ VFX
            SpawnExplosionVFX(explosionCenter);

            Debug.Log("[VolatileHusk] PHÁT NỔ! Gây 50 DMG diện rộng!");

            JuiceManager.Shake(0.5f, 0.25f);
            JuiceManager.HitStop(0.04f);

            // 3. Tiêu diệt chính bản thân Husk (thông báo chết để Spawner đếm, rồi thu hồi về Pool / Destroy)
            CurrentState = EnemyState.Dead;
            NotifyDeath();
            Despawn();
        }

        /// <summary>
        /// Khởi tạo hiệu ứng vụ nổ VFX hoặc Procedural Ring nếu chưa gán Prefab.
        /// </summary>
        private void SpawnExplosionVFX(Vector3 center)
        {
            if (explosionVFXPrefab != null)
            {
                Vanguard.Pooling.PrefabPool.Get(explosionVFXPrefab, center, Quaternion.identity, 2f);
            }
            else
            {
                // Fallback tạo hiệu ứng nổ trực quan bằng LineRenderer hình cầu lửa
                GameObject vfxObj = new GameObject("ExplosionVFX_Procedural");
                vfxObj.transform.position = center + Vector3.up * 0.1f;
                StartCoroutine(ProceduralExplosionRing(vfxObj, explosionRadius, 0.35f));
            }
        }

        private IEnumerator ProceduralExplosionRing(GameObject rootObj, float targetRadius, float duration)
        {
            LineRenderer line = rootObj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;

            const int segments = 32;
            line.positionCount = segments;
            Vector3[] points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            line.SetPositions(points);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            Color fireColor = new Color(1f, 0.4f, 0f, 1f);
            mat.color = fireColor;
            line.material = mat;
            line.startColor = fireColor;
            line.endColor = Color.red;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float currentRad = Mathf.Lerp(0.2f, targetRadius, Mathf.Sin(t * Mathf.PI * 0.5f));
                rootObj.transform.localScale = new Vector3(currentRad, 1f, currentRad);

                float alpha = Mathf.Lerp(1f, 0f, t);
                Color c = new Color(fireColor.r, fireColor.g, fireColor.b, alpha);
                line.startColor = c;
                line.endColor = c;
                line.startWidth = Mathf.Lerp(0.6f, 0.05f, t);
                line.endWidth = line.startWidth;

                yield return null;
            }

            Destroy(mat);
            Destroy(rootObj);
        }

        public override void ApplyStun(float duration)
        {
            // Ngòi nổ đã kích hoạt thì KHÔNG thể bị ngắt bởi Stun thông thường
            if (isFusing) return;
            base.ApplyStun(duration);
        }

        protected override void Die()
        {
            // Nếu bị đánh gục trước hoặc trong lúc đếm ngược: vẫn lập tức phát nổ!
            if (!hasExploded)
            {
                Explode();
                return;
            }

            base.Die();
        }

        private void OnDisable()
        {
            fuseTelegraph.Cancel();
        }

        protected override void OnSpawnReset()
        {
            fuseTelegraph.Cancel();
            fuseCoroutine = null;
            isFusing = false;
            hasExploded = false;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            // Bán kính kích nổ AOE (Đỏ)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);

            // Cự ly kích hoạt ngòi nổ (Vàng)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
