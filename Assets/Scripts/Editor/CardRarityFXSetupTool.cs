#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vanguard.UI;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 8 - Card 8.6: gắn shader viền glow + CardRarityFX cho các thẻ trong modal chọn nâng cấp.
    /// Idempotent: chạy lại an toàn.
    /// </summary>
    public static class CardRarityFXSetupTool
    {
        private const string MaterialPath = "Assets/Materials/M_UICardGlow.mat";
        private const float Inset = 20f;

        [MenuItem("Vanguard/Graphics/Setup Card Rarity FX (8.6)")]
        public static void Setup()
        {
            Shader shader = Shader.Find("Vanguard/UICardGlow");
            if (shader == null)
            {
                Debug.LogError("[CardRarityFX] Không tìm thấy shader Vanguard/UICardGlow (kiểm tra lỗi compile shader).");
                return;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_UICardGlow" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);

            UpgradeCardUI[] cards = Resources.FindObjectsOfTypeAll<UpgradeCardUI>();
            int count = 0;
            foreach (UpgradeCardUI card in cards)
            {
                if (EditorUtility.IsPersistent(card) || !card.gameObject.scene.IsValid()) continue;

                RectTransform cardRect = (RectTransform)card.transform;

                // Khôi phục gốc thẻ (bản trước lỡ kéo giãn gốc thẻ làm thẻ bị co nhỏ)
                Undo.RecordObject(cardRect, "Restore card rect");
                cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.sizeDelta = new Vector2(220f, 340f);
                cardRect.localScale = Vector3.one;

                // Gốc thẻ vốn là Image viền phẳng -> tắt đi, thay bằng GlowFrame
                Image rootImage = card.GetComponent<Image>();
                if (rootImage != null)
                {
                    rootImage.material = null;
                    rootImage.enabled = false;
                    EditorUtility.SetDirty(rootImage);
                }

                Transform frame = card.transform.Find("GlowFrame");
                if (frame == null)
                {
                    GameObject go = new GameObject("GlowFrame", typeof(RectTransform), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(go, "Create GlowFrame");
                    go.transform.SetParent(card.transform, false);
                    frame = go.transform;
                }
                frame.SetAsFirstSibling();
                RectTransform fr = (RectTransform)frame;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.offsetMin = new Vector2(-Inset, -Inset);
                fr.offsetMax = new Vector2(Inset, Inset);
                fr.localScale = Vector3.one;

                Image border = frame.GetComponent<Image>();
                border.sprite = null;
                border.raycastTarget = true;
                border.material = mat;
                border.color = Color.white;

                SerializedObject so = new SerializedObject(card);
                so.FindProperty("borderImage").objectReferenceValue = border;
                so.ApplyModifiedProperties();

                CardRarityFX fx = card.GetComponent<CardRarityFX>();
                if (fx == null) fx = Undo.AddComponent<CardRarityFX>(card.gameObject);
                fx.Configure(border, mat, Inset);
                EditorUtility.SetDirty(fx);
                EditorUtility.SetDirty(border);
                count++;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[CardRarityFX] Đã gắn glow cho {count} thẻ.");
        }
    }
}
#endif
