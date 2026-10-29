using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using Vanguard.Player;
using Vanguard.UI;
using Vanguard.Waves;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Smoke test tự động toàn bộ 10 wave + 2 boss (Sprint 9 - 9.5), chỉ chạy trong Editor / Development Build.
    /// F6: bật/tắt. Runner giữ Player sống, tự chọn thẻ nâng cấp, bỏ qua wave sau một khoảng giao chiến,
    /// luân phiên quality tier theo wave (kiểm tra đổi tier lúc chạy), đếm Error/Exception, theo dõi bộ nhớ và số Material/Mesh
    /// để phát hiện rò rỉ, rồi ghi báo cáo ra Console và file Logs/smoke_report.txt trong thư mục project.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmokeTestRunner : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        private const float NormalWaveSeconds = 20f;
        private const float BossWaveSeconds = 35f;
        private const float ModalPickDelay = 0.4f;
        private const long MaterialLeakThreshold = 60;
        private const long HeapLeakThresholdMb = 60;

        private struct WaveRecord
        {
            public int Wave;
            public GraphicsTier Tier;
            public float DwellSeconds;
            public float MaxFrameMs;
            public long MonoMb;
            public long TotalMb;
            public int Materials;
            public int Meshes;
            public int Errors;
        }

        private readonly List<WaveRecord> records = new List<WaveRecord>();
        private readonly List<string> errorSamples = new List<string>();
        private readonly HashSet<string> errorKeys = new HashSet<string>();

        public const string AutoKey = "Vanguard.AutoTest";
        private const int BenchmarkWave = 9;
        private const float BenchmarkDelay = 6f;

        private bool autoMode;
        private bool benchmarkStarted;
        private bool active;
        private PlayerHealth player;
        private int errorCount;
        private int errorsAtWaveStart;
        private int currentWave;
        private float waveStartTime;
        private float waveLimit;
        private float maxFrameMs;
        private float modalOpenedAt = -1f;
        private bool finished;
        private GraphicsTier originalTier;
        private long baseMonoMb;
        private int baseMaterials;
        private int baseMeshes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<SmokeTestRunner>() != null) return;
            var go = new GameObject("SmokeTestRunner") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            SmokeTestRunner runner = go.AddComponent<SmokeTestRunner>();
#if UNITY_EDITOR
            // Chạy tự động từ menu Vanguard/Test/Run Full Auto Test: cờ chỉ có hiệu lực một lần
            if (UnityEditor.EditorPrefs.GetBool(AutoKey, false))
            {
                UnityEditor.EditorPrefs.SetBool(AutoKey, false);
                runner.autoMode = true;
                Application.runInBackground = true;
                runner.StartCoroutine(runner.AutoBegin());
            }
