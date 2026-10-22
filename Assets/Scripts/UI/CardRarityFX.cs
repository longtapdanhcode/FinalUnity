using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vanguard.Cards;

namespace Vanguard.UI
{
    /// <summary>
    /// Hiệu ứng viền/glow theo độ hiếm của thẻ nâng cấp (Sprint 8 - Card 8.6).
    /// Dùng shader Vanguard/UICardGlow trên Image viền; hover = phóng nhẹ + tăng sáng + tia lửa.
    /// Toàn bộ chạy theo unscaledTime vì modal chọn thẻ đặt timeScale = 0.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardRarityFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int ColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int RadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int FrameId = Shader.PropertyToID("_FrameWidth");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
        private static readonly int ShimmerStrengthId = Shader.PropertyToID("_ShimmerStrength");
        private static readonly int SparkleId = Shader.PropertyToID("_Sparkle");
        private static readonly int HoverId = Shader.PropertyToID("_Hover");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int InsetId = Shader.PropertyToID("_Inset");
        private static readonly int TimeId = Shader.PropertyToID("_UTime");

        [Header("References")]
        [SerializeField] private Image glowImage = null;
        [SerializeField] private Material glowMaterial = null;

        [Header("Hover")]
        [SerializeField, Min(1f)] private float hoverScale = 1.06f;
        [SerializeField, Min(1f)] private float hoverSpeed = 12f;
        [SerializeField, Min(0f)] private float sparksPerSecond = 22f;

        [Header("Layout")]
        [Tooltip("Số pixel Background tràn ra ngoài thân thẻ (khớp offset của Background)")]
        [SerializeField, Min(0f)] private float glowInset = 20f;

