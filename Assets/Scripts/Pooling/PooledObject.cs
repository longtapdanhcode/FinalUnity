using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.Pooling
{
    /// <summary>
    /// Gắn tự động vào mọi instance do <see cref="PrefabPool"/> tạo. Lưu prefab gốc để trả về đúng pool,
    /// hẹn giờ tự trả, và áp ngân sách hạt (VFX budget) theo quality tier cho các ParticleSystem bên trong.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        internal GameObject Prefab;
        internal bool InPool;

        private ParticleSystem[] systems;
        private int[] baseMaxParticles;
        private float[] baseRate;
        private float releaseAt = -1f;

        internal void Initialize(GameObject prefab)
        {
            Prefab = prefab;
            systems = GetComponentsInChildren<ParticleSystem>(true);
            baseMaxParticles = new int[systems.Length];
            baseRate = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                // Pool tự quản vòng đời: không để particle tự Destroy/Disable object
                main.stopAction = ParticleSystemStopAction.None;
                baseMaxParticles[i] = main.maxParticles;
                baseRate[i] = systems[i].emission.rateOverTimeMultiplier;
            }
        }

        /// <summary>Gọi mỗi lần lấy ra từ pool: reset hạt, áp budget, hẹn giờ trả.</summary>
        internal void OnSpawn(float autoReleaseAfter)
        {
            InPool = false;
            float budget = GraphicsSettingsService.VfxBudget;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                ParticleSystem.MainModule main = ps.main;
                main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(baseMaxParticles[i] * budget));
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier = baseRate[i] * budget;
                ps.Clear(true);
                ps.Play(true);
            }

            if (autoReleaseAfter <= 0f) autoReleaseAfter = EstimateLifetime();
            releaseAt = autoReleaseAfter > 0f ? Time.time + autoReleaseAfter : -1f;
            enabled = releaseAt >= 0f;
        }

        internal void OnReturn()
        {
            releaseAt = -1f;
            enabled = false;
        }

        private float EstimateLifetime()
        {
            float max = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule m = systems[i].main;
                float t = m.duration + m.startLifetime.constantMax;
                if (t > max) max = t;
            }
            return max;
        }

        private void Update()
        {
            if (releaseAt >= 0f && Time.time >= releaseAt) PrefabPool.Release(gameObject);
        }
    }
}
