#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sửa clip Mixamo "có tịnh tiến" (Dash/Combo/Attack...) bị model trượt rồi giật ngược về chỗ cũ:
    /// bỏ "Bake Into Pose" cho Root Transform Position (XZ) để chuyển động ngang bị tách khỏi pose;
    /// vì Animator.applyRootMotion = false nên phần tách ra bị bỏ đi -> animation chạy tại chỗ.
    /// </summary>
    public static class InPlaceAnimationFixTool
    {
        [MenuItem("Vanguard/Graphics/Fix Clips In-Place (no snap-back)")]
        public static void Fix()
        {
            int fixedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null || !imp.importAnimation) continue;

                ModelImporterClipAnimation[] clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) continue;

                bool changed = false;
                List<ModelImporterClipAnimation> list = new List<ModelImporterClipAnimation>(clips);
                for (int i = 0; i < list.Count; i++)
                {
                    ModelImporterClipAnimation c = list[i];
                    if (!c.lockRootPositionXZ && !c.keepOriginalPositionXZ) continue;
                    c.lockRootPositionXZ = false;
                    c.keepOriginalPositionXZ = false;
                    list[i] = c;
                    changed = true;
                }

                if (!changed) continue;
                imp.clipAnimations = list.ToArray();
                imp.SaveAndReimport();
                fixedCount++;
            }
            Debug.Log($"[InPlaceFix] Đã sửa {fixedCount} file clip (Root Transform Position XZ không còn bake into pose).");
        }
    }
}
#endif
