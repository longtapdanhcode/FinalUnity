using UnityEngine;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Component runtime trên Player cho thẻ Meteor Fall: mỗi cú lướt gọi một thiên thạch giáng xuống điểm xuất phát.
    /// </summary>
    public class MeteorFallEffect : MonoBehaviour
    {
        private PlayerDash playerDash;
        private GameObject meteorPrefab;
        private float damage = 80f;
        private float stunDuration = 1f;
        private float explosionRadius = 4.5f;
        private float fallDelay = 0.4f;
        private float knockbackForce = 20f;
        private float knockbackDuration = 0.3f;

        private void Awake()
        {
            playerDash = GetComponent<PlayerDash>();
        }

        private void OnEnable()
        {
            if (playerDash != null) playerDash.OnDashStarted += HandleDashStarted;
        }

        private void OnDisable()
        {
            if (playerDash != null) playerDash.OnDashStarted -= HandleDashStarted;
        }

        public void Configure(GameObject prefab, float dmg, float stun, float radius, float delay,
            float knockForce, float knockDuration)
        {
            meteorPrefab = prefab;
            damage = dmg;
            stunDuration = stun;
            explosionRadius = radius;
            fallDelay = delay;
            knockbackForce = knockForce;
            knockbackDuration = knockDuration;
        }

        private void HandleDashStarted()
        {
            if (meteorPrefab == null) return;

            // Điểm bắt đầu lướt, chiếu xuống mặt đất
            Vector3 origin = transform.position;
            Vector3 groundPoint = origin;
            groundPoint.y = 0f;
            if (Physics.Raycast(origin + Vector3.up, Vector3.down, out RaycastHit hit, 10f,
                    ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast"), QueryTriggerInteraction.Ignore))
            {
                groundPoint = hit.point;
            }

            GameObject meteorObj = Vanguard.Pooling.PrefabPool.Get(meteorPrefab, groundPoint, Quaternion.identity);
            if (meteorObj.TryGetComponent(out MeteorImpact impact))
            {
                impact.Launch(groundPoint, damage, stunDuration, explosionRadius, fallDelay, knockbackForce, knockbackDuration);
            }
            else
            {
                Debug.LogWarning("[MeteorFall] meteorPrefab thiếu component MeteorImpact!", meteorObj);
                Vanguard.Pooling.PrefabPool.Release(meteorObj);
            }
        }
    }
}
