using UnityEngine;
using Vanguard.Combat;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Hiếm "Thunder Shock": đòn chém thứ 3 phóng tia sét giật lan sang quái lân cận.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Rare_ThunderShock", menuName = "Vanguard/Cards/Rare/Thunder Shock")]
    public class ThunderShockCardSO : UpgradeCardSO
    {
        [Header("Thunder Shock")]
        [SerializeField] private float shockDamage = 25f;
        [SerializeField] private int maxChainTargets = 3;
        [SerializeField] private float chainRadius = 6f;
        [SerializeField] private GameObject lightningVFXPrefab = null;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerCombat _))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerCombat trên Player!", this);
                return;
            }

            if (!player.TryGetComponent(out ThunderShockEffect effect))
            {
                effect = player.AddComponent<ThunderShockEffect>();
            }
            effect.Configure(shockDamage, maxChainTargets, chainRadius, lightningVFXPrefab);

            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Đòn chém thứ 3 giật lan {maxChainTargets} mục tiêu ({shockDamage} DMG)!");
        }
    }
}
