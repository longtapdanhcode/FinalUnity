#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 8 - 8.4: tạo material M_Telegraph (shader Vanguard/Telegraph) và gán cho các prefab Telegraph.</summary>
    public static class TelegraphSetupTool
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Combat/TelegraphCircle.prefab",
            "Assets/Prefabs/Combat/TelegraphSector.prefab",
        };

        [MenuItem("Vanguard/Graphics/Setup Telegraph Material")]
        public static void Setup()
        {
            Shader shader = Shader.Find("Vanguard/Telegraph");
            if (shader == null) { Debug.LogError("[Telegraph] Không tìm thấy shader Vanguard/Telegraph (kiểm tra lỗi compile shader)."); return; }

            const string matPath = "Assets/Materials/M_Telegraph.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);

            foreach (string path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Telegraph] Không thấy prefab {path}");
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                foreach (MeshRenderer r in contents.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.Log($"[Telegraph] Đã gán M_Telegraph cho {path}.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Telegraph] Hoàn tất. Gọi telegraph qua TelegraphManager (boss = đỏ, Husk = cam, Meteor = cyan).");
        }
    }
}
#endif
