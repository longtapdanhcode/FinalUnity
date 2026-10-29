using UnityEngine;

namespace Vanguard.Combat
{
    /// <summary>
    /// Quản lý viên đạn ma thuật Hư Vô được bắn từ Void Caster (Quái phù thủy tầm xa).
    /// Bay thẳng theo hướng định sẵn, gây sát thương lên người chơi và tự hủy khi va chạm hoặc hết thời gian sống.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VoidProjectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [Tooltip("Tốc độ bay của viên đạn (m/s)")]
        [SerializeField] private float speed = 10f;

        [Tooltip("Lượng sát thương gây ra khi trúng mục tiêu")]
        [SerializeField] private float damage = 15f;

        [Tooltip("Thời gian sống tối đa trước khi tự hủy (giây)")]
        [SerializeField] private float lifeTime = 5f;

        [Tooltip("LayerMask các đối tượng nhận va chạm (Player, Default/Obstacle)")]
        [SerializeField] private LayerMask hitLayers;

        private float timer;
        private bool hasHit;

        public float Speed => speed;
        public float Damage => damage;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // Đảm bảo có Rigidbody để nhận OnTriggerEnter với cả Static Colliders
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            // Tự động cấu hình hitLayers nếu chưa được thiết lập trong Inspector
            if (hitLayers.value == 0)
            {
                hitLayers = LayerMask.GetMask("Player", "Default");
                if (hitLayers.value == 0)
                {
                    // Fallback loại trừ Layer Enemy
                    hitLayers = ~LayerMask.GetMask("Enemy");
                }
            }
        }

        private void OnEnable()
        {
            // Đạn được lấy lại từ pool: reset trạng thái
            timer = 0f;
            hasHit = false;
        }

        private void Update()
        {
            // Bay thẳng theo trục transform.forward với vận tốc speed
            transform.position += transform.forward * (speed * Time.deltaTime);

            // Tự động Destroy sau lifeTime nếu không chạm mục tiêu
            timer += Time.deltaTime;
            if (timer >= lifeTime)
            {
                Vanguard.Pooling.PrefabPool.Release(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            // Bỏ qua Layer "Enemy" (đạn xuyên qua đồng đội quái)
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (other.gameObject.layer == enemyLayer)
            {
                return;
            }

            if (other.GetComponentInParent<Enemy.EnemyBase>() != null)
            {
                return;
            }

            // Nếu trúng mục tiêu mang IDamageable (Player)
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                hasHit = true;
                damageable.TakeDamage(damage, transform.position, transform.forward);
                Debug.Log($"[VoidProjectile] Đạn trúng {other.name}! Gây {damage} DMG.");
                Vanguard.Pooling.PrefabPool.Release(gameObject);
                return;
            }

            // Nếu trúng tường/chướng ngại vật (Layer Default hoặc khớp hitLayers)
            int defaultLayer = LayerMask.NameToLayer("Default");
            if (other.gameObject.layer == defaultLayer || (hitLayers.value != 0 && (hitLayers.value & (1 << other.gameObject.layer)) != 0))
            {
                hasHit = true;
                Vanguard.Pooling.PrefabPool.Release(gameObject);
            }
        }

        /// <summary>
        /// Khởi tạo chỉ số sát thương và vận tốc tùy chỉnh từ bên ngoài.
        /// </summary>
        public void Initialize(float damageAmount, float projectileSpeed)
        {
            damage = damageAmount;
            speed = projectileSpeed;
        }
    }
}
