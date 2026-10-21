using UnityEditor;
using UnityEngine;
using Vanguard.Cards;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tạo Prefab Decoy, LightningShockVFX và 3 thẻ Hiếm (Sprint 4 - Card 4.3).
    /// </summary>
    public static class RareCardsSetupTool
    {
        private const string RareFolder = "Assets/Data/Cards/Rare";
        private const string DecoyPath = "Assets/Prefabs/Combat/Decoy.prefab";
        private const string LightningPath = "Assets/Prefabs/Combat/LightningShockVFX.prefab";

        [MenuItem("Vanguard/Generate Rare Cards & VFX")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Cards");
            EnsureFolder("Assets/Data/Cards", "Rare");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Combat");
            EnsureFolder("Assets", "Materials");

            GameObject decoyPrefab = CreateDecoyPrefab();
            GameObject lightningPrefab = CreateLightningPrefab();

            CreateCard<HellfireSlashCardSO>("Card_Rare_HellfireSlash", "card_rare_hellfire_slash", "Hellfire Slash",
                "Đòn chém thiêu đốt mục tiêu 10 DMG/giây trong 3s (cộng dồn tối đa 3 lần).", null, null);
            CreateCard<ThunderShockCardSO>("Card_Rare_ThunderShock", "card_rare_thunder_shock", "Thunder Shock",
                "Đòn chém thứ 3 phóng tia sét giật lan 3 mục tiêu lân cận gây 25 DMG.", "lightningVFXPrefab", lightningPrefab);
            CreateCard<DecoyDashCardSO>("Card_Rare_DecoyDash", "card_rare_decoy_dash", "Decoy Dash",
                "Khi Lướt (Dash) để lại một ảo ảnh thu hút toàn bộ quái vật tấn công trong 1.5s.", "decoyPrefab", decoyPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RareCardsSetupTool] ✅ Đã tạo Decoy, LightningShockVFX và 3 thẻ Hiếm tại " + RareFolder + "!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        internal static Material GetOrCreateUnlitMaterial(string path, Color color, bool transparent)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mat.SetColor("_BaseColor", color);
            mat.color = color;

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject CreateDecoyPrefab()
        {
            Material mat = GetOrCreateUnlitMaterial("Assets/Materials/M_Decoy.mat", new Color(0.1f, 0.9f, 1f, 0.4f), true);

            GameObject decoy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            decoy.name = "Decoy";
            decoy.transform.localScale = Vector3.one;

            // Trigger để không cản đường vật lý; layer Default để đạn của Caster vẫn va trúng
            CapsuleCollider col = decoy.GetComponent<CapsuleCollider>();
            col.isTrigger = true;

            Renderer rend = decoy.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            decoy.AddComponent<DecoyLifetime>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(decoy, DecoyPath);
            Object.DestroyImmediate(decoy);
            return prefab;
        }

        private static GameObject CreateLightningPrefab()
        {
            Material mat = GetOrCreateUnlitMaterial("Assets/Materials/M_LightningShock.mat", new Color(0.55f, 0.85f, 1f, 1f), false);

            GameObject bolt = new GameObject("LightningShockVFX");
            LineRenderer line = bolt.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 6;
            line.startWidth = 0.12f;
            line.endWidth = 0.05f;
            line.sharedMaterial = mat;
            line.startColor = new Color(0.7f, 0.92f, 1f, 1f);
            line.endColor = new Color(0.4f, 0.75f, 1f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            bolt.AddComponent<LightningShockVFX>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bolt, LightningPath);
            Object.DestroyImmediate(bolt);
            return prefab;
        }

        private static void CreateCard<T>(string assetName, string id, string cardName, string description,
            string prefabField, GameObject prefab) where T : UpgradeCardSO
        {
            string path = $"{RareFolder}/{assetName}.asset";
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
            so.FindProperty("rarity").enumValueIndex = (int)CardRarity.Rare;
            if (prefabField != null)
            {
                so.FindProperty(prefabField).objectReferenceValue = prefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(card);
        }
    }
}
