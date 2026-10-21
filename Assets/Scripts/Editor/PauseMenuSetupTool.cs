#if UNITY_EDITOR
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateLabel(panel.transform, "Title", "TẠM DỪNG", font, 36, new Vector2(0, 170), new Vector2(500, 60));

            Button resume = CreateButton(panel.transform, "Resume", "Tiếp tục (Esc)", font, new Vector2(0, 70));
            Button tier = CreateButton(panel.transform, "Tier", "Chất lượng", font, new Vector2(0, -10));
            Button postFx = CreateButton(panel.transform, "PostFx", "Post-Processing", font, new Vector2(0, -90));
            Button vsync = CreateButton(panel.transform, "VSync", "VSync", font, new Vector2(0, -170));

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

        private static Text CreateLabel(Transform parent, string name, string text, Font font, int size, Vector2 pos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            Text t = go.GetComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0f, 0.9f, 1f, 1f);
            return t;
        }

        private static Button CreateButton(Transform parent, string name, string text, Font font, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(420, 60);
            go.GetComponent<Image>().color = new Color(0.12f, 0.04f, 0.25f, 1f);
            CreateLabel(go.transform, "Text", text, font, 24, Vector2.zero, new Vector2(420, 60)).color = Color.white;
            return go.GetComponent<Button>();
        }
    }
}
#endif
