using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Làm "tà ảnh" Decoy: tô sáng neon rồi tan biến dần bằng dissolve của NeonLit trong suốt vòng đời.
    /// </summary>
    public class DecoyGhostFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int RimId = Shader.PropertyToID("_RimColor");
        private static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");

        private Material[] materials;
        private float lifetime;
        private float age;
        private float fadeStart;

        /// <param name="duration">Tổng thời gian sống của Decoy.</param>
        /// <param name="fadeStartNormalized">Thời điểm (0-1) bắt đầu tan biến.</param>
        public void Initialize(float duration, Color ghostColor, float fadeStartNormalized = 0.5f)
        {
            lifetime = Mathf.Max(0.1f, duration);
            fadeStart = Mathf.Clamp01(fadeStartNormalized);

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Material>();
            foreach (Renderer r in renderers)
            {
                Material[] shared = r.sharedMaterials;
                Material[] instances = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    Material m = new Material(shared[i]);
                    if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, ghostColor * 0.15f);
                    if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, ghostColor * 2f);
                    if (m.HasProperty(RimId)) m.SetColor(RimId, ghostColor * 4f);
                    instances[i] = m;
                    list.Add(m);
                }
                r.sharedMaterials = instances;
            }
            materials = list.ToArray();
        }

        private void Update()
        {
            if (materials == null) return;
            age += Time.deltaTime;
            float t = Mathf.InverseLerp(fadeStart * lifetime, lifetime, age);
            foreach (Material m in materials)
            {
                if (m != null && m.HasProperty(DissolveId)) m.SetFloat(DissolveId, t);
            }
        }

        private void OnDestroy()
        {
            if (materials == null) return;
            foreach (Material m in materials) if (m != null) Destroy(m);
        }
    }
}
