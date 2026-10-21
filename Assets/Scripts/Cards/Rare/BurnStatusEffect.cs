using UnityEngine;
using Vanguard.Enemy;

namespace Vanguard.Cards
{
    /// <summary>
    /// Hiệu ứng thiêu đốt cộng dồn trên quái (Hellfire Slash). Mỗi giây gây (sát thương/giây × số tầng).
    /// Được gắn một lần lên quái và tái sử dụng cùng Object Pool; tự reset khi quái bị thu hồi (OnDisable).
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public class BurnStatusEffect : MonoBehaviour
    {
        private const float TickInterval = 1f;

        private EnemyBase enemy;
        private float damagePerSecond;
        private float remaining;
        private float tickTimer;
        private int stacks;

        public int Stacks => stacks;
        public bool IsBurning => stacks > 0;

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>();
        }

        /// <summary>
        /// Thêm một tầng thiêu đốt (tối đa maxStacks) và làm mới thời gian.
        /// </summary>
        public void AddStack(float dps, float duration, int maxStacks)
        {
            if (enemy == null || enemy.IsDead) return;

            if (stacks == 0) tickTimer = 0f;

            damagePerSecond = dps;
            remaining = duration;
            stacks = Mathf.Min(maxStacks, stacks + 1);
        }

        private void Update()
        {
            if (stacks == 0) return;

            if (enemy.IsDead)
            {
                Clear();
                return;
            }

            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;

            if (tickTimer >= TickInterval - 0.001f)
            {
                tickTimer -= TickInterval;
                enemy.TakeDamage(damagePerSecond * stacks, transform.position, Vector3.zero);
            }

            if (remaining <= 0.001f)
            {
                Clear();
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Clear()
        {
            stacks = 0;
            remaining = 0f;
            tickTimer = 0f;
        }
    }
}
