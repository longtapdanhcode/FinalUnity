#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 7: in thông tin FBX trong Assets/Models (mesh, bone, clip, rig, kích thước) ra Console.</summary>
    public static class ModelInspectTool
    {
        [MenuItem("Vanguard/Graphics/Inspect Models")]
        public static void Inspect()
        {
            StringBuilder sb = new StringBuilder("[ModelInspect]\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (imp == null || go == null) continue;

                SkinnedMeshRenderer[] smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                MeshRenderer[] mr = go.GetComponentsInChildren<MeshRenderer>(true);
                int bones = smr.Length > 0 ? smr[0].bones.Length : 0;
                Bounds b = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                string clips = string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))
                    .Select(c => $"{c.name} ({c.length:F2}s)"));
                sb.AppendLine($"{path} | rig={imp.animationType} scale={imp.globalScale} | skinned={smr.Length} static={mr.Length} bones={bones} | size={b.size:F2} | clips: {clips}");
            }
            string outPath = System.IO.Path.Combine(Application.dataPath, "../docs/ModelInspect.txt");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[ModelInspect] Đã ghi docs/ModelInspect.txt");
        }
    }
}
#endif
