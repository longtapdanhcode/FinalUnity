using System.Collections;
using UnityEngine;
using Vanguard.Combat;
using Vanguard.FX;
using Vanguard.Juice;
using Vanguard.Spawning;

namespace Vanguard.Enemy
{
    /// <summary>
    /// Gargoyle Bạo Quân (Mini-Boss Wave 5): máu dày, di chuyển chậm, kháng đẩy lùi,
    /// triệu hồi quái trợ chiến khi xuất trận và đập búa AOE có báo trước (Telegraph).
    /// </summary>
    public class GargoyleBoss : EnemyBase
    {
        private const int HitBufferSize = 8;

        [Header("Ground Slam")]
        [SerializeField] private float slamRadius = 5.0f;
        [SerializeField] private float slamDamage = 35f;
        [Tooltip("Thời gian báo động đỏ trước khi giáng đòn (giây)")]
        [SerializeField] private float telegraphDuration = 1.2f;
        [SerializeField] private float slamCooldown = 6.0f;
        [Tooltip("Cự ly tới Player để bắt đầu đập búa (mét)")]
        [SerializeField] private float slamTriggerRange = 6f;
        [Tooltip("Thời gian khựng lại sau khi đập xong (giây)")]
        [SerializeField] private float slamRecoverTime = 0.6f;
        [SerializeField] private float slamKnockbackForce = 18f;
        [SerializeField] private float slamKnockbackDuration = 0.3f;

        [Header("Resistance")]
        [Tooltip("Tỉ lệ lực đẩy lùi bị giảm (0.9 = giảm 90%)")]
        [Range(0f, 1f)]
        [SerializeField] private float knockbackResistance = 0.9f;
        [Tooltip("Hệ số thời gian choáng (0.5 = chỉ choáng một nửa). Không bị choáng khi đang đập búa")]
        [SerializeField] private float stunDurationMultiplier = 0.5f;

        [Header("Minion Summoning")]
        [SerializeField] private EnemyType[] minionTypes =
        {
            EnemyType.Crawler, EnemyType.Crawler, EnemyType.VolatileHusk, EnemyType.VolatileHusk
        };
        [SerializeField] private float summonRadius = 3.5f;

        private EnemySpawner spawner;
        private Coroutine slamCoroutine;
        private float slamTimer;
        private TelegraphHandle slamTelegraph;
        private int playerMask;
        private readonly Collider[] hitBuffer = new Collider[HitBufferSize];
        private readonly IDamageable[] damaged = new IDamageable[HitBufferSize];

        public bool IsSlamming => CurrentState == EnemyState.Slamming;

        protected override void Awake()
        {
            maxHealth = 350f;
            moveSpeed = 2.8f;
            attackRange = 3.5f;
            attackCooldown = slamCooldown;

            base.Awake();

            playerMask = LayerMask.GetMask("Player");
        }

        public override void ChangeState(EnemyState newState)
        {
            if (newState == EnemyState.Slamming)
            {
                if (IsDead) return;

                CurrentState = EnemyState.Slamming;
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
                return;
            }

            base.ChangeState(newState);
        }

        protected override void UpdateChaseState()
        {
            if (target == null) return;

            slamTimer -= Time.deltaTime;

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= slamTriggerRange && slamTimer <= 0f)
            {
                PerformAttack();
                return;
            }

            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
        }

        protected override void PerformAttack()
        {
            if (slamCoroutine != null) StopCoroutine(slamCoroutine);
            slamCoroutine = StartCoroutine(GroundSlamRoutine());
        }

