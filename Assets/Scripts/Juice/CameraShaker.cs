using UnityEngine;

namespace Vanguard.Juice
{
    /// <summary>
    /// Rung camera độc lập bằng Perlin Noise trên localPosition (không phụ thuộc Cinemachine).
    /// Gắn trên Main Camera hoặc Camera Pivot. Chạy theo unscaledTime nên vẫn rung mượt khi Hit-Stop.
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        [Tooltip("Biên độ rung tối đa (mét) ứng với intensity = 1")]
        [SerializeField] private float maxAmplitude = 0.3f;

        [Tooltip("Tần số nhiễu Perlin (càng cao rung càng gắt)")]
        [SerializeField] private float frequency = 28f;

        private float intensity;
        private float duration;
        private float elapsed;
        private float seed;

        // Offset đã cộng vào localPosition ở frame trước, để gỡ ra trước khi cộng offset mới
        // (an toàn nếu script khác cũng di chuyển camera)
        private Vector3 appliedOffset;

        public bool IsShaking => elapsed < duration;

        /// <summary>
        /// Bắt đầu rung. Nếu đang rung mạnh hơn thì giữ cú rung mạnh hơn.
        /// </summary>
        public void Shake(float shakeIntensity, float shakeDuration)
        {
            if (shakeIntensity <= 0f || shakeDuration <= 0f) return;

            float remaining = IsShaking ? intensity * (1f - elapsed / duration) : 0f;
            if (shakeIntensity < remaining) return;

            intensity = shakeIntensity;
            duration = shakeDuration;
            elapsed = 0f;
            seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            Vector3 newOffset = Vector3.zero;

            if (IsShaking)
            {
                elapsed += Time.unscaledDeltaTime;

                // Giảm dần về 0 (ease-out bậc hai) để cú rung kết thúc êm
                float falloff = Mathf.Clamp01(1f - elapsed / duration);
                falloff *= falloff;

                float t = Time.unscaledTime * frequency;
                newOffset = new Vector3(
                    Mathf.PerlinNoise(seed, t) * 2f - 1f,
                    Mathf.PerlinNoise(seed + 31.7f, t) * 2f - 1f,
                    Mathf.PerlinNoise(seed + 63.4f, t) * 2f - 1f
                ) * (intensity * maxAmplitude * falloff);
            }

            if (newOffset == Vector3.zero && appliedOffset == Vector3.zero) return;

            transform.localPosition += newOffset - appliedOffset;
            appliedOffset = newOffset;
        }

        private void OnDisable()
        {
            // Trả camera về vị trí gốc nếu bị tắt giữa lúc đang rung
            if (appliedOffset != Vector3.zero)
            {
                transform.localPosition -= appliedOffset;
                appliedOffset = Vector3.zero;
            }
            elapsed = duration;
        }
    }
}
