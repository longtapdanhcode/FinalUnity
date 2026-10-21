using UnityEngine;
using Vanguard.Combat;
using Vanguard.Enemy;

namespace Vanguard.Cards
{
    /// <summary>
    /// Component runtime trên Player cho thẻ Hellfire Slash: lắng nghe PlayerCombat.OnHitLanded
    /// và áp dụng / cộng dồn BurnStatusEffect lên quái bị chém trúng.
    /// </summary>
    public class HellfireSlashEffect : MonoBehaviour
    {
        private PlayerCombat combat;
        private float burnDmgPerSec = 10f;
        private float burnDuration = 3f;
        private int maxStacks = 3;

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            if (combat != null) combat.OnHitLanded += HandleHitLanded;
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnHitLanded -= HandleHitLanded;
        }

        public void Configure(float dps, float duration, int stackLimit)
        {
            burnDmgPerSec = dps;
            burnDuration = duration;
            maxStacks = Mathf.Max(1, stackLimit);
        }

        private void HandleHitLanded(int comboStep, Collider enemyCollider)
        {
            if (enemyCollider == null) return;

            EnemyBase enemy = enemyCollider.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) return;

            if (!enemy.TryGetComponent(out BurnStatusEffect burn))
            {
                burn = enemy.gameObject.AddComponent<BurnStatusEffect>();
            }
            burn.AddStack(burnDmgPerSec, burnDuration, maxStacks);
        }
    }
}
