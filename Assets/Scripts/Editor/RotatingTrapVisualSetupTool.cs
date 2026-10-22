#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Environment;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 8 - Card 8.2: thay bẫy xoay phẳng màu đỏ bằng visual neon hazard
    /// (thân tối, viền cam phát sáng, trục giữa, tia lửa ở đầu lưỡi).
    /// Collider và logic CenterRotatingTrap giữ nguyên; mọi visual mới là con của bẫy nên tự bật/tắt theo wave.
    /// </summary>
    public static class RotatingTrapVisualSetupTool
    {
        private const string Tag = "TV_";

        [MenuItem("Vanguard/Graphics/Setup Rotating Trap Visual")]
        public static void Setup()
        {
            CenterRotatingTrap trap = Object.FindAnyObjectByType<CenterRotatingTrap>(FindObjectsInactive.Include);
            if (trap == null) { Debug.LogError("[TrapVisual] Không tìm thấy CenterRotatingTrap trong scene."); return; }

            Shader neon = Shader.Find("Vanguard/NeonLit");
            if (neon == null) { Debug.LogError("[TrapVisual] Không tìm thấy shader Vanguard/NeonLit."); return; }

            ArtPaletteSO palette = AssetDatabase.LoadAssetAtPath<ArtPaletteSO>("Assets/Data/ArtPalette.asset");
            if (palette == null) palette = ScriptableObject.CreateInstance<ArtPaletteSO>();
            Color hazard = palette.hazardOrange;

            Material body = GetMaterial("M_TrapBody", neon, new Color(0.05f, 0.02f, 0.03f), hazard * 0.2f, hazard, 0.3f, 3f);
            Material edge = GetMaterial("M_TrapEdge", neon, Color.black, hazard * palette.dangerIntensity, hazard, 0.25f, 5f);
            Material tip = GetMaterial("M_TrapTip", neon, Color.black, hazard * palette.fxIntensity, hazard, 0.2f, 6f);
            Material spark = GetMaterial("M_TrapSpark", neon, Color.black, hazard * palette.fxIntensity, hazard, 0f, 1f);

            Transform root = trap.transform;
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Setup Rotating Trap Visual");
            CleanOldVisuals(root);

            List<Transform> blades = new List<Transform>();
            foreach (Transform child in root)
            {
                if (child.GetComponent<Collider>() != null) blades.Add(child);
            }
            if (blades.Count == 0) { Debug.LogError("[TrapVisual] Không tìm thấy lưỡi (Collider con) dưới CenterRotatingTrap."); return; }

            foreach (Transform blade in blades)
            {
                MeshRenderer mr = blade.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = body;

                // Hai dải viền sáng dọc hai cạnh trên của lưỡi (toạ độ local của khối cube đơn vị)
                foreach (float z in new[] { -0.5f, 0.5f })
                {
                    GameObject strip = CreateBox(blade, Tag + "Edge", new Vector3(0f, 0.5f, z), new Vector3(1f, 0.25f, 0.12f), edge);
                    strip.transform.localRotation = Quaternion.identity;
                }

                // Đầu lưỡi: khối phát sáng + hệ hạt tia lửa
                foreach (float x in new[] { -0.5f, 0.5f })
                {
                    Vector3 tipWorld = blade.TransformPoint(new Vector3(x, 0f, 0f));
                    GameObject tipGlow = CreateBox(root, Tag + "Tip", Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f), tip);
                    tipGlow.transform.position = tipWorld + Vector3.up * 0.1f;
                    CreateSparkSystem(root, tipWorld + Vector3.up * 0.2f, spark);
                }
            }

            // Trục giữa: nắp tối đặt CAO HƠN mặt trên của lưỡi để che chỗ hai lưỡi giao nhau
            // (tránh z-fighting), viền sáng bên dưới nắp và lõi phát sáng ở giữa.
            CreatePrimitive(root, PrimitiveType.Cylinder, Tag + "HubRim", new Vector3(0f, 0.24f, 0f), new Vector3(2.5f, 0.03f, 2.5f), edge);
            CreatePrimitive(root, PrimitiveType.Cylinder, Tag + "HubCap", new Vector3(0f, 0.27f, 0f), new Vector3(2.1f, 0.06f, 2.1f), body);
            CreatePrimitive(root, PrimitiveType.Cylinder, Tag + "HubCore", new Vector3(0f, 0.335f, 0f), new Vector3(0.7f, 0.01f, 0.7f), tip);

            // Hạt tia lửa khi trúng mục tiêu (phát bằng script)
            ParticleSystem hitSparks = CreateSparkSystem(root, root.position, spark);
            hitSparks.gameObject.name = Tag + "HitSparks";
            ConfigureHitSparks(hitSparks);

            TrapVisualFX fx = root.GetComponent<TrapVisualFX>();
            if (fx == null) fx = Undo.AddComponent<TrapVisualFX>(root.gameObject);
            SerializedObject so = new SerializedObject(fx);
            so.FindProperty("hitSparks").objectReferenceValue = hitSparks;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Làm trống danh sách tự tìm để Awake tái quét các visual con mới
            SerializedObject trapSo = new SerializedObject(trap);
            SerializedProperty visuals = trapSo.FindProperty("bladeVisuals");
            if (visuals != null) { visuals.arraySize = 0; }
            SerializedProperty colliders = trapSo.FindProperty("bladeColliders");
            if (colliders != null) { colliders.arraySize = 0; }
            trapSo.ApplyModifiedPropertiesWithoutUndo();

            VerifyColliders(root);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[TrapVisual] Đã dựng visual bẫy xoay. Bấm Play (bẫy bật từ wave 8, hoặc tăng isActive để test).");
        }

        private static void CleanOldVisuals(Transform root)
        {
            List<GameObject> toDelete = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name.StartsWith(Tag)) toDelete.Add(t.gameObject);
            }
            foreach (GameObject go in toDelete) if (go != null) Object.DestroyImmediate(go);
        }

        private static void VerifyColliders(Transform root)
        {
            int total = 0, triggers = 0;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                total++;
                if (c.isTrigger) triggers++;
                if (c.name.StartsWith(Tag)) Debug.LogWarning($"[TrapVisual] {c.name} có Collider ngoài ý muốn.");
            }
            Debug.Log($"[TrapVisual] Kiểm tra: tổng collider dưới bẫy = {total} (trigger = {triggers}); các visual TV_* không có collider.");
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            return CreatePrimitive(parent, PrimitiveType.Cube, name, localPos, localScale, mat);
        }

        private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static ParticleSystem CreateSparkSystem(Transform parent, Vector3 worldPos, Material mat)
        {
            GameObject go = new GameObject(Tag + "Sparks");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // Cone phát lên trên

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.05f;

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void ConfigureHitSparks(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.angle = 55f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static Material GetMaterial(string name, Shader shader, Color baseColor, Color emission, Color rim, float pulseAmount, float pulseSpeed)
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
            m.SetFloat("_PulseAmount", pulseAmount);
            m.SetFloat("_PulseSpeed", pulseSpeed);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif
