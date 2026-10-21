#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 8 - Card 8.1 (phần sàn): gán material lưới neon Vanguard/NeonGrid cho object "Ground".
    /// Chạy lại nhiều lần an toàn, hỗ trợ Ctrl+Z.
    /// </summary>
    public static class VoidGroundSetupTool
    {
        private const string ShaderName = "Vanguard/NeonGrid";
        private const string MaterialPath = "Assets/Materials/M_VoidGround.mat";
        private const string GroundName = "Ground";

        [MenuItem("Vanguard/Graphics/Setup Void Ground (Neon Grid)")]
        public static void Setup()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Void Ground", $"Không tìm thấy shader '{ShaderName}'. Kiểm tra Console xem shader còn lỗi biên dịch không.", "OK");
                return;
            }

            GameObject ground = GameObject.Find(GroundName);
            if (ground == null || !ground.TryGetComponent(out Renderer rend))
            {
                EditorUtility.DisplayDialog("Void Ground", $"Không tìm thấy GameObject '{GroundName}' có Renderer trong scene đang mở.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_VoidGround" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            // Tâm mờ dần đặt đúng giữa sàn
            Vector3 c = ground.transform.position;
            mat.SetVector("_FadeCenter", new Vector4(c.x, c.y, c.z, 0f));
            EditorUtility.SetDirty(mat);

            Undo.RecordObject(rend, "Assign Void Ground Material");
            rend.sharedMaterial = mat;
            EditorUtility.SetDirty(rend);

            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[VoidGround] Đã gán lưới neon cho Ground và lưu Scene. Chỉnh màu/kích thước ô tại Assets/Materials/M_VoidGround.");
        }
    }
}
#endif