        private struct Spark
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
        }

        private Material instance;
        private RectTransform selfRect;
        private RectTransform glowRect;
        private readonly List<Spark> sparks = new List<Spark>(32);
        private Color baseColor = Color.white;
        private CardRarity rarity = CardRarity.Common;
        private float hover;
        private bool pointerInside;
        private float sparkAccumulator;
        private float sparkleStrength;

        public void Configure(Image image, Material material, float inset)
        {
            glowImage = image;
            glowMaterial = material;
            glowInset = inset;
        }

        private void Awake()
        {
            selfRect = transform as RectTransform;
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (instance != null || glowImage == null || glowMaterial == null) return;
            instance = new Material(glowMaterial) { name = glowMaterial.name + " (Instance)" };
            glowImage.material = instance;
            glowRect = glowImage.rectTransform;
        }

        private void OnEnable()
        {
            hover = 0f;
            pointerInside = false;
            if (selfRect != null) selfRect.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            ClearSparks();
        }

        private void OnDestroy()
        {
            if (instance != null) Destroy(instance);
        }

        /// <summary>Áp preset theo độ hiếm của thẻ.</summary>
        public void Apply(CardRarity cardRarity, Color color)
        {
            EnsureMaterial();
            if (instance == null) return;

            rarity = cardRarity;
            baseColor = color;
            float intensity, radius, frame, pulseSpeed, pulseAmount, shimmerSpeed, shimmerStrength;
            float hdr;

            switch (cardRarity)
            {
                case CardRarity.Legendary:
                    intensity = 1.8f; radius = 30f; frame = 4f; pulseSpeed = 3.5f; pulseAmount = 0.4f;
                    shimmerSpeed = 1.1f; shimmerStrength = 1.0f; sparkleStrength = 1f; hdr = 1.6f;
                    break;
                case CardRarity.Rare:
                    intensity = 1.0f; radius = 20f; frame = 3f; pulseSpeed = 2f; pulseAmount = 0.25f;
                    shimmerSpeed = 0.5f; shimmerStrength = 0.4f; sparkleStrength = 0f; hdr = 1.3f;
                    break;
                default:
                    intensity = 0.45f; radius = 10f; frame = 2f; pulseSpeed = 0f; pulseAmount = 0f;
                    shimmerSpeed = 0f; shimmerStrength = 0f; sparkleStrength = 0f; hdr = 1f;
                    break;
            }

            Color glow = new Color(color.r * hdr, color.g * hdr, color.b * hdr, 1f);
            instance.SetColor(ColorId, glow);
            instance.SetFloat(IntensityId, intensity);
            instance.SetFloat(RadiusId, radius);
            instance.SetFloat(FrameId, frame);
            instance.SetFloat(PulseSpeedId, pulseSpeed);
            instance.SetFloat(PulseAmountId, pulseAmount);
            instance.SetFloat(ShimmerSpeedId, shimmerSpeed);
            instance.SetFloat(ShimmerStrengthId, shimmerStrength);
            instance.SetFloat(SparkleId, sparkleStrength);
            instance.SetFloat(InsetId, glowInset);
            hover = 0f;
            instance.SetFloat(HoverId, 0f);
            if (glowImage != null) glowImage.color = Color.white;
        }

        public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; }
        public void OnPointerExit(PointerEventData eventData) { pointerInside = false; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hover = Mathf.MoveTowards(hover, pointerInside ? 1f : 0f, hoverSpeed * dt);

            if (selfRect != null)
            {
                float s = Mathf.Lerp(1f, hoverScale, hover);
                selfRect.localScale = new Vector3(s, s, 1f);
            }

            if (instance != null)
            {
                instance.SetFloat(TimeId, Time.unscaledTime);
                instance.SetFloat(HoverId, hover);
                if (glowRect != null) instance.SetVector(SizeId, glowRect.rect.size);
            }

            if (pointerInside && rarity != CardRarity.Common)
            {
                sparkAccumulator += sparksPerSecond * (rarity == CardRarity.Legendary ? 1.6f : 1f) * dt;
                while (sparkAccumulator >= 1f)
                {
                    sparkAccumulator -= 1f;
                    SpawnSpark();
                }
            }

            TickSparks(dt);
        }

        private void SpawnSpark()
        {
            if (selfRect == null || sparks.Count >= 48) return;

            Rect r = selfRect.rect;
            // Điểm ngẫu nhiên trên chu vi thân thẻ
            float perimeter = 2f * (r.width + r.height);
            float t = Random.value * perimeter;
            Vector2 pos;
            Vector2 normal;
            if (t < r.width) { pos = new Vector2(r.xMin + t, r.yMax); normal = Vector2.up; }
            else if (t < r.width + r.height) { pos = new Vector2(r.xMax, r.yMax - (t - r.width)); normal = Vector2.right; }
            else if (t < 2f * r.width + r.height) { pos = new Vector2(r.xMax - (t - r.width - r.height), r.yMin); normal = Vector2.down; }
            else { pos = new Vector2(r.xMin, r.yMin + (t - 2f * r.width - r.height)); normal = Vector2.left; }

            GameObject go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(selfRect, false);
            RectTransform rt = (RectTransform)go.transform;
            float size = Random.Range(2f, rarity == CardRarity.Legendary ? 5f : 3.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos - selfRect.rect.center;

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            Color c = Color.Lerp(baseColor, Color.white, 0.5f);
            img.color = c;

            Vector2 tangent = new Vector2(-normal.y, normal.x) * Random.Range(-18f, 18f);
            Vector2 vel = normal * Random.Range(25f, 70f) + tangent + Vector2.up * Random.Range(5f, 25f);
            float life = Random.Range(0.35f, 0.8f);
            sparks.Add(new Spark { Rect = rt, Image = img, Velocity = vel, Life = life, MaxLife = life });
        }

        private void TickSparks(float dt)
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark sp = sparks[i];
                sp.Life -= dt;
                if (sp.Life <= 0f || sp.Rect == null)
                {
                    if (sp.Rect != null) Destroy(sp.Rect.gameObject);
                    sparks.RemoveAt(i);
                    continue;
                }

                sp.Rect.anchoredPosition += sp.Velocity * dt;
                Color c = sp.Image.color;
                c.a = Mathf.Clamp01(sp.Life / sp.MaxLife);
                sp.Image.color = c;
                sparks[i] = sp;
            }
        }

        private void ClearSparks()
        {
            for (int i = 0; i < sparks.Count; i++)
                if (sparks[i].Rect != null) Destroy(sparks[i].Rect.gameObject);
            sparks.Clear();
        }
    }
}
