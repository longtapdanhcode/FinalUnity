using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Environment;
using Vanguard.Spawning;
using Vanguard.Waves;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tạo 10 asset WaveConfigSO theo GDD và thiết lập WaveManager trong Scene (Sprint 3 - Card 3.3).
    /// </summary>
    public static class WaveDataGeneratorTool
    {
        private const string WaveFolder = "Assets/Data/Waves";
        private const WaveConfigSO.WaveClearCondition ClearAll = WaveConfigSO.WaveClearCondition.ClearAllEnemies;
        private const WaveConfigSO.WaveClearCondition ClearOrTimer = WaveConfigSO.WaveClearCondition.ClearOrTimer;

        [MenuItem("Vanguard/Generate 10 Waves Data")]
        public static void Generate()
        {
            EnsureFolders();

            var assets = new WaveConfigSO[10];

            // Wave 5 / Wave 10: chưa có Prefab Mini-Boss Gargoyle / Void Warden nên dùng quái đại diện tạm
            assets[0] = CreateWave(1, "Khởi Đầu Của Vực Thẳm", ClearAll, 0f, 0.8f, false, G(EnemyType.Crawler, 15));
            assets[1] = CreateWave(2, "Bầy Rệp Hư Vô", ClearAll, 0f, 0.7f, false, G(EnemyType.Crawler, 15));
            assets[2] = CreateWave(3, "Tiếng Gọi Từ Xa", ClearAll, 0f, 0.6f, false, G(EnemyType.Crawler, 15), G(EnemyType.VoidCaster, 10));
            assets[3] = CreateWave(4, "Mưa Ma Thuật", ClearAll, 0f, 0.6f, false, G(EnemyType.Crawler, 12), G(EnemyType.VoidCaster, 13));
            assets[4] = CreateWave(5, "Mini-Boss: Gargoyle", ClearAll, 0f, 1.0f, false, G(EnemyType.Gargoyle, 1), G(EnemyType.Crawler, 4));
            assets[5] = CreateWave(6, "Ngòi Nổ Cháy Chậm", ClearOrTimer, 60f, 0.5f, false, G(EnemyType.Crawler, 25), G(EnemyType.VolatileHusk, 15));
            assets[6] = CreateWave(7, "Triều Nổ", ClearOrTimer, 60f, 0.5f, false, G(EnemyType.Crawler, 20), G(EnemyType.VolatileHusk, 20));
            assets[7] = CreateWave(8, "Lưỡi Hái Tử Thần", ClearAll, 0f, 0.4f, true, G(EnemyType.Crawler, 20), G(EnemyType.VoidCaster, 15), G(EnemyType.VolatileHusk, 15));
            assets[8] = CreateWave(9, "Cơn Bão Cuối Cùng", ClearAll, 0f, 0.4f, true, G(EnemyType.Crawler, 20), G(EnemyType.VoidCaster, 15), G(EnemyType.VolatileHusk, 15));
            assets[9] = CreateWave(10, "Boss: Void Warden", ClearAll, 0f, 1.0f, true, G(EnemyType.VoidWarden, 1));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetupSceneManager(assets);

            Debug.Log("[WaveDataGeneratorTool] ✅ Đã tạo 10 WaveConfigSO tại " + WaveFolder + " và thiết lập WaveManager trong Scene!");
        }

        private static WaveConfigSO.EnemyGroup G(EnemyType type, int count)
        {
            return new WaveConfigSO.EnemyGroup { enemyType = type, count = count };
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder(WaveFolder))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Waves");
            }
        }

        private static WaveConfigSO CreateWave(int number, string title, WaveConfigSO.WaveClearCondition condition,
            float survivalTime, float spawnInterval, bool trap, params WaveConfigSO.EnemyGroup[] groups)
        {
            string path = $"{WaveFolder}/Wave_{number:00}.asset";
            WaveConfigSO wave = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(path);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveConfigSO>();
                AssetDatabase.CreateAsset(wave, path);
            }

            // Ghi đè dữ liệu để luôn khớp GDD khi chạy lại tool
            wave.waveNumber = number;
            wave.waveTitle = title;
            wave.hudTag = number == 5 ? "MINI-BOSS" : number == 10 ? "BOSS CUỐI" : string.Empty;
            wave.clearCondition = condition;
            wave.survivalTime = survivalTime > 0f ? survivalTime : 60f;
            wave.spawnInterval = spawnInterval;
            wave.enableCenterTrap = trap;
            wave.enemyGroups.Clear();
            wave.enemyGroups.AddRange(groups);

            EditorUtility.SetDirty(wave);
            return wave;
        }

        private static void SetupSceneManager(WaveConfigSO[] assets)
        {
            WaveManager manager = Object.FindAnyObjectByType<WaveManager>();
            if (manager == null)
            {
                GameObject go = GameObject.Find("WaveManager") ?? new GameObject("WaveManager");
                manager = go.GetComponent<WaveManager>() ?? go.AddComponent<WaveManager>();
            }

            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[WaveDataGeneratorTool] Không tìm thấy EnemySpawner trong Scene. Chạy 'Vanguard > Setup Enemy Spawner & Spawn Points' trước.");
            }

            CenterRotatingTrap trap = Object.FindAnyObjectByType<CenterRotatingTrap>(FindObjectsInactive.Include);
            if (trap == null)
            {
                Debug.LogWarning("[WaveDataGeneratorTool] Không tìm thấy CenterRotatingTrap trong Scene. Chạy 'Vanguard > Setup Arena Bounds & Rotating Trap' trước.");
            }

            SerializedObject so = new SerializedObject(manager);

            SerializedProperty configsProp = so.FindProperty("waveConfigs");
            configsProp.arraySize = assets.Length;
            for (int i = 0; i < assets.Length; i++)
            {
                configsProp.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
            }

            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("centerTrap").objectReferenceValue = trap;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            Selection.activeGameObject = manager.gameObject;

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
    }
}
