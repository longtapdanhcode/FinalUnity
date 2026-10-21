#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 7 - Card 7.3: đổi các material URP/Lit sang shader Vanguard/NeonLit.
    /// Giữ nguyên _BaseColor, _BaseMap và emission cũ; bỏ qua material dùng shader khác (particle, telegraph...).
    /// Hỗ trợ Undo (Ctrl+Z) cho toàn bộ thao tác.
    /// </summary>
    public static class NeonMaterialMigrationTool
    {
        private const string TargetShaderName = "Vanguard/NeonLit";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private static readonly string[] SearchFolders = { "Assets/Material", "Assets/Materials" };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");

        [MenuItem("Vanguard/Graphics/Migrate Lit Materials to NeonLit")]
        public static void Migrate()
        {
            Shader neon = Shader.Find(TargetShaderName);
            if (neon == null)
            {
                EditorUtility.DisplayDialog("NeonLit", $"Không tìm thấy shader '{TargetShaderName}'. Kiểm tra Console xem shader còn lỗi biên dịch không.", "OK");
                return;
            }

            List<Material> targets = new List<Material>();
            List<string> skipped = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Material", SearchFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;

                if (mat.shader.name == LitShaderName) targets.Add(mat);
                else if (mat.shader != neon) skipped.Add($"{path} ({mat.shader.name})");
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("NeonLit", "Không có material URP/Lit nào cần chuyển.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Migrate to NeonLit",
                    $"Sẽ đổi {targets.Count} material URP/Lit sang {TargetShaderName}.\nBỏ qua {skipped.Count} material dùng shader khác.\nCó thể hoàn tác bằng Ctrl+Z. Tiếp tục?",
                    "Chuyển", "Huỷ"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Material mat in targets)
            {
                Undo.RecordObject(mat, "Migrate to NeonLit");

                Color baseColor = mat.GetColor(BaseColorId);
                Texture baseMap = mat.HasProperty(BaseMapId) ? mat.GetTexture(BaseMapId) : null;
                Color emission = mat.IsKeywordEnabled("_EMISSION") ? mat.GetColor(EmissionColorId) : Color.black;

                mat.shader = neon;
                mat.SetColor(BaseColorId, baseColor);
                mat.SetTexture(BaseMapId, baseMap);
                mat.SetColor(EmissionColorId, emission);

                // Rim mặc định lấy theo emission (nếu có) để giữ màu nhận diện của từng loại enemy
                Color rim = emission.maxColorComponent > 0.01f ? emission : new Color(0.6f, 0.2f, 1f, 1f);
                mat.SetColor(RimColorId, rim);
                mat.SetFloat(PulseAmountId, emission.maxColorComponent > 0.01f ? 0.3f : 0f);

                EditorUtility.SetDirty(mat);
            }

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();

            Debug.Log($"[NeonLit] Đã chuyển {targets.Count} material.\nBỏ qua {skipped.Count}:\n{string.Join("\n", skipped)}");
        }
    }
}
#endif
