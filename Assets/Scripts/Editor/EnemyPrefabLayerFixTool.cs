using UnityEditor;
using UnityEngine;
using Vanguard.Enemy;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Đồng bộ Layer "Enemy" cho toàn bộ Prefab trong Assets/Prefabs/Enemies/ (Sprint 3 - Fix Layer).
    /// </summary>
    public static class EnemyPrefabLayerFixTool
    {
        private const string PrefabFolder = "Assets/Prefabs/Enemies";

        [MenuItem("Vanguard/Fix Enemy Prefab Layers")]
        public static void Fix()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer == -1)
            {
                Debug.LogError("[EnemyPrefabLayerFixTool] Layer \"Enemy\" chưa tồn tại trong Project Settings > Tags and Layers!");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            int fixedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<EnemyBase>() == null) continue;

                    bool changed = false;
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.gameObject.layer != enemyLayer)
                        {
                            t.gameObject.layer = enemyLayer;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        fixedCount++;
                        Debug.Log($"[EnemyPrefabLayerFixTool] Đã đặt Layer Enemy cho: {path}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EnemyPrefabLayerFixTool] ✅ Hoàn tất: {fixedCount}/{guids.Length} Prefab được sửa Layer.");
        }
    }
}
