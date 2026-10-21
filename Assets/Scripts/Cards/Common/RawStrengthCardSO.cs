using UnityEngine;
using Vanguard.Combat;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Thường "Raw Strength": tăng sát thương tấn công cơ bản.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Common_RawStrength", menuName = "Vanguard/Cards/Common/Raw Strength")]
    public class RawStrengthCardSO : UpgradeCardSO
    {
        [Header("Raw Strength")]
        [Tooltip("Tỉ lệ sát thương cộng thêm (0.15 = +15%)")]
        [SerializeField] private float damageBonusPercent = 0.15f;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerCombat playerCombat))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerCombat trên Player!", this);
                return;
            }

            playerCombat.DamageMultiplier += damageBonusPercent;
            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Tăng {damageBonusPercent * 100f:0.##}% sát thương!");
        }
    }
}
