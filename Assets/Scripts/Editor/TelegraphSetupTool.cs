using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.FX;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tạo Material M_Telegraph_Red, 2 Prefab TelegraphCircle / TelegraphSector
    /// và TelegraphManager trong Scene (Sprint 5 - Card 5.4).
    /// </summary>
    public static class TelegraphSetupTool
    {
        private const string MaterialPath = "Assets/Materials/M_Telegraph_Red.mat";
        private const string CirclePath = "Assets/Prefabs/Combat/TelegraphCircle.prefab";
        private const string SectorPath = "Assets/Prefabs/Combat/TelegraphSector.prefab";

        [MenuItem("Vanguard/Setup Telegraph Warning System")]
        public static void Setup()
        {
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Combat");

            // Unlit URP, Transparent + Alpha blend, đỏ rực #FF1A1A
            Material material = RareCardsSetupTool.GetOrCreateUnlitMaterial(
                MaterialPath, new Color32(0xFF, 0x1A, 0x1A, 0x73), true);

            TelegraphWarning circle = CreatePrefab(CirclePath, "TelegraphCircle", TelegraphShape.Circle, material);
            TelegraphWarning sector = CreatePrefab(SectorPath, "TelegraphSector", TelegraphShape.Sector, material);

            SetupManager(circle, sector);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[TelegraphSetupTool] ✅ Đã tạo M_Telegraph_Red, TelegraphCircle, TelegraphSector, TelegraphManager và lưu Scene!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static TelegraphWarning CreatePrefab(string path, string prefabName, TelegraphShape shape, Material material)
        {
            GameObject root = new GameObject(prefabName);
            TelegraphWarning warning = root.AddComponent<TelegraphWarning>();

            // Hai lớp phẳng: Base (khung nền mờ) và Fill (nạp đầy). Mesh được gán lúc chạy theo hình dạng yêu cầu
            MeshRenderer baseRenderer = CreateLayer(root.transform, "Base", material, 0, 0f);
            MeshRenderer fillRenderer = CreateLayer(root.transform, "Fill", material, 1, 0.005f);

            SerializedObject so = new SerializedObject(warning);
            so.FindProperty("shape").enumValueIndex = (int)shape;
            so.FindProperty("baseRenderer").objectReferenceValue = baseRenderer;
            so.FindProperty("fillRenderer").objectReferenceValue = fillRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<TelegraphWarning>();
        }

        private static MeshRenderer CreateLayer(Transform parent, string layerName, Material material,
            int sortingOrder, float yOffset)
        {
            GameObject go = new GameObject(layerName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.AddComponent<MeshFilter>();

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void SetupManager(TelegraphWarning circle, TelegraphWarning sector)
        {
            TelegraphManager manager = Object.FindAnyObjectByType<TelegraphManager>();
            if (manager == null)
            {
                manager = new GameObject("TelegraphManager").AddComponent<TelegraphManager>();
            }

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("circlePrefab").objectReferenceValue = circle;
            so.FindProperty("sectorPrefab").objectReferenceValue = sector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
    }
}
