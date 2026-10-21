using UnityEditor;
using UnityEngine;
using Vanguard.Cards;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tạo Prefab MeteorImpact và 3 thẻ Huyền Thoại (Sprint 4 - Card 4.4).
    /// </summary>
    public static class LegendaryCardsSetupTool
    {
        private const string LegendaryFolder = "Assets/Data/Cards/Legendary";
        private const string MeteorPath = "Assets/Prefabs/Combat/MeteorImpact.prefab";

        [MenuItem("Vanguard/Generate Legendary Cards & Meteor")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Cards");
            EnsureFolder("Assets/Data/Cards", "Legendary");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Combat");
            EnsureFolder("Assets", "Materials");

            GameObject meteorPrefab = CreateMeteorPrefab();

            CreateCard<BloodGodVortexCardSO>("Card_Legendary_BloodGodVortex", "card_legendary_blood_god_vortex",
                "Blood God's Vortex", "Tăng 30% bán kính vung kiếm và hồi 4% Max HP mỗi khi hạ gục một quái.", null, null);
            CreateCard<MeteorFallCardSO>("Card_Legendary_MeteorFall", "card_legendary_meteor_fall",
                "Meteor Fall", "Mỗi lần Lướt gọi một thiên thạch giáng xuống điểm xuất phát, gây 80 sát thương diện rộng và làm choáng 1s.",
                "meteorPrefab", meteorPrefab);
            CreateCard<VoidRageCardSO>("Card_Legendary_VoidRage", "card_legendary_void_rage",
                "Void Rage", "Khi máu dưới 30%: gấp đôi sát thương và +50% tỉ lệ chí mạng.", null, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LegendaryCardsSetupTool] ✅ Đã tạo MeteorImpact và 3 thẻ Huyền Thoại tại " + LegendaryFolder + "!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material GetOrCreateMeteorRockMaterial()
        {
            const string path = "Assets/Materials/M_MeteorRock.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "M_MeteorRock" };
            Color baseColor = new Color(0.55f, 0.12f, 0.03f, 1f);
            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(3f, 0.6f, 0.1f, 1f)); // Rực lửa
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Transform CreateVisual(Transform parent, string visualName, PrimitiveType type, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = visualName;
            go.transform.SetParent(parent, false);

            // Chỉ là hình ảnh: không cản vật lý, không đổ bóng
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static GameObject CreateMeteorPrefab()
        {
            Material rockMat = GetOrCreateMeteorRockMaterial();
            Material explosionMat = RareCardsSetupTool.GetOrCreateUnlitMaterial(
                "Assets/Materials/M_MeteorExplosion.mat", new Color(1f, 0.45f, 0.05f, 0.6f), true);

            GameObject root = new GameObject("MeteorImpact");

            Transform meteor = CreateVisual(root.transform, "MeteorVisual", PrimitiveType.Sphere, rockMat);
            meteor.localScale = new Vector3(1.6f, 1.6f, 1.6f);

            Transform explosion = CreateVisual(root.transform, "ExplosionVFX", PrimitiveType.Sphere, explosionMat);
            explosion.gameObject.SetActive(false);

            MeteorImpact impact = root.AddComponent<MeteorImpact>();
            SerializedObject so = new SerializedObject(impact);
            so.FindProperty("meteorVisual").objectReferenceValue = meteor;
            so.FindProperty("explosionVFX").objectReferenceValue = explosion;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MeteorPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateCard<T>(string assetName, string id, string cardName, string description,
            string prefabField, GameObject prefab) where T : UpgradeCardSO
        {
            string path = $"{LegendaryFolder}/{assetName}.asset";
            T card = AssetDatabase.LoadAssetAtPath<T>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(card, path);
            }

            var so = new SerializedObject(card);
            so.FindProperty("cardID").stringValue = id;
            so.FindProperty("cardName").stringValue = cardName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("rarity").enumValueIndex = (int)CardRarity.Legendary;
            if (prefabField != null)
            {
                so.FindProperty(prefabField).objectReferenceValue = prefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(card);
        }
    }
}
