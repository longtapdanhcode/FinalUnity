#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 8 - Card 8.1: dựng môi trường arena dạng module (low-poly) bằng material NeonLit:
    /// cột neon, tường thấp, viền emissive, Light Probe Group và đèn nhấn.
    /// Chỉ tạo visual; không thêm Collider và không đụng ArenaBounds/NavMesh.
    /// </summary>
    public static class ArenaEnvironmentSetupTool
    {
        private const float HalfSize = 25f;
        private const string RootName = "ArenaVisuals";
        private const string PillarPath = "Assets/Prefabs/Environment/ArenaPillar.prefab";

        private static readonly Color Cyan = new Color(0f, 0.8f, 1.2f, 1f);
        private static readonly Color Magenta = new Color(1.1f, 0.15f, 1.4f, 1f);

        [MenuItem("Vanguard/Graphics/Setup Arena Environment")]
        public static void Setup()
        {
            Shader neon = Shader.Find("Vanguard/NeonLit");
            if (neon == null) { Debug.LogError("[Arena] Không tìm thấy shader Vanguard/NeonLit."); return; }

            Material dark = GetMaterial("M_ArenaDark", neon, new Color(0.03f, 0.02f, 0.07f), new Color(0.05f, 0.02f, 0.12f), new Color(0.35f, 0.1f, 0.8f), 0f);
            Material neonCyan = GetMaterial("M_ArenaNeonCyan", neon, Color.black, Cyan * 1.6f, Cyan, 0.15f);
            Material neonMagenta = GetMaterial("M_ArenaNeonMagenta", neon, Color.black, Magenta * 1.6f, Magenta, 0.15f);

            EnsureFolder("Assets/Prefabs/Environment");
            GameObject pillarPrefab = BuildPillarPrefab(dark, neonCyan);

            GameObject old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup Arena Environment");

            // Tường thấp + viền emissive trên đỉnh và sát sàn
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = (side % 2 == 0) ? 1f : -1f;
                Vector3 normal = alongX ? new Vector3(0, 0, sign) : new Vector3(sign, 0, 0);
                Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
                Material edge = (side % 2 == 0) ? neonCyan : neonMagenta;

                CreateBox(root.transform, $"Wall_{side}", normal * (HalfSize + 0.3f) + Vector3.up * 0.6f, rot, new Vector3(2f * HalfSize + 1f, 1.2f, 0.6f), dark);
                CreateBox(root.transform, $"WallTopEdge_{side}", normal * (HalfSize + 0.3f) + Vector3.up * 1.25f, rot, new Vector3(2f * HalfSize + 1f, 0.1f, 0.35f), edge);
                CreateBox(root.transform, $"FloorEdge_{side}", normal * (HalfSize - 0.4f) + Vector3.up * 0.05f, rot, new Vector3(2f * HalfSize, 0.08f, 0.25f), edge);
            }

            // 8 cột: 4 góc + 4 giữa cạnh, xen kẽ đèn cyan/magenta
            List<Vector3> pillarPositions = new List<Vector3>
            {
                new Vector3(HalfSize, 0, HalfSize), new Vector3(-HalfSize, 0, HalfSize),
                new Vector3(HalfSize, 0, -HalfSize), new Vector3(-HalfSize, 0, -HalfSize),
                new Vector3(HalfSize, 0, 0), new Vector3(-HalfSize, 0, 0),
                new Vector3(0, 0, HalfSize), new Vector3(0, 0, -HalfSize),
            };
            for (int i = 0; i < pillarPositions.Count; i++)
            {
                GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, root.transform);
                p.name = $"Pillar_{i}";
                p.transform.position = pillarPositions[i];
                Light l = p.GetComponentInChildren<Light>();
                if (l != null) l.color = (i % 2 == 0) ? new Color(0f, 0.8f, 1f) : new Color(1f, 0.2f, 1f);
            }

            // Light Probe Group phủ arena (2 tầng cao độ) cho nhân vật skinned
            GameObject probes = new GameObject("ArenaLightProbes");
            probes.transform.SetParent(root.transform, false);
            LightProbeGroup group = probes.AddComponent<LightProbeGroup>();
            List<Vector3> pts = new List<Vector3>();
            for (int x = -24; x <= 24; x += 12)
                for (int z = -24; z <= 24; z += 12)
                    foreach (float y in new[] { 1.5f, 4f })
                        pts.Add(new Vector3(x, y, z));
            group.probePositions = pts.ToArray();

            // Lưới sàn: tâm vùng sáng bám theo Player (camera đã bám theo nên không thể cố định ở gốc)
            GameObject ground = GameObject.Find("Ground");
            if (ground != null && ground.GetComponent<Vanguard.Graphics.GridFadeFollow>() == null)
            {
                Undo.AddComponent<Vanguard.Graphics.GridFadeFollow>(ground);
            }

            VerifyNoColliders(root);

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;
            Debug.Log("[Arena] Đã dựng ArenaVisuals. Mở Window > Rendering > Lighting > Generate Lighting để bake Light Probe.");
        }

#pragma warning disable CS0618 // NavigationStatic bị đánh dấu deprecated nhưng vẫn dùng được để kiểm tra
        private static void VerifyNoColliders(GameObject root)
        {
            int colliders = root.GetComponentsInChildren<Collider>(true).Length;
            int navStatic = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.AreStaticEditorFlagsSet(t.gameObject, StaticEditorFlags.NavigationStatic)) navStatic++;
            }
            GameObject bounds = GameObject.Find("ArenaBounds");
            int boundsColliders = bounds != null ? bounds.GetComponentsInChildren<Collider>(true).Length : -1;
            Debug.Log($"[Arena] Kiểm tra: collider trong ArenaVisuals = {colliders} (phải = 0), NavigationStatic = {navStatic} (phải = 0), collider ArenaBounds = {boundsColliders} (không đổi).");
        }

#pragma warning restore CS0618

        private static GameObject BuildPillarPrefab(Material dark, Material neonCyan)
        {
            GameObject root = new GameObject("ArenaPillar");
            CreateBox(root.transform, "Body", new Vector3(0, 1.25f, 0), Quaternion.identity, new Vector3(0.9f, 2.5f, 0.9f), dark);
            foreach (float y in new[] { 0.6f, 1.6f, 2.5f })
            {
                CreateBox(root.transform, $"Ring_{y}", new Vector3(0, y, 0), Quaternion.identity, new Vector3(1f, 0.1f, 1f), neonCyan);
            }

            GameObject lightGo = new GameObject("AccentLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 2.8f, 0);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.color = new Color(0f, 0.8f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PillarPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material GetMaterial(string name, Shader shader, Color baseColor, Color emission, Color rim, float pulse)
        {
            string path = $"Assets/Materials/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_EmissionColor", emission);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_PulseAmount", pulse);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
