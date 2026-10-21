using UnityEngine;
using Vanguard.Enemy;
using Vanguard.Player;
using Vanguard.Spawning;

namespace Vanguard.Cards
{
    /// <summary>
    /// Component runtime trên Player cho thẻ Blood God's Vortex: hồi một phần Max HP mỗi khi có quái bị hạ gục.
    /// Các lần nhận thẻ sau sẽ cộng dồn tỉ lệ hồi máu.
    /// </summary>
    public class BloodGodVortexEffect : MonoBehaviour
    {
        private PlayerHealth health;
        private EnemySpawner spawner;
        private float healPercentOnKill;
        private bool subscribed;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        private void Start()
        {
            TrySubscribe();
            if (!subscribed)
            {
                Debug.LogWarning("[BloodGodVortex] Không tìm thấy EnemySpawner trong Scene, thẻ sẽ không hồi máu khi hạ gục quái!", this);
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribed && spawner != null)
            {
                spawner.OnEnemyKilled -= HandleEnemyKilled;
            }
            subscribed = false;
        }

        /// <summary>
        /// Cộng dồn tỉ lệ hồi máu mỗi lần hạ gục (0.04 = 4% Max HP).
        /// </summary>
        public void AddHealPercent(float percent)
        {
            healPercentOnKill += percent;
        }

        private void TrySubscribe()
        {
            if (subscribed) return;

            if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null) return;

            spawner.OnEnemyKilled += HandleEnemyKilled;
            subscribed = true;
        }

        private void HandleEnemyKilled(EnemyBase enemy, EnemyType type)
        {
            if (health == null || health.IsDead || healPercentOnKill <= 0f) return;

            float amount = health.MaxHealth * healPercentOnKill;
            health.Heal(amount);
            Debug.Log($"[BloodGodVortex] Hấp thu huyết tế: Hồi {amount} HP!");
        }
    }
}
