using UnityEngine;
using UnityEngine.UI;

namespace Vanguard.UI
{
    /// <summary>
    /// Khung panel neon cho UI (Sprint 9 - 9.1): dùng shader Vanguard/UICardGlow để vẽ nền kính tối,
    /// viền sáng, glow ngoài, nhịp thở và dải shimmer. Gắn lên một Image con được kéo giãn ra ngoài panel
    /// đúng bằng <see cref="inset"/> pixel (phần tràn ra là vùng glow).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class NeonPanelFX : MonoBehaviour
    {
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int RadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int FrameId = Shader.PropertyToID("_FrameWidth");
        private static readonly int CornerId = Shader.PropertyToID("_Corner");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
        private static readonly int ShimmerStrengthId = Shader.PropertyToID("_ShimmerStrength");
        private static readonly int InsetId = Shader.PropertyToID("_Inset");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int TimeId = Shader.PropertyToID("_UTime");
        private static readonly int HoverId = Shader.PropertyToID("_Hover");

        [SerializeField] private Material sharedMaterial = null;
        [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(0.1f, 0.85f, 1f, 1f);
        [SerializeField] private Color fillColor = new Color(0.03f, 0.04f, 0.09f, 0.88f);
        [SerializeField, Min(0f)] private float intensity = 0.8f;
        [SerializeField, Min(1f)] private float glowRadius = 14f;
        [SerializeField, Min(0f)] private float frameWidth = 2f;
        [SerializeField, Min(0f)] private float corner = 8f;
        [SerializeField, Min(0f)] private float inset = 16f;
        [SerializeField, Min(0f)] private float pulseSpeed = 1.2f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.12f;
        [SerializeField, Min(0f)] private float shimmerSpeed = 0.12f;
        [SerializeField, Range(0f, 1f)] private float shimmerStrength = 0.15f;
        [SerializeField, Range(0f, 1f)] private float hover;

        private Image image;
        private RectTransform rect;
        private Material instance;

        /// <summary>Đặt toàn bộ thông số một lần (dùng bởi Editor tool).</summary>
        public void Configure(Material material, Color glow, Color fill, float glowIntensity, float insetPx,
            float cornerPx = 8f, float frame = 2f, float radius = 14f)
        {
            sharedMaterial = material;
            glowColor = glow;
            fillColor = fill;
            intensity = glowIntensity;
            inset = insetPx;
            corner = cornerPx;
            frameWidth = frame;
            glowRadius = radius;
        }

        /// <summary>Độ sáng khi hover/nhấn mạnh (0-1).</summary>
        public float Hover
        {
            get => hover;
            set { hover = Mathf.Clamp01(value); if (instance != null) instance.SetFloat(HoverId, hover); }
        }

        private void OnEnable()
        {
            image = GetComponent<Image>();
            rect = transform as RectTransform;
            if (sharedMaterial == null) return;

            instance = new Material(sharedMaterial) { name = sharedMaterial.name + " (Panel)", hideFlags = HideFlags.HideAndDontSave };
            image.material = instance;
            image.color = Color.white;
            Apply();
        }

        private void OnDisable()
        {
            if (image != null) image.material = null;
            if (instance != null)
            {
                if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
                instance = null;
            }
        }

        private void OnValidate()
        {
            if (instance != null) Apply();
        }

        private void Apply()
        {
            instance.SetColor(GlowColorId, glowColor);
            instance.SetColor(FillColorId, fillColor);
            instance.SetFloat(IntensityId, intensity);
            instance.SetFloat(RadiusId, glowRadius);
            instance.SetFloat(FrameId, frameWidth);
            instance.SetFloat(CornerId, corner);
            instance.SetFloat(InsetId, inset);
            instance.SetFloat(PulseSpeedId, pulseSpeed);
            instance.SetFloat(PulseAmountId, pulseAmount);
            instance.SetFloat(ShimmerSpeedId, shimmerSpeed);
            instance.SetFloat(ShimmerStrengthId, shimmerStrength);
            instance.SetFloat(HoverId, hover);
        }

        private void Update()
        {
            if (instance == null || rect == null) return;
            instance.SetFloat(TimeId, Application.isPlaying ? Time.unscaledTime : (float)UnityEditorTime());
            instance.SetVector(SizeId, rect.rect.size);
        }

        private static double UnityEditorTime()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.unscaledTime;
#endif
        }
    }
}
