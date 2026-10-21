using UnityEngine;

namespace Vanguard.Cards
{
    /// <summary>
    /// Lớp cơ sở trừu tượng cho mọi thẻ bài nâng cấp Roguelite (Sprint 4 - Card 4.1).
    /// Mỗi loại thẻ cụ thể kế thừa và hiện thực ApplyUpgrade.
    /// </summary>
    public abstract class UpgradeCardSO : ScriptableObject
    {
        private static readonly Color CommonColor = new Color32(0xCC, 0xCC, 0xCC, 0xFF);
        private static readonly Color RareColor = new Color32(0x33, 0x99, 0xFF, 0xFF);
        private static readonly Color LegendaryColor = new Color32(0xFF, 0x99, 0x00, 0xFF);

        [Header("Card Info")]
        [SerializeField] private string cardID = string.Empty;
        [SerializeField] private string cardName = string.Empty;
        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;
        [SerializeField] private CardRarity rarity = CardRarity.Common;
        [SerializeField] private Sprite icon = null;

        #region Properties
        public string CardID => cardID;
        public string CardName => cardName;
        public string Description => description;
        public CardRarity Rarity => rarity;
        public Sprite Icon => icon;

        /// <summary>
        /// Màu hiển thị theo phẩm chất: Common #CCCCCC, Rare #3399FF, Legendary #FF9900.
        /// </summary>
        public Color RarityColor
        {
            get
            {
                switch (rarity)
                {
                    case CardRarity.Rare: return RareColor;
                    case CardRarity.Legendary: return LegendaryColor;
                    default: return CommonColor;
                }
            }
        }
        #endregion

        /// <summary>
        /// Áp dụng hiệu ứng nâng cấp lên người chơi.
        /// </summary>
        /// <param name="player">GameObject người chơi (chứa PlayerHealth, PlayerController, PlayerCombat...).</param>
        public abstract void ApplyUpgrade(GameObject player);
    }
}
