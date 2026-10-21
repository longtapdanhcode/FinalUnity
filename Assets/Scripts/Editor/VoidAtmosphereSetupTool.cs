#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 6 - Card 6.4: áp Skybox "Void", Fog và Ambient theo Art Bible vào scene đang mở.
    /// Có thể chạy lại nhiều lần (idempotent) và hoàn tác bằng Ctrl+Z.
    /// </summary>
    public static class VoidAtmosphereSetupTool
    {
        private const string SkyShaderName = "Vanguard/VoidSky";
        private const string SkyMaterialPath = "Assets/Materials/M_VoidSkybox.mat";

        private static readonly Color FogColor = new Color(0.05f, 0.03f, 0.11f, 1f);
        private static readonly Color AmbientColor = new Color(0.14f, 0.11f, 0.24f, 1f);
        private static readonly Color SunColor = new Color(0.72f, 0.66f, 1f, 1f);

        [MenuItem("Vanguard/Graphics/Setup Void Skybox & Fog")]
        public static void Setup()
        {
            Shader sky = Shader.Find(SkyShaderName);
            if (sky == null)
            {
                EditorUtility.DisplayDialog("Void Atmosphere", $"Không tìm thấy shader '{SkyShaderName}'. Kiểm tra Console xem shader còn lỗi biên dịch không.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (skyMat == null)
            {
                skyMat = new Material(sky) { name = "M_VoidSkybox" };
                AssetDatabase.CreateAsset(skyMat, SkyMaterialPath);
            }
            else if (skyMat.shader != sky)
            {
                skyMat.shader = sky;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Void Skybox & Fog");

            // Skybox + Ambient phẳng tối để emissive nổi bật
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;

            // Fog mũ bình phương: gần như không thấy ở sát camera, nuốt dần phần xa của arena
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.018f;

            // Camera chính vẽ Skybox thay vì màu nền
            Camera cam = Camera.main;
            if (cam != null)
            {
                Undo.RecordObject(cam, "Camera Skybox");
                cam.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(cam);
            }
            else
            {
                Debug.LogWarning("[VoidAtmosphere] Không tìm thấy Main Camera, hãy tự đặt Background Type = Skybox.");
            }

            // Đèn hướng ngả lạnh để hợp tông void
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in Object.FindObjectsByType<Light>())
                {
                    if (l.type == LightType.Directional) { sun = l; break; }
                }
            }
            if (sun != null)
            {
                Undo.RecordObject(sun, "Sun Tint");
                sun.color = SunColor;
                EditorUtility.SetDirty(sun);
            }

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[VoidAtmosphere] Đã áp Skybox, Fog, Ambient và lưu Scene. Chỉnh thêm tại Window > Rendering > Lighting.");
        }
    }
}
#endif
