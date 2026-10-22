using UnityEngine;
using UnityEngine.VFX;

namespace Vanguard.Graphics
{
    /// <summary>
    /// Pool VisualEffect dùng chung: không Instantiate/Destroy trong lúc chiến đấu.
    /// Graph nào chưa được gán trong VfxLibrary sẽ bị bỏ qua (an toàn khi chưa dựng xong VFX).
    /// Quy ước graph: Exposed "Color" (Color/HDR), "Scale" (Float, tuỳ chọn); Start Event mặc định "OnPlay", Stop Event "OnStop".
    /// </summary>
    public class VfxManager : MonoBehaviour
    {
        private const string ColorProp = "Color";
        private const string ScaleProp = "Scale";

        public static VfxManager Instance { get; private set; }

        [SerializeField] private VfxLibrarySO library = null;

        private class Pool
        {
            public VisualEffect[] effects;
            public float[] endTimes;
            public float lifetime;
            public int next;
        }

        private Pool[] pools;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildPools();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildPools()
        {
            int count = System.Enum.GetValues(typeof(VfxId)).Length;
            pools = new Pool[count];
            if (library == null) return;

            for (int id = 0; id < count; id++)
            {
                if (!library.TryGet((VfxId)id, out VfxLibrarySO.Entry entry)) continue;
                if ((VfxId)id == VfxId.DashTrail) continue; // DashTrail là hiệu ứng gắn cố định, không pool

                int size = Mathf.Max(1, entry.poolSize);
                Pool pool = new Pool
                {
                    effects = new VisualEffect[size],
                    endTimes = new float[size],
                    lifetime = Mathf.Max(0.1f, entry.lifetime),
                };
                for (int i = 0; i < size; i++)
                {
                    pool.effects[i] = CreateEffect($"VFX_{(VfxId)id}_{i}", entry.asset, transform);
                }
                pools[id] = pool;
            }
        }

        // Tên event không tồn tại trong graph: chặn VFX tự phát khi vừa bật (mặc định là "OnPlay").
        // Hiệu ứng chỉ chạy khi code gọi Play()/SendEvent("OnPlay") rõ ràng.
        private const string NoAutoPlayEvent = "OnNeverAutoPlay";

        private static VisualEffect CreateEffect(string name, VisualEffectAsset asset, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false); // gắn component khi đang tắt để OnEnable chưa kịp phát event khởi tạo
            go.transform.SetParent(parent, false);
            VisualEffect ve = go.AddComponent<VisualEffect>();
            ve.visualEffectAsset = asset;
            ve.initialEventName = NoAutoPlayEvent;
            return ve;
        }

        /// <summary>Tạo hiệu ứng gắn cố định vào parent (dùng cho Dash Trail). Trả về null nếu graph chưa gán.</summary>
        public VisualEffect CreateAttached(VfxId id, Transform parent)
        {
            if (library == null || !library.TryGet(id, out VfxLibrarySO.Entry entry)) return null;
            VisualEffect ve = CreateEffect($"VFX_{id}", entry.asset, parent);
            ve.gameObject.SetActive(true);
            return ve;
        }

        public static void Play(VfxId id, Vector3 position, Quaternion rotation, Color color, float scale = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, position, rotation, color, scale);
        }

        private void PlayInternal(VfxId id, Vector3 position, Quaternion rotation, Color color, float scale)
        {
            if (pools == null || (int)id >= pools.Length) return;
            Pool pool = pools[(int)id];
            if (pool == null) return;

            int index = pool.next;
            pool.next = (pool.next + 1) % pool.effects.Length;

            VisualEffect ve = pool.effects[index];
            ve.transform.SetPositionAndRotation(position, rotation);
            ve.gameObject.SetActive(true);
            SetParameters(ve, color, scale);
            ve.Reinit();
            ve.Play();
            pool.endTimes[index] = Time.unscaledTime + pool.lifetime;
        }

        public static void SetParameters(VisualEffect ve, Color color, float scale = 1f)
        {
            if (ve.HasVector4(ColorProp)) ve.SetVector4(ColorProp, color);
            if (ve.HasFloat(ScaleProp)) ve.SetFloat(ScaleProp, scale);
        }

        private void Update()
        {
            if (pools == null) return;
            float now = Time.unscaledTime;
            for (int p = 0; p < pools.Length; p++)
            {
                Pool pool = pools[p];
                if (pool == null) continue;
                for (int i = 0; i < pool.effects.Length; i++)
                {
                    VisualEffect ve = pool.effects[i];
                    if (ve != null && ve.gameObject.activeSelf && now >= pool.endTimes[i])
                    {
                        ve.Stop();
                        ve.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}
