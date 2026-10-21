#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 6 - Card 6.2: tạo asset ArtPalette mặc định tại Assets/Data/ArtPalette.asset.</summary>
    public static class ArtPaletteSetupTool
    {
        private const string Path = "Assets/Data/ArtPalette.asset";

        [MenuItem("Vanguard/Graphics/Create Art Palette")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            ArtPaletteSO palette = AssetDatabase.LoadAssetAtPath<ArtPaletteSO>(Path);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<ArtPaletteSO>();
                AssetDatabase.CreateAsset(palette, Path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ArtPalette] Đã tạo bảng màu mặc định tại {Path}.");
            }
            else
            {
                Debug.Log($"[ArtPalette] Đã tồn tại tại {Path}, giữ nguyên giá trị hiện có.");
            }

            Selection.activeObject = palette;
            EditorGUIUtility.PingObject(palette);
        }
    }
}
#endif
