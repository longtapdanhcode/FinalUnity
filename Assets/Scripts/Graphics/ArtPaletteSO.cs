using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Bảng màu và mức sáng emissive dùng chung cho toàn game (Art Bible - Sprint 6, Card 6.2).
    /// Mọi shader/VFX/UI mới nên lấy màu từ đây thay vì hardcode.
    /// Tạo asset: Vanguard/Graphics/Create Art Palette hoặc Create > Vanguard > Art Palette.
    /// </summary>
    [CreateAssetMenu(fileName = "ArtPalette", menuName = "Vanguard/Art Palette")]
    public class ArtPaletteSO : ScriptableObject
    {
        [Header("Nền (Void)")]
        public Color voidBlack = new Color(0.02f, 0.01f, 0.06f, 1f);
        public Color voidViolet = new Color(0.12f, 0.04f, 0.25f, 1f);
        public Color fogColor = new Color(0.05f, 0.03f, 0.11f, 1f);

        [Header("Phe người chơi")]
        [ColorUsage(false, true)] public Color playerCyan = new Color(0f, 0.8f, 1.2f, 1f);

        [Header("Phe kẻ địch")]
        [ColorUsage(false, true)] public Color enemyRed = new Color(1f, 0.15f, 0.12f, 1f);
        [ColorUsage(false, true)] public Color voidPurple = new Color(0.6f, 0.2f, 1f, 1f);
        [ColorUsage(false, true)] public Color huskOrange = new Color(1f, 0.35f, 0.05f, 1f);
        [ColorUsage(false, true)] public Color bossMagenta = new Color(0.9f, 0.2f, 1.6f, 1f);

        [Header("Nguy hiểm / Cảnh báo")]
        [ColorUsage(false, true)] public Color dangerRed = new Color(1f, 0.1f, 0.1f, 1f);
        [ColorUsage(false, true)] public Color hazardOrange = new Color(1f, 0.28f, 0.05f, 1f);

        [Header("Độ hiếm thẻ")]
        public Color rarityCommon = new Color(0.7f, 0.75f, 0.85f, 1f);
        [ColorUsage(false, true)] public Color rarityRare = new Color(0.2f, 0.6f, 1.4f, 1f);
        [ColorUsage(false, true)] public Color rarityLegendary = new Color(1.4f, 0.9f, 0.2f, 1f);

        [Header("Bậc cường độ emissive (nhân với màu gốc)")]
        [Tooltip("Lưới sàn, trang trí nền")] public float backgroundIntensity = 1.5f;
        [Tooltip("Kẻ địch thường")] public float enemyIntensity = 2f;
        [Tooltip("Người chơi - luôn nổi hơn kẻ địch thường")] public float playerIntensity = 3f;
        [Tooltip("Cảnh báo (telegraph), nguy hiểm")] public float dangerIntensity = 4f;
        [Tooltip("VFX chiêu cuối, boss, thẻ Legendary")] public float fxIntensity = 6f;

        /// <summary>Trả về màu HDR bằng cách nhân màu gốc với cường độ.</summary>
        public static Color WithIntensity(Color baseColor, float intensity)
        {
            return new Color(baseColor.r * intensity, baseColor.g * intensity, baseColor.b * intensity, baseColor.a);
        }
    }
}
