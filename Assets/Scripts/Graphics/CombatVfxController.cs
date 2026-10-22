using UnityEngine;
using UnityEngine.VFX;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Gắn trên Player: nối sự kiện gameplay có sẵn (Dash, Combo, Hit, Soul Burst) với VfxManager.
    /// Không sửa logic chiến đấu; thiếu graph thì tự bỏ qua.
    /// </summary>
    public class CombatVfxController : MonoBehaviour
    {
        [Header("Màu slash theo nhịp combo (HDR)")]
        [ColorUsage(false, true)] [SerializeField] private Color slash1 = new Color(0f, 0.8f, 1.2f, 1f);
        [ColorUsage(false, true)] [SerializeField] private Color slash2 = new Color(1.1f, 0.15f, 1.4f, 1f);
        [ColorUsage(false, true)] [SerializeField] private Color slash3 = new Color(1.4f, 0.9f, 0.2f, 1f);

        [Header("Màu khác (HDR)")]
        [ColorUsage(false, true)] [SerializeField] private Color dashColor = new Color(0f, 0.8f, 1.2f, 1f);
        [ColorUsage(false, true)] [SerializeField] private Color impactColor = new Color(1.4f, 1.4f, 1.6f, 1f);
        [ColorUsage(false, true)] [SerializeField] private Color soulBurstColor = new Color(0.9f, 0.2f, 1.6f, 1f);

        private PlayerDash dash;
        private PlayerCombat combat;
        private SoulBurst soulBurst;
        private VisualEffect dashTrail;

        private void Awake()
        {
            dash = GetComponent<PlayerDash>();
            combat = GetComponent<PlayerCombat>();
            soulBurst = GetComponent<SoulBurst>();
            if (soulBurst == null) soulBurst = FindAnyObjectByType<SoulBurst>();
        }

        private void Start()
        {
            if (VfxManager.Instance != null)
            {
                dashTrail = VfxManager.Instance.CreateAttached(VfxId.DashTrail, transform);
                if (dashTrail != null)
                {
                    dashTrail.transform.localPosition = Vector3.up * 0.2f;
                    VfxManager.SetParameters(dashTrail, dashColor);
                }
            }
        }

        private void OnEnable()
        {
            if (dash != null) { dash.OnDashStarted += HandleDashStarted; dash.OnDashEnded += HandleDashEnded; }
            if (combat != null) { combat.OnAttackPerformed += HandleAttack; combat.OnHitLanded += HandleHit; }
            if (soulBurst != null) soulBurst.OnSoulBurstFired += HandleSoulBurst;
        }

        private void OnDisable()
        {
            if (dash != null) { dash.OnDashStarted -= HandleDashStarted; dash.OnDashEnded -= HandleDashEnded; }
            if (combat != null) { combat.OnAttackPerformed -= HandleAttack; combat.OnHitLanded -= HandleHit; }
            if (soulBurst != null) soulBurst.OnSoulBurstFired -= HandleSoulBurst;
        }

        private void HandleDashStarted()
        {
            if (dashTrail != null) dashTrail.SendEvent("OnPlay");
        }

        private void HandleDashEnded()
        {
            if (dashTrail != null) dashTrail.SendEvent("OnStop");
        }

        private void HandleAttack(int step)
        {
            Color c = step <= 1 ? slash1 : (step == 2 ? slash2 : slash3);
            Vector3 pos = transform.position + Vector3.up * 1f + transform.forward * 0.8f;
            // Nhịp 3 là đòn kết: slash lớn hơn
            VfxManager.Play(VfxId.Slash, pos, transform.rotation, c, step >= 3 ? 1.4f : 1f);
        }

        private void HandleHit(int step, Collider enemy)
        {
            if (enemy == null) return;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 point = enemy.ClosestPoint(origin);
            Quaternion rot = Quaternion.LookRotation((origin - point).sqrMagnitude > 0.0001f ? (origin - point).normalized : -transform.forward);
            VfxManager.Play(VfxId.Impact, point, rot, impactColor, step >= 3 ? 1.5f : 1f);
        }

        private void HandleSoulBurst()
        {
            VfxManager.Play(VfxId.SoulBurst, transform.position + Vector3.up * 0.1f, Quaternion.identity, soulBurstColor);
        }
    }
}
