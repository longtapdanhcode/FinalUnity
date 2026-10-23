#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 9 - Card 9.2: chuẩn bị Lighting cho arena.
    /// - Môi trường tĩnh (ArenaVisuals) bật Contribute GI + emissive được bake (Meta pass trong NeonLit).
    /// - Light Probe Group dày hơn cho nhân vật động.
    /// - Bỏ point light trên cột (NeonLit không nhận additional light, dùng emissive thay thế).
    /// - Directional Light chuyển Mixed (Baked Indirect) để lightmap chỉ chứa ánh sáng gián tiếp.
    /// - Tắt shadow cho additional light trong URP Asset PC (giảm atlas, bóng chính do main light đảm nhận).
    /// </summary>
    public static class LightingSetupTool
    {
        private const string PillarPath = "Assets/Prefabs/Environment/ArenaPillar.prefab";
        private const string PcAssetPath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Vanguard/Graphics/Setup Lighting (9.2)")]
        public static void Setup()
        {
            RemovePillarLights();
            int statics = MarkStaticEnvironment();
            int probes = BuildProbes();
            ConfigureMainLight();
            ConfigureUrpAsset();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Lighting] Static GI: {statics} object, Light Probe: {probes}. Chạy menu Vanguard/Graphics/Bake Lighting (9.2) để bake.");
        }

        [MenuItem("Vanguard/Graphics/Bake Lighting (9.2)")]
        public static void Bake()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[Lighting] Đang bake, đợi xong."); return; }
            if (Lightmapping.TryGetLightingSettings(out LightingSettings ls))
            {
                ls.bakedGI = true;
                ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
                EditorUtility.SetDirty(ls);
            }
            Lightmapping.BakeAsync();
            Debug.Log("[Lighting] Bắt đầu bake (xem tiến độ ở góc dưới phải Unity / Window > Rendering > Lighting).");
        }

        private static void RemovePillarLights()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PillarPath);
            int removed = 0;
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(l.gameObject);
                removed++;
            }
            if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, PillarPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[Lighting] Đã bỏ {removed} point light trên prefab cột (thay bằng emissive).");
        }

        private static int MarkStaticEnvironment()
        {
            GameObject root = GameObject.Find("ArenaVisuals");
            if (root == null) { Debug.LogWarning("[Lighting] Không thấy ArenaVisuals."); return 0; }

            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic |
                                            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
            int count = 0;
            HashSet<Material> mats = new HashSet<Material>();
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<Rigidbody>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
                r.receiveGI = ReceiveGI.Lightmaps;
                count++;
                foreach (Material m in r.sharedMaterials) if (m != null) mats.Add(m);
            }

            foreach (Material m in mats)
            {
                if (m.shader == null || m.shader.name != "Vanguard/NeonLit") continue;
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(m);
            }
            return count;
        }

        private static int BuildProbes()
        {
            GameObject root = GameObject.Find("ArenaVisuals");
            Transform t = root != null ? root.transform.Find("ArenaLightProbes") : null;
            LightProbeGroup group;
            if (t == null)
            {
                GameObject go = new GameObject("ArenaLightProbes");
                if (root != null) go.transform.SetParent(root.transform, false);
                group = go.AddComponent<LightProbeGroup>();
            }
            else
            {
                group = t.GetComponent<LightProbeGroup>();
                if (group == null) group = t.gameObject.AddComponent<LightProbeGroup>();
            }

            // Lưới 8m phủ arena (hai tầng cao độ) + cụm dày quanh bẫy xoay ở tâm
            List<Vector3> pts = new List<Vector3>();
            for (int x = -24; x <= 24; x += 8)
                for (int z = -24; z <= 24; z += 8)
                    foreach (float y in new[] { 1.5f, 4f })
                        pts.Add(new Vector3(x, y, z));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                pts.Add(new Vector3(Mathf.Cos(a) * 3.5f, 1.5f, Mathf.Sin(a) * 3.5f));
            }
            group.probePositions = pts.ToArray();
            EditorUtility.SetDirty(group);
            return pts.Count;
        }

        private static void ConfigureMainLight()
        {
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l.type != LightType.Directional) continue;
                l.lightmapBakeType = LightmapBakeType.Mixed;
                l.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(l);
            }
        }

        private static void ConfigureUrpAsset()
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(PcAssetPath);
            if (asset == null) return;
            SerializedObject so = new SerializedObject(asset);
            SerializedProperty p = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (p != null) p.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
#endif
