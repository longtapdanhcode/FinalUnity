using System.Collections;
using UnityEngine;
using Vanguard.Cards;
using Vanguard.Combat;
using Vanguard.FX;
using Vanguard.Juice;
using Vanguard.Player;
using Vanguard.UI;

namespace Vanguard.Enemy
{
    /// <summary>
    /// Void Warden - Chúa Tể Hư Vô (Boss cuối Wave 10): 2 Phase, miễn nhiễm đẩy lùi và choáng.
    /// Phase 1: Tam Đoạn Trảm Hình Quạt, Hút Tâm Lực &amp; Đấm Nổ Đất.
    /// Phase 2 (máu &lt;= 50%): thêm Mưa Thiên Thạch Hư Vô, di chuyển nhanh hơn, hồi chiêu ngắn hơn.
    /// </summary>
    public class VoidWardenBoss : EnemyBase
    {
        private const int HitBufferSize = 8;

        [Header("Triple Arc Slash")]
        [SerializeField] private float arcDamage = 25f;
        [Tooltip("Góc quét của mỗi nhát chém (độ)")]
        [SerializeField] private float arcAngle = 90f;
        [SerializeField] private float arcRange = 4.5f;
        [Tooltip("Thời gian telegraph nháy đỏ trước mỗi nhát (giây)")]
        [SerializeField] private float arcTelegraphTime = 0.4f;
        [SerializeField] private int arcSlashCount = 3;
        [SerializeField] private float arcGapTime = 0.15f;
        [SerializeField] private float arcCooldown = 5f;
        [Tooltip("Cự ly tới Player để bắt đầu chuỗi chém (mét)")]
        [SerializeField] private float arcTriggerRange = 5.5f;

        [Header("Void Vortex & Ground Blast")]
        [SerializeField] private float vortexDuration = 1.5f;
        [Tooltip("Tốc độ hút Player về phía Boss (m/s)")]
        [SerializeField] private float vortexPullSpeed = 6f;
        [Tooltip("Dừng hút khi Player cách Boss dưới khoảng này (mét)")]
        [SerializeField] private float vortexMinDistance = 1.8f;
        [SerializeField] private float blastRadius = 6f;
        [SerializeField] private float blastDamage = 50f;
        [SerializeField] private float blastKnockbackForce = 30f;
        [SerializeField] private float blastKnockbackDuration = 0.4f;
        [SerializeField] private float vortexCooldown = 12f;
        [SerializeField] private float vortexTriggerRange = 12f;

        [Header("Astral Meteor Rain (Phase 2)")]
        [Tooltip("Dùng lại MeteorImpact.prefab của thẻ Meteor Fall")]
        [SerializeField] private GameObject meteorImpactPrefab = null;
        [SerializeField] private int meteorCountMin = 6;
        [SerializeField] private int meteorCountMax = 8;
        [Tooltip("Thời gian gầm vang trước khi triệu hồi (giây)")]
        [SerializeField] private float meteorRoarTime = 2f;
        [SerializeField] private float meteorInterval = 0.25f;
        [Tooltip("Thời gian cảnh báo vòng đỏ trước khi thiên thạch chạm đất (giây)")]
        [SerializeField] private float meteorWarningTime = 0.8f;
        [SerializeField] private float meteorDamage = 30f;
        [SerializeField] private float meteorRadius = 3.5f;
        [SerializeField] private float meteorKnockbackForce = 15f;
        [SerializeField] private float meteorKnockbackDuration = 0.3f;
        [SerializeField] private Vector3 arenaCenter = Vector3.zero;
        [SerializeField] private float arenaRadius = 18f;
        [SerializeField] private float meteorCooldown = 15f;

        [Header("Phase 2")]
        [Range(0.1f, 1f)]
        [SerializeField] private float phase2HealthThreshold = 0.5f;
        [SerializeField] private float phase2SpeedMultiplier = 1.2f;
        [Tooltip("Hệ số nhân thời gian hồi chiêu ở Phase 2 (0.75 = giảm 25%)")]
        [SerializeField] private float phase2CooldownMultiplier = 0.75f;
        [SerializeField] private float transitionTime = 1.5f;
        [SerializeField] private float shockwaveRadius = 10f;
        [SerializeField] private float shockwaveForce = 14f;
        [SerializeField] private float shockwaveDuration = 0.3f;

