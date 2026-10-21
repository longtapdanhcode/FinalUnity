using UnityEngine;

namespace Vanguard.Cards
{
    /// <summary>
    /// Tia sét LineRenderer giữa hai điểm, nhấp nháy zigzag rồi tự tắt (SetActive false) để tái sử dụng.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LightningShockVFX : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.25f;
        [SerializeField] private float jitter = 0.25f;

        private LineRenderer line;
        private Vector3 from;
        private Vector3 to;
        private float endTime;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
        }

        public void Play(Vector3 start, Vector3 end)
        {
            from = start;
            to = end;
            endTime = Time.time + lifetime;
            gameObject.SetActive(true);
            Redraw();
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                gameObject.SetActive(false);
                return;
            }
            Redraw();
        }

        private void Redraw()
        {
            if (line == null) line = GetComponent<LineRenderer>();

            int count = line.positionCount;
            if (count < 2) return;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 point = Vector3.Lerp(from, to, t);
                if (i > 0 && i < count - 1)
                {
                    point += Random.insideUnitSphere * jitter;
                }
                line.SetPosition(i, point);
            }
        }
    }
}