        private IEnumerator GroundSlamRoutine()
        {
            ChangeState(EnemyState.Slamming);

            // Quay mặt về mục tiêu một lần trước khi đập
            if (target != null)
            {
                Vector3 lookDir = target.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(lookDir);
            }

            ShowTelegraph(true);

            float elapsed = 0f;
            while (elapsed < telegraphDuration)
            {
                if (IsDead)
                {
                    ShowTelegraph(false);
                    slamCoroutine = null;
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            ShowTelegraph(false);
            DealSlamDamage();

            slamTimer = slamCooldown;

            yield return new WaitForSeconds(slamRecoverTime);

            slamCoroutine = null;
            if (!IsDead)
            {
                ChangeState(EnemyState.Chase);
            }
        }

        private void DealSlamDamage()
        {
            Vector3 center = transform.position;
            int hits = Physics.OverlapSphereNonAlloc(center, slamRadius, hitBuffer, playerMask);
            int damagedCount = 0;

            for (int i = 0; i < hits; i++)
            {
                if (hitBuffer[i] == null) continue;

                IDamageable damageable = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead) continue;

                bool duplicate = false;
                for (int d = 0; d < damagedCount; d++)
                {
                    if (damaged[d] == damageable) { duplicate = true; break; }
                }
                if (duplicate) continue;
                damaged[damagedCount++] = damageable;

                Vector3 dir = hitBuffer[i].transform.position - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

                // Hất văng trước, sát thương sau (giống Husk) để PlayerHealth xử lý i-Frames của Dash
                IKnockbackable knockback = hitBuffer[i].GetComponentInParent<IKnockbackable>();
                if (knockback != null) knockback.ApplyKnockback(dir, slamKnockbackForce, slamKnockbackDuration);

                damageable.TakeDamage(slamDamage, hitBuffer[i].transform.position, dir);
            }

            for (int d = 0; d < damagedCount; d++) damaged[d] = null;

            JuiceManager.Shake(0.6f, 0.3f);
            Debug.Log($"[GargoyleBoss] ĐẬP BÚA! Trúng {damagedCount} mục tiêu ({slamDamage} DMG, bán kính {slamRadius}m).");
        }

        /// <summary>
        /// Bật vòng cảnh báo nạp đầy dưới chân Boss (đúng bán kính đập búa, kéo dài telegraphDuration) hoặc hủy nó.
        /// </summary>
        private void ShowTelegraph(bool show)
        {
            if (show)
            {
                slamTelegraph = TelegraphManager.ShowCircle(transform.position, slamRadius, telegraphDuration, danger: Vanguard.FX.TelegraphDanger.High);
            }
            else
            {
                slamTelegraph.Cancel();
            }
        }

        private void OnDisable()
        {
            slamTelegraph.Cancel();
        }

        private void SummonMinions()
        {
            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || minionTypes == null || minionTypes.Length == 0) return;

            Debug.Log($"[GargoyleBoss] Triệu hồi {minionTypes.Length} quái phụ trợ chiến!");

            float angleOffset = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < minionTypes.Length; i++)
            {
                float angle = angleOffset + i * Mathf.PI * 2f / minionTypes.Length;
                Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * summonRadius;
                spawner.SpawnEnemy(minionTypes[i], position);
            }
        }

        protected override void OnSpawnReset()
        {
            slamCoroutine = null;
            slamTimer = slamCooldown * 0.5f;
            ShowTelegraph(false);
            SummonMinions();
        }

        public override void ApplyKnockback(Vector3 dir, float force, float duration)
        {
            // Đang đập búa: miễn nhiễm hoàn toàn để không bị ngắt hoạt cảnh
            if (IsSlamming) return;

            base.ApplyKnockback(dir, force * (1f - knockbackResistance), duration);
        }

        public override void ApplyStun(float duration)
        {
            if (IsSlamming) return;

            base.ApplyStun(duration * stunDurationMultiplier);
        }

        protected override void Die()
        {
            if (slamCoroutine != null)
            {
                StopCoroutine(slamCoroutine);
                slamCoroutine = null;
            }
            ShowTelegraph(false);

            Debug.Log("[GargoyleBoss] Gargoyle Bạo Quân đã bị tiêu diệt!");
            base.Die();
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, slamRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, slamTriggerRange);
        }
#endif
    }
}
