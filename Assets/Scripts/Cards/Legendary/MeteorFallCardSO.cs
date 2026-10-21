using UnityEngine;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Huyền Thoại "Meteor Fall": mỗi lần lướt, một thiên thạch giáng xuống điểm xuất phát gây sát thương diện rộng.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Legendary_MeteorFall", menuName = "Vanguard/Cards/Legendary/Meteor Fall")]
    public class MeteorFallCardSO : UpgradeCardSO
    {
        [Header("Meteor Fall")]
        [SerializeField] private GameObject meteorPrefab = null;
        [SerializeField] private float damage = 80f;
        [SerializeField] private float stunDuration = 1.0f;
        [SerializeField] private float explosionRadius = 4.5f;

        [Tooltip("Độ trễ từ lúc thiên thạch xuất hiện trên cao đến khi chạm đất (giây)")]
        [SerializeField] private float fallDelay = 0.4f;

        [Header("Knockback")]
        [SerializeField] private float knockbackForce = 20f;
        [SerializeField] private float knockbackDuration = 0.3f;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerDash _))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerDash trên Player!", this);
                return;
            }

            if (meteorPrefab == null)
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: chưa gán meteorPrefab!", this);
            }

            if (!player.TryGetComponent(out MeteorFallEffect effect))
            {
                effect = player.AddComponent<MeteorFallEffect>();
            }
            effect.Configure(meteorPrefab, damage, stunDuration, explosionRadius, fallDelay, knockbackForce, knockbackDuration);

            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Mỗi lần lướt gọi thiên thạch ({damage} DMG, bán kính {explosionRadius}m)!");
        }
    }
}
