using UnityEngine;
using Vanguard.Enemy;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Hiệu ứng pha cho boss (Sprint 8 - 8.5): emission/rim "thở" lúc bình thường, bùng sáng khi ra chiêu
    /// (Gargoyle: nạp đòn đập; Void Warden: Cast + Phase 2). Dùng MaterialPropertyBlock, không tạo material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPhaseFX : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int RimId = Shader.PropertyToID("_RimColor");

        [SerializeField, ColorUsage(false, true)] private Color alertColor = new Color(1f, 0.3f, 0.05f, 1f);
        [SerializeField, Min(0f)] private float alertIntensity = 3f;
        [SerializeField, Min(0.1f)] private float calmPulseSpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float alertPulseSpeed = 6f;
        [SerializeField, Min(0.1f)] private float riseSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float fallSpeed = 2f;
        [Tooltip("Mức sáng duy trì khi Void Warden ở Phase 2 (0-1)")]
        [SerializeField, Range(0f, 1f)] private float phase2Level = 0.7f;

        private EnemyBase enemy;
        private VoidWardenBoss warden;
        private GargoyleBoss gargoyle;
        private SkinnedMeshRenderer[] renderers;
        private MaterialPropertyBlock block;
        private Color baseEmission = Color.black;
        private Color baseRim = Color.white;
        private float level;
        private bool wasPhase2;

        public void Configure(Color color, float intensity, float rise)
        {
            alertColor = color;
            alertIntensity = intensity;
            riseSpeed = rise;
        }

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>();
            warden = enemy as VoidWardenBoss;
            gargoyle = enemy as GargoyleBoss;
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            block = new MaterialPropertyBlock();

            if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
            {
                Material m = renderers[0].sharedMaterial;
                if (m.HasProperty(EmissionId)) baseEmission = m.GetColor(EmissionId);
                if (m.HasProperty(RimId)) baseRim = m.GetColor(RimId);
            }
        }

        private void OnEnable()
        {
            level = 0f;
            wasPhase2 = false;
        }

        private void Update()
        {
            if (enemy == null || renderers.Length == 0 || enemy.IsDead) return;

            float target = 0f;
            if (warden != null)
            {
                bool p2 = warden.IsPhase2;
                if (p2 && !wasPhase2) OnEnterPhase2();
                wasPhase2 = p2;
                target = p2 ? phase2Level : 0f;
                if (warden.IsCasting) target = 1f;
            }
            else if (gargoyle != null && gargoyle.IsSlamming)
            {
                target = 1f;
            }

            level = Mathf.MoveTowards(level, target, (target > level ? riseSpeed : fallSpeed) * Time.deltaTime);

            float t = Time.time;
            Color calm = baseEmission * (1f + 0.12f * Mathf.Sin(t * calmPulseSpeed));
            Color alert = alertColor * (alertIntensity * (1f + 0.18f * Mathf.Sin(t * alertPulseSpeed)));
            Color emission = Color.Lerp(calm, alert, level);
            Color rim = Color.Lerp(baseRim, alertColor, level);

            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer r = renderers[i];
                if (r == null) continue;
                // Giữ Get để không xoá Flash/Dissolve do NeonModelFX ghi chung trên renderer này
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, emission);
                block.SetColor(RimId, rim);
                r.SetPropertyBlock(block);
            }
        }

        private void OnEnterPhase2()
        {
            Vector3 pos = transform.position + Vector3.up * 1.5f;
            VfxManager.Play(VfxId.SoulBurst, pos, Quaternion.identity, alertColor, 2.5f);
            Vanguard.Juice.JuiceManager.Shake(1.4f, 0.7f);
        }
    }
}
