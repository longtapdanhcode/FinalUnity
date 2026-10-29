using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Enemy
{
    /// <summary>
    /// Lớp cơ sở trừu tượng cho tất cả kẻ địch, tích hợp FSM (Idle, Chase, Attack, Stunned, Dead)
    /// và hiện thực hóa IDamageable, IKnockbackable, IStunnable.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Collider))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, IKnockbackable, IStunnable
    {
        [Header("Stats")]
        [SerializeField] protected float maxHealth = 50f;
        [SerializeField] protected float moveSpeed = 4f;
        [SerializeField] protected float attackRange = 1.5f;
        [SerializeField] protected float attackCooldown = 1.5f;
        [SerializeField, Min(0.1f)] protected float deathLingerTime = 0.5f; // thời gian giữ xác (cho animation chết)

        // Runtime states
        protected float currentHealth;
        protected Transform target;
        protected NavMeshAgent agent;
        protected Collider enemyCollider;

        protected float attackTimer;
        protected float stunTimer;
        private float tauntTimer;
        private Coroutine knockbackCoroutine;
        private Coroutine flashCoroutine;
        private Coroutine deathDestroyCoroutine;
        private bool deathNotified;

        // Visual flash cache
        protected Renderer[] renderers;
        protected MaterialPropertyBlock propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount"); // shader Vanguard/NeonLit
        private readonly WaitForSeconds flashWait = new WaitForSeconds(0.08f);

        #region Properties & Events
        public EnemyState CurrentState { get; protected set; } = EnemyState.Idle;
        public bool IsDead => CurrentState == EnemyState.Dead;
        public float DeathLingerTime => deathLingerTime;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        public event Action<EnemyBase> OnEnemyDeath;
        public event Action<float, float> OnEnemyHealthChanged;

        /// <summary>
        /// Phát ra khi xác quái đã hiển thị xong và cần được thu hồi (Object Pooling).
        /// Nếu không có ai lắng nghe (không dùng EnemySpawner), quái sẽ tự Destroy như cũ.
        /// </summary>
        public event Action<EnemyBase> OnDespawnRequested;
        #endregion

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            enemyCollider = GetComponent<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            propBlock = new MaterialPropertyBlock();

            currentHealth = maxHealth;
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange * 0.85f;
        }

        protected virtual void Start()
        {
            FindPlayerTarget();
            OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        protected virtual void Update()
        {
            if (IsDead) return;

            // Hết thời gian bị khiêu khích (Decoy) -> quay lại nhắm Player
            if (tauntTimer > 0f)
            {
                tauntTimer -= Time.deltaTime;
                if (tauntTimer <= 0f)
                {
                    target = null;
                }
            }

            if (target == null)
            {
                FindPlayerTarget();
                if (target == null) return;
            }

            switch (CurrentState)
            {
                case EnemyState.Idle:
                    UpdateIdleState();
                    break;
                case EnemyState.Chase:
                    UpdateChaseState();
                    break;
                case EnemyState.Attack:
                    UpdateAttackState();
                    break;
                case EnemyState.Stunned:
                    UpdateStunnedState();
                    break;
            }
        }

        #region Target Search
        /// <summary>
        /// Tạm thời đổi mục tiêu sang một Transform khác (ví dụ Decoy) trong khoảng thời gian chỉ định.
        /// Hết giờ hoặc khi mục tiêu bị hủy, quái tự tìm lại Player.
        /// </summary>
        public void TauntTo(Transform newTarget, float duration)
        {
            if (IsDead || newTarget == null || duration <= 0f) return;

            target = newTarget;
            tauntTimer = duration;
        }

        protected virtual void FindPlayerTarget()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
                return;
            }

            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                target = playerHealth.transform;
            }
        }
        #endregion

        #region FSM Logic
        public virtual void ChangeState(EnemyState newState)
        {
            if (IsDead && newState != EnemyState.Dead) return;

            CurrentState = newState;

            switch (newState)
            {
                case EnemyState.Idle:
                case EnemyState.Attack:
                case EnemyState.Stunned:
                    if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                    }
                    break;

                case EnemyState.Chase:
                    if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        agent.isStopped = false;
                    }
                    break;

                case EnemyState.Dead:
                    Die();
                    break;
            }
        }

        protected virtual void UpdateIdleState()
        {
            if (target != null)
            {
                ChangeState(EnemyState.Chase);
            }
        }

        protected virtual void UpdateChaseState()
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.SetDestination(target.position);
            }
        }

        protected virtual void UpdateAttackState()
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > attackRange * 1.25f)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            // Xoay mặt nhìn về phía Player
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

            // Đếm cooldown và tung đòn
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                PerformAttack();
            }
        }

        protected virtual void UpdateStunnedState()
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                ChangeState(EnemyState.Chase);
            }
        }

        protected abstract void PerformAttack();

        protected virtual void Die()
        {
            // KHÔNG gọi StopAllCoroutines() để tránh ngắt mất Coroutine Knockback đang trượt dở
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
                ResetRenderersColor();
            }

            if (agent != null)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
                agent.enabled = false;
            }

            if (enemyCollider != null)
            {
                enemyCollider.enabled = false;
            }

            NotifyDeath();

            // Nếu không có knockback đang chạy, tự hủy sau 0.5s để người chơi thấy xác
            // Nếu có knockback đang chạy, KnockbackRoutine sẽ tự hủy GameObject khi trượt xong
            if (knockbackCoroutine == null)
            {
                deathDestroyCoroutine = StartCoroutine(DestroyAfterDelay(deathLingerTime));
            }
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            deathDestroyCoroutine = null;
            Despawn();
        }

        /// <summary>
        /// Phát sự kiện OnEnemyDeath đúng 1 lần cho mỗi vòng đời (kể cả khi bị tái sử dụng từ Pool).
        /// </summary>
        protected void NotifyDeath()
        {
            if (deathNotified) return;
            deathNotified = true;
            OnEnemyDeath?.Invoke(this);
        }

        /// <summary>
        /// Thu hồi quái: trả về Pool nếu có Spawner lắng nghe, ngược lại Destroy như hành vi cũ.
        /// </summary>
        protected void Despawn()
        {
            if (OnDespawnRequested != null)
            {
                OnDespawnRequested.Invoke(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region Pooling
        /// <summary>
        /// Khôi phục toàn bộ trạng thái quái khi được lấy ra từ Pool: hồi đầy máu, hủy Stun/Knockback,
        /// bật lại Collider/NavMeshAgent và Warp an toàn lên NavMesh. Gọi SAU khi GameObject đã SetActive(true).
        /// </summary>
        public void ResetForSpawn(Vector3 position, Quaternion rotation)
        {
            // Coroutine đã tự dừng khi SetActive(false), chỉ cần xóa các handle cũ
            StopAllCoroutines();
            knockbackCoroutine = null;
            flashCoroutine = null;
            deathDestroyCoroutine = null;
            ResetRenderersColor();

            currentHealth = maxHealth;
            stunTimer = 0f;
            attackTimer = 0f;
            deathNotified = false;
            CurrentState = EnemyState.Idle;

            // Đảm bảo quái luôn nằm trên Layer "Enemy" để đòn chém / AOE (OverlapSphere theo LayerMask) quét trúng
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                gameObject.layer = enemyLayer;
            }

            // Hủy trạng thái bị khiêu khích còn sót lại từ vòng đời trước
            if (tauntTimer > 0f)
            {
                tauntTimer = 0f;
                target = null;
            }

            // Tìm lại Player nếu mất tham chiếu (Player respawn / quái mới tạo từ Pool)
            if (target == null)
            {
                FindPlayerTarget();
            }

            if (enemyCollider != null)
            {
                enemyCollider.enabled = true;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(position);
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }

            OnSpawnReset();

            // Khôi phục AI: truy đuổi Player ngay khi xuất hiện
            if (target != null)
            {
                ChangeState(EnemyState.Chase);
            }

            OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Hook cho lớp con xóa trạng thái riêng (coroutine tấn công, ngòi nổ...) khi tái sử dụng từ Pool.
        /// </summary>
        protected virtual void OnSpawnReset() { }
        #endregion

        #region Interfaces Implementation
        public virtual void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (IsDead || damageAmount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
            OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);

            // Hiệu ứng nhấp nháy đỏ
            if (gameObject.activeInHierarchy)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(DamageFlashRoutine());
            }

            if (currentHealth <= 0f)
            {
                ChangeState(EnemyState.Dead);
            }
        }

        public virtual void ApplyKnockback(Vector3 dir, float force, float duration)
        {
            // Quái đã bị thu hồi về Pool trong cùng frame (vd. Husk nổ dây chuyền) thì bỏ qua, tránh StartCoroutine trên object inactive
            if (!isActiveAndEnabled || force <= 0f || duration <= 0f) return;

            // Triệt tiêu hoàn toàn dir.y = 0f, chuẩn hóa dir
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;

            // Tạm tắt agent.enabled = false
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
            }

            // Hủy bộ đếm hủy quái nếu có (tránh bị xóa giữa chừng khi đang trượt văng)
            if (deathDestroyCoroutine != null)
            {
                StopCoroutine(deathDestroyCoroutine);
                deathDestroyCoroutine = null;
            }

            if (knockbackCoroutine != null)
            {
                StopCoroutine(knockbackCoroutine);
            }
            knockbackCoroutine = StartCoroutine(KnockbackRoutine(dir, force, duration));
        }

        public virtual void ApplyStun(float duration)
        {
            if (IsDead || duration <= 0f) return;

            stunTimer = Mathf.Max(stunTimer, duration);
            ChangeState(EnemyState.Stunned);
        }

        private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Trượt lùi mượt mà nội suy SmoothStep từ force về 0
                float currentSpeed = Mathf.Lerp(force, 0f, Mathf.SmoothStep(0f, 1f, t));
                transform.position += dir * (currentSpeed * Time.deltaTime);

                yield return null;
            }

            knockbackCoroutine = null;

            // Khi hết duration:
            if (IsDead)
            {
                // Nếu đã chết: giữ nguyên vị trí và thu hồi GameObject
                Despawn();
            }
            else
            {
                // Nếu chưa chết: bật lại agent.enabled = true
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }

                if (agent != null)
                {
                    agent.enabled = true;
                    if (agent.isOnNavMesh)
                    {
                        agent.isStopped = (CurrentState != EnemyState.Chase);
                    }
                }
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            SetRenderersColor(Color.red);
            yield return flashWait;
            ResetRenderersColor();
            flashCoroutine = null;
        }

        private void SetRenderersColor(Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].GetPropertyBlock(propBlock);

                    // Shader NeonLit có _FlashAmount -> flash sáng rực; shader khác -> fallback đổi _BaseColor
                    Material mat = renderers[i].sharedMaterial;
                    if (mat != null && mat.HasProperty(FlashAmountId))
                        propBlock.SetFloat(FlashAmountId, 1f);
                    else
                        propBlock.SetColor(BaseColorId, color);

                    renderers[i].SetPropertyBlock(propBlock);
                }
            }
        }

        private void ResetRenderersColor()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].SetPropertyBlock(null);
                }
            }
        }
        #endregion

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange * 0.85f);
        }
#endif
    }
}
