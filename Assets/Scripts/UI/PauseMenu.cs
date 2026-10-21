using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vanguard.Graphics;

namespace Vanguard.UI
{
    /// <summary>Pause menu (Esc) với panel Graphics: Quality tier, Post-Processing, VSync.</summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel = null;
        [SerializeField] private Button resumeButton = null;
        [SerializeField] private Button tierButton = null;
        [SerializeField] private Button postFxButton = null;
        [SerializeField] private Button vsyncButton = null;

        private bool isPaused;
        private float timeScaleBeforePause = 1f;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (tierButton != null) tierButton.onClick.AddListener(GraphicsSettingsService.CycleTier);
            if (postFxButton != null) postFxButton.onClick.AddListener(() => GraphicsSettingsService.SetPostProcessing(!GraphicsSettingsService.PostProcessing));
            if (vsyncButton != null) vsyncButton.onClick.AddListener(() => GraphicsSettingsService.SetVSync(!GraphicsSettingsService.VSync));
        }

        private void OnEnable() => GraphicsSettingsService.Changed += RefreshLabels;

        private void OnDisable()
        {
            GraphicsSettingsService.Changed -= RefreshLabels;
            if (isPaused) Resume();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            if (isPaused) Resume();
            else if (Time.timeScale > 0f) Pause(); // timeScale = 0 nghĩa là đang ở menu chọn thẻ -> bỏ qua
        }

        private void Pause()
        {
            isPaused = true;
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            if (panel != null) panel.SetActive(true);
            RefreshLabels();
        }

        public void Resume()
        {
            if (!isPaused) return;
            isPaused = false;
            Time.timeScale = timeScaleBeforePause;
            if (panel != null) panel.SetActive(false);
        }

        private void RefreshLabels()
        {
            SetLabel(tierButton, $"Chất lượng: {GraphicsSettingsService.Tier}");
            SetLabel(postFxButton, $"Post-Processing: {(GraphicsSettingsService.PostProcessing ? "BẬT" : "TẮT")}");
            SetLabel(vsyncButton, $"VSync: {(GraphicsSettingsService.VSync ? "BẬT" : "TẮT")}");
        }

        private static void SetLabel(Button button, string text)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }
    }
}
