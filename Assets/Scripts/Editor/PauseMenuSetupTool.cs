#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vanguard.UI;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 6 - Card 6.5: dựng PauseMenu (Esc) dưới Canvas và gán tham chiếu.</summary>
    public static class PauseMenuSetupTool
    {
        [MenuItem("Vanguard/Graphics/Setup Pause Menu")]
        public static void Setup()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[PauseMenuSetup] Không tìm thấy Canvas trong scene.");
                return;
            }

            Transform existing = canvas.transform.Find("PauseMenu");
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            GameObject root = new GameObject("PauseMenu", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Setup Pause Menu");
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0.02f, 0.01f, 0.06f, 0.85f);

            NeonUIKit.ApplyFx(CreateLabel(panel.transform, "Title", "TẠM DỪNG", 44, new Vector2(0, 170), new Vector2(500, 70)), NeonUIKit.TextFx.GlowCyan);

            Button resume = CreateButton(panel.transform, "Resume", "Tiếp tục (Esc)", new Vector2(0, 70));
            Button tier = CreateButton(panel.transform, "Tier", "Chất lượng", new Vector2(0, -10));
            Button postFx = CreateButton(panel.transform, "PostFx", "Post-Processing", new Vector2(0, -90));
            Button vsync = CreateButton(panel.transform, "VSync", "VSync", new Vector2(0, -170));

            PauseMenu menu = root.AddComponent<PauseMenu>();
            SerializedObject so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("tierButton").objectReferenceValue = tier;
            so.FindProperty("postFxButton").objectReferenceValue = postFx;
            so.FindProperty("vsyncButton").objectReferenceValue = vsync;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;
            Debug.Log("[PauseMenuSetup] Đã tạo PauseMenu. Nhấn Esc khi Play để mở.");
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta, bool bold = true)
        {
            TextMeshProUGUI t = NeonUIKit.CreateText(parent, name, text, size, bold ? FontStyle.Bold : FontStyle.Normal,
                new Color(0.1f, 0.85f, 1f, 1f), TextAnchor.MiddleCenter);
            RectTransform rt = t.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(420, 60);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            CreateLabel(go.transform, "Text", text, 24, Vector2.zero, new Vector2(420, 60)).color = Color.white;
            NeonUIKit.AddPanel(go.transform, new Color(0.8f, 0.3f, 1f, 1f), 10f, 0.6f, 6f);
            return go.GetComponent<Button>();
        }
    }
}
#endif
