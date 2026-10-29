#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 9 - 9.5: chạy toàn bộ smoke test 10 wave + benchmark 3 tier mà không cần bấm phím.
    /// Kết quả ghi vào Logs/smoke_report.txt và Logs/bench_report.txt trong thư mục project; xong tự thoát Play.
    /// </summary>
    public static class AutoTestMenu
    {
        [MenuItem("Vanguard/Test/Run Full Auto Test (9.5)")]
        public static void Run()
        {
            EditorPrefs.SetBool(SmokeTestRunner.AutoKey, true);
            if (EditorApplication.isPlaying)
            {
                // Dừng Play hiện tại rồi vào Play mới để test bắt đầu từ wave 1
                EditorApplication.playModeStateChanged += EnterPlayAfterStop;
                EditorApplication.isPlaying = false;
            }
            else
            {
                EditorApplication.isPlaying = true;
            }
            Debug.Log("[AutoTest] Đang khởi động test tự động (khoảng 5-6 phút). Báo cáo: Logs/smoke_report.txt, Logs/bench_report.txt");
        }

        private static void EnterPlayAfterStop(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= EnterPlayAfterStop;
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
