using UnityEngine;

namespace Vanguard.Juice
{
    /// <summary>
    /// Pivot bám theo mục tiêu (Player) mượt, chỉ di chuyển trên mặt phẳng XZ.
    /// Camera là con của pivot nên CameraShaker (rung trên localPosition) vẫn hoạt động bình thường.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target = null;
        [Tooltip("Độ nhanh bám theo (càng lớn càng bám sát)")]
        [SerializeField, Min(0.1f)] private float followSharpness = 8f;
        [Tooltip("Dịch pivot khỏi mục tiêu (XZ), ví dụ nhìn lệch về phía trước")]
        [SerializeField] private Vector2 offsetXZ = Vector2.zero;

        [Header("Giới hạn vùng camera (tuỳ chọn)")]
        [SerializeField] private bool clampToBounds = false;
        [SerializeField] private Vector2 minXZ = new Vector2(-10f, -10f);
        [SerializeField] private Vector2 maxXZ = new Vector2(10f, 10f);

        private void Start()
        {
            if (target != null) SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 goal = GetGoal();
            // Làm mượt kiểu mũ, độc lập frame rate và an toàn khi timeScale = 0
            float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, goal, k);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null) SnapToTarget();
        }

        public void SnapToTarget()
        {
            transform.position = GetGoal();
        }

        private Vector3 GetGoal()
        {
            Vector3 p = target.position;
            float x = p.x + offsetXZ.x;
            float z = p.z + offsetXZ.y;
            if (clampToBounds)
            {
                x = Mathf.Clamp(x, minXZ.x, maxXZ.x);
                z = Mathf.Clamp(z, minXZ.y, maxXZ.y);
            }
            return new Vector3(x, transform.position.y, z);
        }
    }
}
