#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 9 - 9.5: công cụ kiểm tra tự chọn preset lần chạy đầu.</summary>
    public static class GraphicsPrefsTool
    {
        [MenuItem("Vanguard/Graphics/Reset Quality Preset to Auto (9.5)")]
        public static void ResetToAuto()
        {
            GraphicsSettingsService.ResetToAuto();
            Debug.Log("[Graphics 9.5] Đã xoá preset đã lưu. Bấm Play để thấy tự chọn lần chạy đầu (xem Console: [Graphics] Lần chạy đầu...).");
        }
    }
}
#endif
