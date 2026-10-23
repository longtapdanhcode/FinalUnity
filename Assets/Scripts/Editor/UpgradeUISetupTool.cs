using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Vanguard.Cards;
using Vanguard.Player;
using Vanguard.UI;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor dựng UI chọn thẻ nâng cấp và UpgradeManager trong Scene (Sprint 4 - Card 4.5).
    /// </summary>
    public static class UpgradeUISetupTool
    {
        private const string CardsFolder = "Assets/Data/Cards";
        private const string ModalName = "UpgradeSelectionModal";
        private const int CardSlots = 3;

        private static readonly Color PanelDark = new Color(0.08f, 0.07f, 0.12f, 1f);
        private static readonly Color Gold = new Color(1f, 0.6f, 0f, 1f);

        [MenuItem("Vanguard/Setup Upgrade Selection UI")]
        public static void Setup()
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            // Dựng lại modal từ đầu để tránh trùng lặp
            Transform oldModal = canvas.transform.Find(ModalName);
            if (oldModal != null) Object.DestroyImmediate(oldModal.gameObject);

            GameObject modal = BuildModal(canvas.transform, out List<UpgradeCardUI> cardUIs);
            modal.SetActive(false);

            List<UpgradeCardSO> cards = LoadAllCards();
            ConfigureManager(modal, cardUIs, cards);

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[UpgradeUISetupTool] ✅ Đã dựng UpgradeSelectionModal với {CardSlots} khung thẻ, nạp {cards.Count} thẻ vào UpgradeManager và lưu Scene!");
        }

        #region Canvas & EventSystem
        internal static Canvas GetOrCreateCanvas()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null && canvas.isRootCanvas) return canvas;

            GameObject go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        internal static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

            // Dự án chỉ dùng Input System mới: StandaloneInputModule sẽ gây lỗi, thay bằng InputSystemUIInputModule
            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) Object.DestroyImmediate(legacyModule);

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
        #endregion

        #region Modal Layout
        private static GameObject BuildModal(Transform canvasTransform, out List<UpgradeCardUI> cardUIs)
        {
            GameObject modal = CreateUIObject(ModalName, canvasTransform);
            Stretch(modal.GetComponent<RectTransform>());
            Image dim = modal.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.8f);

            // Tiêu đề
            TextMeshProUGUI title = CreateText(modal.transform, "TitleText", "CHỌN NÂNG CẤP HƯ VÔ", 56, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(1200f, 90f));

            // Khung chứa 3 thẻ bài
            GameObject container = CreateUIObject("CardsContainer", modal.transform);
            SetRect(container.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1000f, 380f));
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 50f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            cardUIs = new List<UpgradeCardUI>(CardSlots);
            for (int i = 0; i < CardSlots; i++)
            {
                cardUIs.Add(BuildCard(container.transform, i));
            }

            return modal;
        }

        private static UpgradeCardUI BuildCard(Transform parent, int index)
        {
            // Gốc thẻ = viền (đổi màu theo phẩm chất)
            GameObject card = CreateUIObject($"Card_{index + 1}", parent);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 340f));
            Image border = card.AddComponent<Image>();
            border.color = Color.white;
            border.raycastTarget = false;

            // Nền thẻ thụt vào 4px để lộ viền
            GameObject inner = CreateUIObject("Background", card.transform);
            RectTransform innerRect = inner.GetComponent<RectTransform>();
            Stretch(innerRect);
            innerRect.offsetMin = new Vector2(4f, 4f);
            innerRect.offsetMax = new Vector2(-4f, -4f);
            Image innerImage = inner.AddComponent<Image>();
            innerImage.color = PanelDark;
            innerImage.raycastTarget = false;

            // Icon
            GameObject iconObj = CreateUIObject("Icon", card.transform);
            SetRect(iconObj.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(90f, 90f));
            Image icon = iconObj.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            // Tên thẻ
            TextMeshProUGUI titleText = CreateText(card.transform, "TitleText", "Tên thẻ", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(200f, 36f));

            // Phẩm chất
            TextMeshProUGUI rarityText = CreateText(card.transform, "RarityText", "THƯỜNG", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(rarityText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -154f), new Vector2(200f, 22f));

            // Mô tả hiệu ứng
            TextMeshProUGUI descText = CreateText(card.transform, "DescriptionText", "Mô tả", 17, FontStyle.Normal,
                new Color(0.85f, 0.85f, 0.9f, 1f), TextAnchor.UpperCenter);
            SetRect(descText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -184f), new Vector2(190f, 90f));

            // Nút chọn
            GameObject buttonObj = CreateUIObject("SelectButton", card.transform);
            SetRect(buttonObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(160f, 40f));
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.25f, 0.2f, 0.4f, 1f);
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.6f, 0.45f, 1f, 1f);
            colors.pressedColor = new Color(0.4f, 0.3f, 0.7f, 1f);
            button.colors = colors;

            TextMeshProUGUI buttonLabel = CreateText(buttonObj.transform, "Label", "CHỌN", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(buttonLabel.rectTransform);

            // Gắn UpgradeCardUI và nối tham chiếu
            UpgradeCardUI cardUI = card.AddComponent<UpgradeCardUI>();
            SerializedObject so = new SerializedObject(cardUI);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("descriptionText").objectReferenceValue = descText;
            so.FindProperty("rarityText").objectReferenceValue = rarityText;
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("borderImage").objectReferenceValue = border;
            so.FindProperty("selectButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            return cardUI;
        }
        #endregion

        #region Manager
        private static List<UpgradeCardSO> LoadAllCards()
        {
            var cards = new List<UpgradeCardSO>();
            string[] guids = AssetDatabase.FindAssets("t:UpgradeCardSO", new[] { CardsFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UpgradeCardSO card = AssetDatabase.LoadAssetAtPath<UpgradeCardSO>(path);
                if (card != null) cards.Add(card);
            }
            cards.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

            if (cards.Count == 0)
            {
                Debug.LogWarning("[UpgradeUISetupTool] Không tìm thấy thẻ nào trong Assets/Data/Cards/. Hãy chạy các menu Generate Common/Rare/Legendary Cards trước.");
            }
            return cards;
        }

        private static void ConfigureManager(GameObject modal, List<UpgradeCardUI> cardUIs, List<UpgradeCardSO> cards)
        {
            UpgradeManager manager = Object.FindAnyObjectByType<UpgradeManager>();
            if (manager == null)
            {
                manager = new GameObject("UpgradeManager").AddComponent<UpgradeManager>();
            }

            PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogWarning("[UpgradeUISetupTool] Không tìm thấy Player (PlayerHealth) trong Scene, UpgradeManager sẽ tự tìm lúc chạy.");
            }

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("modalPanel").objectReferenceValue = modal;
            so.FindProperty("player").objectReferenceValue = playerHealth != null ? playerHealth.gameObject : null;

            SerializedProperty uiProp = so.FindProperty("cardUIItems");
            uiProp.arraySize = cardUIs.Count;
            for (int i = 0; i < cardUIs.Count; i++)
            {
                uiProp.GetArrayElementAtIndex(i).objectReferenceValue = cardUIs[i];
            }

            SerializedProperty cardsProp = so.FindProperty("allAvailableCards");
            cardsProp.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
        #endregion

        #region UI Helpers
        internal static GameObject CreateUIObject(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static TextMeshProUGUI CreateText(Transform parent, string objectName, string content, int fontSize,
            FontStyle style, Color color, TextAnchor alignment)
        {
            return NeonUIKit.CreateText(parent, objectName, content, fontSize, style, color, alignment);
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
        #endregion
    }
}