        [Header("Visuals")]
        [SerializeField] private Renderer armorRenderer = null;
        [SerializeField] private Color phase1Base = new Color(0.04f, 0.02f, 0.07f, 1f);
        [SerializeField] private Color phase1Emission = new Color(0.35f, 0.08f, 0.8f, 1f);
        [SerializeField] private Color phase2Base = new Color(0.14f, 0.02f, 0.2f, 1f);
        [SerializeField] private Color phase2Emission = new Color(2.2f, 0.15f, 3f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly Collider[] hitBuffer = new Collider[HitBufferSize];
        private readonly IDamageable[] damaged = new IDamageable[HitBufferSize];
        private readonly WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

        private Coroutine skillCoroutine;
        private bool isPhase2;
        private bool isTransitioning;
        private float arcTimer;
        private float vortexTimer;
        private float meteorTimer;

        private int playerMask;
        private TelegraphHandle coneTelegraph;
        private TelegraphHandle circleTelegraph;

        // Tham chiếu Player thật (target có thể là Decoy nên không dùng để hút / khóa di chuyển)
        private PlayerHealth playerHealth;
        private Rigidbody playerRb;
        private PlayerController playerController;
        private PlayerDash playerDash;
        private bool playerControlLocked;

        private Material armorMaterial;
        private WorldSpaceBossHealthBar healthBar;

        public bool IsPhase2 => isPhase2;
        public bool IsCasting => CurrentState == EnemyState.Casting;

        protected override void Awake()
        {
            maxHealth = 2000f;
            moveSpeed = 3.2f;
            attackRange = 4f;
            attackCooldown = 99f;

            base.Awake();

            playerMask = LayerMask.GetMask("Player");

            healthBar = GetComponentInChildren<WorldSpaceBossHealthBar>(true);

            // Material riêng (instance) để đổi màu Phase 2; không bị ResetRenderersColor của lần nháy đỏ xóa
            if (armorRenderer != null)
            {
                armorMaterial = armorRenderer.material;
            }
        }

        private void OnDestroy()
        {
            if (armorMaterial != null) Destroy(armorMaterial);
        }

        #region FSM
        public override void ChangeState(EnemyState newState)
        {
            if (newState == EnemyState.Casting)
            {
                if (IsDead) return;

                CurrentState = EnemyState.Casting;
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

            float dt = Time.deltaTime;
            arcTimer -= dt;
            vortexTimer -= dt;
            meteorTimer -= dt;

            float distance = Vector3.Distance(transform.position, target.position);

            if (isPhase2 && meteorTimer <= 0f)
            {
                StartSkill(MeteorRainRoutine());
                return;
            }
            if (vortexTimer <= 0f && distance <= vortexTriggerRange)
            {
                StartSkill(VortexBlastRoutine());
                return;
            }
            if (arcTimer <= 0f && distance <= arcTriggerRange)
            {
                StartSkill(TripleSlashRoutine());
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
            StartSkill(TripleSlashRoutine());
        }

        private void StartSkill(IEnumerator routine)
        {
            if (skillCoroutine != null) StopCoroutine(skillCoroutine);
            skillCoroutine = StartCoroutine(routine);
        }

        private void FinishSkill()
        {
            skillCoroutine = null;
            if (!IsDead) ChangeState(EnemyState.Chase);
        }

        private float ScaledCooldown(float cooldown)
        {
            return isPhase2 ? cooldown * phase2CooldownMultiplier : cooldown;
        }
        #endregion

        #region Skill 1: Triple Arc Slash
        private IEnumerator TripleSlashRoutine()
        {
            ChangeState(EnemyState.Casting);

            for (int i = 0; i < arcSlashCount; i++)
            {
                FaceTarget();
                Vector3 direction = transform.forward;

                // Telegraph hình quạt nạp đầy trong arcTelegraphTime
                coneTelegraph = TelegraphManager.ShowSector(transform.position, direction, arcAngle, arcRange, arcTelegraphTime);
                float elapsed = 0f;
                while (elapsed < arcTelegraphTime)
                {
                    if (IsDead)
                    {
                        coneTelegraph.Cancel();
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                coneTelegraph.Cancel();
                DealArcDamage(direction);

                if (i < arcSlashCount - 1) yield return new WaitForSeconds(arcGapTime);
            }

            arcTimer = ScaledCooldown(arcCooldown);
            yield return new WaitForSeconds(0.5f);
            FinishSkill();
        }

        private void DealArcDamage(Vector3 direction)
        {
            Vector3 center = transform.position;
            int hits = Physics.OverlapSphereNonAlloc(center, arcRange + 0.5f, hitBuffer, playerMask);
            int damagedCount = 0;
            float halfAngle = arcAngle * 0.5f;

            for (int i = 0; i < hits; i++)
            {
                if (hitBuffer[i] == null) continue;

                IDamageable damageable = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead || IsAlreadyDamaged(damageable, damagedCount)) continue;

                Vector3 toTarget = hitBuffer[i].transform.position - center;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(direction, toTarget) > halfAngle) continue;

                damaged[damagedCount++] = damageable;
                damageable.TakeDamage(arcDamage, hitBuffer[i].transform.position, toTarget.normalized);
                Debug.Log($"[VoidWarden] Tam Đoạn Trảm trúng Player ({arcDamage} DMG)!");
            }

            ClearDamaged(damagedCount);
        }
        #endregion

        #region Skill 2: Void Vortex & Ground Blast
        private IEnumerator VortexBlastRoutine()
        {
            ChangeState(EnemyState.Casting);
            EnsurePlayerRefs();

            circleTelegraph = TelegraphManager.ShowCircle(transform.position, blastRadius, vortexDuration);

            float elapsed = 0f;
            while (elapsed < vortexDuration)
            {
                if (IsDead)
                {
                    ReleasePlayer();
                    circleTelegraph.Cancel();
                    yield break;
                }

                PullPlayer();
                elapsed += Time.fixedDeltaTime;
                yield return waitFixed;
            }

            ReleasePlayer();
            circleTelegraph.Cancel();
            DealBlastDamage();

            vortexTimer = ScaledCooldown(vortexCooldown);
            yield return new WaitForSeconds(0.8f);
            FinishSkill();
        }

        private void PullPlayer()
        {
            if (playerRb == null) return;

            // Đang lướt thì để PlayerDash toàn quyền điều khiển vận tốc (cho phép thoát khỏi vòng hút)
            if (playerDash != null && playerDash.IsDashing) return;

            Vector3 toBoss = transform.position - playerRb.position;
            toBoss.y = 0f;
            float distance = toBoss.magnitude;
            Vector3 pull = distance > vortexMinDistance ? toBoss / distance * vortexPullSpeed : Vector3.zero;

            if (playerController != null)
            {
                playerController.CanMove = false;
                playerControlLocked = true;
            }

            playerRb.linearVelocity = new Vector3(pull.x, playerRb.linearVelocity.y, pull.z);
        }

        private void ReleasePlayer()
        {
            if (!playerControlLocked) return;
            playerControlLocked = false;

            if (playerController != null && (playerDash == null || !playerDash.IsDashing))
            {
                playerController.CanMove = true;
            }
        }

        private void DealBlastDamage()
        {
            Vector3 center = transform.position;
            int hits = Physics.OverlapSphereNonAlloc(center, blastRadius, hitBuffer, playerMask);
            int damagedCount = 0;

            for (int i = 0; i < hits; i++)
            {
                if (hitBuffer[i] == null) continue;

                IDamageable damageable = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead || IsAlreadyDamaged(damageable, damagedCount)) continue;
                damaged[damagedCount++] = damageable;

                Vector3 dir = hitBuffer[i].transform.position - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

                // Hất văng ra rìa sàn đấu trước, sát thương sau (để PlayerHealth xử lý i-Frames của Dash)
                IKnockbackable knockback = hitBuffer[i].GetComponentInParent<IKnockbackable>();
                if (knockback != null) knockback.ApplyKnockback(dir, blastKnockbackForce, blastKnockbackDuration);

                damageable.TakeDamage(blastDamage, hitBuffer[i].transform.position, dir);
                Debug.Log($"[VoidWarden] Đấm Nổ Đất trúng Player ({blastDamage} DMG)!");
            }

            ClearDamaged(damagedCount);
            JuiceManager.Shake(0.6f, 0.3f);
        }
        #endregion

        #region Skill 3: Astral Meteor Rain (Phase 2)
        private IEnumerator MeteorRainRoutine()
        {
            ChangeState(EnemyState.Casting);
            Debug.Log("[VoidWarden] Gầm vang! Mưa Thiên Thạch Hư Vô!");

            float elapsed = 0f;
            while (elapsed < meteorRoarTime)
            {
                if (IsDead) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (meteorImpactPrefab == null)
            {
                Debug.LogWarning("[VoidWarden] Chưa gán meteorImpactPrefab, bỏ qua Mưa Thiên Thạch!", this);
            }
            else
            {
                int count = Random.Range(meteorCountMin, meteorCountMax + 1);
                WaitForSeconds interval = new WaitForSeconds(meteorInterval);
                for (int i = 0; i < count; i++)
                {
                    if (IsDead) yield break;

                    // Phân bố đều theo diện tích trong bán kính sàn đấu
                    float angle = Random.value * Mathf.PI * 2f;
                    float radius = arenaRadius * Mathf.Sqrt(Random.value);
                    Vector3 point = arenaCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                    GameObject meteorObj = Instantiate(meteorImpactPrefab, point, Quaternion.identity);
                    if (meteorObj.TryGetComponent(out MeteorImpact impact))
                    {
                        impact.Launch(point, meteorDamage, 0f, meteorRadius, meteorWarningTime,
                            meteorKnockbackForce, meteorKnockbackDuration, playerMask);
                    }
                    else
                    {
                        Destroy(meteorObj);
                    }

                    yield return interval;
                }
            }

            meteorTimer = ScaledCooldown(meteorCooldown);
            yield return new WaitForSeconds(0.8f);
            FinishSkill();
        }
        #endregion

        #region Phase 2
        public override void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
        {
            // Bất tử trong lúc hú chuyển Phase
            if (isTransitioning) return;

            base.TakeDamage(damageAmount, hitPoint, hitDirection);

            if (!isPhase2 && !IsDead && currentHealth <= maxHealth * phase2HealthThreshold)
            {
                EnterPhase2();
            }
        }

        private void EnterPhase2()
        {
            isPhase2 = true;
            isTransitioning = true;

            // Ngắt kỹ năng đang thi triển
            if (skillCoroutine != null) StopCoroutine(skillCoroutine);
            ReleasePlayer();
            HideTelegraphs();

            skillCoroutine = StartCoroutine(Phase2TransitionRoutine());
        }

        private IEnumerator Phase2TransitionRoutine()
        {
            ChangeState(EnemyState.Casting);
            Debug.Log("[VoidWarden] PHASE 2: Cuồng Bạo Hư Vô!");

            float elapsed = 0f;
            while (elapsed < transitionTime)
            {
                elapsed += Time.deltaTime;
                SetArmorBlend(Mathf.Clamp01(elapsed / transitionTime));
                yield return null;
            }
            SetArmorBlend(1f);

            if (agent != null) agent.speed = moveSpeed * phase2SpeedMultiplier;
            if (healthBar != null) healthBar.SetEnraged(true);

            // Hú xung kích Hư Vô đẩy lùi nhẹ người chơi
            Shockwave();

            // Chạm mốc 50% là tung Mưa Thiên Thạch ngay
            meteorTimer = 0f;
            isTransitioning = false;
            FinishSkill();
        }

        private void Shockwave()
        {
            Vector3 center = transform.position;
            int hits = Physics.OverlapSphereNonAlloc(center, shockwaveRadius, hitBuffer, playerMask);

            for (int i = 0; i < hits; i++)
            {
                if (hitBuffer[i] == null) continue;

                IKnockbackable knockback = hitBuffer[i].GetComponentInParent<IKnockbackable>();
                if (knockback == null) continue;

                Vector3 dir = hitBuffer[i].transform.position - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
                knockback.ApplyKnockback(dir, shockwaveForce, shockwaveDuration);
            }
        }

        private void SetArmorBlend(float t)
        {
            if (armorMaterial == null) return;

            armorMaterial.SetColor(BaseColorId, Color.Lerp(phase1Base, phase2Base, t));
            armorMaterial.SetColor(EmissionColorId, Color.Lerp(phase1Emission, phase2Emission, t));
        }
        #endregion

        #region Helpers
        private void FaceTarget()
        {
            if (target == null) return;

            Vector3 look = target.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);
        }

        private void EnsurePlayerRefs()
        {
            if (playerHealth != null) return;

            playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth == null) return;

            playerHealth.TryGetComponent(out playerRb);
            playerHealth.TryGetComponent(out playerController);
            playerHealth.TryGetComponent(out playerDash);
        }

        private bool IsAlreadyDamaged(IDamageable damageable, int damagedCount)
        {
            for (int d = 0; d < damagedCount; d++)
            {
                if (damaged[d] == damageable) return true;
            }
            return false;
        }

        private void ClearDamaged(int damagedCount)
        {
            for (int d = 0; d < damagedCount; d++) damaged[d] = null;
        }

        private void HideTelegraphs()
        {
            coneTelegraph.Cancel();
            circleTelegraph.Cancel();
        }

        private void OnDisable()
        {
            HideTelegraphs();
        }
        #endregion

        protected override void OnSpawnReset()
        {
            skillCoroutine = null;
            isPhase2 = false;
            isTransitioning = false;
            arcTimer = 2f;
            vortexTimer = 6f;
            meteorTimer = 0f;

            ReleasePlayer();
            HideTelegraphs();
            SetArmorBlend(0f);
            if (agent != null) agent.speed = moveSpeed;
            if (healthBar != null) healthBar.SetEnraged(false);
        }

        public override void ApplyKnockback(Vector3 dir, float force, float duration)
        {
            // Miễn nhiễm hoàn toàn đẩy lùi
        }

        public override void ApplyStun(float duration)
        {
            // Miễn nhiễm hoàn toàn choáng
        }

        protected override void Die()
        {
            if (skillCoroutine != null)
            {
                StopCoroutine(skillCoroutine);
                skillCoroutine = null;
            }
            isTransitioning = false;
            ReleasePlayer();
            HideTelegraphs();

            Debug.Log("[VoidWarden] Chúa Tể Hư Vô đã bị tiêu diệt!");
            base.Die();
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, arcRange);

            Gizmos.color = new Color(0.7f, 0.2f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(arenaCenter, arenaRadius);
        }
#endif
    }
}
