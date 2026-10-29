using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Vanguard.Combat;

namespace Vanguard.Enemy
{
    /// <summary>
    /// Void Caster (Quái phù thủy tầm xa): Máu trung bình (60 HP), di chuyển chậm (2.8),
    /// duy trì tầm bắn 8m, tự động thả diều (Kite) lùi lại khi Player áp sát dưới 4.5m,
    /// niệm chú 0.5s rồi bắn đạn VoidProjectile (15 DMG) với chu kỳ hồi chiêu 2.5s.
    /// </summary>
    public class VoidCasterEnemy : EnemyBase
    {
        [Header("Void Caster Combat")]
        [Tooltip("Sát thương mỗi phát bắn ma thuật Hư Vô")]
        [SerializeField] private float attackDamage = 15f;

        [Tooltip("Khoảng cách tối thiểu trước khi lùi lại thả diều (Kite)")]
        [SerializeField] private float retreatDistance = 4.5f;

        [Tooltip("Thời gian lấy đà/niệm chú trước khi bắn (giây)")]
        [SerializeField] private float castDelay = 0.5f;

        [Tooltip("Prefab viên đạn ma thuật VoidProjectile")]
        [SerializeField] private GameObject projectilePrefab = null;

        [Tooltip("Vị trí nòng bắn/điểm xuất phát của viên đạn")]
        [SerializeField] private Transform firePoint = null;

        private Coroutine castCoroutine;
        private bool isCasting;

        #region Properties
        public float AttackDamage => attackDamage;
        public float RetreatDistance => retreatDistance;
        public float CastDelay => castDelay;
        public bool IsCasting => isCasting;
        #endregion

        private void Reset()
        {
            maxHealth = 60f;
            moveSpeed = 2.8f;
            attackRange = 8f;
            attackCooldown = 2.5f;
            attackDamage = 15f;
            retreatDistance = 4.5f;
            castDelay = 0.5f;
        }

        protected override void Awake()
        {
            // Thiết lập chỉ số theo Checklist Card [2.3]
            maxHealth = 60f;
            moveSpeed = 2.8f;
            attackRange = 8f;
            attackCooldown = 2.5f;

            base.Awake();

            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.stoppingDistance = 0.5f; // Cho phép NavMeshAgent di chuyển chính xác tới vị trí lùi
                agent.updateRotation = false; // Tự kiểm soát xoay mặt hướng về Player
            }
        }

