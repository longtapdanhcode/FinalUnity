using System.Collections.Generic;
using UnityEngine;
using Vanguard.Combat;
using Vanguard.Enemy;

namespace Vanguard.Cards
{
    /// <summary>
    /// Component runtime trên Player cho thẻ Thunder Shock: khi đòn combo thứ 3 trúng quái đầu tiên,
    /// phóng tia sét giật lan sang các quái gần nhất trong bán kính.
    /// </summary>
    public class ThunderShockEffect : MonoBehaviour
    {
        private const int BufferSize = 32;

        private PlayerCombat combat;
        private float shockDamage = 25f;
        private int maxChainTargets = 3;
        private float chainRadius = 6f;
        private GameObject lightningVFXPrefab;
        private int enemyMask;

        private int lastTriggerFrame = -1;

        // Bộ nhớ đệm (0 GC Alloc khi giật)
        private readonly Collider[] buffer = new Collider[BufferSize];
        private readonly EnemyBase[] candidates = new EnemyBase[BufferSize];
        private readonly float[] candidateSqrDist = new float[BufferSize];
        private readonly List<LightningShockVFX> vfxPool = new List<LightningShockVFX>();

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            enemyMask = LayerMask.GetMask("Enemy");
        }

        private void OnEnable()
        {
            if (combat != null) combat.OnHitLanded += HandleHitLanded;
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnHitLanded -= HandleHitLanded;
        }

        public void Configure(float damage, int chainTargets, float radius, GameObject vfxPrefab)
        {
            shockDamage = damage;
            maxChainTargets = Mathf.Max(1, chainTargets);
            chainRadius = radius;
            lightningVFXPrefab = vfxPrefab;
        }

        private void HandleHitLanded(int comboStep, Collider enemyCollider)
        {
            if (comboStep != 3 || enemyCollider == null) return;

            // Một nhát chém có thể trúng nhiều quái: chỉ giật 1 lần, từ quái đầu tiên
            if (lastTriggerFrame == Time.frameCount) return;

            EnemyBase source = enemyCollider.GetComponentInParent<EnemyBase>();
            if (source == null) return;

            lastTriggerFrame = Time.frameCount;
            Chain(source);
        }

        private void Chain(EnemyBase source)
        {
            Vector3 origin = source.transform.position;
            int hits = Physics.OverlapSphereNonAlloc(origin, chainRadius, buffer, enemyMask);

            int count = 0;
            for (int i = 0; i < hits && count < BufferSize; i++)
            {
                if (buffer[i] == null) continue;

                EnemyBase enemy = buffer[i].GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy == source || enemy.IsDead) continue;

                bool duplicate = false;
                for (int c = 0; c < count; c++)
                {
                    if (candidates[c] == enemy) { duplicate = true; break; }
                }
                if (duplicate) continue;

                candidates[count] = enemy;
                candidateSqrDist[count] = (enemy.transform.position - origin).sqrMagnitude;
                count++;
            }

            Vector3 previousPoint = origin + Vector3.up;
            int chains = Mathf.Min(maxChainTargets, count);
            for (int n = 0; n < chains; n++)
            {
                // Chọn quái gần nhất chưa bị giật
                int best = -1;
                float bestDist = float.MaxValue;
                for (int c = 0; c < count; c++)
                {
                    if (candidateSqrDist[c] < bestDist)
                    {
                        bestDist = candidateSqrDist[c];
                        best = c;
                    }
                }
                if (best < 0) break;

                EnemyBase target = candidates[best];
                candidateSqrDist[best] = float.MaxValue;

                Vector3 targetPoint = target.transform.position + Vector3.up;
                Vector3 hitDir = targetPoint - previousPoint;
                hitDir.y = 0f;
                target.TakeDamage(shockDamage, targetPoint, hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : transform.forward);

                PlayBolt(previousPoint, targetPoint);
                previousPoint = targetPoint;
            }

            for (int c = 0; c < count; c++) candidates[c] = null;

            if (chains > 0)
            {
                Debug.Log($"[UpgradeCard] Thunder Shock giật lan {chains} mục tiêu, mỗi mục tiêu {shockDamage} DMG!");
            }
        }

        private void PlayBolt(Vector3 from, Vector3 to)
        {
            if (lightningVFXPrefab == null) return;

            LightningShockVFX bolt = null;
            for (int i = 0; i < vfxPool.Count; i++)
            {
                if (vfxPool[i] != null && !vfxPool[i].gameObject.activeSelf)
                {
                    bolt = vfxPool[i];
                    break;
                }
            }

            if (bolt == null)
            {
                GameObject obj = Instantiate(lightningVFXPrefab);
                bolt = obj.GetComponent<LightningShockVFX>();
                if (bolt == null)
                {
                    Destroy(obj);
                    return;
                }
                vfxPool.Add(bolt);
            }

            bolt.Play(from, to);
        }
    }
}
