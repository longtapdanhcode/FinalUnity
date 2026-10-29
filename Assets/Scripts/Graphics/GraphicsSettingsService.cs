using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vanguard.Graphics
{
    public enum GraphicsTier { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Quản lý Quality Tier + tuỳ chọn đồ hoạ, lưu bằng PlayerPrefs (Sprint 6 - Card 6.5).
    /// Thay đổi được áp dụng lên URP Asset đang dùng lúc chạy và được khôi phục khi thoát,
    /// nên không làm bẩn file asset trong project.
    /// </summary>
    public static class GraphicsSettingsService
    {
        private const string TierKey = "gfx.tier";
        private const string PostFxKey = "gfx.postfx";
        private const string VSyncKey = "gfx.vsync";

        private struct TierPreset
        {
            public float shadowDistance;
            public float renderScale;
            public int msaa;
            public bool hqBloom;
            public int shadowCascades;
            public int shadowResolution;
            public int maxAdditionalLights;
            public bool bloomQuarter;
            public int bloomIterations;
            public float lodBias;
            public float lodDistanceScale;
            public float vfxBudget;
            public float maxVfxDistance;
        }

        private static readonly TierPreset[] Presets =
        {
            new TierPreset { shadowDistance = 20f, renderScale = 0.8f, msaa = 1, hqBloom = false, shadowCascades = 1, shadowResolution = 1024, maxAdditionalLights = 0, bloomQuarter = true, bloomIterations = 4, lodBias = 0.7f, lodDistanceScale = 0.6f, vfxBudget = 0.4f, maxVfxDistance = 30f },
            new TierPreset { shadowDistance = 30f, renderScale = 1.0f, msaa = 1, hqBloom = false, shadowCascades = 2, shadowResolution = 2048, maxAdditionalLights = 2, bloomQuarter = true, bloomIterations = 5, lodBias = 1.0f, lodDistanceScale = 0.85f, vfxBudget = 0.7f, maxVfxDistance = 50f },
            new TierPreset { shadowDistance = 40f, renderScale = 1.0f, msaa = 4, hqBloom = true, shadowCascades = 4, shadowResolution = 2048, maxAdditionalLights = 4, bloomQuarter = false, bloomIterations = 6, lodBias = 1.5f, lodDistanceScale = 1.2f, vfxBudget = 1.0f, maxVfxDistance = 90f },
        };

        private static bool hasBackup;
        private static float backupShadowDistance;
        private static float backupRenderScale;
        private static int backupMsaa;
        private static int backupCascades;
        private static int backupShadowResolution;
        private static int backupMaxAdditionalLights;

        public static GraphicsTier Tier { get; private set; } = GraphicsTier.High;
        public static bool PostProcessing { get; private set; } = true;
        public static bool VSync { get; private set; }

        /// <summary>Hệ số số hạt (0-1) áp lên ParticleSystem và số VFX Graph hoạt động đồng thời.</summary>
        public static float VfxBudget => Presets[(int)Tier].vfxBudget;
        /// <summary>VFX xa camera hơn mức này sẽ không được phát.</summary>
        public static float MaxVfxDistance => Presets[(int)Tier].maxVfxDistance;
        public static float LodDistanceScale => Presets[(int)Tier].lodDistanceScale;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!PlayerPrefs.HasKey(TierKey))
            {
                // Lần chạy đầu: tự chọn preset theo phần cứng, lưu lại để người chơi vẫn đổi được trong menu
                Tier = DetectTier();
                PlayerPrefs.SetInt(TierKey, (int)Tier);
                PlayerPrefs.Save();
                Debug.Log($"[Graphics] Lần chạy đầu: tự chọn {Tier} ({DescribeHardware()})");
            }
            else
            {
                Tier = (GraphicsTier)Mathf.Clamp(PlayerPrefs.GetInt(TierKey, (int)GraphicsTier.High), 0, Presets.Length - 1);
            }
            PostProcessing = PlayerPrefs.GetInt(PostFxKey, 1) == 1;
            VSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;

            Application.quitting -= RestoreAsset;
            Application.quitting += RestoreAsset;

            ApplyAll();
        }

        /// <summary>Chọn tier theo phần cứng: VRAM, RAM, số nhân CPU, GPU tích hợp, mức shader.</summary>
        public static GraphicsTier DetectTier()
        {
            if (SystemInfo.graphicsShaderLevel < 45) return GraphicsTier.Low;

            string gpu = SystemInfo.graphicsDeviceName.ToLowerInvariant();
            int vram = SystemInfo.graphicsMemorySize;   // MB (máy bộ nhớ hợp nhất: gần bằng RAM)
            int ram = SystemInfo.systemMemorySize;      // MB
            int cores = SystemInfo.processorCount;

            // Apple Silicon: GPU mạnh, phân theo RAM
            if (gpu.Contains("apple"))
            {
                return ram >= 16000 ? GraphicsTier.High : GraphicsTier.Medium;
            }

            bool integrated = (gpu.Contains("intel") && !gpu.Contains("arc")) || gpu.Contains("uhd") || gpu.Contains("iris")
                              || gpu.Contains("radeon graphics") || gpu.Contains("vega 3") || gpu.Contains("vega 8");

            int score = 0;
            if (vram >= 6000) score += 2; else if (vram >= 3500) score += 1; else if (vram < 2000) score -= 1;
            if (cores >= 8) score += 1; else if (cores < 4) score -= 1;
            if (ram >= 16000) score += 1; else if (ram < 8000) score -= 1;
            if (integrated) score -= 2;

            if (score >= 3) return GraphicsTier.High;
            if (score >= 1) return GraphicsTier.Medium;
            return GraphicsTier.Low;
        }

        private static string DescribeHardware()
        {
            return $"{SystemInfo.graphicsDeviceName}, VRAM {SystemInfo.graphicsMemorySize} MB, RAM {SystemInfo.systemMemorySize} MB, {SystemInfo.processorCount} luồng CPU";
        }

        /// <summary>Xoá lựa chọn đã lưu để lần chạy sau tự dò lại phần cứng.</summary>
        public static void ResetToAuto()
        {
            PlayerPrefs.DeleteKey(TierKey);
            PlayerPrefs.Save();
        }

        public static void SetTier(GraphicsTier tier)
        {
            Tier = tier;
            PlayerPrefs.SetInt(TierKey, (int)tier);
            ApplyAll();
        }

        public static void CycleTier() => SetTier((GraphicsTier)(((int)Tier + 1) % Presets.Length));

        public static void SetPostProcessing(bool enabled)
        {
            PostProcessing = enabled;
            PlayerPrefs.SetInt(PostFxKey, enabled ? 1 : 0);
            ApplyAll();
        }

        public static void SetVSync(bool enabled)
        {
            VSync = enabled;
            PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
            ApplyAll();
        }

        private static void ApplyAll()
        {
            TierPreset preset = Presets[(int)Tier];

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (!hasBackup)
                {
                    backupShadowDistance = urp.shadowDistance;
                    backupRenderScale = urp.renderScale;
                    backupMsaa = urp.msaaSampleCount;
                    backupCascades = urp.shadowCascadeCount;
                    backupShadowResolution = urp.mainLightShadowmapResolution;
                    backupMaxAdditionalLights = urp.maxAdditionalLightsCount;
                    hasBackup = true;
                }
                urp.shadowDistance = preset.shadowDistance;
                urp.renderScale = preset.renderScale;
                urp.msaaSampleCount = preset.msaa;
                urp.shadowCascadeCount = preset.shadowCascades;
                urp.mainLightShadowmapResolution = preset.shadowResolution;
                urp.maxAdditionalLightsCount = preset.maxAdditionalLights;
            }

            QualitySettings.vSyncCount = VSync ? 1 : 0;
            QualitySettings.lodBias = preset.lodBias;

            NeonPostFX fx = NeonPostFX.Instance;
            if (fx != null)
            {
                fx.PostProcessingEnabled = PostProcessing;
                fx.HighQualityBloom = preset.hqBloom;
                fx.SetBloomCost(preset.bloomQuarter, preset.bloomIterations);
            }

            Changed?.Invoke();
        }

        private static void RestoreAsset()
        {
            if (!hasBackup) return;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = backupShadowDistance;
                urp.renderScale = backupRenderScale;
                urp.msaaSampleCount = backupMsaa;
                urp.shadowCascadeCount = backupCascades;
                urp.mainLightShadowmapResolution = backupShadowResolution;
                urp.maxAdditionalLightsCount = backupMaxAdditionalLights;
            }
            hasBackup = false;
        }
    }
}
