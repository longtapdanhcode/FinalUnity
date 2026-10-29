using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Benchmark 3 quality tier (Sprint 9 - 9.5), chỉ chạy trong Editor / Development Build.
    /// F5: lần lượt đặt Low -> Medium -> High, mỗi tier khởi động 2 giây rồi đo 8 giây, in một dòng [Bench] cho mỗi tier
    /// và kết luận PASS/FAIL cho mục tiêu 60 FPS (1% low) ở tier Medium. Xong tự khôi phục tier cũ.
    /// Nên chạy lúc đang ở wave 9-10 (đông quái / boss).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QualityBenchmark : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        private const float WarmupSeconds = 2f;
        private const float MeasureSeconds = 8f;
        private const float TargetFps = 60f;
        private const int MaxFrames = 16384;

        private readonly float[] frames = new float[MaxFrames];
        private Coroutine running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<QualityBenchmark>() != null) return;
            var go = new GameObject("QualityBenchmark") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<QualityBenchmark>();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f5Key.wasPressedThisFrame && running == null)
            {
                running = StartCoroutine(Run());
            }
        }

        private IEnumerator Run()
        {
            GraphicsTier original = GraphicsSettingsService.Tier;
            float mediumLow1Pct = 0f;
            Debug.Log("[Bench] Bắt đầu: 3 tier x (2s khởi động + 8s đo). Giữ nguyên cảnh, đừng mở menu.");

            for (int t = 0; t < 3; t++)
            {
                var tier = (GraphicsTier)t;
                GraphicsSettingsService.SetTier(tier);
                yield return new WaitForSecondsRealtime(WarmupSeconds);

                int n = 0;
                float elapsed = 0f;
                while (elapsed < MeasureSeconds && n < MaxFrames)
                {
                    float dt = Time.unscaledDeltaTime;
                    frames[n++] = dt;
                    elapsed += dt;
                    yield return null;
                }

                Summarize(tier, n, out float avgFps, out float lowFps);
                if (tier == GraphicsTier.Medium) mediumLow1Pct = lowFps;
            }

            GraphicsSettingsService.SetTier(original);
            string verdict = mediumLow1Pct >= TargetFps ? "PASS" : "FAIL";
            Debug.Log($"[Bench] Kết luận: Medium 1% low = {mediumLow1Pct:F0} FPS (mục tiêu {TargetFps:F0}) -> {verdict}. Đã khôi phục tier {original}.");
            running = null;
        }

        private void Summarize(GraphicsTier tier, int n, out float avgFps, out float lowFps)
        {
            System.Array.Sort(frames, 0, n);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += frames[i];
            float avgMs = sum / Mathf.Max(1, n) * 1000f;
            float p99Ms = frames[Mathf.Min(n - 1, Mathf.CeilToInt(n * 0.99f) - 1)] * 1000f;
            avgFps = 1000f / Mathf.Max(0.01f, avgMs);
            lowFps = 1000f / Mathf.Max(0.01f, p99Ms);

            Debug.Log($"[Bench] {tier,-6} | {n} frame | avg {avgMs:F2} ms ({avgFps:F0} FPS) | p99 {p99Ms:F2} ms ({lowFps:F0} FPS 1% low) | max {frames[n - 1] * 1000f:F1} ms | {Screen.width}x{Screen.height}");
        }
#endif
    }
}
