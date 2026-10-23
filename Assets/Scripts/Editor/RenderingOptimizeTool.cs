#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 9 - Card 9.3: tối ưu render cho GPU tầm trung.
    /// - PC Renderer: Deferred -> Forward (NeonLit/NeonGrid/Telegraph chỉ có pass Forward, không dùng GBuffer; Forward còn cho MSAA hoạt động).
    /// - Tắt SSAO (shader tự viết không đọc SSAO nên chỉ tốn GPU) và Opaque Texture (không shader nào đọc).
    /// - Soft shadow Medium thay vì High.
    /// - Animator của quái: CullUpdateTransforms + SkinnedMeshRenderer không update khi ngoài màn hình.
    /// - Báo cáo: số material theo shader, material trùng cấu hình, material có MPB/instancing.
    /// </summary>
    public static class RenderingOptimizeTool
    {
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PcAssetPath = "Assets/Settings/PC_RPAsset.asset";
        private const string EnemyFolder = "Assets/Prefabs/Enemies";

        [MenuItem("Vanguard/Graphics/Optimize Rendering (9.3)")]
        public static void Run()
        {
            var log = new StringBuilder("[Perf 9.3] ");
            OptimizeRenderer(log);
            OptimizeUrpAsset(log);
            OptimizeEnemyPrefabs(log);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static void OptimizeRenderer(StringBuilder log)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (data == null) { log.Append("không thấy PC_Renderer; "); return; }

            var so = new SerializedObject(data);
            SerializedProperty mode = so.FindProperty("m_RenderingMode");
            if (mode != null && mode.intValue != (int)RenderingMode.Forward)
            {
                mode.intValue = (int)RenderingMode.Forward;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Append("Renderer: Deferred->Forward; ");
            }

            int off = 0;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
            {
                if (f == null) continue;
                if (f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion") && f.isActive)
                {
                    f.SetActive(false);
                    EditorUtility.SetDirty(f);
                    off++;
                }
            }
            if (off > 0) log.Append("tắt SSAO; ");
            EditorUtility.SetDirty(data);
        }

        private static void OptimizeUrpAsset(StringBuilder log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcAssetPath);
            if (asset == null) return;

            var so = new SerializedObject(asset);
            bool changed = false;
            changed |= SetBool(so, "m_RequireOpaqueTexture", false, log, "tắt Opaque Texture");
            SerializedProperty soft = so.FindProperty("m_SoftShadowQuality");
            if (soft != null && soft.intValue > 2) { soft.intValue = 2; changed = true; log.Append("soft shadow Medium; "); }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }
        }

        private static bool SetBool(SerializedObject so, string name, bool value, StringBuilder log, string message)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            log.Append(message).Append("; ");
            return true;
        }

        private static void OptimizeEnemyPrefabs(StringBuilder log)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;

                foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
                {
                    if (a.cullingMode != AnimatorCullingMode.CullUpdateTransforms) { a.cullingMode = AnimatorCullingMode.CullUpdateTransforms; dirty = true; }
                }
                foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (r.updateWhenOffscreen) { r.updateWhenOffscreen = false; dirty = true; }
                    if (r.skinnedMotionVectors) { r.skinnedMotionVectors = false; dirty = true; }
                }

                if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); count++; }
                PrefabUtility.UnloadPrefabContents(root);
            }
            log.Append($"prefab quái đã chỉnh: {count}; ");
        }

        [MenuItem("Vanguard/Graphics/Audit Materials (9.3)")]
        public static void AuditMaterials()
        {
            var byShader = new SortedDictionary<string, List<string>>();
            var signatures = new Dictionary<string, string>();
            var duplicates = new List<string>();
            int instancing = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;

                string sn = m.shader.name;
                if (!byShader.TryGetValue(sn, out List<string> list)) byShader[sn] = list = new List<string>();
                list.Add(m.name);
                if (m.enableInstancing) instancing++;

                string sig = Signature(m);
                if (signatures.TryGetValue(sig, out string other)) duplicates.Add($"{m.name} == {other}");
                else signatures[sig] = m.name;
            }

            var sb = new StringBuilder("[Perf 9.3] Material theo shader:\n");
            foreach (var kv in byShader) sb.Append($"  {kv.Key}: {kv.Value.Count}\n");
            sb.Append($"GPU Instancing bật: {instancing} (SRP Batcher đã ưu tiên hơn, không cần bật thêm)\n");
            sb.Append(duplicates.Count == 0 ? "Không có material trùng cấu hình.\n" : "Material trùng cấu hình (có thể gộp):\n  " + string.Join("\n  ", duplicates) + "\n");
            Debug.Log(sb.ToString());
        }

        private static string Signature(Material m)
        {
            var sb = new StringBuilder(m.shader.name);
            Shader s = m.shader;
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                string n = s.GetPropertyName(i);
                switch (s.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: sb.Append(n).Append(m.GetColor(n)); break;
                    case ShaderPropertyType.Vector: sb.Append(n).Append(m.GetVector(n)); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: sb.Append(n).Append(m.GetFloat(n)); break;
                    case ShaderPropertyType.Texture:
                        Texture t = m.GetTexture(n);
                        sb.Append(n).Append(t != null ? t.GetInstanceID() : 0);
                        break;
                }
            }
            sb.Append(m.renderQueue);
            foreach (string kw in m.shaderKeywords) sb.Append(kw);
            return sb.ToString();
        }
    }
}
#endif
