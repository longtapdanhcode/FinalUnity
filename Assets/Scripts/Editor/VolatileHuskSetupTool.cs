using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using Vanguard.Enemy;
using UnityEditor.SceneManagement;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tự động tạo ExplosionVFX, M_VolatileHusk, Prefab VolatileHusk và thiết lập Scene.
    /// </summary>
    public static class VolatileHuskSetupTool
    {
        [MenuItem("Vanguard/Setup Volatile Husk")]
        public static void Setup()
        {
            // 1. Đảm bảo các thư mục cần thiết tồn tại
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Combat");
            EnsureFolder("Assets/Prefabs", "Enemies");
            EnsureFolder("Assets", "Materials");

            // 2. Tạo Prefab ExplosionVFX (ParticleSystem nổ lửa nở bung nhanh 0.4s)
            string vfxPath = "Assets/Prefabs/Combat/ExplosionVFX.prefab";
            GameObject vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);

            // Tạo Particle Material
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            Material pMat = new Material(pShader);
            pMat.name = "M_Explosion_Particle";
            pMat.color = new Color(1f, 0.45f, 0f, 1f);
            AssetDatabase.CreateAsset(pMat, "Assets/Materials/M_Explosion_Particle.mat");

            GameObject vfxGo = new GameObject("ExplosionVFX");
            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f;
            main.startLifetime = 0.4f;
            main.startSpeed = 9f;
            main.startSize = 0.7f;
            main.startColor = new Color(1f, 0.4f, 0.05f, 1f);
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            var colOverLifetime = ps.colorOverLifetime;
            colOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0f), 0.6f),
                    new GradientColorKey(new Color(0.3f, 0f, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colOverLifetime.color = grad;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0.15f);
            sz.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = vfxGo.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && pMat != null)
            {
                psRenderer.sharedMaterial = pMat;
            }

            vfxPrefab = PrefabUtility.SaveAsPrefabAsset(vfxGo, vfxPath);
            Object.DestroyImmediate(vfxGo);
            Debug.Log($"[VolatileHuskSetup] 1/4: Đã tạo Prefab ExplosionVFX tại: {vfxPath}");

            // 3. Tạo Material M_VolatileHusk (vàng cam pha đỏ, bật Emission URP)
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material huskMat = new Material(litShader);
            huskMat.color = new Color(1f, 0.35f, 0.05f, 1f); // Vàng cam rực lửa
            huskMat.EnableKeyword("_EMISSION");
            huskMat.SetColor("_EmissionColor", new Color(1.8f, 0.35f, 0f, 1f));

            string matPath1 = "Assets/Materials/M_VolatileHusk.mat";
            AssetDatabase.CreateAsset(huskMat, matPath1);
            Debug.Log($"[VolatileHuskSetup] 2/4: Đã tạo Material M_VolatileHusk tại: {matPath1}");

            // 4. Tạo Prefab VolatileHusk
            // Sphere nhỏ (Scale 0.7, 0.7, 0.7), Layer "Enemy", NavMeshAgent (Speed 6.5), VolatileHuskEnemy
            GameObject huskGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            huskGo.name = "VolatileHusk";
            huskGo.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                huskGo.layer = enemyLayer;
            }

            Renderer rend = huskGo.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = huskMat;
            }

            NavMeshAgent agent = huskGo.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = huskGo.AddComponent<NavMeshAgent>();
            }
            agent.speed = 6.5f;
            agent.stoppingDistance = 1.6f;

            VolatileHuskEnemy huskComp = huskGo.GetComponent<VolatileHuskEnemy>();
            if (huskComp == null)
            {
                huskComp = huskGo.AddComponent<VolatileHuskEnemy>();
            }

            // Gán Serialized Property explosionVFXPrefab
            SerializedObject so = new SerializedObject(huskComp);
            SerializedProperty vfxProp = so.FindProperty("explosionVFXPrefab");
            if (vfxProp != null)
            {
                vfxProp.objectReferenceValue = vfxPrefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            string huskPrefabPath = "Assets/Prefabs/Enemies/VolatileHusk.prefab";
            GameObject huskPrefab = PrefabUtility.SaveAsPrefabAsset(huskGo, huskPrefabPath);
            Object.DestroyImmediate(huskGo);
            Debug.Log($"[VolatileHuskSetup] 3/4: Đã tạo Prefab VolatileHusk tại: {huskPrefabPath}");

            // 5. Đặt 1 con VolatileHusk ra Scene tại (0, 0.5, 7) và 1 con Crawler gần đó để test sát thương lan
            var activeScene = EditorSceneManager.GetActiveScene();

            // Xóa VolatileHusk cũ nếu có
            GameObject oldHusk = GameObject.Find("VolatileHusk");
            if (oldHusk != null) Object.DestroyImmediate(oldHusk);

            // Đặt VolatileHusk
            GameObject sceneHusk = (GameObject)PrefabUtility.InstantiatePrefab(huskPrefab, activeScene);
            sceneHusk.name = "VolatileHusk";
            sceneHusk.transform.position = new Vector3(0f, 0.5f, 7f);
            sceneHusk.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));

            // Đặt Crawler gần đó (1.8m cách Husk, nằm trong bán kính nổ 4.0m)
            GameObject oldCrawlerTarget = GameObject.Find("Crawler_AOETarget");
            if (oldCrawlerTarget != null) Object.DestroyImmediate(oldCrawlerTarget);

            GameObject crawlerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Crawler.prefab");
            if (crawlerPrefab != null)
            {
                GameObject sceneCrawler = (GameObject)PrefabUtility.InstantiatePrefab(crawlerPrefab, activeScene);
                sceneCrawler.name = "Crawler_AOETarget";
                // Đặt tại (1.6, 0.5, 7.3) -> cách VolatileHusk 1.6m (nằm gọn trong vùng nổ 4.0m)
                sceneCrawler.transform.position = new Vector3(1.6f, 0.5f, 7.3f);
                sceneCrawler.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, 0f, 0f));
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VolatileHuskSetup] 4/4: Đã đặt VolatileHusk tại (0, 0.5, 7) và Crawler_AOETarget tại (1.6, 0.5, 7.3) vào Scene {activeScene.name} thành công!");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string fullPath = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
