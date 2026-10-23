using UnityEngine;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Graphics
{
    /// <summary>Điều khiển animation Player từ các sự kiện gameplay có sẵn (không đụng logic).</summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        private const string Idle = "Idle", Run = "Run", Dash = "Dash", Combo = "Combo", SoulBurstState = "SoulBurst", Death = "Death";

        [SerializeField] private StateAnimator model = null;
        [SerializeField, Min(0.01f)] private float runThreshold = 0.5f;
        [SerializeField, Min(0.1f)] private float attackLock = 0.35f;
        [SerializeField, Min(0.1f)] private float dashLock = 0.35f;
        [SerializeField, Min(0.1f)] private float soulBurstLock = 1f;

        private Rigidbody rb;
        private PlayerDash dash;
        private PlayerCombat combat;
        private PlayerHealth health;
        private SoulBurst soulBurst;
        private NeonModelFX fx;
        private bool dead;
        private bool dashFacing;
        private Vector3 dashDirection;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            dash = GetComponent<PlayerDash>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<PlayerHealth>();
            soulBurst = GetComponent<SoulBurst>();
            if (soulBurst == null) soulBurst = FindAnyObjectByType<SoulBurst>();
            if (model == null) model = GetComponentInChildren<StateAnimator>();
            if (model != null)
            {
                fx = model.GetComponent<NeonModelFX>();
                if (fx == null) fx = model.gameObject.AddComponent<NeonModelFX>();
            }
        }

        private void OnEnable()
        {
            if (dash != null) { dash.OnDashStarted += HandleDash; dash.OnDashEnded += HandleDashEnded; }
            if (combat != null) combat.OnAttackPerformed += HandleAttack;
            if (health != null) { health.OnPlayerDeath += HandleDeath; health.OnDamaged += HandleDamaged; }
            if (soulBurst != null) soulBurst.OnSoulBurstFired += HandleSoulBurst;
        }

        private void OnDisable()
        {
            if (dash != null) { dash.OnDashStarted -= HandleDash; dash.OnDashEnded -= HandleDashEnded; }
            EndDashFacing();
            if (combat != null) combat.OnAttackPerformed -= HandleAttack;
            if (health != null) { health.OnPlayerDeath -= HandleDeath; health.OnDamaged -= HandleDamaged; }
            if (soulBurst != null) soulBurst.OnSoulBurstFired -= HandleSoulBurst;
        }

        private void Update()
        {
            if (model == null || dead) return;
            Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
            v.y = 0f;
            model.Play(v.magnitude > runThreshold ? Run : Idle, 0.12f);
        }

        private void HandleDash()
        {
            if (dead) return;
            model.Play(Dash, 0.05f, 0f, dashLock, true);

            // Hướng dash theo WASD nhưng thân nhân vật quay theo chuột -> clip lao tới trông như bị kéo ngược.
            // Xoay model theo hướng lướt trong suốt cú dash, trả về hướng cũ khi kết thúc.
            Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
            v.y = 0f;
            if (v.sqrMagnitude > 0.01f)
            {
                dashDirection = v.normalized;
                dashFacing = true;
                model.transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
            }
        }

        private void HandleDashEnded() { EndDashFacing(); }

        private void EndDashFacing()
        {
            dashFacing = false;
            if (model != null) model.transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            // Giữ model đúng hướng lướt kể cả khi root còn xoay theo chuột
            if (dashFacing && model != null) model.transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
        }

        private void HandleAttack(int step)
        {
            if (dead) return;
            // Clip "Combo" chứa 3 đòn nối tiếp: mỗi bước nhảy tới 1/3 clip tương ứng
            float offset = Mathf.Clamp((step - 1) % 3, 0, 2) / 3f;
            model.Play(Combo, 0.05f, offset, attackLock, true);
        }

        private void HandleSoulBurst() { if (!dead) model.Play(SoulBurstState, 0.05f, 0f, soulBurstLock, true); }

        private void HandleDamaged(float amount)
        {
            if (!dead && fx != null) fx.Flash(0.1f);
        }

        private void HandleDeath()
        {
            dead = true;
            EndDashFacing();
            model.Play(Death, 0.1f, 0f, 0f, true);
            if (fx != null) fx.Dissolve(1.0f, 0.8f);
        }
    }
}
