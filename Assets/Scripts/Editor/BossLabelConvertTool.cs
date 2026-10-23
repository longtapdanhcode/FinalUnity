#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 9 - 9.1: đổi nhãn tên boss (World Space) trong prefab từ Text cũ sang TextMeshPro.</summary>
    public static class BossLabelConvertTool
    {
        private static readonly string[] Prefabs =
        {
            "Assets/Prefabs/Enemies/Gargoyle.prefab",
            "Assets/Prefabs/Enemies/VoidWarden.prefab",
        };

        [MenuItem("Vanguard/Graphics/Convert Boss Labels to TMP (9.1)")]
        public static void Convert()
        {
            TMP_FontAsset font = NeonUIKit.GetFont(true);
            if (font == null) return;

            int count = 0;
            foreach (string path in Prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
                {
                    GameObject go = legacy.gameObject;
                    string content = legacy.text;
                    Color color = legacy.color;
                    int size = legacy.fontSize;

                    Outline outline = go.GetComponent<Outline>();
                    if (outline != null) Object.DestroyImmediate(outline);
                    Object.DestroyImmediate(legacy);

                    TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.font = font;
                    tmp.text = content;
                    tmp.fontSize = size;
                    tmp.color = color;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.raycastTarget = false;
                    NeonUIKit.ApplyFx(tmp, NeonUIKit.TextFx.Plain);
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BossLabel] Đã chuyển {count} nhãn Text sang TextMeshPro.");
        }
    }
}
#endif
