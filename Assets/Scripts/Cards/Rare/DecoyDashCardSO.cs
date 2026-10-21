using UnityEngine;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ Hiếm "Decoy Dash": mỗi lần lướt để lại một ảo ảnh thu hút quái xung quanh.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_Rare_DecoyDash", menuName = "Vanguard/Cards/Rare/Decoy Dash")]
    public class DecoyDashCardSO : UpgradeCardSO
    {
        [Header("Decoy Dash")]
        [Tooltip("Thời gian ảo ảnh tồn tại (giây)")]
        [SerializeField] private float decoyDuration = 1.5f;

        [Tooltip("Bán kính thu hút quái (mét)")]
        [SerializeField] private float tauntRadius = 10f;

        [SerializeField] private GameObject decoyPrefab = null;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null || !player.TryGetComponent(out PlayerDash _))
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: không tìm thấy PlayerDash trên Player!", this);
                return;
            }

            if (decoyPrefab == null)
            {
                Debug.LogWarning($"[UpgradeCard] {CardName}: chưa gán decoyPrefab!", this);
            }

            if (!player.TryGetComponent(out DecoyDashEffect effect))
            {
                effect = player.AddComponent<DecoyDashEffect>();
            }
            effect.Configure(decoyPrefab, decoyDuration, tauntRadius);

            Debug.Log($"[UpgradeCard] Kích hoạt {CardName}: Lướt để lại ảo ảnh {decoyDuration}s thu hút quái!");
        }
    }
}
