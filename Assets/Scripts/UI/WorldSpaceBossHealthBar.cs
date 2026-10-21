using UnityEngine;
using UnityEngine.UI;
using Vanguard.Enemy;

namespace Vanguard.UI
{
    /// <summary>
    /// Thanh máu World Space trên đầu Boss: cập nhật mượt, luôn quay mặt về Main Camera (Billboard),
    /// tự ẩn khi Boss chết và đổi màu khi Boss Cuồng Bạo (Enraged).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WorldSpaceBossHealthBar : MonoBehaviour
    {
        [Tooltip("RectTransform của phần máu đỏ (neo trái); độ dài thay đổi theo % máu")]
        [SerializeField] private RectTransform fillRect = null;

        [Tooltip("Image của phần máu, dùng để đổi màu theo trạng thái (tùy chọn)")]
        [SerializeField] private Image fillImage = null;
        [SerializeField] private Color normalColor = new Color(1f, 0.1f, 0.05f, 1f);
        [SerializeField] private Color enragedColor = new Color(0.65f, 0.1f, 0.95f, 1f);

        [Tooltip("Tốc độ thanh máu đuổi theo giá trị thật (đơn vị fill/giây)")]
        [SerializeField] private float fillSpeed = 2.5f;

        private EnemyBase boss;
        private Canvas canvas;
        private Camera mainCamera;
        private float targetFill = 1f;
        private float displayedFill = 1f;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            boss = GetComponentInParent<EnemyBase>();
        }

        private void OnEnable()
        {
            if (boss == null) return;

            boss.OnEnemyHealthChanged += HandleHealthChanged;
            boss.OnEnemyDeath += HandleDeath;

            // Boss luôn xuất hiện với đầy máu, chưa Cuồng Bạo (reset từ Pool)
            targetFill = 1f;
            displayedFill = 1f;
            ApplyFill();
            SetEnraged(false);
            canvas.enabled = true;
        }

        private void OnDisable()
        {
            if (boss == null) return;

            boss.OnEnemyHealthChanged -= HandleHealthChanged;
            boss.OnEnemyDeath -= HandleDeath;
        }

        /// <summary>
        /// Đổi màu thanh máu giữa trạng thái thường và Cuồng Bạo (Phase 2).
        /// </summary>
        public void SetEnraged(bool enraged)
        {
            if (fillImage != null) fillImage.color = enraged ? enragedColor : normalColor;
        }

        private void HandleHealthChanged(float current, float max)
        {
            targetFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private void HandleDeath(EnemyBase dead)
        {
            canvas.enabled = false;
        }

        private void LateUpdate()
        {
            if (!canvas.enabled) return;

            if (!Mathf.Approximately(displayedFill, targetFill))
            {
                displayedFill = Mathf.MoveTowards(displayedFill, targetFill, fillSpeed * Time.deltaTime);
                ApplyFill();
            }

            // Billboard: luôn song song mặt phẳng camera để đọc rõ từ góc nhìn chéo trên cao
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        private void ApplyFill()
        {
            if (fillRect != null) fillRect.anchorMax = new Vector2(displayedFill, 1f);
        }
    }
}
