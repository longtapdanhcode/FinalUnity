using UnityEngine;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Component runtime trên Player cho thẻ Void Rage: khi máu dưới ngưỡng, tăng sát thương và tỉ lệ chí mạng;
    /// khi máu hồi lên trên ngưỡng thì gỡ buff. Các lần nhận thẻ sau sẽ cộng dồn bonus.
    /// </summary>
    public class VoidRageEffect : MonoBehaviour
    {
        private PlayerHealth health;
        private PlayerCombat combat;

        private float healthThreshold;
        private float bonusDamage;
        private float bonusCritChance;

        private bool isEnraged;
        private float appliedDamage;
        private float appliedCrit;

        public bool IsEnraged => isEnraged;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            combat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnHealthChanged -= HandleHealthChanged;
            RemoveBuff();
        }

        /// <summary>
        /// Cộng dồn cấu hình từ một thẻ Void Rage (lấy ngưỡng máu cao nhất).
        /// </summary>
        public void AddStack(float threshold, float damagePercent, float critChance)
        {
            // Gỡ buff cũ rồi áp dụng lại với tổng bonus mới
            bool wasEnraged = isEnraged;
            RemoveBuff();

            healthThreshold = Mathf.Max(healthThreshold, threshold);
            bonusDamage += damagePercent;
            bonusCritChance += critChance;

            if (health != null) Evaluate(health.CurrentHealth, health.MaxHealth, wasEnraged);
        }

        private void HandleHealthChanged(float current, float max)
        {
            Evaluate(current, max, isEnraged);
        }

        private void Evaluate(float current, float max, bool wasEnraged)
        {
            bool shouldBeEnraged = max > 0f && current < max * healthThreshold;

            if (shouldBeEnraged && !isEnraged)
            {
                ApplyBuff();
                if (!wasEnraged)
                {
                    Debug.Log($"[VoidRage] CUỒNG NỘ HƯ VÔ! Sát thương +{bonusDamage * 100f:0.##}%, chí mạng +{bonusCritChance * 100f:0.##}%!");
                }
            }
            else if (!shouldBeEnraged && isEnraged)
            {
                RemoveBuff();
                Debug.Log("[VoidRage] Cơn cuồng nộ lắng xuống, chỉ số trở về bình thường.");
            }
        }

        private void ApplyBuff()
        {
            if (isEnraged || combat == null) return;

            appliedDamage = bonusDamage;
            appliedCrit = bonusCritChance;
            combat.DamageMultiplier += appliedDamage;
            combat.CritChance += appliedCrit;
            isEnraged = true;
        }

        private void RemoveBuff()
        {
            if (!isEnraged) return;

            if (combat != null)
            {
                combat.DamageMultiplier -= appliedDamage;
                combat.CritChance = Mathf.Max(0f, combat.CritChance - appliedCrit);
            }
            appliedDamage = 0f;
            appliedCrit = 0f;
            isEnraged = false;
        }
    }
}
