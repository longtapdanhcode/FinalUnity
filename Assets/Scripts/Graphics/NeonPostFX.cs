using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Điều khiển post-processing neon (Bloom, Vignette, Chromatic Aberration) qua Global Volume.
    /// Cho phép "xung" tạm thời (Soul Burst, trúng đòn) mà không đụng logic gameplay.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class NeonPostFX : MonoBehaviour
    {
        public static NeonPostFX Instance { get; private set; }

        [Header("Base values")]
        [SerializeField, Range(0f, 5f)] private float bloomIntensity = 1.2f;
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.3f;
        [SerializeField, Range(0f, 1f)] private float chromaticBase = 0.05f;

        [Header("Color Adjustments")]
        [SerializeField, Range(-100f, 100f)] private float saturation = 15f;
        [SerializeField, Range(-100f, 100f)] private float contrast = 12f;
        [SerializeField, Range(-2f, 2f)] private float postExposure = 0f;

        [Header("Options")]
        [SerializeField] private bool postProcessingEnabled = true;
        [SerializeField, Min(0.01f)] private float pulseDecayDuration = 0.25f;

        private Volume volume;
        private Bloom bloom;
        private Vignette vignette;
        private ChromaticAberration chromatic;
        private ColorAdjustments colorAdjustments;

        private float pulseAmount;
        private float pulseDecayRate;

        public bool HighQualityBloom
        {
            get => bloom != null && bloom.highQualityFiltering.value;
            set { if (bloom != null) bloom.highQualityFiltering.Override(value); }
        }

        public bool PostProcessingEnabled
        {
            get => postProcessingEnabled;
            set { postProcessingEnabled = value; if (volume != null) volume.weight = value ? 1f : 0f; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            volume = GetComponent<Volume>();

            // volume.profile tạo bản sao runtime, không ghi đè asset gốc
            VolumeProfile profile = volume.profile;
            if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>(true);
            if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(true);
            if (!profile.TryGet(out chromatic)) chromatic = profile.Add<ChromaticAberration>(true);

            if (!profile.TryGet(out colorAdjustments)) colorAdjustments = profile.Add<ColorAdjustments>(true);

            ApplyBase();
            volume.weight = postProcessingEnabled ? 1f : 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ApplyBase()
        {
            bloom.intensity.Override(bloomIntensity);
            vignette.intensity.Override(vignetteIntensity);
            chromatic.intensity.Override(chromaticBase);
            colorAdjustments.saturation.Override(saturation);
            colorAdjustments.contrast.Override(contrast);
            colorAdjustments.postExposure.Override(postExposure);
        }

        /// <summary>Xung chromatic aberration + bloom, tự giảm dần theo thời gian thực (không bị hit-stop làm chậm).</summary>
        public static void Pulse(float strength = 0.6f)
        {
            if (Instance != null) Instance.DoPulse(strength);
        }

        public void DoPulse(float strength)
        {
            if (!postProcessingEnabled) return;
            pulseAmount = Mathf.Max(pulseAmount, Mathf.Clamp01(strength));
            pulseDecayRate = pulseAmount / pulseDecayDuration;
        }

        private void Update()
        {
            if (pulseAmount <= 0f) return;

            pulseAmount = Mathf.Max(0f, pulseAmount - pulseDecayRate * Time.unscaledDeltaTime);
            chromatic.intensity.value = Mathf.Clamp01(chromaticBase + pulseAmount);
            bloom.intensity.value = bloomIntensity + pulseAmount * 2f;
        }
    }
}
