using UnityEngine;

namespace Vanguard.Combat
{
    /// <summary>
    /// Interface cốt lõi cho các đối tượng có thể nhận sát thương (Player, Quái vật, Vật thể phá hủy).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Đối tượng đã chết hoặc bị phá hủy hay chưa.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Xử lý nhận sát thương từ nguồn tấn công.
        /// </summary>
        /// <param name="damageAmount">Lượng sát thương nhận vào.</param>
        /// <param name="hitPoint">Vị trí điểm trúng đòn trong không gian thế giới.</param>
        /// <param name="hitDirection">Vector hướng của đòn tấn công tác động lên đối tượng.</param>
        void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection);
    }
}
