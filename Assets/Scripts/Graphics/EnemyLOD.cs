using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// LOD theo khoảng cách tới camera cho quái (Sprint 9 - 9.4). Mô hình Mixamo chỉ có 1 mức mesh nên LOD ở đây
    /// giảm chi phí render thay vì đổi mesh: tắt bóng đổ và giảm số bone skinning khi ở xa.
    /// Ngưỡng nhân với LodDistanceScale của quality tier (tier thấp hạ LOD sớm hơn). Kiểm tra theo chu kỳ, lệch pha giữa các quái.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyLOD : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float nearDistance = 25f;
        [SerializeField, Min(1f)] private float farDistance = 45f;
        [SerializeField, Min(0.05f)] private float checkInterval = 0.3f;

        private SkinnedMeshRenderer[] renderers;
        private Camera cam;
        private float nextCheck;
        private int level = -1;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private void OnEnable()
        {
            level = -1;
            nextCheck = Time.unscaledTime + Random.value * checkInterval; // lệch pha giữa các quái
            GraphicsSettingsService.Changed += ForceRefresh;
        }

        private void OnDisable()
        {
            GraphicsSettingsService.Changed -= ForceRefresh;
        }

        private void ForceRefresh() { level = -1; nextCheck = 0f; }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float scale = GraphicsSettingsService.LodDistanceScale;
            float near = nearDistance * scale;
            float far = farDistance * scale;
            float d2 = (transform.position - cam.transform.position).sqrMagnitude;

            int wanted = d2 < near * near ? 0 : (d2 < far * far ? 1 : 2);
            if (wanted == level) return;
            level = wanted;
            Apply(wanted);
        }

        private void Apply(int lod)
        {
            UnityEngine.Rendering.ShadowCastingMode shadows = lod == 0
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            SkinQuality quality = lod == 0 ? SkinQuality.Auto : (lod == 1 ? SkinQuality.Bone2 : SkinQuality.Bone1);

            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer r = renderers[i];
                if (r == null) continue;
                r.shadowCastingMode = shadows;
                r.quality = quality;
            }
        }
    }
}
