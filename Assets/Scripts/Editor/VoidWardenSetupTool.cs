using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Vanguard.Enemy;
using Vanguard.Spawning;
using Vanguard.UI;
using Vanguard.Waves;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor dựng Prefab Void Warden (Boss cuối), gán vào EnemySpawner và cập nhật Wave 10 (Sprint 5 - Card 5.2).
    /// </summary>
    public static class VoidWardenSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/VoidWarden.prefab";
        private const string MeteorPrefabPath = "Assets/Prefabs/Combat/MeteorImpact.prefab";
        private const string Wave10Path = "Assets/Data/Waves/Wave_10.asset";
        private const string BossLabel = "VOID WARDEN - CHÚA TỂ HƯ VÔ [BOSS CUỐI]";

        [MenuItem("Vanguard/Setup Void Warden Final Boss")]
        public static void Setup()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Enemies");
            EnsureFolder("Assets", "Materials");

            GameObject prefab = CreateVoidWardenPrefab();
            AssignToSpawner(prefab);
            UpdateWave10();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[VoidWardenSetupTool] ✅ Đã tạo VoidWarden.prefab, gán vào EnemySpawner, cập nhật Wave 10 và lưu Scene!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material GetOrCreateArmorMaterial()
        {
            const string path = "Assets/Materials/M_VoidWarden_Armor.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "M_VoidWarden_Armor" };
            Color baseColor = new Color(0.04f, 0.02f, 0.07f, 1f); // Đen bóng pha tím
            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            mat.SetFloat("_Metallic", 0.7f);
            mat.SetFloat("_Smoothness", 0.85f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.35f, 0.08f, 0.8f, 1f)); // Tím huyền bí rực sáng
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreateVoidWardenPrefab()
        {
            Material armor = GetOrCreateArmorMaterial();

            GameObject root = new GameObject("VoidWarden");
            root.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);

            // Collider: giá trị local, nhân scale 2.5 -> cao 5m, bán kính 1.5m ngoài thế giới
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.6f;
            col.center = Vector3.zero;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.height = 5f;
            agent.radius = 1.5f;
            agent.baseOffset = 1.25f; // Cùng tỉ lệ với Gargoyle (scale 2 -> offset 1)
            agent.speed = 3.2f;

            // Thân giáp
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            Renderer armorRenderer = visual.GetComponent<Renderer>();
            armorRenderer.sharedMaterial = armor;

            // Thanh máu World Space: parent scale 2.5 -> 0.004 local = 0.01 world
            RectTransform fillRect = GargoyleSetupTool.CreateHealthBar(root.transform, BossLabel, 2.0f, 0.004f, 400f);

            VoidWardenBoss boss = root.AddComponent<VoidWardenBoss>();
            SerializedObject bossSo = new SerializedObject(boss);
            bossSo.FindProperty("armorRenderer").objectReferenceValue = armorRenderer;

            GameObject meteorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeteorPrefabPath);
            if (meteorPrefab != null)
            {
                bossSo.FindProperty("meteorImpactPrefab").objectReferenceValue = meteorPrefab;
            }
            else
            {
                Debug.LogWarning($"[VoidWardenSetupTool] Không tìm thấy {MeteorPrefabPath}. Chạy 'Vanguard > Generate Legendary Cards & Meteor' rồi chạy lại tool này để gán Mưa Thiên Thạch.");
            }
            bossSo.ApplyModifiedPropertiesWithoutUndo();

            WorldSpaceBossHealthBar bar = root.GetComponentInChildren<WorldSpaceBossHealthBar>(true);
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("fillRect").objectReferenceValue = fillRect;
            barSo.FindProperty("fillImage").objectReferenceValue = fillRect.GetComponent<Image>();
            barSo.ApplyModifiedPropertiesWithoutUndo();

            // Layer Enemy cho toàn bộ cây đối tượng
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = enemyLayer;
                }
            }
            else
            {
                Debug.LogWarning("[VoidWardenSetupTool] Layer \"Enemy\" chưa tồn tại trong Project Settings > Tags and Layers!");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AssignToSpawner(GameObject prefab)
        {
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[VoidWardenSetupTool] Không tìm thấy EnemySpawner trong Scene. Chạy 'Vanguard > Setup Enemy Spawner & Spawn Points' rồi chạy lại tool này.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty pools = so.FindProperty("enemyPools");

            SerializedProperty entry = null;
            for (int i = 0; i < pools.arraySize; i++)
            {
                SerializedProperty candidate = pools.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("type").enumValueIndex == (int)EnemyType.VoidWarden)
                {
                    entry = candidate;
                    break;
                }
            }

            if (entry == null)
            {
                pools.arraySize++;
                entry = pools.GetArrayElementAtIndex(pools.arraySize - 1);
            }

            entry.FindPropertyRelative("type").enumValueIndex = (int)EnemyType.VoidWarden;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("prewarmCount").intValue = 1;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        private static void UpdateWave10()
        {
            WaveConfigSO wave = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(Wave10Path);
            if (wave == null)
            {
                Debug.LogWarning($"[VoidWardenSetupTool] Không tìm thấy {Wave10Path}. Chạy 'Vanguard > Generate 10 Waves Data' (đã cập nhật Wave 10) rồi chạy lại.");
                return;
            }

            wave.clearCondition = WaveConfigSO.WaveClearCondition.ClearAllEnemies;
            wave.enemyGroups.Clear();
            wave.enemyGroups.Add(new WaveConfigSO.EnemyGroup { enemyType = EnemyType.VoidWarden, count = 1 });
            EditorUtility.SetDirty(wave);
        }
    }
}
