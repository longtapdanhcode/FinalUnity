#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.UI;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 9 - 9.1: thêm GameFlowUI (fade chuyển cảnh + màn Thắng/Thua) vào Scene.</summary>
    public static class GameFlowSetupTool
    {
        [MenuItem("Vanguard/Graphics/Setup Game Flow UI (9.1)")]
        public static void Setup()
        {
            TMP_FontAsset bold = NeonUIKit.GetFont(true);
            TMP_FontAsset medium = NeonUIKit.GetFont(false);
            if (bold == null || medium == null) return;

            // Tạo material panel (dùng chung với NeonPanelFX)
            NeonUIKit.EnsurePanelMaterial();

            GameFlowUI flow = Object.FindAnyObjectByType<GameFlowUI>();
            if (flow == null)
            {
                GameObject go = new GameObject("GameFlowUI");
                Undo.RegisterCreatedObjectUndo(go, "Setup GameFlowUI");
                flow = go.AddComponent<GameFlowUI>();
            }

            SerializedObject so = new SerializedObject(flow);
            so.FindProperty("boldFont").objectReferenceValue = bold;
            so.FindProperty("mediumFont").objectReferenceValue = medium;
            so.FindProperty("victoryTextMaterial").objectReferenceValue = NeonUIKit.GetPreset(bold, NeonUIKit.TextFx.GlowGold);
            so.FindProperty("defeatTextMaterial").objectReferenceValue = NeonUIKit.GetPreset(bold, NeonUIKit.TextFx.GlowRed);
            so.FindProperty("panelMaterial").objectReferenceValue = NeonUIKit.EnsurePanelMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[GameFlowSetup] Đã thêm GameFlowUI: fade-in khi vào trận, màn Thắng/Thua với nút Chơi lại / Thoát.");
        }
    }
}
#endif
