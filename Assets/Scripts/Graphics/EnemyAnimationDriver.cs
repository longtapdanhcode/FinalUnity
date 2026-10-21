using UnityEngine;
using Vanguard.Enemy;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Điều khiển animation quái theo EnemyState / sự kiện máu / chết.
    /// Hỗ trợ Object Pool: OnEnable đưa Animator về Idle.
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyAnimationDriver : MonoBehaviour
    {
        private const string Idle = "Idle", Move = "Move", Attack = "Attack", Hit = "Hit", Death = "Death";

        [SerializeField] private StateAnimator model = null;
        [SerializeField, Min(0.1f)] private float attackLock = 0.9f;
        [SerializeField, Min(0.05f)] private float hitLock = 0.3f;

        private NeonModelFX fx;
        private EnemyBase enemy;
        private EnemyState lastState = EnemyState.Idle;
        private float lastHealth;
        private bool deathPlayed;

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>();
            if (model == null) model = GetComponentInChildren<StateAnimator>();
            if (model != null)
            {
                fx = model.GetComponent<NeonModelFX>();
                if (fx == null) fx = model.gameObject.AddComponent<NeonModelFX>();
            }
        }

        private void OnEnable()
        {
            enemy.OnEnemyHealthChanged += HandleHealthChanged;
            deathPlayed = false;
            lastState = EnemyState.Idle;
            lastHealth = enemy.CurrentHealth;
            if (model != null) model.ResetState(Idle);
            if (fx != null) fx.ResetFX();
        }

        private void OnDisable()
        {
            enemy.OnEnemyHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            bool tookDamage = current < lastHealth && current > 0f;
            lastHealth = current;
            if (tookDamage && model != null && !deathPlayed)
            {
                model.Play(Hit, 0.04f, 0f, hitLock, true);
            }
        }

        private void Update()
        {
            if (model == null) return;

            if (enemy.IsDead)
            {
                if (!deathPlayed)
                {
                    deathPlayed = true;
                    model.Play(Death, 0.08f, 0f, 0f, true);
                    // Tan biến ở nửa sau thời gian giữ xác
                    if (fx != null) fx.Dissolve(enemy.DeathLingerTime * 0.45f, enemy.DeathLingerTime * 0.5f);
                }
                return;
            }
            deathPlayed = false;

            EnemyState state = enemy.CurrentState;
            bool attacking = state == EnemyState.Attack || state == EnemyState.Slamming || state == EnemyState.Casting;
            if (attacking && lastState != state)
            {
                model.Play(Attack, 0.08f, 0f, attackLock, true);
            }
            lastState = state;

            if (state == EnemyState.Stunned) model.Play(Idle, 0.1f);
            else if (state == EnemyState.Chase) model.Play(Move, 0.12f);
            else if (state == EnemyState.Idle) model.Play(Idle, 0.12f);
        }
    }
}
