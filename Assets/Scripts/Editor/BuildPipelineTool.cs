using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Build Standalone tự động, gọi được từ menu hoặc dòng lệnh (Sprint 5 - Card 5.5):
    /// Unity -batchmode -quit -projectPath &lt;đường dẫn&gt; -executeMethod Vanguard.EditorTools.BuildPipelineTool.BuildStandalone
    /// </summary>
    public static class BuildPipelineTool
    {
        private const string OutputFolder = "Build";

        [MenuItem("Vanguard/Build Standalone")]
        public static void BuildStandaloneMenu()
        {
            BuildStandalone();
        }

        public static void BuildStandalone()
        {
            bool batchMode = Application.isBatchMode;

            string[] scenes = GetScenesInBuild();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildPipelineTool] Không có Scene hợp lệ để build. Thêm Scene vào File > Build Profiles (Scene List).");
                if (batchMode) EditorApplication.Exit(1);
                return;
            }

            if (!TryGetTarget(out BuildTarget target, out string outputPath))
            {
                Debug.LogError("[BuildPipelineTool] Hệ điều hành của Editor chưa được hỗ trợ build Standalone.");
                if (batchMode) EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildPipelineTool] Bắt đầu build {target} ({scenes.Length} scene) -> {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildPipelineTool] ✅ Build thành công: {summary.outputPath}\n" +
                          $"Dung lượng: {FormatSize(summary.totalSize)} | Thời gian: {summary.totalTime.TotalSeconds:0.0}s | " +
                          $"Cảnh báo: {summary.totalWarnings} | Lỗi: {summary.totalErrors}");
            }
            else
            {
                Debug.LogError($"[BuildPipelineTool] ❌ Build thất bại ({summary.result}). " +
                               $"Cảnh báo: {summary.totalWarnings} | Lỗi: {summary.totalErrors}");
            }

            if (batchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// Lấy các Scene đang bật trong Build Settings (và còn tồn tại trên đĩa).
        /// Nếu danh sách trống thì dùng Scene đang mở như phương án dự phòng.
        /// </summary>
        private static string[] GetScenesInBuild()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length > 0) return scenes;

            string activeScenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(activeScenePath))
            {
                Debug.LogWarning($"[BuildPipelineTool] Build Settings chưa có Scene nào, dùng Scene đang mở: {activeScenePath}");
                return new[] { activeScenePath };
            }
            return scenes;
        }

        /// <summary>
        /// Chọn nền tảng theo hệ điều hành của Editor và đường dẫn xuất trong thư mục Build/ ở gốc dự án.
        /// </summary>
        private static bool TryGetTarget(out BuildTarget target, out string outputPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string buildRoot = Path.Combine(projectRoot, OutputFolder);
            string productName = string.Join("_", PlayerSettings.productName.Split(Path.GetInvalidFileNameChars()));

            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    target = BuildTarget.StandaloneOSX;
                    outputPath = Path.Combine(buildRoot, productName + ".app");
                    return true;
                case RuntimePlatform.WindowsEditor:
                    target = BuildTarget.StandaloneWindows64;
                    outputPath = Path.Combine(buildRoot, productName, productName + ".exe");
                    return true;
                case RuntimePlatform.LinuxEditor:
                    target = BuildTarget.StandaloneLinux64;
                    outputPath = Path.Combine(buildRoot, productName, productName + ".x86_64");
                    return true;
                default:
                    target = default;
                    outputPath = string.Empty;
                    return false;
            }
        }

        private static string FormatSize(ulong bytes)
        {
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        }
    }
}
