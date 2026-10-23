using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Overlay đo hiệu năng (Sprint 9 - 9.3) chỉ chạy trong Editor / Development Build.
    /// F3: bật/tắt overlay. F4: in một dòng tổng kết (min/avg/max/1% low) ra Console để dán vào báo cáo.
    /// Đọc số liệu bằng ProfilerRecorder (không cấp phát), chuỗi hiển thị chỉ dựng lại mỗi 0.5 giây.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerfStatsOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float RefreshInterval = 0.5f;
        private const int FrameHistory = 600;

        private ProfilerRecorder drawCalls;
        private ProfilerRecorder batches;
        private ProfilerRecorder setPass;
        private ProfilerRecorder triangles;
        private ProfilerRecorder gcAlloc;
        private ProfilerRecorder gcAllocBytes;

        private readonly float[] frameMs = new float[FrameHistory];
        private readonly float[] sortBuffer = new float[FrameHistory];
        private int frameCount;
        private int writeIndex;
        private float timer;
        private bool visible = true;
        private readonly StringBuilder sb = new StringBuilder(256);
        private string text = string.Empty;
        private GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PerfStatsOverlay>() != null) return;
            var go = new GameObject("PerfStatsOverlay") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<PerfStatsOverlay>();
        }

        private void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            gcAllocBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void OnDisable()
        {
            drawCalls.Dispose();
            batches.Dispose();
            setPass.Dispose();
            triangles.Dispose();
            gcAlloc.Dispose();
            gcAllocBytes.Dispose();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f3Key.wasPressedThisFrame) visible = !visible;
                if (kb.f4Key.wasPressedThisFrame) LogSummary();
            }

            float dt = Time.unscaledDeltaTime;
            frameMs[writeIndex] = dt * 1000f;
            writeIndex = (writeIndex + 1) % FrameHistory;
            if (frameCount < FrameHistory) frameCount++;

            timer += dt;
            if (timer < RefreshInterval || !visible) return;
            timer = 0f;
            Rebuild();
        }

        private void Rebuild()
        {
            float avg = 0f, max = 0f;
            for (int i = 0; i < frameCount; i++)
            {
                avg += frameMs[i];
                if (frameMs[i] > max) max = frameMs[i];
            }
            avg /= Mathf.Max(1, frameCount);

            sb.Clear();
            sb.Append("FPS ").Append((1000f / Mathf.Max(0.01f, avg)).ToString("F0"))
              .Append("  avg ").Append(avg.ToString("F1")).Append(" ms  max ").Append(max.ToString("F1")).Append(" ms\n");
            sb.Append("Draw ").Append(drawCalls.LastValue)
              .Append("  Batches ").Append(batches.LastValue)
              .Append("  SetPass ").Append(setPass.LastValue)
              .Append("  Tris ").Append((triangles.LastValue / 1000).ToString()).Append("k\n");
            sb.Append("GC alloc/frame ").Append(gcAlloc.LastValue).Append(" (").Append(gcAllocBytes.LastValue).Append(" B)");
            text = sb.ToString();
        }

        private void LogSummary()
        {
            if (frameCount == 0) return;
            System.Array.Copy(frameMs, sortBuffer, frameCount);
            System.Array.Sort(sortBuffer, 0, frameCount);

            float sum = 0f;
            for (int i = 0; i < frameCount; i++) sum += sortBuffer[i];
            float avg = sum / frameCount;
            float p99 = sortBuffer[Mathf.Min(frameCount - 1, Mathf.CeilToInt(frameCount * 0.99f) - 1)];

            Debug.Log($"[Perf] {frameCount} frame | avg {avg:F2} ms ({1000f / avg:F0} FPS) | min {sortBuffer[0]:F2} | p99 {p99:F2} ms ({1000f / p99:F0} FPS 1% low) | max {sortBuffer[frameCount - 1]:F2} " +
                      $"| Draw {drawCalls.LastValue} | Batches {batches.LastValue} | SetPass {setPass.LastValue} | Tris {triangles.LastValue} | GC {gcAlloc.LastValue} alloc/{gcAllocBytes.LastValue} B " +
                      $"| Tier {GraphicsSettingsService.Tier} | {Screen.width}x{Screen.height}");
        }

        private void OnGUI()
        {
            if (!visible || string.IsNullOrEmpty(text)) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };
                style.normal.textColor = new Color(0.4f, 1f, 0.9f, 1f);
            }
            GUI.Label(new Rect(10f, 10f, 520f, 70f), text, style);
        }
#endif
    }
}
