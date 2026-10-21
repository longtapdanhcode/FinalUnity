using UnityEngine;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Thường "Storm Boots": tăng tốc độ di chuyển.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Common_StormBoots", menuName = "Vanguard/Cards/Common/Storm Boots")]
    public class StormBootsCardSO : UpgradeCardSO
    {
        [Header("Storm Boots")]
        [Tooltip("Tỉ lệ tốc độ di chuyển cộng thêm (0.12 = +12%)")]
        [SerializeField] private float speedBonusPercent = 0.12f;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerController controller))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerController trên Player!", this);
                return;
            }

            controller.MoveSpeed *= 1f + speedBonusPercent;
            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Tăng {speedBonusPercent * 100f:0.##}% tốc độ di chuyển!");
        }
    }
}
