using System.Collections;
using UnityEngine;
using Vanguard.Combat;
using Vanguard.FX;
using Vanguard.Juice;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thiên thạch rơi từ trên cao xuống điểm chỉ định: hiện vòng cảnh báo, lao xuống sau fallDelay,
    /// nổ diện rộng (sát thương + choáng + đẩy văng mục tiêu thuộc hitMask) rồi tự dọn dẹp.
    /// Mặc định quét Layer Enemy (thẻ Meteor Fall của Player); Boss truyền Layer Player để dùng lại cùng prefab.
    /// </summary>
    public class MeteorImpact : MonoBehaviour
    {
        private const int BufferSize = 64;
        private const float SpawnHeight = 8f;
        private const float ExplosionVisualDuration = 0.35f;

        [Header("Visuals (gán tự động bởi LegendaryCardsSetupTool)")]
        [SerializeField] private Transform meteorVisual = null;
        [Tooltip("Vòng cảnh báo tĩnh cũ: không còn dùng, thay bằng TelegraphWarning. Tự ẩn nếu prefab cũ còn giữ")]
        [SerializeField] private Transform warningRing = null;
        [SerializeField] private Transform explosionVFX = null;

        private void Awake()
        {
            if (warningRing != null) warningRing.gameObject.SetActive(false);
        }

        private readonly Collider[] buffer = new Collider[BufferSize];
        private readonly IDamageable[] processed = new IDamageable[BufferSize];

        /// <summary>
        /// Bắt đầu đòn thiên thạch tại groundPoint. hitMask &lt; 0 nghĩa là mặc định Layer Enemy.
        /// </summary>
        public void Launch(Vector3 groundPoint, float damage, float stunDuration, float radius,
            float fallDelay, float knockbackForce, float knockbackDuration, int hitMask = -1)
        {
            transform.position = groundPoint;
            int mask = hitMask >= 0 ? hitMask : LayerMask.GetMask("Enemy");
            StartCoroutine(FallRoutine(groundPoint, damage, stunDuration, radius, fallDelay, knockbackForce, knockbackDuration, mask));
        }

        private IEnumerator FallRoutine(Vector3 groundPoint, float damage, float stunDuration, float radius,
            float fallDelay, float knockbackForce, float knockbackDuration, int mask)
        {
            Vector3 start = groundPoint + Vector3.up * SpawnHeight;

            // Vòng cảnh báo nạp đầy đúng bằng thời gian rơi
            TelegraphHandle warning = TelegraphManager.ShowCircle(groundPoint, radius, fallDelay, danger: Vanguard.FX.TelegraphDanger.Friendly);
            if (explosionVFX != null) explosionVFX.gameObject.SetActive(false);
            if (meteorVisual != null)
            {
                meteorVisual.gameObject.SetActive(true);
                meteorVisual.position = start;
            }

            float duration = Mathf.Max(0.05f, fallDelay);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Tăng tốc dần như rơi tự do
                if (meteorVisual != null)
                {
                    meteorVisual.position = Vector3.Lerp(start, groundPoint, t * t);
                }
                yield return null;
            }

            if (meteorVisual != null) meteorVisual.gameObject.SetActive(false);
            warning.Cancel();

            Explode(groundPoint, damage, stunDuration, radius, knockbackForce, knockbackDuration, mask);

            // Hiệu ứng nổ nở ra rồi mờ dần
            if (explosionVFX != null)
            {
                explosionVFX.gameObject.SetActive(true);
                explosionVFX.position = groundPoint + Vector3.up * 0.5f;

                float vfxElapsed = 0f;
                while (vfxElapsed < ExplosionVisualDuration)
                {
                    vfxElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(vfxElapsed / ExplosionVisualDuration);
                    float size = Mathf.Lerp(0.5f, radius * 2f, Mathf.Sin(t * Mathf.PI * 0.5f));
                    explosionVFX.localScale = new Vector3(size, size, size);
                    yield return null;
                }
            }

            Vanguard.Pooling.PrefabPool.Release(gameObject);
        }

        private void Explode(Vector3 center, float damage, float stunDuration, float radius,
            float knockbackForce, float knockbackDuration, int mask)
        {
            int hits = Physics.OverlapSphereNonAlloc(center, radius, buffer, mask);
            int processedCount = 0;

            for (int i = 0; i < hits; i++)
            {
                if (buffer[i] == null) continue;

                IDamageable damageable = buffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead) continue;

                // Tránh xử lý trùng khi mục tiêu có nhiều collider
                bool duplicate = false;
                for (int p = 0; p < processedCount; p++)
                {
                    if (processed[p] == damageable) { duplicate = true; break; }
                }
                if (duplicate) continue;
                processed[processedCount++] = damageable;

                Vector3 targetPosition = buffer[i].transform.position;
                Vector3 dir = targetPosition - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

                // Đẩy văng và choáng trước, sát thương sau
                if (knockbackForce > 0f)
                {
                    IKnockbackable knockback = buffer[i].GetComponentInParent<IKnockbackable>();
                    if (knockback != null) knockback.ApplyKnockback(dir, knockbackForce, knockbackDuration);
                }
                if (stunDuration > 0f)
                {
                    IStunnable stunnable = buffer[i].GetComponentInParent<IStunnable>();
                    if (stunnable != null) stunnable.ApplyStun(stunDuration);
                }
                damageable.TakeDamage(damage, targetPosition, dir);
            }

            for (int p = 0; p < processedCount; p++) processed[p] = null;

            // Chấn động mặt đất khi thiên thạch chạm đất
            JuiceManager.Shake(0.6f, 0.3f);

            Debug.Log($"[Meteor] Thiên thạch giáng xuống! Trúng {processedCount} mục tiêu ({damage} DMG, bán kính {radius}m).");
        }
    }
}
