using UnityEngine;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Component runtime trên Player cho thẻ Decoy Dash: để lại một Decoy tại điểm bắt đầu mỗi cú lướt.
    /// </summary>
    public class DecoyDashEffect : MonoBehaviour
    {
        private PlayerDash playerDash;
        private GameObject decoyPrefab;
        private float decoyDuration = 1.5f;
        private float tauntRadius = 10f;

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

        public void Configure(GameObject prefab, float duration, float radius)
        {
            decoyPrefab = prefab;
            decoyDuration = duration;
            tauntRadius = radius;
        }

        private void HandleDashStarted()
        {
            if (decoyPrefab == null) return;

            GameObject decoy = Instantiate(decoyPrefab, transform.position, transform.rotation);
            if (decoy.TryGetComponent(out DecoyLifetime lifetime))
            {
                lifetime.Initialize(decoyDuration, tauntRadius);
            }
            else
            {
                Destroy(decoy, decoyDuration);
            }
        }
    }
}
