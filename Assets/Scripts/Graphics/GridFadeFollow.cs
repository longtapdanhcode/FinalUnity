using UnityEngine;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Đưa tâm vùng sáng (fade) của NeonGrid theo mục tiêu để lưới luôn rõ quanh Player
    /// thay vì cố định ở gốc toạ độ. Dùng MaterialPropertyBlock nên không sửa asset material.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class GridFadeFollow : MonoBehaviour
    {
        private static readonly int FadeCenterId = Shader.PropertyToID("_FadeCenter");

        [SerializeField] private Transform target = null;

        private Renderer rend;
        private MaterialPropertyBlock block;
        private Vector2 lastXZ = new Vector2(float.NaN, float.NaN);

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            // Chỉ cập nhật khi Player thực sự di chuyển -> tránh SetPropertyBlock mỗi frame
            if ((lastXZ.x - p.x) * (lastXZ.x - p.x) + (lastXZ.y - p.z) * (lastXZ.y - p.z) < 0.0001f) return;
            lastXZ = new Vector2(p.x, p.z);
            rend.GetPropertyBlock(block);
            block.SetVector(FadeCenterId, new Vector4(p.x, 0f, p.z, 0f));
            rend.SetPropertyBlock(block);
        }
    }
}
