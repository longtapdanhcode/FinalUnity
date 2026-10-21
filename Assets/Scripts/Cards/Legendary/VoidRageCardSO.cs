using UnityEngine;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Huyền Thoại "Void Rage": khi máu xuống thấp, gấp đôi sát thương và tăng tỉ lệ chí mạng.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Legendary_VoidRage", menuName = "Vanguard/Cards/Legendary/Void Rage")]
    public class VoidRageCardSO : UpgradeCardSO
    {
        [Header("Void Rage")]
        [Tooltip("Kích hoạt khi máu dưới tỉ lệ này (0.3 = 30% Max HP)")]
        [SerializeField] private float healthThreshold = 0.3f;

        [Tooltip("Sát thương cộng thêm khi cuồng nộ (1.0 = +100%)")]
        [SerializeField] private float bonusDamagePercent = 1.0f;

        [Tooltip("Tỉ lệ chí mạng cộng thêm khi cuồng nộ (0.5 = +50%, chí mạng x2 sát thương)")]
        [SerializeField] private float bonusCritChance = 0.5f;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null
                || !player.TryGetComponent(out PlayerCombat _)
                || !player.TryGetComponent(out PlayerHealth _))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: Player thiếu PlayerCombat hoặc PlayerHealth!", this);
                return;
            }

            if (!player.TryGetComponent(out VoidRageEffect effect))
            {
                effect = player.AddComponent<VoidRageEffect>();
            }
            effect.AddStack(healthThreshold, bonusDamagePercent, bonusCritChance);

            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Dưới {healthThreshold * 100f:0.##}% HP, +{bonusDamagePercent * 100f:0.##}% sát thương và +{bonusCritChance * 100f:0.##}% chí mạng!");
        }
    }
}
