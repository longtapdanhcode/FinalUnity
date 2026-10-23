using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Vanguard.UI
{
    /// <summary>
    /// Hiệu ứng xuất hiện của modal chọn thẻ (Sprint 9 - 9.1): nền mờ dần, tiêu đề trượt xuống,
    /// 3 thẻ bật ra lần lượt (scale overshoot + fade). Chạy theo unscaledTime vì modal đặt timeScale = 0.
    /// </summary>
    public sealed class UpgradeModalAnimator : MonoBehaviour
    {
        [SerializeField] private float dimTargetAlpha = 0.85f;
        [SerializeField, Min(0.05f)] private float dimTime = 0.25f;
        [SerializeField, Min(0.05f)] private float cardTime = 0.38f;
        [SerializeField, Min(0f)] private float cardStagger = 0.09f;
        [SerializeField, Min(0f)] private float headerSlide = 40f;

        private Image dim;
        private CanvasGroup[] headers;
        private RectTransform[] headerRects;
        private Vector2[] headerHome;
        private CardRarityFX[] cards;
        private CanvasGroup[] cardGroups;
        private Coroutine routine;

        private void Awake()
        {
            dim = GetComponent<Image>();
            cards = GetComponentsInChildren<CardRarityFX>(true);
            cardGroups = new CanvasGroup[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                cardGroups[i] = cards[i].GetComponent<CanvasGroup>();
                if (cardGroups[i] == null) cardGroups[i] = cards[i].gameObject.AddComponent<CanvasGroup>();
            }

            string[] names = { "TitleText", "SubtitleText" };
            headers = new CanvasGroup[names.Length];
            headerRects = new RectTransform[names.Length];
            headerHome = new Vector2[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = transform.Find(names[i]);
                if (t == null) continue;
                headerRects[i] = (RectTransform)t;
                headerHome[i] = headerRects[i].anchoredPosition;
                headers[i] = t.GetComponent<CanvasGroup>();
                if (headers[i] == null) headers[i] = t.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            SetState(0f, 0f, 0f);
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Animate());
        }

        private void OnDisable()
        {
            routine = null;
            SetState(1f, 1f, 1f);
            for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].EntryScale = 1f;
        }

        private IEnumerator Animate()
        {
            float total = Mathf.Max(dimTime, headerSlide > 0f ? 0.35f : 0f) + cardStagger * cards.Length + cardTime;
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float dimK = Mathf.Clamp01(t / dimTime);
                float headK = EaseOut(Mathf.Clamp01(t / 0.35f));
                SetDim(dimK);
                SetHeader(headK);
                for (int i = 0; i < cards.Length; i++)
                {
                    float k = Mathf.Clamp01((t - 0.12f - i * cardStagger) / cardTime);
                    SetCard(i, k);
                }
                yield return null;
            }
            SetState(1f, 1f, 1f);
            routine = null;
        }

        private void SetState(float dimK, float headK, float cardK)
        {
            SetDim(dimK);
            SetHeader(headK);
            for (int i = 0; i < cards.Length; i++) SetCard(i, cardK);
        }

        private void SetDim(float k)
        {
            if (dim == null) return;
            Color c = dim.color;
            c.a = dimTargetAlpha * k;
            dim.color = c;
        }

        private void SetHeader(float k)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] == null) continue;
                headers[i].alpha = k;
                headerRects[i].anchoredPosition = headerHome[i] + new Vector2(0f, headerSlide * (1f - k));
            }
        }

        private void SetCard(int i, float k)
        {
            if (cards[i] == null) return;
            cardGroups[i].alpha = Mathf.Clamp01(k * 1.6f);
            cards[i].EntryScale = Mathf.LerpUnclamped(0.78f, 1f, EaseOutBack(k));
        }

        private static float EaseOut(float x) { return 1f - (1f - x) * (1f - x) * (1f - x); }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
