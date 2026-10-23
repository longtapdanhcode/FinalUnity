#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Vanguard.UI;

namespace Vanguard.EditorTools
{
    /// <summary>Bộ tiện ích dựng UI neon (Sprint 9 - 9.1): font TMP Chakra Petch, material chữ, panel glow.</summary>
    internal static class NeonUIKit
    {
        internal enum TextFx { Plain, GlowCyan, GlowGold, GlowMagenta, GlowRed }

        private const string FontDir = "Assets/Fonts";
        private const string PanelMaterialPath = "Assets/Materials/M_UIPanelGlow.mat";
        private const string Lower = "aàáảãạăằắẳẵặâầấẩẫậeèéẻẽẹêềếểễệiìíỉĩịoòóỏõọôồốổỗộơờớởỡợuùúủũụưừứửữựyỳýỷỹỵđ";

        internal static readonly Color Cyan = new Color(0.1f, 0.85f, 1f, 1f);
        internal static readonly Color Magenta = new Color(0.8f, 0.3f, 1f, 1f);
        internal static readonly Color Gold = new Color(1f, 0.75f, 0.2f, 1f);
        internal static readonly Color Red = new Color(1f, 0.2f, 0.3f, 1f);

        internal static TMP_FontAsset GetFont(bool bold)
        {
            string weight = bold ? "Bold" : "Medium";
            string path = $"{FontDir}/ChakraPetch-{weight} SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            Font source = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/ChakraPetch-{weight}.ttf");
            if (source == null)
            {
                Debug.LogError($"[NeonUIKit] Thiếu {FontDir}/ChakraPetch-{weight}.ttf");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = $"ChakraPetch-{weight} SDF";
            AssetDatabase.CreateAsset(asset, path);

            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }

            asset.TryAddCharacters(Lower + Lower.ToUpperInvariant() + "0123456789 .,:;!?-+/()[]%&*'\"…—•", out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[NeonUIKit] Font thiếu ký tự: {missing}");

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        internal static Material GetPreset(TMP_FontAsset font, TextFx fx)
        {
            string path = $"{FontDir}/{font.name} - {fx}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material m = new Material(font.material) { name = $"{font.name} - {fx}" };

            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);

            Color glow = fx switch
            {
                TextFx.GlowCyan => Cyan,
                TextFx.GlowGold => Gold,
                TextFx.GlowMagenta => Magenta,
                TextFx.GlowRed => Red,
                _ => Color.clear
            };
            if (fx != TextFx.Plain)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Glow);
                m.SetColor(ShaderUtilities.ID_GlowColor, glow);
                m.SetFloat(ShaderUtilities.ID_GlowPower, 0.45f);
                m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
                m.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            }

            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        internal static void ApplyFx(TMP_Text text, TextFx fx)
        {
            if (text == null || text.font == null) return;
            text.fontSharedMaterial = GetPreset(text.font, fx);
        }

        internal static TextAlignmentOptions Map(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        internal static TextMeshProUGUI CreateText(Transform parent, string objectName, string content, int fontSize,
            FontStyle style, Color color, TextAnchor alignment, TextFx fx = TextFx.Plain)
        {
            GameObject go = UpgradeUISetupTool.CreateUIObject(objectName, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = GetFont(style == FontStyle.Bold || style == FontStyle.BoldAndItalic);
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = Map(alignment);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            ApplyFx(text, fx);
            return text;
        }

        internal static Material EnsurePanelMaterial()
        {
            Shader shader = Shader.Find("Vanguard/UICardGlow");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PanelMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_UIPanelGlow" };
                AssetDatabase.CreateAsset(mat, PanelMaterialPath);
            }
            else mat.shader = shader;
            return mat;
        }

        /// <summary>Gắn khung neon (Image con "Glow" ở dưới cùng) cho panel; trả về component.</summary>
        internal static NeonPanelFX AddPanel(Transform panel, Color glow, float inset = 16f, float intensity = 0.8f,
            float corner = 8f, Color? fill = null)
        {
            Material mat = EnsurePanelMaterial();

            GameObject go = UpgradeUISetupTool.CreateUIObject("Glow", panel);
            go.transform.SetAsFirstSibling();
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(-inset, -inset);
            r.offsetMax = new Vector2(inset, inset);

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;

            NeonPanelFX fx = go.AddComponent<NeonPanelFX>();
            fx.Configure(mat, glow, fill ?? new Color(0.03f, 0.04f, 0.09f, 0.88f), intensity, inset, corner);
            return fx;
        }
    }
}
#endif
