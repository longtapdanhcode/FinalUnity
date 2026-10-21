using UnityEngine;
using Vanguard.Graphics;
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
            ApplyGhostVisual(decoy);

            if (decoy.TryGetComponent(out DecoyLifetime lifetime))
            {
                lifetime.Initialize(decoyDuration, tauntRadius);
            }
            else
            {
                Destroy(decoy, decoyDuration);
            }
        }

        /// <summary>Thay capsule placeholder bằng bản sao đóng băng của model Player, tô neon và tan dần.</summary>
        private void ApplyGhostVisual(GameObject decoy)
        {
            StateAnimator source = GetComponentInChildren<StateAnimator>();
            if (source == null) return; // không có model -> giữ capsule cũ

            foreach (MeshRenderer placeholder in decoy.GetComponentsInChildren<MeshRenderer>())
            {
                placeholder.enabled = false;
            }

            GameObject ghost = Instantiate(source.gameObject, decoy.transform);
            ghost.name = "GhostModel";

            // Giữ nguyên tư thế hiện tại: bỏ Animator để không còn chạy animation
            StateAnimator copyState = ghost.GetComponent<StateAnimator>();
            if (copyState != null) Destroy(copyState);
            Animator copyAnimator = ghost.GetComponent<Animator>();
            if (copyAnimator != null) Destroy(copyAnimator);

            DecoyGhostFade fade = decoy.AddComponent<DecoyGhostFade>();
            fade.Initialize(decoyDuration, new Color(0f, 0.8f, 1.2f, 1f));
        }
    }
}
