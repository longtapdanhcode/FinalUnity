using UnityEditor;
using UnityEngine;
using Vanguard.Cards;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tạo 3 asset thẻ Thường: Raw Strength, Storm Boots, Blood Milk (Sprint 4 - Card 4.2).
    /// </summary>
    public static class CommonCardsSetupTool
    {
        private const string CommonFolder = "Assets/Data/Cards/Common";

        [MenuItem("Vanguard/Generate Common Cards")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Cards");
            EnsureFolder("Assets/Data/Cards", "Common");

            CreateCard<RawStrengthCardSO>("Card_Common_RawStrength", "card_common_raw_strength",
                "Raw Strength", "Tăng 15% sát thương tấn công cơ bản.");
            CreateCard<StormBootsCardSO>("Card_Common_StormBoots", "card_common_storm_boots",
                "Storm Boots", "Tăng 12% tốc độ di chuyển.");
            CreateCard<BloodMilkCardSO>("Card_Common_BloodMilk", "card_common_blood_milk",
                "Blood Milk", "Hồi ngay 30 HP và tăng vĩnh viễn 20 HP tối đa.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CommonCardsSetupTool] ✅ Đã tạo 3 thẻ Thường tại {CommonFolder}!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void CreateCard<T>(string assetName, string id, string cardName, string description)
            where T : UpgradeCardSO
        {
            string path = $"{CommonFolder}/{assetName}.asset";
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
            so.FindProperty("rarity").enumValueIndex = (int)CardRarity.Common;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(card);
        }
    }
}
