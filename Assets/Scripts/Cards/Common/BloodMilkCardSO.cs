using UnityEngine;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Thường "Blood Milk": tăng vĩnh viễn Max HP và hồi máu ngay lập tức.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Common_BloodMilk", menuName = "Vanguard/Cards/Common/Blood Milk")]
    public class BloodMilkCardSO : UpgradeCardSO
    {
        [Header("Blood Milk")]
        [SerializeField] private float maxHealthBonus = 20f;
        [SerializeField] private float healAmount = 30f;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerHealth health))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerHealth trên Player!", this);
                return;
            }

            // Tăng Max HP trước, sau đó hồi máu (Heal đã giới hạn không vượt Max HP mới)
            health.IncreaseMaxHealth(maxHealthBonus);
            health.Heal(healAmount);
            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Tăng {maxHealthBonus} Max HP và hồi {healAmount} HP!");
        }
    }
}
