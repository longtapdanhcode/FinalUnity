using UnityEngine;
using Vanguard.Enemy;

namespace Vanguard.Cards
{
    /// <summary>
    /// Ảo ảnh Decoy: thu hút quái xung quanh nhắm vào nó trong suốt thời gian tồn tại, rồi tự hủy.
    /// </summary>
    public class DecoyLifetime : MonoBehaviour
    {
        private const int BufferSize = 64;
        private const float RescanInterval = 0.25f;

        [SerializeField] private float lifetime = 1.5f;
        [SerializeField] private float tauntRadius = 10f;

        private readonly Collider[] buffer = new Collider[BufferSize];
        private int enemyMask;
        private float age;
        private float rescanTimer;

        private void Awake()
        {
            enemyMask = LayerMask.GetMask("Enemy");
        }

        public void Initialize(float duration, float radius)
        {
            lifetime = duration;
            tauntRadius = radius;
        }

        private void Start()
        {
            Taunt();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Quét lại định kỳ để thu hút cả quái mới spawn trong lúc Decoy còn tồn tại
            rescanTimer += Time.deltaTime;
            if (rescanTimer >= RescanInterval)
            {
                rescanTimer = 0f;
                Taunt();
            }
        }

        private void Taunt()
        {
            float remaining = lifetime - age;
            if (remaining <= 0f) return;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, tauntRadius, buffer, enemyMask);
            for (int i = 0; i < hits; i++)
            {
                if (buffer[i] == null) continue;

                EnemyBase enemy = buffer[i].GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.TauntTo(transform, remaining);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, tauntRadius);
        }
#endif
    }
}
