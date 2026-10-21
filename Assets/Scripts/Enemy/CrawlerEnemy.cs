using System.Collections;
using UnityEngine;
using Vanguard.Combat;

namespace Vanguard.Enemy
{
    /// <summary>
    /// Crawler (Quái rệp cận chiến): Máu thấp (40), tốc độ nhanh (4.5), cắn cận chiến (10 DMG) sau 0.3s lấy đà.
    /// </summary>
    public class CrawlerEnemy : EnemyBase
    {
        [Header("Crawler Specifics")]
        [Tooltip("Sát thương mỗi cú cắn")]
        [SerializeField] private float attackDamage = 10f;

        [Tooltip("Thời gian khựng lại lấy đà trước khi cắn (giây)")]
        [SerializeField] private float windupDelay = 0.3f;

        [Tooltip("LayerMask quét người chơi (nếu để trống sẽ kiểm tra theo khoảng cách tới target)")]
        [SerializeField] private LayerMask playerLayer;

        private Coroutine attackCoroutine;
        private readonly Collider[] hitColliders = new Collider[4];

        protected override void Awake()
        {
            maxHealth = 40f;
            moveSpeed = 4.5f;
            attackRange = 1.6f;
            attackCooldown = 1.2f;

            base.Awake();

            if (playerLayer.value == 0)
            {
                playerLayer = LayerMask.GetMask("Player");
            }
        }

        protected override void PerformAttack()
        {
            if (attackCoroutine != null) StopCoroutine(attackCoroutine);
            attackCoroutine = StartCoroutine(BiteRoutine());
        }

        private IEnumerator BiteRoutine()
        {
            // Dừng NavMeshAgent trong lúc tấn công
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            // Thời gian khựng lại lấy đà (windup 0.3s)
            float elapsed = 0f;
            while (elapsed < windupDelay)
            {
                // Hủy đòn cắn nếu bị choáng hoặc chết trong lúc lấy đà
                if (CurrentState == EnemyState.Stunned || IsDead)
                {
                    attackCoroutine = null;
                    yield break;
                }

                // Xoay mặt nhìn về phía Player
                if (target != null)
                {
                    Vector3 lookDir = target.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            Quaternion.LookRotation(lookDir),
                            720f * Time.deltaTime
                        );
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (CurrentState == EnemyState.Stunned || IsDead)
            {
                attackCoroutine = null;
                yield break;
            }

            // Thực hiện cú cắn: kiểm tra cự ly và quét collider Player
            bool hitPlayer = false;
            IDamageable damageable = null;

            if (playerLayer.value != 0)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, attackRange, hitColliders, playerLayer);
                for (int i = 0; i < hitCount; i++)
                {
                    damageable = hitColliders[i].GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        hitPlayer = true;
                        break;
                    }
                }
            }

            // Fallback: kiểm tra cự ly trực tiếp với target
            if (!hitPlayer && target != null && Vector3.Distance(transform.position, target.position) <= attackRange)
            {
                damageable = target.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    hitPlayer = true;
                }
            }

            if (hitPlayer && damageable != null)
            {
                damageable.TakeDamage(attackDamage, transform.position, transform.forward);
                Debug.Log("[Crawler] Cắn trúng Player! Gây 10 DMG.");
            }

            attackCoroutine = null;
        }

        protected override void Die()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }

            Debug.Log("[Crawler] Đã bị tiêu diệt!");
            base.Die();
        }

        protected override void OnSpawnReset()
        {
            attackCoroutine = null;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
