using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vanguard.Player;
using Vanguard.Waves;

namespace Vanguard.UI
{
    /// <summary>
    /// Chuyển cảnh và màn kết thúc (Sprint 9 - 9.1): fade-in khi vào trận, màn THẮNG / THUA có số liệu
    /// và nút Chơi lại / Thoát. Overlay được dựng bằng code lúc chạy (Canvas riêng, vẽ trên cùng).
    /// </summary>
    public sealed class GameFlowUI : MonoBehaviour
    {
        [Header("Font & Material (gán bởi Editor tool)")]
        [SerializeField] private TMP_FontAsset boldFont = null;
        [SerializeField] private TMP_FontAsset mediumFont = null;
        [SerializeField] private Material victoryTextMaterial = null;
        [SerializeField] private Material defeatTextMaterial = null;
        [SerializeField] private Material panelMaterial = null;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float introFade = 0.8f;
        [SerializeField, Min(0f)] private float victoryDelay = 1.8f;
        [SerializeField, Min(0f)] private float defeatDelay = 2.2f;
        [SerializeField, Range(0f, 1f)] private float endDimAlpha = 0.78f;

        private Canvas canvas;
        private Image fade;
        private CanvasGroup endGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI statsText;
        private PlayerHealth playerHealth;
        private bool ended;

        private void Awake()
        {
            BuildOverlay();
        }

        private void OnEnable()
        {
            WaveManager.OnAllWavesCleared += HandleVictory;
            playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null) playerHealth.OnPlayerDeath += HandleDefeat;
        }

        private void OnDisable()
        {
            WaveManager.OnAllWavesCleared -= HandleVictory;
            if (playerHealth != null) playerHealth.OnPlayerDeath -= HandleDefeat;
        }

        private void Start()
        {
            StartCoroutine(FadeTo(0f, introFade));
        }

        private void HandleVictory() { if (!ended) StartCoroutine(ShowEnd(true, victoryDelay)); }
        private void HandleDefeat() { if (!ended) StartCoroutine(ShowEnd(false, defeatDelay)); }

        private IEnumerator ShowEnd(bool victory, float delay)
        {
            ended = true;
            yield return new WaitForSecondsRealtime(delay);

            titleText.text = victory ? "CHIẾN THẮNG" : "THẤT BẠI";
            titleText.fontSharedMaterial = victory ? victoryTextMaterial : defeatTextMaterial;
            titleText.color = victory ? new Color(1f, 0.93f, 0.7f, 1f) : new Color(1f, 0.75f, 0.78f, 1f);

            WaveManager waves = WaveManager.Instance;
            int total = waves != null ? waves.TotalWaves : 10;
            int reached = waves != null ? Mathf.Clamp(waves.CurrentWaveNumber, 1, total) : 1;
            float t = Time.timeSinceLevelLoad;
            statsText.text = victory
                ? $"Bạn đã chinh phục cả {total} đợt tấn công\nThời gian: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}"
                : $"Gục ngã ở đợt {reached}/{total}\nThời gian: {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            yield return FadeTo(endDimAlpha, 0.6f);
            endGroup.gameObject.SetActive(true);
            endGroup.alpha = 0f;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.unscaledDeltaTime / 0.4f;
                endGroup.alpha = Mathf.Clamp01(k);
                yield return null;
            }
            endGroup.interactable = true;
            endGroup.blocksRaycasts = true;
            Time.timeScale = 0f;
        }

        private IEnumerator FadeTo(float alpha, float duration)
        {
            float start = fade.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(start, alpha, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetFade(alpha);
        }

        private void SetFade(float alpha)
        {
            Color c = fade.color;
            c.a = alpha;
            fade.color = c;
            fade.raycastTarget = alpha > 0.5f;
        }

        private void Restart()
        {
            StartCoroutine(RestartRoutine());
        }

        private IEnumerator RestartRoutine()
        {
            endGroup.interactable = false;
            yield return FadeTo(1f, 0.4f);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #region Build
        private void BuildOverlay()
        {
            GameObject root = new GameObject("GameFlowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            fade = NewImage("Fade", root.transform, new Color(0f, 0f, 0.02f, 1f));
            Stretch(fade.rectTransform);

            GameObject end = new GameObject("EndScreen", typeof(RectTransform), typeof(CanvasGroup));
            end.transform.SetParent(root.transform, false);
            Stretch((RectTransform)end.transform);
            endGroup = end.GetComponent<CanvasGroup>();
            endGroup.interactable = false;
            endGroup.blocksRaycasts = false;

            titleText = NewText("Title", end.transform, "", 120, boldFont, Color.white, new Vector2(0f, 150f), new Vector2(1400f, 160f));
            titleText.characterSpacing = 10f;
            statsText = NewText("Stats", end.transform, "", 32, mediumFont, new Color(0.8f, 0.88f, 1f, 1f), new Vector2(0f, 20f), new Vector2(1000f, 110f));

            BuildButton(end.transform, "Restart", "CHƠI LẠI", new Vector2(-170f, -140f), new Color(0.1f, 0.85f, 1f, 1f), Restart);
            BuildButton(end.transform, "Quit", "THOÁT", new Vector2(170f, -140f), new Color(0.8f, 0.3f, 1f, 1f), Quit);
            end.SetActive(false);
        }

        private void BuildButton(Transform parent, string objectName, string label, Vector2 pos, Color glow, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(NeonButtonHover));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280f, 64f);

            Image hit = go.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            GameObject glowObj = new GameObject("Glow", typeof(RectTransform), typeof(Image), typeof(NeonPanelFX));
            glowObj.transform.SetParent(go.transform, false);
            RectTransform gr = (RectTransform)glowObj.transform;
            gr.anchorMin = Vector2.zero;
            gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(-14f, -14f);
            gr.offsetMax = new Vector2(14f, 14f);
            glowObj.GetComponent<Image>().raycastTarget = false;
            NeonPanelFX fx = glowObj.GetComponent<NeonPanelFX>();
            fx.Configure(panelMaterial, glow, new Color(0.03f, 0.04f, 0.09f, 0.9f), 0.7f, 14f, 8f);
            go.GetComponent<NeonButtonHover>().Configure(fx);

            TextMeshProUGUI text = NewText("Label", go.transform, label, 30, boldFont, Color.white, Vector2.zero, rt.sizeDelta);
            text.characterSpacing = 5f;
        }

        private static Image NewImage(string objectName, Transform parent, Color color)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI NewText(string objectName, Transform parent, string content, int size, TMP_FontAsset font,
            Color color, Vector2 pos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        #endregion
    }
}
