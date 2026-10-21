using UnityEngine;
using UnityEngine.UI;
using Vanguard.Player;
using Vanguard.Waves;

namespace Vanguard.UI
{
    /// <summary>
    /// HUD trong trận (Sprint 5 - Card 5.5): thanh máu và Dash ở góc trên trái,
    /// số Wave và tiến trình diệt quái ở giữa phía trên.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("Top-Left: Máu & Dash")]
        [SerializeField] private Slider hpSlider = null;
        [SerializeField] private Text hpText = null;
        [Tooltip("Ảnh lấp đầy của từng lượt Dash (Image Type = Filled Radial 360)")]
        [SerializeField] private Image[] dashChargeIcons = null;

        [Header("Top-Center: Wave & Quái")]
        [SerializeField] private Text waveNumberText = null;
        [SerializeField] private Slider enemyRemainingSlider = null;
        [SerializeField] private Text enemyRemainingText = null;

        [Header("Nguồn dữ liệu (tự tìm nếu để trống)")]
        [SerializeField] private PlayerHealth playerHealth = null;
        [SerializeField] private PlayerDash playerDash = null;

        [Header("Style")]
        [Tooltip("Tốc độ thanh trượt đuổi theo giá trị thật")]
        [SerializeField] private float sliderSmoothSpeed = 8f;
        [SerializeField] private Color dashReadyColor = new Color(0.2f, 1f, 0.85f, 1f);
        [SerializeField] private Color dashChargingColor = new Color(0.2f, 0.55f, 0.5f, 0.7f);

        private float targetHp = 1f;
        private float targetEnemyFill;
        private bool playerBound;
        private bool wavesSubscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HUDController] Đã có HUDController khác trong Scene, hủy bản sao này.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance != this) return;

            WaveManager.OnWaveStarted += HandleWaveStarted;
            WaveManager.OnEnemiesRemainingChanged += HandleEnemiesRemainingChanged;
            WaveManager.OnAllWavesCleared += HandleAllWavesCleared;
            wavesSubscribed = true;

            BindPlayer();
        }

        private void OnDisable()
        {
            if (wavesSubscribed)
            {
                WaveManager.OnWaveStarted -= HandleWaveStarted;
                WaveManager.OnEnemiesRemainingChanged -= HandleEnemiesRemainingChanged;
                WaveManager.OnAllWavesCleared -= HandleAllWavesCleared;
                wavesSubscribed = false;
            }

            if (playerBound && playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
            }
            playerBound = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (Instance != this) return;

            // Tìm Player nếu lúc OnEnable chưa có tham chiếu
            BindPlayer();

            // Đồng bộ trạng thái Wave hiện tại (có thể Wave 1 đã bắt đầu trước khi HUD khởi động)
            WaveManager waves = WaveManager.Instance;
            if (waves != null)
            {
                if (waves.IsWaveActive) UpdateWaveText(waves.CurrentWaveNumber);
                else UpdateWaveText(1);
                HandleEnemiesRemainingChanged(waves.EnemiesRemainingInWave, waves.TotalEnemiesInWave);
            }

            // Hiển thị ngay giá trị ban đầu, không trượt từ đầu
            if (hpSlider != null) hpSlider.value = targetHp;
            if (enemyRemainingSlider != null) enemyRemainingSlider.value = targetEnemyFill;
        }

        private void Update()
        {
            // Dùng unscaledDeltaTime để thanh vẫn mượt khi Hit-Stop / mở menu
            float t = 1f - Mathf.Exp(-sliderSmoothSpeed * Time.unscaledDeltaTime);

            if (hpSlider != null) hpSlider.value = Mathf.Lerp(hpSlider.value, targetHp, t);
            if (enemyRemainingSlider != null)
            {
                enemyRemainingSlider.value = Mathf.Lerp(enemyRemainingSlider.value, targetEnemyFill, t);
            }

            RefreshDashIcons();
        }

        #region Player
        private void BindPlayer()
        {
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerDash == null) playerDash = FindAnyObjectByType<PlayerDash>();

            if (playerBound || playerHealth == null) return;

            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerBound = true;
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void HandleHealthChanged(float current, float max)
        {
            targetHp = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (hpText != null)
            {
                hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)} HP";
            }
        }

        private void RefreshDashIcons()
        {
            if (dashChargeIcons == null || playerDash == null) return;

            int max = playerDash.MaxCharges;
            int charges = playerDash.CurrentCharges;
            float progress = playerDash.CooldownProgress;

            for (int i = 0; i < dashChargeIcons.Length; i++)
            {
                Image icon = dashChargeIcons[i];
                if (icon == null) continue;

                icon.enabled = i < max;
                if (i < charges)
                {
                    // Sẵn sàng: ngọc bích sáng
                    icon.fillAmount = 1f;
                    icon.color = dashReadyColor;
                }
                else if (i == charges)
                {
                    // Đang nạp: lấp đầy dần theo thời gian hồi
                    icon.fillAmount = progress;
                    icon.color = dashChargingColor;
                }
                else
                {
                    icon.fillAmount = 0f;
                    icon.color = dashChargingColor;
                }
            }
        }
        #endregion

        #region Waves
        private void HandleWaveStarted(int waveNumber)
        {
            UpdateWaveText(waveNumber);
        }

        private void HandleAllWavesCleared()
        {
            if (waveNumberText != null) waveNumberText.text = "CHIẾN THẮNG!";
        }

        private void HandleEnemiesRemainingChanged(int remaining, int total)
        {
            total = Mathf.Max(total, remaining);
            targetEnemyFill = total > 0 ? Mathf.Clamp01(remaining / (float)total) : 0f;
            if (enemyRemainingText != null)
            {
                enemyRemainingText.text = $"Quái còn lại: {remaining} / {total}";
            }
        }

        private void UpdateWaveText(int waveNumber)
        {
            if (waveNumberText == null) return;

            WaveManager waves = WaveManager.Instance;
            int totalWaves = waves != null ? waves.TotalWaves : 10;
            string text = $"WAVE {waveNumber:00}/{totalWaves:00}";

            if (waves != null && !string.IsNullOrEmpty(waves.CurrentWaveTag))
            {
                text += $" - {waves.CurrentWaveTag}";
            }
            waveNumberText.text = text;
        }
        #endregion
    }
}
