using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Bọc Animator, phát state theo tên bằng CrossFade (controller chỉ chứa state, không có transition).
    /// Hỗ trợ "khoá" state (Attack/Hit/Dash...) trong một khoảng thời gian để locomotion không đè lên.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class StateAnimator : MonoBehaviour
    {
        private Animator animator;
        private int currentHash;
        private string lastName;
        private int lastNameHash;
        private float lockUntil;

        public Animator Animator => animator != null ? animator : (animator = GetComponent<Animator>());
        public bool IsLocked => Time.time < lockUntil;

        private void Awake()
        {
            Animator.applyRootMotion = false;
        }

        /// <summary>Phát state. lockDuration &gt; 0 sẽ chặn các lệnh Play không-force trong khoảng đó.</summary>
        public void Play(string stateName, float fade = 0.1f, float normalizedOffset = 0f, float lockDuration = 0f, bool force = false)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return;
            if (!force && IsLocked) return;

            // Tên state là hằng chuỗi (interned) nên so sánh tham chiếu đủ để bỏ qua hash lại mỗi frame
            if (!ReferenceEquals(stateName, lastName))
            {
                lastName = stateName;
                lastNameHash = Animator.StringToHash(stateName);
            }
            int hash = lastNameHash;
            if (!force && hash == currentHash && lockDuration <= 0f) return;

            currentHash = hash;
            lockUntil = lockDuration > 0f ? Time.time + lockDuration : 0f;
            Animator.CrossFadeInFixedTime(hash, fade, 0, 0f, normalizedOffset);
        }

        /// <summary>Đưa Animator về trạng thái ban đầu (dùng khi quái được lấy lại từ Pool).</summary>
        public void ResetState(string defaultState)
        {
            lockUntil = 0f;
            currentHash = 0;
            if (Animator == null || Animator.runtimeAnimatorController == null) return;
            Animator.Rebind();
            Animator.Update(0f);
            Play(defaultState, 0f, 0f, 0f, true);
        }
    }
}
