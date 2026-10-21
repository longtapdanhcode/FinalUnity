using UnityEditor;
using UnityEngine;
using Vanguard.Cards;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tạo thư mục dữ liệu thẻ bài và 3 thẻ mẫu (Sprint 4 - Card 4.1).
    /// </summary>
    public static class CardDataSetupTool
    {
        private const string CardRoot = "Assets/Data/Cards";

        [MenuItem("Vanguard/Setup Card Directories & Samples")]
        public static void Setup()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Cards");
            EnsureFolder(CardRoot, "Common");
            EnsureFolder(CardRoot, "Rare");
            EnsureFolder(CardRoot, "Legendary");

            CreateSample("Common", "Card_Common_Vitality", "common_vitality", "Thể Lực Cường Tráng",
                "Tăng 20 máu tối đa và hồi tương ứng.", CardRarity.Common, StatUpgradeCardSO.StatType.MaxHP, 20f);
            CreateSample("Rare", "Card_Rare_Swiftblade", "rare_swiftblade", "Lưỡi Kiếm Thần Tốc",
                "Tăng 20% tốc độ đánh.", CardRarity.Rare, StatUpgradeCardSO.StatType.AttackSpeed, 0.2f);
            CreateSample("Legendary", "Card_Legendary_VoidFury", "legendary_voidfury", "Cuồng Nộ Hư Vô",
                "Tăng 50% sát thương đòn chém.", CardRarity.Legendary, StatUpgradeCardSO.StatType.AttackDamage, 0.5f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CardDataSetupTool] ✅ Đã thiết lập thư mục Cards (Common/Rare/Legendary) và 3 thẻ mẫu!");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void CreateSample(string folder, string assetName, string id, string cardName,
            string description, CardRarity rarity, StatUpgradeCardSO.StatType stat, float value)
        {
            string path = $"{CardRoot}/{folder}/{assetName}.asset";
            var card = AssetDatabase.LoadAssetAtPath<StatUpgradeCardSO>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<StatUpgradeCardSO>();
                AssetDatabase.CreateAsset(card, path);
            }

            // Ghi qua SerializedObject để kiểm chứng serialization của các field private
            var so = new SerializedObject(card);
            so.FindProperty("cardID").stringValue = id;
            so.FindProperty("cardName").stringValue = cardName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("rarity").enumValueIndex = (int)rarity;
            so.FindProperty("statType").enumValueIndex = (int)stat;
            so.FindProperty("value").floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(card);
        }
    }
}
