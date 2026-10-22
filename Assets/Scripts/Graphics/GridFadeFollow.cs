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
            rend.GetPropertyBlock(block);
            block.SetVector(FadeCenterId, new Vector4(target.position.x, 0f, target.position.z, 0f));
            rend.SetPropertyBlock(block);
        }
    }
}
