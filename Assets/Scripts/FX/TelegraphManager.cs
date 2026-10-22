using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vanguard.FX
{
    /// <summary>
    /// Object Pool và cổng truy cập chung cho TelegraphWarning (Sprint 5 - Card 5.4).
    /// Nếu Scene chưa có TelegraphManager, một bản tự động (không prefab) sẽ được tạo khi cần.
    /// </summary>
    public class TelegraphManager : MonoBehaviour
    {
        public static TelegraphManager Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private TelegraphWarning circlePrefab = null;
        [Tooltip("Cũng dùng cho cảnh báo dạng Line")]
        [SerializeField] private TelegraphWarning sectorPrefab = null;

        [Header("Prewarm")]
        [SerializeField] private int circlePrewarm = 6;
        [SerializeField] private int sectorPrewarm = 3;

        // Chỉ số Pool: 0 = Circle, 1 = Sector, 2 = Line
        private readonly Stack<TelegraphWarning>[] pools =
        {
            new Stack<TelegraphWarning>(), new Stack<TelegraphWarning>(), new Stack<TelegraphWarning>()
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TelegraphManager] Đã có TelegraphManager khác trong Scene, hủy bản sao này.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;

            Prewarm(TelegraphShape.Circle, circlePrewarm);
            Prewarm(TelegraphShape.Sector, sectorPrewarm);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region Static API
        public static TelegraphHandle ShowCircle(Vector3 position, float radius, float duration, Action onComplete = null,
            TelegraphDanger danger = TelegraphDanger.Medium)
        {
            TelegraphWarning warning = GetInstance().Acquire(TelegraphShape.Circle);
            warning.SetDanger(danger);
            warning.ShowCircle(position, radius, duration, onComplete);
            return new TelegraphHandle(warning);
        }

        public static TelegraphHandle ShowSector(Vector3 position, Vector3 forward, float angle, float radius,
            float duration, Action onComplete = null, TelegraphDanger danger = TelegraphDanger.Medium)
        {
            TelegraphWarning warning = GetInstance().Acquire(TelegraphShape.Sector);
            warning.SetDanger(danger);
            warning.ShowSector(position, forward, angle, radius, duration, onComplete);
            return new TelegraphHandle(warning);
        }

        public static TelegraphHandle ShowLine(Vector3 position, Vector3 forward, float length, float width,
            float duration, Action onComplete = null, TelegraphDanger danger = TelegraphDanger.Medium)
        {
            TelegraphWarning warning = GetInstance().Acquire(TelegraphShape.Line);
            warning.SetDanger(danger);
            warning.ShowLine(position, forward, length, width, duration, onComplete);
            return new TelegraphHandle(warning);
        }

        private static TelegraphManager GetInstance()
        {
            if (Instance == null)
            {
                new GameObject("TelegraphManager (Auto)").AddComponent<TelegraphManager>();
            }
            return Instance;
        }
        #endregion

        #region Pooling
        private void Prewarm(TelegraphShape shape, int count)
        {
            for (int i = 0; i < count; i++)
            {
                TelegraphWarning warning = Create(shape);
                warning.gameObject.SetActive(false);
                pools[(int)shape].Push(warning);
            }
        }

        private TelegraphWarning Acquire(TelegraphShape shape)
        {
            Stack<TelegraphWarning> pool = pools[(int)shape];
            while (pool.Count > 0)
            {
                TelegraphWarning warning = pool.Pop();
                if (warning != null) return warning; // Bỏ qua đối tượng đã bị Destroy từ bên ngoài
            }
            return Create(shape);
        }

        private TelegraphWarning Create(TelegraphShape shape)
        {
            TelegraphWarning prefab = shape == TelegraphShape.Circle ? circlePrefab : sectorPrefab;

            TelegraphWarning warning;
            if (prefab != null)
            {
                warning = Instantiate(prefab, transform);
            }
            else
            {
                // Chưa có Prefab: tự dựng bản thô (Base / Fill do TelegraphWarning tự tạo)
                GameObject go = new GameObject($"Telegraph_{shape}");
                go.transform.SetParent(transform, false);
                warning = go.AddComponent<TelegraphWarning>();
            }

            warning.name = $"Telegraph_{shape}";
            warning.Owner = this;
            warning.PoolIndex = (int)shape;
            return warning;
        }

        internal void Release(TelegraphWarning warning)
        {
            if (warning == null) return;
            pools[warning.PoolIndex].Push(warning);
        }
        #endregion
    }
}
