using UnityEngine;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Huyền Thoại "Blood God's Vortex": mở rộng bán kính vung kiếm và hồi máu khi hạ gục quái.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Legendary_BloodGodVortex", menuName = "Vanguard/Cards/Legendary/Blood God Vortex")]
    public class BloodGodVortexCardSO : UpgradeCardSO
    {
        [Header("Blood God's Vortex")]
        [Tooltip("Hệ số nhân bán kính vung kiếm (1.3 = +30%)")]
        [SerializeField] private float attackRadiusMultiplier = 1.3f;

        [Tooltip("Tỉ lệ Max HP hồi lại mỗi khi hạ gục 1 quái (0.04 = 4%)")]
        [SerializeField] private float healPercentOnKill = 0.04f;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null
                || !player.TryGetComponent(out PlayerCombat combat)
                || !player.TryGetComponent(out PlayerHealth _))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: Player thiếu PlayerCombat hoặc PlayerHealth!", this);
                return;
            }

            combat.AttackRadiusMultiplier *= attackRadiusMultiplier;

            if (!player.TryGetComponent(out BloodGodVortexEffect effect))
            {
                effect = player.AddComponent<BloodGodVortexEffect>();
            }
            effect.AddHealPercent(healPercentOnKill);

            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Bán kính chém x{attackRadiusMultiplier}, hồi {healPercentOnKill * 100f:0.##}% Max HP mỗi lần hạ gục!");
        }
    }
}
