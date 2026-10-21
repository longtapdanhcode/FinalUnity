using UnityEngine;
using Vanguard.Combat;
using Vanguard.Player;

namespace Vanguard.Cards
{
    /// <summary>
    /// Thẻ nâng cấp chỉ số cơ bản cho người chơi.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "Vanguard/Cards/Stat Upgrade Card")]
    public class StatUpgradeCardSO : UpgradeCardSO
    {
        public enum StatType
        {
            MaxHP,
            MovementSpeed,
            AttackDamage,
            AttackSpeed
        }

        [Header("Stat Upgrade")]
        [SerializeField] private StatType statType = StatType.MaxHP;

        [Tooltip("MaxHP / MovementSpeed: cộng thẳng giá trị. AttackDamage / AttackSpeed: cộng theo tỉ lệ (0.2 = +20%)")]
        [SerializeField] private float value = 10f;

        public StatType Stat => statType;
        public float Value => value;

        public override void ApplyUpgrade(GameObject player)
        {
            if (player == null)
            {
                Debug.LogWarning($"[UpgradeCard] '{name}': player null, không thể áp dụng nâng cấp.", this);
                return;
            }

            switch (statType)
            {
                case StatType.MaxHP:
                    if (player.TryGetComponent(out PlayerHealth health))
                    {
                        health.IncreaseMaxHealth(value, true);
                    }
                    else LogMissing<PlayerHealth>();
                    break;

                case StatType.MovementSpeed:
                    if (player.TryGetComponent(out PlayerController controller))
                    {
                        controller.MoveSpeed += value;
                    }
                    else LogMissing<PlayerController>();
                    break;

                case StatType.AttackDamage:
                    if (player.TryGetComponent(out PlayerCombat damageCombat))
                    {
                        damageCombat.DamageMultiplier += value;
                    }
                    else LogMissing<PlayerCombat>();
                    break;

                case StatType.AttackSpeed:
                    if (player.TryGetComponent(out PlayerCombat speedCombat))
                    {
                        speedCombat.AttackSpeedMultiplier += value;
                    }
                    else LogMissing<PlayerCombat>();
                    break;
            }

            Debug.Log($"[UpgradeCard] Áp dụng '{CardName}' ({Rarity}): {statType} +{value}");
        }

        private void LogMissing<T>()
        {
            Debug.LogWarning($"[UpgradeCard] '{name}': Player thiếu component {typeof(T).Name}.", this);
        }
    }
}
