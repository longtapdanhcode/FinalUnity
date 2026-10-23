using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Vanguard.Cards;

namespace Vanguard.UI
{
    /// <summary>
    /// Hiển thị một thẻ bài nâng cấp trong modal chọn thẻ (Sprint 4 - Card 4.5).
    /// </summary>
    public class UpgradeCardUI : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text titleText = null;
        [SerializeField] private TMP_Text descriptionText = null;
        [SerializeField] private TMP_Text rarityText = null;

        [Header("Images")]
        [SerializeField] private Image iconImage = null;
        [Tooltip("Viền thẻ, đổi màu theo phẩm chất")]
        [SerializeField] private Image borderImage = null;

        [Header("Interaction")]
        [SerializeField] private Button selectButton = null;

        private UpgradeCardSO currentCard;

        public UpgradeCardSO CurrentCard => currentCard;
        public CardRarityFX RarityFX => GetComponent<CardRarityFX>();

        /// <summary>
        /// Đổ dữ liệu thẻ lên UI và đăng ký callback khi người chơi bấm chọn.
        /// </summary>
        public void SetupCard(UpgradeCardSO card, Action<UpgradeCardSO> onSelectedCallback)
        {
            currentCard = card;
            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            Color rarityColor = card.RarityColor;

            if (titleText != null) titleText.text = card.CardName;
            if (descriptionText != null) descriptionText.text = card.Description;

            if (rarityText != null)
            {
                rarityText.text = GetRarityLabel(card.Rarity);
                rarityText.color = rarityColor;
            }

            if (borderImage != null) borderImage.color = rarityColor;

            // Viền glow theo độ hiếm (Card 8.6); nếu chưa gắn CardRarityFX thì giữ viền màu phẳng
            CardRarityFX fx = GetComponent<CardRarityFX>();
            if (fx != null) fx.Apply(card.Rarity, rarityColor);

            if (iconImage != null)
            {
                if (card.Icon != null)
                {
                    iconImage.sprite = card.Icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    // Chưa có icon: hiển thị khối màu phẩm chất làm placeholder
                    iconImage.sprite = null;
                    iconImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.35f);
                }
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelectedCallback?.Invoke(card));
                selectButton.interactable = true;
            }
        }

        private static string GetRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Rare: return "HIẾM";
                case CardRarity.Legendary: return "HUYỀN THOẠI";
                default: return "THƯỜNG";
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null) selectButton.onClick.RemoveAllListeners();
        }
    }
}
