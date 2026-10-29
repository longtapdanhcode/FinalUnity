#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Enemy;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 9 - Card 9.4: gắn EnemyLOD cho mọi prefab quái (kể cả boss) và giới hạn far clip của camera chính.
    /// Pool VFX (PrefabPool) và ngân sách hạt theo tier chạy hoàn toàn bằng code, không cần setup scene.
    /// Idempotent: chạy lại nhiều lần không tạo trùng.
    /// </summary>
    public static class PoolLodSetupTool
    {
        private const string EnemyFolder = "Assets/Prefabs/Enemies";
        private const float MaxFarClip = 150f;

        [MenuItem("Vanguard/Graphics/Setup Pool & LOD (9.4)")]
        public static void Run()
        {
            int lod = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root.GetComponent<EnemyBase>() != null && root.GetComponent<EnemyLOD>() == null)
                {
                    root.AddComponent<EnemyLOD>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    lod++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            string cam = "không có Camera.main";
            Camera main = Camera.main;
            if (main != null)
            {
                if (main.farClipPlane > MaxFarClip)
                {
                    main.farClipPlane = MaxFarClip;
                    EditorUtility.SetDirty(main);
                    EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
                    EditorSceneManager.SaveOpenScenes();
                    cam = $"far clip -> {MaxFarClip}";
                }
                else cam = $"far clip giữ {main.farClipPlane}";
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Pool&LOD 9.4] EnemyLOD thêm vào {lod} prefab quái; camera: {cam}.");
        }
    }
}
#endif
