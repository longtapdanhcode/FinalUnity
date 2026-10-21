namespace Vanguard.Enemy
{
    /// <summary>
    /// Các trạng thái cơ bản của Enemy trong hệ thống FSM.
    /// </summary>
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Stunned,
        Dead,
        Slamming,
        Casting
    }
}
