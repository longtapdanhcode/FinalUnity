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
        }

        private static readonly TierPreset[] Presets =
        {
            new TierPreset { shadowDistance = 20f, renderScale = 0.8f, msaa = 1, hqBloom = false },
            new TierPreset { shadowDistance = 30f, renderScale = 1.0f, msaa = 1, hqBloom = false },
            new TierPreset { shadowDistance = 40f, renderScale = 1.0f, msaa = 4, hqBloom = true },
        };

        private static bool hasBackup;
        private static float backupShadowDistance;
        private static float backupRenderScale;
        private static int backupMsaa;

        public static GraphicsTier Tier { get; private set; } = GraphicsTier.High;
        public static bool PostProcessing { get; private set; } = true;
        public static bool VSync { get; private set; }

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Tier = (GraphicsTier)Mathf.Clamp(PlayerPrefs.GetInt(TierKey, (int)GraphicsTier.High), 0, Presets.Length - 1);
            PostProcessing = PlayerPrefs.GetInt(PostFxKey, 1) == 1;
            VSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;

            Application.quitting -= RestoreAsset;
            Application.quitting += RestoreAsset;

            ApplyAll();
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
                    hasBackup = true;
                }
                urp.shadowDistance = preset.shadowDistance;
                urp.renderScale = preset.renderScale;
                urp.msaaSampleCount = preset.msaa;
            }

            QualitySettings.vSyncCount = VSync ? 1 : 0;

            NeonPostFX fx = NeonPostFX.Instance;
            if (fx != null)
            {
                fx.PostProcessingEnabled = PostProcessing;
                fx.HighQualityBloom = preset.hqBloom;
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
            }
            hasBackup = false;
        }
    }
}
