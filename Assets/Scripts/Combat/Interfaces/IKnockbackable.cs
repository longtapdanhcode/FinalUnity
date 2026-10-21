using UnityEngine;

namespace Vanguard.Combat
{
    /// <summary>
    /// Interface cốt lõi cho các đối tượng có thể bị đẩy lùi (Knockback) do đòn đánh hoặc vụ nổ.
    /// </summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// Áp dụng lực đẩy lùi lên đối tượng.
        /// </summary>
        /// <param name="forceDirection">Hướng tác dụng lực đẩy lùi.</param>
        /// <param name="forceMagnitude">Độ lớn của lực đẩy (tốc độ hoặc xung lực).</param>
        /// <param name="duration">Thời gian hiệu lực đẩy lùi kéo dài (giây).</param>
        void ApplyKnockback(Vector3 forceDirection, float forceMagnitude, float duration);
    }
}