        protected override void UpdateChaseState()
        {
            if (target == null)
            {
                FindPlayerTarget();
                if (target == null) return;
            }

            float distance = Vector3.Distance(transform.position, target.position);

            // Nếu cự ly tới Player <= attackRange (8m): chuyển sang trạng thái Tấn công / Thả diều
            if (distance <= attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            // Xoay mặt nhìn về phía Player
            RotateTowardsPlayer();

            // Bám theo Player
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
        }

        protected override void UpdateAttackState()
        {
            if (target == null)
            {
                FindPlayerTarget();
                if (target == null) return;
            }

            float distance = Vector3.Distance(transform.position, target.position);

            // Nếu Player chạy quá xa khỏi tầm bắn (> 8m * 1.2 = 9.6m): chuyển sang Chase
            if (distance > attackRange * 1.2f)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            // Dừng lại, xoay mặt nhìn về Player
            RotateTowardsPlayer();

            // Cơ chế di chuyển & Thả diều (Kiting AI): chỉ lùi nếu không đang niệm chú
            if (!isCasting)
            {
                if (distance < retreatDistance)
                {
                    // Player áp sát quá gần (< retreatDistance 4.5m): Tìm vị trí lùi ngược hướng Player trên NavMesh
                    Vector3 retreatDir = transform.position - target.position;
                    retreatDir.y = 0f;
                    retreatDir = retreatDir.sqrMagnitude > 0.001f ? retreatDir.normalized : -transform.forward;

                    Vector3 desiredRetreatPos = transform.position + retreatDir * 3f;
                    if (NavMesh.SamplePosition(desiredRetreatPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                    {
                        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                        {
                            agent.isStopped = false;
                            agent.SetDestination(hit.position);
                        }
                    }
                }
                else
                {
                    // Đang ở cự ly an toàn (4.5m - 8m): đứng lại duy trì khoảng cách
                    if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                    }
                }
            }

            // Đếm cooldown và tung đòn bắn
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f && !isCasting)
            {
                attackTimer = attackCooldown;
                PerformAttack();
            }
        }

        /// <summary>
        /// Xoay mượt mà mặt nhân vật nhìn về phía Player trên mặt phẳng Y = 0.
        /// </summary>
        private void RotateTowardsPlayer()
        {
            if (target == null) return;

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

        protected override void PerformAttack()
        {
            if (castCoroutine != null) StopCoroutine(castCoroutine);
            castCoroutine = StartCoroutine(CastSpellRoutine());
        }

        /// <summary>
        /// Coroutine niệm chú lấy đà trong 0.5s trước khi bắn. Bị ngắt nếu dính Choáng hoặc Chết.
        /// </summary>
        private IEnumerator CastSpellRoutine()
        {
            isCasting = true;

            // Khựng lại niệm chú trong castDelay (0.5s)
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            float elapsed = 0f;
            while (elapsed < castDelay)
            {
                // Kiểm tra nếu bị Stun (ApplyStun) hoặc chết (Dead) trong lúc niệm thì hủy đòn bắn
                if (CurrentState == EnemyState.Stunned || IsDead)
                {
                    isCasting = false;
                    castCoroutine = null;
                    yield break;
                }

                RotateTowardsPlayer();

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (CurrentState == EnemyState.Stunned || IsDead)
            {
                isCasting = false;
                castCoroutine = null;
                yield break;
            }

            // Spawn viên đạn projectilePrefab tại firePoint.position, hướng ngắm về ngực Player
            SpawnProjectile();

            isCasting = false;
            castCoroutine = null;
        }

        /// <summary>
        /// Khởi tạo viên đạn ma thuật Hư Vô bay về hướng ngực Player.
        /// </summary>
        private void SpawnProjectile()
        {
            Vector3 spawnPos = firePoint != null
                ? firePoint.position
                : transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;

            // Hướng ngắm về ngực Player (target.position + Vector3.up * 1f)
            Vector3 targetChest = target != null
                ? target.position + Vector3.up * 1.0f
                : spawnPos + transform.forward * 10f;

            Vector3 shootDir = targetChest - spawnPos;
            if (shootDir.sqrMagnitude > 0.0001f)
            {
                shootDir.Normalize();
            }
            else
            {
                shootDir = transform.forward;
            }

            Quaternion spawnRot = Quaternion.LookRotation(shootDir);

            GameObject projObj;
            if (projectilePrefab != null)
            {
                projObj = Vanguard.Pooling.PrefabPool.Get(projectilePrefab, spawnPos, spawnRot);
            }
            else
            {
                projObj = CreateDefaultProjectile(spawnPos, spawnRot);
            }

            VoidProjectile proj = projObj.GetComponent<VoidProjectile>();
            if (proj != null)
            {
                proj.Initialize(attackDamage, 10f);
            }

            Debug.Log($"[VoidCaster] Bắn đạn ma thuật Hư Vô ({attackDamage} DMG) hướng về Player!");
        }

        /// <summary>
        /// Tạo đạn ma thuật dạng Procedural nếu projectilePrefab chưa được gán trong Inspector.
        /// </summary>
        private GameObject CreateDefaultProjectile(Vector3 position, Quaternion rotation)
        {
            GameObject projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projObj.name = "VoidProjectile_Procedural";
            projObj.transform.position = position;
            projObj.transform.rotation = rotation;
            projObj.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            SphereCollider sc = projObj.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
            }

            Renderer rend = projObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.color = new Color(0.7f, 0.1f, 1f, 1f); // Neon Tím Hư Vô
                    rend.material = mat;
                }
            }

            projObj.AddComponent<VoidProjectile>();
            return projObj;
        }

        protected override void Die()
        {
            if (castCoroutine != null)
            {
                StopCoroutine(castCoroutine);
                castCoroutine = null;
            }
            isCasting = false;

            Debug.Log("[VoidCaster] Đã bị tiêu diệt!");
            base.Die();
        }

        protected override void OnSpawnReset()
        {
            castCoroutine = null;
            isCasting = false;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            // Tầm bắn 8m (Màu tím Hư Vô)
            Gizmos.color = new Color(0.7f, 0.2f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Tầm lùi thả diều 4.5m (Màu vàng)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, retreatDistance);
        }
#endif
    }
}
