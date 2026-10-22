using UnityEngine;
using UnityEngine.VFX;

namespace Vanguard.Graphics
{
    public enum VfxId { DashTrail = 0, Slash = 1, SoulBurst = 2, Impact = 3 }

    /// <summary>Bảng ánh xạ VfxId -> VisualEffectAsset (VFX Graph) kèm kích thước pool và thời gian sống (Sprint 8 - 8.3).</summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "Vanguard/VFX Library")]
    public class VfxLibrarySO : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public VfxId id;
            public VisualEffectAsset asset;
            [Min(1)] public int poolSize;
            [Min(0.1f)] public float lifetime;
        }

        public Entry[] entries = System.Array.Empty<Entry>();

        public bool TryGet(VfxId id, out Entry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].id == id)
                {
                    entry = entries[i];
                    return entry.asset != null;
                }
            }
            entry = default;
            return false;
        }
    }
}