#endif
        }

        private System.Collections.IEnumerator AutoBegin()
        {
            yield return new WaitForSecondsRealtime(2f);
            Begin();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f6Key.wasPressedThisFrame)
            {
                if (active) Stop("dừng thủ công"); else Begin();
            }
            if (!active) return;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            if (frameMs > maxFrameMs) maxFrameMs = frameMs;

            KeepPlayerAlive();
            HandleUpgradeModal();
            HandleWaveTimeout();
        }

        private void Begin()
        {
            player = FindAnyObjectByType<PlayerHealth>();
            if (player == null || WaveManager.Instance == null)
            {
                Debug.LogWarning("[Smoke] Thiếu Player hoặc WaveManager, không chạy được.");
                return;
            }

            // Chỉ cho test: nhiều Husk nổ dây chuyền trong cùng frame (50 DMG/lần) có thể hạ Player dù đã hồi máu
            player.IncreaseMaxHealth(100000f, true);

            records.Clear();
            errorSamples.Clear();
            errorKeys.Clear();
            errorCount = 0;
            finished = false;
            benchmarkStarted = false;
            currentWave = 0;
            originalTier = GraphicsSettingsService.Tier;

            Collect();
            baseMonoMb = Profiler.GetMonoUsedSizeLong() >> 20;
            baseMaterials = Resources.FindObjectsOfTypeAll<Material>().Length;
            baseMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;

            Application.logMessageReceived += OnLog;
            WaveManager.OnWaveStarted += OnWaveStarted;
            WaveManager.OnAllWavesCleared += OnAllWavesCleared;
            player.OnPlayerDeath += OnPlayerDeath;

            active = true;
            Debug.Log("[Smoke] Bắt đầu. Nếu đang ở giữa chừng, nên bắt đầu từ wave 1 (Play lại rồi bấm F6 ngay).");
            // Nếu đã có wave đang chạy, bắt đầu theo dõi nó luôn
            if (WaveManager.Instance.IsWaveActive) OnWaveStarted(WaveManager.Instance.CurrentWaveNumber);
        }

        private void Stop(string reason)
        {
            if (!active) return;
            active = false;
            Application.logMessageReceived -= OnLog;
            WaveManager.OnWaveStarted -= OnWaveStarted;
            WaveManager.OnAllWavesCleared -= OnAllWavesCleared;
            if (player != null) player.OnPlayerDeath -= OnPlayerDeath;
            GraphicsSettingsService.SetTier(originalTier);
            WriteReport(reason);
#if UNITY_EDITOR
            if (autoMode) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDisable()
        {
            if (active) Stop("component bị tắt");
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            string key = condition.Length > 140 ? condition.Substring(0, 140) : condition;
            if (errorKeys.Add(key) && errorSamples.Count < 20) errorSamples.Add($"[{type}] {key}");
        }

        private void KeepPlayerAlive()
        {
            if (player == null || player.IsDead) return;
            if (player.HealthNormalized < 0.6f) player.Heal(player.MaxHealth);
        }

        private void HandleUpgradeModal()
        {
            UpgradeManager um = FindUpgradeManager();
            if (um == null || !um.IsModalOpen) { modalOpenedAt = -1f; return; }

            if (modalOpenedAt < 0f) { modalOpenedAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - modalOpenedAt < ModalPickDelay) return;

            UpgradeCardUI[] cards = FindObjectsByType<UpgradeCardUI>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].CurrentCard != null)
                {
                    um.OnCardChosen(cards[i].CurrentCard);
                    break;
                }
            }
            modalOpenedAt = -1f;
        }

        private UpgradeManager cachedUpgrade;
        private UpgradeManager FindUpgradeManager()
        {
            if (cachedUpgrade == null) cachedUpgrade = FindAnyObjectByType<UpgradeManager>();
            return cachedUpgrade;
        }

        private void HandleWaveTimeout()
        {
            WaveManager waves = WaveManager.Instance;
            if (waves == null || !waves.IsWaveActive || currentWave == 0) return;

            // Bỏ qua wave sau khi đã giao chiến đủ lâu (thời gian thực, không phụ thuộc timeScale)
            // Benchmark 3 tier ở wave đông nhất (chỉ khi chạy tự động)
            QualityBenchmark bench = QualityBenchmark.Instance;
            if (bench != null && bench.IsRunning) { waveStartTime += Time.unscaledDeltaTime; return; }
            if (autoMode && !benchmarkStarted && currentWave == BenchmarkWave && bench != null
                && Time.unscaledTime - waveStartTime >= BenchmarkDelay)
            {
                benchmarkStarted = true;
                bench.Begin();
                return;
            }

            if (Time.unscaledTime - waveStartTime >= waveLimit) waves.SkipCurrentWave();
        }

        private void OnWaveStarted(int wave)
        {
            // Chốt số liệu của wave trước
            if (currentWave > 0) RecordWave();

            currentWave = wave;
            waveStartTime = Time.unscaledTime;
            maxFrameMs = 0f;
            errorsAtWaveStart = errorCount;

            WaveManager waves = WaveManager.Instance;
            bool boss = waves != null && waves.TotalEnemiesInWave <= 2;
            waveLimit = boss ? BossWaveSeconds : NormalWaveSeconds;

            // Luân phiên tier theo wave để kiểm tra đổi chất lượng ngay giữa trận
            GraphicsSettingsService.SetTier((GraphicsTier)((wave - 1) % 3));
            Debug.Log($"[Smoke] Wave {wave} ({(boss ? "BOSS" : "thường")}), tier {GraphicsSettingsService.Tier}, giao chiến {waveLimit:F0}s");
        }

        private void OnAllWavesCleared()
        {
            if (currentWave > 0) RecordWave();
            finished = true;
            Stop("hoàn thành 10 wave");
        }

        private void OnPlayerDeath()
        {
            Stop("Player chết (lỗi runner/ cân bằng)");
        }

        private void RecordWave()
        {
            records.Add(new WaveRecord
            {
                Wave = currentWave,
                Tier = GraphicsSettingsService.Tier,
                DwellSeconds = Time.unscaledTime - waveStartTime,
                MaxFrameMs = maxFrameMs,
                MonoMb = Profiler.GetMonoUsedSizeLong() >> 20,
                TotalMb = Profiler.GetTotalAllocatedMemoryLong() >> 20,
                Materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                Meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                Errors = errorCount - errorsAtWaveStart,
            });
        }

        private static void Collect()
        {
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        private void WriteReport(string reason)
        {
            if (currentWave > 0 && (records.Count == 0 || records[records.Count - 1].Wave != currentWave)) RecordWave();

            Collect();
            long endMonoMb = Profiler.GetMonoUsedSizeLong() >> 20;
            int endMaterials = Resources.FindObjectsOfTypeAll<Material>().Length;
            int endMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;

            var sb = new StringBuilder();
            sb.AppendLine($"[Smoke] BÁO CÁO ({reason}) - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Wave | Tier   | Giao chiến | Max frame | Mono MB | Total MB | Material | Mesh | Lỗi");
            foreach (WaveRecord r in records)
            {
                sb.AppendLine($"{r.Wave,4} | {r.Tier,-6} | {r.DwellSeconds,8:F0}s | {r.MaxFrameMs,7:F1}ms | {r.MonoMb,7} | {r.TotalMb,8} | {r.Materials,8} | {r.Meshes,4} | {r.Errors}");
            }

            long heapGrowth = endMonoMb - baseMonoMb;
            int matGrowth = endMaterials - baseMaterials;
            int meshGrowth = endMeshes - baseMeshes;
            bool leak = heapGrowth > HeapLeakThresholdMb || matGrowth > MaterialLeakThreshold;

            sb.AppendLine($"Sau GC + UnloadUnusedAssets: Mono {baseMonoMb}->{endMonoMb} MB ({heapGrowth:+#;-#;0}), Material {baseMaterials}->{endMaterials} ({matGrowth:+#;-#;0}), Mesh {baseMeshes}->{endMeshes} ({meshGrowth:+#;-#;0})");
            sb.AppendLine($"Tổng Error/Exception: {errorCount}");
            foreach (string e in errorSamples) sb.AppendLine("  " + e);
            sb.AppendLine($"Số wave đã chạy: {records.Count}/10. Rò rỉ: {(leak ? "NGHI NGỜ" : "không thấy")}. Kết quả: {(finished && errorCount == 0 && !leak ? "PASS" : "XEM LẠI")}");

            string text = sb.ToString();
            Debug.Log(text);
            try
            {
                string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "smoke_report.txt"), text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Smoke] Không ghi được file báo cáo: " + ex.Message);
            }
        }
#endif
    }
}
