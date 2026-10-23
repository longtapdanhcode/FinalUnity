using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Làm "tà ảnh" Decoy: tô sáng neon rồi tan biến dần bằng dissolve của NeonLit trong suốt vòng đời.
    /// Dùng MaterialPropertyBlock (không new Material) nên không phát sinh material instance cần dọn và không rò bộ nhớ.
    /// </summary>
    public class DecoyGhostFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int RimId = Shader.PropertyToID("_RimColor");
        private static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float lifetime;
        private float age;
        private float fadeStart;

        /// <param name="duration">Tổng thời gian sống của Decoy.</param>
        /// <param name="fadeStartNormalized">Thời điểm (0-1) bắt đầu tan biến.</param>
        public void Initialize(float duration, Color ghostColor, float fadeStartNormalized = 0.5f)
        {
            lifetime = Mathf.Max(0.1f, duration);
            fadeStart = Mathf.Clamp01(fadeStartNormalized);
            age = 0f;

            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, ghostColor * 0.15f);
            block.SetColor(EmissionId, ghostColor * 2f);
            block.SetColor(RimId, ghostColor * 4f);
            block.SetFloat(DissolveId, 0f);
            Apply();
        }

        private void Update()
        {
            if (renderers == null) return;
            age += Time.deltaTime;
            float t = Mathf.InverseLerp(fadeStart * lifetime, lifetime, age);
            block.SetFloat(DissolveId, t);
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
