using UnityEngine;
using Vanguard.Combat;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Hiếm "Hellfire Slash": đòn chém thiêu đốt quái, cộng dồn tối đa maxStacks tầng.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Rare_HellfireSlash", menuName = "Vanguard/Cards/Rare/Hellfire Slash")]
    public class HellfireSlashCardSO : UpgradeCardSO
    {
        [Header("Hellfire Slash")]
        [SerializeField] private float burnDmgPerSec = 10f;
        [SerializeField] private float burnDuration = 3f;
        [SerializeField] private int maxStacks = 3;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerCombat _))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerCombat trên Player!", this);
                return;
            }

            // Trạng thái runtime nằm trên Player (không lưu trong ScriptableObject dùng chung)
            if (!player.TryGetComponent(out HellfireSlashEffect effect))
            {
                effect = player.AddComponent<HellfireSlashEffect>();
            }
            effect.Configure(burnDmgPerSec, burnDuration, maxStacks);

            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Thiêu đốt {burnDmgPerSec} DMG/s trong {burnDuration}s (tối đa {maxStacks} tầng)!");
        }
    }
}
