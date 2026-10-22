using System.Collections.Generic;
using UnityEngine;
using Vanguard.Cards;
using Vanguard.Combat;
using Vanguard.Player;
using Vanguard.Waves;

namespace Vanguard.UI
{
    /// <summary>
    /// Bốc 3 thẻ ngẫu nhiên và hiển thị modal chọn nâng cấp sau mỗi wave (Sprint 4 - Card 4.5).
    /// Tạm dừng game trong lúc chọn, áp dụng thẻ rồi cho WaveManager chạy wave kế tiếp.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject modalPanel = null;
        [SerializeField] private List<UpgradeCardUI> cardUIItems = new List<UpgradeCardUI>();

        [Header("Data")]
        [SerializeField] private List<UpgradeCardSO> allAvailableCards = new List<UpgradeCardSO>();
        [SerializeField] private GameObject player = null;

        [Header("Rarity Weights")]
        [Tooltip("Trọng số bốc thẻ theo phẩm chất (càng cao càng dễ xuất hiện)")]
        [SerializeField] private float commonWeight = 60f;
        [SerializeField] private float rareWeight = 30f;
        [SerializeField] private float legendaryWeight = 10f;

        private bool isModalOpen;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        // Khóa input chiến đấu trong lúc chọn thẻ (tránh click thẻ cũng chém / lướt)
        private PlayerCombat playerCombat;
        private PlayerDash playerDash;
        private bool combatWasEnabled;
        private bool dashWasEnabled;

        private readonly List<UpgradeCardSO> drawPool = new List<UpgradeCardSO>();

        public bool IsModalOpen => isModalOpen;

        private void OnEnable()
        {
            WaveManager.OnWaveCompleted += HandleWaveCompleted;
        }

        private void OnDisable()
        {
            WaveManager.OnWaveCompleted -= HandleWaveCompleted;

            // Không để game bị kẹt ở timeScale 0 nếu manager bị tắt khi modal đang mở
            if (isModalOpen) CloseModal();
        }

        private void Start()
        {
            if (modalPanel != null) modalPanel.SetActive(false);

            if (player == null)
            {
                PlayerHealth health = FindAnyObjectByType<PlayerHealth>();
                if (health != null) player = health.gameObject;
            }
            if (player != null)
            {
                playerCombat = player.GetComponent<PlayerCombat>();
                playerDash = player.GetComponent<PlayerDash>();
            }

            // Sau mỗi wave, WaveManager chờ người chơi chọn xong thẻ rồi mới sang wave kế tiếp
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.WaitForExternalResume = true;
            }
            else
            {
                Debug.LogWarning("[UpgradeManager] Không tìm thấy WaveManager trong Scene!", this);
            }
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            // Wave cuối cùng: chiến thắng, không cần chọn thẻ
            WaveManager waves = WaveManager.Instance;
            if (waves != null && waveNumber >= waves.TotalWaves) return;

            OpenModal();
        }

        /// <summary>
        /// Mở modal: dừng thời gian, mở khóa chuột, bốc và hiển thị 3 thẻ.
        /// </summary>
        public void OpenModal()
        {
            if (isModalOpen) return;

            if (modalPanel == null || cardUIItems.Count == 0)
            {
                Debug.LogError("[UpgradeManager] Thiếu modalPanel hoặc cardUIItems! Chạy menu Vanguard > Setup Upgrade Selection UI.", this);
                ResumeWaves();
                return;
            }

            DrawCards(cardUIItems.Count);
            if (drawPool.Count == 0)
            {
                Debug.LogWarning("[UpgradeManager] Kho thẻ trống, bỏ qua bước chọn nâng cấp.", this);
                ResumeWaves();
                return;
            }

            isModalOpen = true;
            modalPanel.SetActive(true);
            Time.timeScale = 0f;

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetCombatInputEnabled(false);

            for (int i = 0; i < cardUIItems.Count; i++)
            {
                if (cardUIItems[i] == null) continue;
                cardUIItems[i].SetupCard(i < drawPool.Count ? drawPool[i] : null, OnCardChosen);
            }
        }

        private void CloseModal()
        {
            isModalOpen = false;
            if (modalPanel != null) modalPanel.SetActive(false);
            Time.timeScale = 1f;

            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;

            SetCombatInputEnabled(true);
        }

        /// <summary>
        /// Người chơi chọn một thẻ: áp dụng nâng cấp, đóng modal và tiếp tục wave kế tiếp.
        /// </summary>
        public void OnCardChosen(UpgradeCardSO card)
        {
            if (!isModalOpen || card == null) return;

            RectTransform chosenRect = FindCardRect(card);

            // Bật lại input trước để các component Effect của thẻ khởi tạo trên Player đang hoạt động
            CloseModal();

            card.ApplyUpgrade(player);
            CardActivationFX.Play(chosenRect, card);
            Debug.Log($"[UpgradeManager] Đã chọn thẻ: {card.CardName} ({card.Rarity})");

            ResumeWaves();
        }

        private RectTransform FindCardRect(UpgradeCardSO card)
        {
            for (int i = 0; i < cardUIItems.Count; i++)
            {
                UpgradeCardUI ui = cardUIItems[i];
                if (ui != null && ui.CurrentCard == card) return ui.transform as RectTransform;
            }
            return null;
        }

        private void ResumeWaves()
        {
            if (WaveManager.Instance != null) WaveManager.Instance.ResumeNextWave();
        }

        private void SetCombatInputEnabled(bool enable)
        {
            if (!enable)
            {
                combatWasEnabled = playerCombat != null && playerCombat.enabled;
                dashWasEnabled = playerDash != null && playerDash.enabled;
                if (combatWasEnabled) playerCombat.enabled = false;
                if (dashWasEnabled) playerDash.enabled = false;
            }
            else
            {
                if (combatWasEnabled && playerCombat != null) playerCombat.enabled = true;
                if (dashWasEnabled && playerDash != null) playerDash.enabled = true;
            }
        }

        /// <summary>
        /// Bốc ngẫu nhiên tối đa count thẻ không trùng lặp, ưu tiên theo trọng số phẩm chất.
        /// Kết quả nằm trong drawPool.
        /// </summary>
        private void DrawCards(int count)
        {
            drawPool.Clear();

            // Dùng danh sách tạm để loại dần các thẻ đã bốc
            var candidates = new List<UpgradeCardSO>(allAvailableCards.Count);
            for (int i = 0; i < allAvailableCards.Count; i++)
            {
                if (allAvailableCards[i] != null) candidates.Add(allAvailableCards[i]);
            }

            while (drawPool.Count < count && candidates.Count > 0)
            {
                float totalWeight = 0f;
                for (int i = 0; i < candidates.Count; i++) totalWeight += GetWeight(candidates[i].Rarity);

                int picked = candidates.Count - 1;
                if (totalWeight > 0f)
                {
                    float roll = Random.value * totalWeight;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        roll -= GetWeight(candidates[i].Rarity);
                        if (roll <= 0f)
                        {
                            picked = i;
                            break;
                        }
                    }
                }
                else
                {
                    picked = Random.Range(0, candidates.Count);
                }

                drawPool.Add(candidates[picked]);
                candidates.RemoveAt(picked);
            }
        }

        private float GetWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Rare: return rareWeight;
                case CardRarity.Legendary: return legendaryWeight;
                default: return commonWeight;
            }
        }
    }
}
