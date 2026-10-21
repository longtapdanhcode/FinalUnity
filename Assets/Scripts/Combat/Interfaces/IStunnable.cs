namespace Vanguard.Combat
{
    /// <summary>
    /// Interface cho các thực thể có thể bị làm choáng (Stun) khi dính đòn kết liễu hoặc hiệu ứng khống chế.
    /// </summary>
    public interface IStunnable
    {
        /// <summary>
        /// Áp dụng trạng thái choáng lên đối tượng trong một khoảng thời gian.
        /// </summary>
        /// <param name="duration">Thời gian bị choáng (giây).</param>
        void ApplyStun(float duration);
    }
}
