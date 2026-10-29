using System.Collections.Generic;
using UnityEngine;

namespace Vanguard.Pooling
{
    /// <summary>
    /// Pool dùng chung cho prefab tạm thời (VFX nổ, đạn, thiên thạch...) thay cho Instantiate/Destroy (Sprint 9 - 9.4).
    /// Get() lấy/tạo instance đã active; Release() trả về pool (hoặc Destroy nếu object không đến từ pool).
    /// </summary>
    public static class PrefabPool
    {
        private static readonly Dictionary<GameObject, Stack<PooledObject>> Pools = new Dictionary<GameObject, Stack<PooledObject>>();
        private static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pools.Clear();
            root = null;
        }

        /// <param name="autoReleaseAfter">&gt; 0: tự trả sau n giây. 0: tự ước lượng theo ParticleSystem (nếu có), không có thì không tự trả.</param>
        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, float autoReleaseAfter = 0f)
        {
            if (prefab == null) return null;

            PooledObject po = null;
            if (Pools.TryGetValue(prefab, out Stack<PooledObject> stack))
            {
                while (stack.Count > 0)
                {
                    po = stack.Pop();
                    if (po != null) break; // bỏ qua instance đã bị Destroy (đổi scene)
                    po = null;
                }
            }

            if (po == null)
            {
                GameObject go = Object.Instantiate(prefab, GetRoot());
                po = go.GetComponent<PooledObject>();
                if (po == null) po = go.AddComponent<PooledObject>();
                po.Initialize(prefab);
            }

            po.transform.SetPositionAndRotation(position, rotation);
            po.gameObject.SetActive(true);
            po.OnSpawn(autoReleaseAfter);
            return po.gameObject;
        }

        public static void Release(GameObject instance)
        {
            if (instance == null) return;
            PooledObject po = instance.GetComponent<PooledObject>();
            if (po == null || po.Prefab == null)
            {
                Object.Destroy(instance); // không thuộc pool (vd. đạn procedural)
                return;
            }
            if (po.InPool) return; // chống trả hai lần

            po.InPool = true;
            po.OnReturn();
            instance.SetActive(false);
            instance.transform.SetParent(GetRoot(), false);

            if (!Pools.TryGetValue(po.Prefab, out Stack<PooledObject> stack))
            {
                stack = new Stack<PooledObject>(8);
                Pools[po.Prefab] = stack;
            }
            stack.Push(po);
        }

        /// <summary>Tạo sẵn n instance để tránh giật ở lần dùng đầu (gọi lúc load).</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var temp = new List<GameObject>(count);
            for (int i = 0; i < count; i++) temp.Add(Get(prefab, Vector3.zero, Quaternion.identity, 0.001f));
            for (int i = 0; i < temp.Count; i++) Release(temp[i]);
        }

        private static Transform GetRoot()
        {
            if (root == null) root = new GameObject("PrefabPool").transform;
            return root;
        }
    }
}
