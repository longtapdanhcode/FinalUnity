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

                SerializedObject so = new SerializedObject(card);
                Image border = so.FindProperty("borderImage").objectReferenceValue as Image;
                if (border == null)
                {
                    Transform bg = card.transform.Find("Background");
                    border = bg != null ? bg.GetComponent<Image>() : null;
                    if (border == null) { Debug.LogWarning($"[CardRarityFX] {card.name}: thiếu borderImage/Background.", card); continue; }
                    so.FindProperty("borderImage").objectReferenceValue = border;
                    so.ApplyModifiedProperties();
                }

                Undo.RecordObject(border.rectTransform, "Card glow inset");
                border.rectTransform.anchorMin = Vector2.zero;
                border.rectTransform.anchorMax = Vector2.one;
                border.rectTransform.offsetMin = new Vector2(-Inset, -Inset);
                border.rectTransform.offsetMax = new Vector2(Inset, Inset);
                border.sprite = null;
                border.raycastTarget = true;
                border.material = mat;
                border.color = Color.white;

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
