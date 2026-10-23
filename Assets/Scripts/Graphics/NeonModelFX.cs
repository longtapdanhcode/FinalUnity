using System.Collections;
using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Hiệu ứng thị giác trên model NeonLit: hit flash và dissolve khi chết (Sprint 7 - 7.4).
    /// Dùng MaterialPropertyBlock nên không tạo material instance và không phá SRP Batcher.
    /// </summary>
    public class NeonModelFX : MonoBehaviour
    {
        private static readonly int FlashId = Shader.PropertyToID("_FlashAmount");
        private static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int EdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
        private static readonly int RimId = Shader.PropertyToID("_RimColor");

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Coroutine routine;
        private Color[] edgeColors;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        /// <summary>Nháy trắng sáng trong duration giây (thời gian thực).</summary>
        public void Flash(float duration = 0.08f)
        {
            if (!isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine(duration));
        }

        /// <summary>Tan biến sau delay giây, kéo dài duration giây.</summary>
        public void Dissolve(float delay, float duration)
        {
            if (!isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(DissolveRoutine(delay, duration));
        }

        /// <summary>Xoá mọi hiệu ứng (dùng khi quái được lấy lại từ Pool).</summary>
        public void ResetFX()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (renderers == null) return;
            ClearBlocks();
        }

        private IEnumerator FlashRoutine(float duration)
        {
            SetFloat(FlashId, 1f);
            yield return new WaitForSecondsRealtime(duration);
            // Xoá block để renderer quay lại đường SRP Batcher thay vì giữ MPB vĩnh viễn
            ClearBlocks();
            routine = null;
        }

        private IEnumerator DissolveRoutine(float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            CacheEdgeColors();
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetDissolve(Mathf.Clamp01(t / duration));
                yield return null;
            }
            CacheEdgeColors();
            SetDissolve(1f);
            routine = null;
        }

        private void ClearBlocks()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].SetPropertyBlock(null);
        }

        private void CacheEdgeColors()
        {
            if (edgeColors != null && edgeColors.Length == renderers.Length) { return; }
            edgeColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material m = renderers[i] != null ? renderers[i].sharedMaterial : null;
                edgeColors[i] = m != null && m.HasProperty(RimId) ? m.GetColor(RimId) * 2f : Color.white;
            }
        }

        private void SetFloat(int id, float value)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(id, value);
                r.SetPropertyBlock(block);
            }
        }

        private void SetDissolve(float amount)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(DissolveId, amount);
                block.SetColor(EdgeColorId, edgeColors[i]);
                r.SetPropertyBlock(block);
            }
        }
    }
}
