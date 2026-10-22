using UnityEngine;
using Vanguard.Environment;
using Vanguard.Juice;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Hiệu ứng thị giác cho CenterRotatingTrap: tia lửa liên tục ở đầu lưỡi (ParticleSystem con)
    /// và chùm tia lửa + rung nhẹ khi bẫy trúng mục tiêu. Không đụng logic va chạm.
    /// </summary>
    [RequireComponent(typeof(CenterRotatingTrap))]
    public class TrapVisualFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem hitSparks = null;
        [SerializeField, Min(1)] private int hitBurstCount = 24;
        [SerializeField, Range(0f, 1f)] private float hitShake = 0.35f;

        private CenterRotatingTrap trap;

        private void Awake()
        {
            trap = GetComponent<CenterRotatingTrap>();
        }

        private void OnEnable()
        {
            if (trap != null) trap.OnTargetHit += HandleHit;
        }

        private void OnDisable()
        {
            if (trap != null) trap.OnTargetHit -= HandleHit;
        }

        private void HandleHit(Vector3 position)
        {
            if (hitSparks != null)
            {
                hitSparks.transform.position = position + Vector3.up * 0.6f;
                hitSparks.Emit(hitBurstCount);
            }
            JuiceManager.Shake(hitShake, 0.12f);
        }
    }
}
