using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Spawning;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tự động tạo "SpawnPoints" (8 điểm quanh rìa võ đài) và gán vào EnemySpawner (Sprint 3 - Card 3.2).
    /// </summary>
    public static class SpawnPointSetupTool
    {
        private const int SpawnPointCount = 8;
        private const float SpawnRadius = 19f; // Trong khoảng 18-20m, cách tường ArenaBounds (±25m) an toàn
        private const string PrefabFolder = "Assets/Prefabs/Enemies";

        [MenuItem("Vanguard/Setup Enemy Spawner & Spawn Points")]
        public static void Setup()
        {
            EnemySpawner spawner = GetOrCreateSpawner();
            Transform[] points = CreateSpawnPoints();

            SerializedObject so = new SerializedObject(spawner);

            SerializedProperty pointsProp = so.FindProperty("spawnPoints");
            if (pointsProp != null)
            {
                pointsProp.arraySize = points.Length;
                for (int i = 0; i < points.Length; i++)
                {
                    pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                }
            }

            AssignEnemyPrefabs(so.FindProperty("enemyPools"));

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
            Selection.activeGameObject = spawner.gameObject;

            // Lưu Scene hiện tại
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[SpawnPointSetupTool] ✅ Đã tạo {points.Length} SpawnPoint (bán kính {SpawnRadius}m) và gán vào EnemySpawner!");
        }

        private static EnemySpawner GetOrCreateSpawner()
        {
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null) return spawner;

            GameObject spawnerGo = new GameObject("EnemySpawner");
            spawnerGo.transform.position = Vector3.zero;
            Debug.Log("[SpawnPointSetupTool] Chưa có EnemySpawner trong Scene, đã tạo mới.");
            return spawnerGo.AddComponent<EnemySpawner>();
        }

        private static Transform[] CreateSpawnPoints()
        {
            // Xóa phiên bản cũ nếu đã tồn tại để tránh trùng lặp
            GameObject existing = GameObject.Find("SpawnPoints");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject("SpawnPoints");
            root.transform.position = Vector3.zero;

            Transform[] points = new Transform[SpawnPointCount];
            for (int i = 0; i < SpawnPointCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SpawnPointCount;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * SpawnRadius, 0f, Mathf.Sin(angle) * SpawnRadius);

                GameObject point = new GameObject($"SpawnPoint_{i:00}");
                point.transform.SetParent(root.transform, false);
                point.transform.localPosition = pos;
                // Hướng mặt vào tâm đấu trường
                point.transform.localRotation = Quaternion.LookRotation(-pos.normalized);
                points[i] = point.transform;
            }

            return points;
        }

        /// <summary>
        /// Gán Prefab quái từ Assets/Prefabs/Enemies/ cho các mục Pool còn trống.
        /// </summary>
        private static void AssignEnemyPrefabs(SerializedProperty poolsProp)
        {
            if (poolsProp == null) return;

            string[] typeNames = System.Enum.GetNames(typeof(EnemyType));
            if (poolsProp.arraySize < typeNames.Length)
            {
                poolsProp.arraySize = typeNames.Length;
            }

            for (int i = 0; i < typeNames.Length; i++)
            {
                SerializedProperty entry = poolsProp.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = entry.FindPropertyRelative("type");
                SerializedProperty prefabProp = entry.FindPropertyRelative("prefab");
                SerializedProperty prewarmProp = entry.FindPropertyRelative("prewarmCount");

                if (prefabProp.objectReferenceValue != null) continue;

                typeProp.enumValueIndex = i;
                if (prewarmProp.intValue <= 0) prewarmProp.intValue = typeNames[i] == nameof(EnemyType.Gargoyle) ? 2 : typeNames[i] == nameof(EnemyType.VoidWarden) ? 1 : 6;

                string path = $"{PrefabFolder}/{typeNames[i]}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    prefabProp.objectReferenceValue = prefab;
                }
                else
                {
                    Debug.LogWarning($"[SpawnPointSetupTool] Không tìm thấy Prefab: {path}");
                }
            }
        }
    }
}
