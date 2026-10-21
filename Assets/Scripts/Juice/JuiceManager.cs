using System.Collections;
using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.Juice
{
    /// <summary>
    /// Quản lý "Game Feel" toàn cục (Sprint 5 - Card 5.3): Hit-Stop (khựng hình) và Screen Shake.
    /// Rung camera dùng CameraShaker (Perlin Noise) nên không cần package Cinemachine.
    /// </summary>
    public class JuiceManager : MonoBehaviour
    {
        // Time.timeScale trong lúc khựng hình (khác 0 để phân biệt với pause của menu chọn thẻ)
        private const float HitStopTimeScale = 0.05f;

        public static JuiceManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CameraShaker cameraShaker = null;

        [Header("Settings")]
        [SerializeField] private bool enableHitStop = true;
        [SerializeField] private bool enableScreenShake = true;

        private bool isHitStopping;
        private float hitStopEndTime; // unscaledTime

        public bool IsHitStopping => isHitStopping;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[JuiceManager] Đã có JuiceManager khác trong Scene, hủy bản sao này.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            if (cameraShaker == null)
            {
                Camera cam = Camera.main;
                if (cam != null) cameraShaker = cam.GetComponent<CameraShaker>();
            }
        }

        private void OnDisable()
        {
            if (Instance == this) EndHitStop();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region Static helpers (an toàn khi Scene chưa có JuiceManager)
        public static void HitStop(float duration = 0.06f)
        {
            if (Instance != null) Instance.DoHitStop(duration);
        }

        public static void Shake(float intensity = 1f, float duration = 0.2f)
        {
            if (Instance != null) Instance.TriggerScreenShake(intensity, duration);
        }
        #endregion

        /// <summary>
        /// Khựng hình trong duration giây (thời gian thực). Bỏ qua nếu game đang tạm dừng (timeScale = 0).
        /// Gọi dồn dập chỉ kéo dài thời gian khựng, không chồng Coroutine.
        /// </summary>
        public void DoHitStop(float duration = 0.06f)
        {
            if (!enableHitStop || duration <= 0f) return;

            // Đang pause (menu chọn thẻ...) -> không can thiệp
            if (!isHitStopping && Time.timeScale <= 0f) return;

            hitStopEndTime = Mathf.Max(hitStopEndTime, Time.unscaledTime + duration);
            NeonPostFX.Pulse(0.3f);

            if (!isHitStopping)
            {
                StartCoroutine(HitStopRoutine());
            }
        }

        private IEnumerator HitStopRoutine()
        {
            isHitStopping = true;
            Time.timeScale = HitStopTimeScale;

            while (Time.unscaledTime < hitStopEndTime)
            {
                // Nơi khác (ví dụ UpgradeManager) đã đổi timeScale -> nhường quyền, không khôi phục đè
                if (!Mathf.Approximately(Time.timeScale, HitStopTimeScale)) break;
                yield return null;
            }

            EndHitStop();
        }

        private void EndHitStop()
        {
            if (!isHitStopping) return;
            isHitStopping = false;

            // Chỉ khôi phục nếu timeScale vẫn là giá trị do hit-stop đặt (tránh phá vỡ pause của menu)
            if (Mathf.Approximately(Time.timeScale, HitStopTimeScale))
            {
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Rung camera với cường độ (0-1+) trong duration giây.
        /// </summary>
        public void TriggerScreenShake(float intensity = 1.0f, float duration = 0.2f)
        {
            if (!enableScreenShake || cameraShaker == null) return;

            cameraShaker.Shake(intensity, duration);
        }
    }
}
