using UnityEngine;

namespace Vanguard.UI
{
    /// <summary>Hover cho nút neon: tăng sáng NeonPanelFX và phóng nhẹ (chạy theo unscaledTime vì menu có thể đang pause).</summary>
    public sealed class NeonButtonHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        [SerializeField] private NeonPanelFX panel = null;
        [SerializeField, Min(1f)] private float scale = 1.05f;
        [SerializeField, Min(1f)] private float speed = 14f;

        private bool inside;
        private float value;

        public void Configure(NeonPanelFX fx) { panel = fx; }

        private void Awake()
        {
            if (panel == null) panel = GetComponentInChildren<NeonPanelFX>(true);
        }

        private void OnEnable()
        {
            inside = false;
            value = 0f;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) { inside = true; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) { inside = false; }

        private void Update()
        {
            value = Mathf.MoveTowards(value, inside ? 1f : 0f, speed * Time.unscaledDeltaTime);
            float s = Mathf.Lerp(1f, scale, value);
            transform.localScale = new Vector3(s, s, 1f);
            if (panel != null) panel.Hover = value;
        }
    }
}
