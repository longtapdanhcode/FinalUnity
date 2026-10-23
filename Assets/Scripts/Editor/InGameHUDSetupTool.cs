using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vanguard.Player;
using Vanguard.UI;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor dựng HUD trong trận và nối HUDController (Sprint 5 - Card 5.5).
    /// </summary>
    public static class InGameHUDSetupTool
    {
        private const string HudName = "InGameHUD";
        private const string CircleSpritePath = "Assets/Sprites/UI/HUD_Circle.png";
        private const int DashSlots = 2;

        private static readonly Color MetalColor = new Color(0.1f, 0.85f, 1f, 1f);
        private static readonly Color PanelDark = new Color(0.07f, 0.07f, 0.1f, 0.92f);
        private static readonly Color GoldFrame = new Color(0.8f, 0.3f, 1f, 1f);
        private static readonly Color GoldText = new Color(0.95f, 0.88f, 1f, 1f);
        private static readonly Color HpColor = new Color(1f, 0.2f, 0.32f, 1f);
        private static readonly Color VoidPurple = new Color(0.7f, 0.3f, 1f, 1f);

        [MenuItem("Vanguard/Setup In-Game HUD")]
        public static void Setup()
        {
            Canvas canvas = UpgradeUISetupTool.GetOrCreateCanvas();
            ConfigureScaler(canvas);

            // Dựng lại HUD từ đầu để tránh trùng lặp
            Transform old = canvas.transform.Find(HudName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Sprite circle = GetOrCreateCircleSprite();

            GameObject root = UpgradeUISetupTool.CreateUIObject(HudName, canvas.transform);
            UpgradeUISetupTool.Stretch(root.GetComponent<RectTransform>());
            // HUD chỉ để xem: không chặn chuột / click
            root.AddComponent<CanvasGroup>().blocksRaycasts = false;
            // Nằm dưới cùng để modal chọn thẻ luôn vẽ đè lên
            root.transform.SetAsFirstSibling();

            // ---- Top-Left ----
            GameObject topLeft = BuildPanel(root.transform, "TopLeftHUD", new Vector2(0f, 1f), new Vector2(30f, -30f),
                new Vector2(440f, 130f), MetalColor);

            Slider hpSlider = BuildSlider(topLeft.transform, "HPSlider", new Vector2(0f, 1f), new Vector2(20f, -16f),
                new Vector2(400f, 34f), new Color(0.2f, 0.04f, 0.04f, 1f), HpColor);

            TextMeshProUGUI hpText = UpgradeUISetupTool.CreateText(topLeft.transform, "HPText", "100 / 100 HP", 22, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter);
            UpgradeUISetupTool.SetRect(hpText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -16f), new Vector2(400f, 34f));

            TextMeshProUGUI dashLabel = UpgradeUISetupTool.CreateText(topLeft.transform, "DashLabel", "DASH", 16, FontStyle.Bold,
                new Color(0.7f, 0.9f, 0.9f, 1f), TextAnchor.MiddleLeft);
            UpgradeUISetupTool.SetRect(dashLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(130f, -76f), new Vector2(80f, 40f));

            Image[] dashIcons = new Image[DashSlots];
            for (int i = 0; i < DashSlots; i++)
            {
                dashIcons[i] = BuildDashIcon(topLeft.transform, i, circle);
            }

            // ---- Top-Center ----
            GameObject topCenter = BuildPanel(root.transform, "TopCenterHUD", new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(560f, 130f), GoldFrame);

            TextMeshProUGUI waveText = UpgradeUISetupTool.CreateText(topCenter.transform, "WaveNumberText", "WAVE 01/10", 38,
                FontStyle.Bold, GoldText, TextAnchor.MiddleCenter);
            UpgradeUISetupTool.SetRect(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(540f, 50f));
            NeonUIKit.ApplyFx(waveText, NeonUIKit.TextFx.GlowMagenta);

            Slider enemySlider = BuildSlider(topCenter.transform, "EnemyRemainingSlider", new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(460f, 22f), new Color(0.12f, 0.04f, 0.18f, 1f), VoidPurple);

            TextMeshProUGUI enemyText = UpgradeUISetupTool.CreateText(topCenter.transform, "EnemyRemainingText", "Quái còn lại: 0 / 0", 20,
                FontStyle.Bold, new Color(0.88f, 0.8f, 1f, 1f), TextAnchor.MiddleCenter);
            UpgradeUISetupTool.SetRect(enemyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(540f, 28f));

            // ---- HUDController ----
            HUDController hud = root.AddComponent<HUDController>();
            PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
            PlayerDash playerDash = Object.FindAnyObjectByType<PlayerDash>();
            if (playerHealth == null || playerDash == null)
            {
                Debug.LogWarning("[InGameHUDSetupTool] Không tìm thấy Player (PlayerHealth / PlayerDash) trong Scene, HUDController sẽ tự tìm lúc chạy.");
            }

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("hpSlider").objectReferenceValue = hpSlider;
            so.FindProperty("hpText").objectReferenceValue = hpText;
            SerializedProperty iconsProp = so.FindProperty("dashChargeIcons");
            iconsProp.arraySize = dashIcons.Length;
            for (int i = 0; i < dashIcons.Length; i++)
            {
                iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = dashIcons[i];
            }
            so.FindProperty("waveNumberText").objectReferenceValue = waveText;
            so.FindProperty("enemyRemainingSlider").objectReferenceValue = enemySlider;
            so.FindProperty("enemyRemainingText").objectReferenceValue = enemyText;
            so.FindProperty("playerHealth").objectReferenceValue = playerHealth;
            so.FindProperty("playerDash").objectReferenceValue = playerDash;
            so.ApplyModifiedPropertiesWithoutUndo();

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[InGameHUDSetupTool] ✅ Đã dựng In-Game HUD, nối HUDController và lưu Scene!");
        }

        private static void ConfigureScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>Panel kính tối + viền neon glow (shader Vanguard/UICardGlow).</summary>
        private static GameObject BuildPanel(Transform parent, string panelName, Vector2 anchor, Vector2 position,
            Vector2 size, Color frameColor)
        {
            GameObject panel = UpgradeUISetupTool.CreateUIObject(panelName, parent);
            UpgradeUISetupTool.SetRect(panel.GetComponent<RectTransform>(), anchor, anchor, anchor, position, size);
            NeonUIKit.AddPanel(panel.transform, frameColor, 16f, 0.75f, 8f);
            return panel;
        }

        private static Slider BuildSlider(Transform parent, string sliderName, Vector2 anchor, Vector2 position,
            Vector2 size, Color backColor, Color fillColor)
        {
            GameObject go = UpgradeUISetupTool.CreateUIObject(sliderName, parent);
            UpgradeUISetupTool.SetRect(go.GetComponent<RectTransform>(), anchor, anchor, anchor, position, size);

            GameObject background = UpgradeUISetupTool.CreateUIObject("Background", go.transform);
            UpgradeUISetupTool.Stretch(background.GetComponent<RectTransform>());
            Image backImage = background.AddComponent<Image>();
            backImage.color = backColor;
            backImage.raycastTarget = false;

            GameObject fillArea = UpgradeUISetupTool.CreateUIObject("Fill Area", go.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            UpgradeUISetupTool.Stretch(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            GameObject fill = UpgradeUISetupTool.CreateUIObject("Fill", fillArea.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = null;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.value = 1f;
            return slider;
        }

        /// <summary>
        /// Ô Dash hình tròn: nền tối + lớp lấp đầy dạng Radial 360 (lớp lấp đầy trả về cho HUDController).
        /// </summary>
        private static Image BuildDashIcon(Transform parent, int index, Sprite circle)
        {
            GameObject container = UpgradeUISetupTool.CreateUIObject($"DashIcon_{index + 1}", parent);
            UpgradeUISetupTool.SetRect(container.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(20f + index * 52f, -76f), new Vector2(40f, 40f));

            GameObject back = UpgradeUISetupTool.CreateUIObject("Back", container.transform);
            UpgradeUISetupTool.Stretch(back.GetComponent<RectTransform>());
            Image backImage = back.AddComponent<Image>();
            backImage.sprite = circle;
            backImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            backImage.raycastTarget = false;

            GameObject fill = UpgradeUISetupTool.CreateUIObject("Fill", container.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            UpgradeUISetupTool.Stretch(fillRect);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.sprite = circle;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(0.2f, 1f, 0.85f, 1f);
            fillImage.raycastTarget = false;
            return fillImage;
        }

        /// <summary>
        /// Tạo sprite hình tròn trắng viền mịn (Radial Fill cần có sprite).
        /// </summary>
        private static Sprite GetOrCreateCircleSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Sprites")) AssetDatabase.CreateFolder("Assets", "Sprites");
            if (!AssetDatabase.IsValidFolder("Assets/Sprites/UI")) AssetDatabase.CreateFolder("Assets/Sprites", "UI");

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            File.WriteAllBytes(CircleSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CircleSpritePath);

            TextureImporter importer = AssetImporter.GetAtPath(CircleSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        }
    }
}
