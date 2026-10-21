#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 6 - Card 6.1: tạo material mẫu NeonLit và kiểm tra tương thích SRP Batcher cho các shader tự viết.
    /// </summary>
    public static class NeonLitSetupTool
    {
        private const string TemplatePath = "Assets/Materials/M_NeonLit_Template.mat";

        private static readonly string[] CustomShaders =
        {
            "Vanguard/NeonLit",
            "Vanguard/NeonGrid",
        };

        [MenuItem("Vanguard/Graphics/Create NeonLit Template Material")]
        public static void CreateTemplate()
        {
            Shader shader = Shader.Find("Vanguard/NeonLit");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("NeonLit", "Không tìm thấy shader 'Vanguard/NeonLit'. Kiểm tra Console xem shader còn lỗi biên dịch không.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TemplatePath);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(shader) { name = "M_NeonLit_Template" };
                AssetDatabase.CreateAsset(mat, TemplatePath);
            }
            else
            {
                mat.shader = shader;
            }

            // Giá trị khởi đầu: thân tối, emissive cyan, rim tím, flash trắng sáng
            mat.SetColor("_BaseColor", new Color(0.08f, 0.06f, 0.12f, 1f));
            mat.SetColor("_EmissionColor", new Color(0f, 0.8f, 1.2f, 1f));
            mat.SetFloat("_PulseSpeed", 2f);
            mat.SetFloat("_PulseAmount", 0.2f);
            mat.SetColor("_RimColor", new Color(0.6f, 0.2f, 1f, 1f));
            mat.SetFloat("_RimPower", 3f);
            mat.SetColor("_FlashColor", new Color(4f, 4f, 4f, 1f));
            mat.SetFloat("_FlashAmount", 0f);
            mat.SetFloat("_DissolveAmount", 0f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);

            Debug.Log($"[NeonLit] {(created ? "Đã tạo" : "Đã cập nhật")} material mẫu tại {TemplatePath}. Nhân bản (Ctrl+D) rồi đổi màu cho Player, Enemy, Boss.");
        }

        [MenuItem("Vanguard/Graphics/Check SRP Batcher Compatibility")]
        public static void CheckSrpBatcher()
        {
            MethodInfo getCode = typeof(ShaderUtil).GetMethod(
                "GetSRPBatcherCompatibilityCode",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Shader), typeof(int) }, null);

            if (!GraphicsSettingsSrpBatcherEnabled())
            {
                Debug.LogWarning("[SRP Batcher] Đang TẮT trong URP Asset. Bật 'SRP Batcher' ở PC_RPAsset > Advanced.");
            }

            foreach (string name in CustomShaders)
            {
                Shader s = Shader.Find(name);
                if (s == null)
                {
                    Debug.LogError($"[SRP Batcher] Không tìm thấy shader '{name}'.");
                    continue;
                }

                if (getCode == null)
                {
                    Debug.LogWarning($"[SRP Batcher] Phiên bản Unity này không có API kiểm tra. Chọn shader '{name}' trong Project và xem dòng 'SRP Batcher' ở Inspector.");
                    continue;
                }

                try
                {
                    int code = (int)getCode.Invoke(null, new object[] { s, 0 });
                    if (code == 0)
                        Debug.Log($"[SRP Batcher] ✅ '{name}' tương thích SRP Batcher.");
                    else
                        Debug.LogWarning($"[SRP Batcher] ❌ '{name}' KHÔNG tương thích (mã {code}). Mở shader trong Inspector để xem lý do chi tiết.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SRP Batcher] Không kiểm tra được '{name}': {e.Message}");
                }
            }
        }

        private static bool GraphicsSettingsSrpBatcherEnabled()
        {
            return UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching;
        }
    }
}
#endif
