using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using Vanguard.Combat;
using Vanguard.Enemy;
using UnityEditor.SceneManagement;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tự động tạo Prefab VoidProjectile, VoidCaster và thiết lập Scene.
    /// </summary>
    public static class VoidSetupTool
    {
        [MenuItem("Vanguard/Setup Void Caster & Projectile")]
        public static void Setup()
        {
            // 1. Tạo thư mục nếu chưa có
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Combat"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Combat");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemies"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Material"))
            {
                AssetDatabase.CreateFolder("Assets", "Material");
            }

            // Tạo Material tím Hư Vô cho Void Caster & Projectile
            Material voidMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_Void_Purple.mat");
            if (voidMat == null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                voidMat = new Material(urpShader);
                voidMat.color = new Color(0.55f, 0.1f, 0.95f, 1f);
                AssetDatabase.CreateAsset(voidMat, "Assets/Material/M_Void_Purple.mat");
            }

            Material projMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_Void_Projectile.mat");
            if (projMat == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                projMat = new Material(unlitShader);
                projMat.color = new Color(0.8f, 0.25f, 1f, 1f);
                AssetDatabase.CreateAsset(projMat, "Assets/Material/M_Void_Projectile.mat");
            }

            // 2. Tạo Prefab VoidProjectile:
            // Sphere (Scale 0.35, 0.35, 0.35), SphereCollider (isTrigger = true), component VoidProjectile
            GameObject projGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projGo.name = "VoidProjectile";
            projGo.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            SphereCollider sc = projGo.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
            }

            Renderer projRend = projGo.GetComponent<Renderer>();
            if (projRend != null && projMat != null)
            {
                projRend.sharedMaterial = projMat;
            }

            if (projGo.GetComponent<VoidProjectile>() == null)
            {
                projGo.AddComponent<VoidProjectile>();
            }

            string projPrefabPath = "Assets/Prefabs/Combat/VoidProjectile.prefab";
            GameObject projPrefab = PrefabUtility.SaveAsPrefabAsset(projGo, projPrefabPath);
            Object.DestroyImmediate(projGo);
            Debug.Log($"[VoidSetup] 1/3: Đã tạo Prefab VoidProjectile tại: {projPrefabPath}");

            // 3. Tạo Prefab VoidCaster:
            // Capsule (Scale 0.9, 1.2, 0.9), NavMeshAgent (StoppingDistance = 7.5, Speed = 2.8), CapsuleCollider, Layer = Enemy
            GameObject casterGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            casterGo.name = "VoidCaster";
            casterGo.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                casterGo.layer = enemyLayer;
            }

            Renderer casterRend = casterGo.GetComponent<Renderer>();
            if (casterRend != null && voidMat != null)
            {
                casterRend.sharedMaterial = voidMat;
            }

            NavMeshAgent agent = casterGo.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = casterGo.AddComponent<NavMeshAgent>();
            }
            agent.speed = 2.8f;
            agent.stoppingDistance = 7.5f;

            // Tạo con rỗng FirePoint (local 0, 0.8, 0.6)
            GameObject firePointGo = new GameObject("FirePoint");
            firePointGo.transform.SetParent(casterGo.transform, false);
            firePointGo.transform.localPosition = new Vector3(0f, 0.8f, 0.6f);
            firePointGo.transform.localRotation = Quaternion.identity;

            VoidCasterEnemy casterComp = casterGo.GetComponent<VoidCasterEnemy>();
            if (casterComp == null)
            {
                casterComp = casterGo.AddComponent<VoidCasterEnemy>();
            }

            // Gán firePoint và projectilePrefab
            SerializedObject so = new SerializedObject(casterComp);
            SerializedProperty fpProp = so.FindProperty("firePoint");
            if (fpProp != null) fpProp.objectReferenceValue = firePointGo.transform;

            SerializedProperty ppProp = so.FindProperty("projectilePrefab");
            if (ppProp != null) ppProp.objectReferenceValue = projPrefab;

            so.ApplyModifiedPropertiesWithoutUndo();

            string casterPrefabPath = "Assets/Prefabs/Enemies/VoidCaster.prefab";
            GameObject casterPrefab = PrefabUtility.SaveAsPrefabAsset(casterGo, casterPrefabPath);
            Object.DestroyImmediate(casterGo);
            Debug.Log($"[VoidSetup] 2/3: Đã tạo Prefab VoidCaster tại: {casterPrefabPath}");

            // 4. Đặt 1 con VoidCaster ra Scene tại vị trí (0, 0.6, 8)
            var activeScene = EditorSceneManager.GetActiveScene();
            GameObject existing = GameObject.Find("VoidCaster");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject sceneCaster = (GameObject)PrefabUtility.InstantiatePrefab(casterPrefab, activeScene);
            sceneCaster.name = "VoidCaster";
            sceneCaster.transform.position = new Vector3(0f, 0.6f, 8f);
            sceneCaster.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VoidSetup] 3/3: Đã đặt VoidCaster ra Scene tại (0, 0.6, 8) và lưu Scene {activeScene.name} thành công!");
        }
    }
}
