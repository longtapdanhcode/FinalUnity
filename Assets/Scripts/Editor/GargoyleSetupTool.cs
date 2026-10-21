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
    /// Công cụ Editor dựng Prefab Gargoyle Mini-Boss, gán vào EnemySpawner và cập nhật Wave 5 (Sprint 5 - Card 5.1).
    /// </summary>
    public static class GargoyleSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/Gargoyle.prefab";
        private const string Wave5Path = "Assets/Data/Waves/Wave_05.asset";
        private const string BossLabel = "Gargoyle Bạo Quân - Mini-Boss";

        [MenuItem("Vanguard/Setup Gargoyle Mini-Boss")]
        public static void Setup()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Enemies");
            EnsureFolder("Assets", "Materials");

            GameObject prefab = CreateGargoylePrefab();
            AssignToSpawner(prefab);
            UpdateWave5();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[GargoyleSetupTool] ✅ Đã tạo Gargoyle.prefab, gán vào EnemySpawner, cập nhật Wave 5 và lưu Scene!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material GetOrCreateSkinMaterial()
        {
            const string path = "Assets/Materials/M_Gargoyle_Skin.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "M_Gargoyle_Skin" };
            Color baseColor = new Color(0.1f, 0.06f, 0.14f, 1f); // Đá đen tím đậm
            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            mat.SetFloat("_Smoothness", 0.25f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.45f, 0.03f, 0.03f, 1f)); // Ánh đỏ hầm hập
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreateGargoylePrefab()
        {
            Material skin = GetOrCreateSkinMaterial();

            GameObject root = new GameObject("Gargoyle");
            root.transform.localScale = new Vector3(2f, 2f, 2f);

            // Collider: giá trị local, nhân scale 2 -> cao 4m, bán kính 1.2m ngoài thế giới
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.6f;
            col.center = Vector3.zero;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.height = 4f;
            agent.radius = 1.2f;
            agent.baseOffset = 1f;
            agent.speed = 2.8f;

            // Hình ảnh thân đá
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            Renderer rend = visual.GetComponent<Renderer>();
            rend.sharedMaterial = skin;

            RectTransform fillRect = CreateHealthBar(root.transform);

            GargoyleBoss boss = root.AddComponent<GargoyleBoss>();
            SerializedObject bossSo = new SerializedObject(boss);
            bossSo.ApplyModifiedPropertiesWithoutUndo();

            WorldSpaceBossHealthBar bar = root.GetComponentInChildren<WorldSpaceBossHealthBar>(true);
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("fillRect").objectReferenceValue = fillRect;
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
                Debug.LogWarning("[GargoyleSetupTool] Layer \"Enemy\" chưa tồn tại trong Project Settings > Tags and Layers!");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Dựng Canvas World Space trên đầu Boss: nền tối, phần máu đỏ rực và nhãn tên. Trả về RectTransform của phần máu.
        /// </summary>
        internal static RectTransform CreateHealthBar(Transform parent, string bossLabel = BossLabel,
            float localY = 1.7f, float localScale = 0.005f, float barWidth = 300f)
        {
            GameObject canvasObj = new GameObject("HealthBarCanvas", typeof(RectTransform));
            canvasObj.transform.SetParent(parent, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(barWidth, 36f);
            // Parent scale 2: 0.005 local = 0.01 world -> thanh máu rộng 3m
            canvasRect.localScale = new Vector3(localScale, localScale, localScale);
            canvasRect.localPosition = new Vector3(0f, localY, 0f);

            GameObject bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(canvasObj.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.02f, 0.02f, 0.9f);
            bgImage.raycastTarget = false;

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(canvasObj.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.1f, 0.05f, 1f); // Đỏ rực
            fillImage.raycastTarget = false;

            GameObject label = new GameObject("NameLabel", typeof(RectTransform));
            label.transform.SetParent(canvasObj.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);
            labelRect.sizeDelta = new Vector2(Mathf.Max(500f, barWidth), 36f);
            Text text = label.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = bossLabel;
            text.fontSize = 26;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.7f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            label.AddComponent<Outline>().effectColor = Color.black;

            canvasObj.AddComponent<WorldSpaceBossHealthBar>();
            return fillRect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AssignToSpawner(GameObject prefab)
        {
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[GargoyleSetupTool] Không tìm thấy EnemySpawner trong Scene. Chạy 'Vanguard > Setup Enemy Spawner & Spawn Points' rồi chạy lại tool này.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty pools = so.FindProperty("enemyPools");

            SerializedProperty entry = null;
            for (int i = 0; i < pools.arraySize; i++)
            {
                SerializedProperty candidate = pools.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("type").enumValueIndex == (int)EnemyType.Gargoyle)
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

            entry.FindPropertyRelative("type").enumValueIndex = (int)EnemyType.Gargoyle;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("prewarmCount").intValue = 2;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        private static void UpdateWave5()
        {
            WaveConfigSO wave = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(Wave5Path);
            if (wave == null)
            {
                Debug.LogWarning($"[GargoyleSetupTool] Không tìm thấy {Wave5Path}. Chạy 'Vanguard > Generate 10 Waves Data' (đã cập nhật Wave 5) rồi chạy lại.");
                return;
            }

            wave.enemyGroups.Clear();
            wave.enemyGroups.Add(new WaveConfigSO.EnemyGroup { enemyType = EnemyType.Gargoyle, count = 1 });
            wave.enemyGroups.Add(new WaveConfigSO.EnemyGroup { enemyType = EnemyType.Crawler, count = 4 });
            EditorUtility.SetDirty(wave);
        }
    }
}
