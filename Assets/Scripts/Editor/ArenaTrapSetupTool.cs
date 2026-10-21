using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Environment;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor tự động thiết lập Arena Bounds và CenterRotatingTrap (Sprint 3 - Card 3.1).
    /// </summary>
    public static class ArenaTrapSetupTool
    {
        [MenuItem("Vanguard/Setup Arena Bounds & Rotating Trap")]
        public static void Setup()
        {
            EnsureFolders();

            Material trapMat = GetOrCreateTrapMaterial();

            SetupArenaBounds();

            SetupCenterRotatingTrap(trapMat);

            // Lưu Scene hiện tại
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[ArenaTrapSetupTool] ✅ Hoàn tất thiết lập Arena Bounds & CenterRotatingTrap và đã lưu Scene!");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Environment");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material GetOrCreateTrapMaterial()
        {
            string matPath1 = "Assets/Materials/M_Hazard_Trap.mat";

            Material trapMat = AssetDatabase.LoadAssetAtPath<Material>(matPath1);

            if (trapMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                trapMat = new Material(litShader)
                {
                    name = "M_Hazard_Trap",
                    color = new Color(1f, 0.28f, 0.05f, 1f) // Cam đỏ cảnh báo nguy hiểm
                };

                // Bật Emission nhẹ cho bẫy cơ học phát sáng cảnh báo
                trapMat.EnableKeyword("_EMISSION");
                trapMat.SetColor("_EmissionColor", new Color(1.4f, 0.35f, 0.05f, 1f));

                AssetDatabase.CreateAsset(trapMat, matPath1);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ArenaTrapSetupTool] Đã tạo Material: {matPath1}");
            }

            return trapMat;
        }

        private static void SetupArenaBounds()
        {
            // Tìm đối tượng cũ nếu đã tồn tại để tránh trùng lặp
            GameObject existingBounds = GameObject.Find("ArenaBounds");
            if (existingBounds != null)
            {
                Object.DestroyImmediate(existingBounds);
            }

            GameObject boundsRoot = new GameObject("ArenaBounds");
            boundsRoot.transform.position = Vector3.zero;

            // 4 bức tường vô hình bao quanh sàn 50x50 (Ground scale 5,1,5 từ -25 đến +25)
            CreateWall(boundsRoot.transform, "NorthWall", new Vector3(0f, 2.5f, 25f), new Vector3(50f, 5f, 1f));
            CreateWall(boundsRoot.transform, "SouthWall", new Vector3(0f, 2.5f, -25f), new Vector3(50f, 5f, 1f));
            CreateWall(boundsRoot.transform, "EastWall", new Vector3(25f, 2.5f, 0f), new Vector3(1f, 5f, 50f));
            CreateWall(boundsRoot.transform, "WestWall", new Vector3(-25f, 2.5f, 0f), new Vector3(1f, 5f, 50f));

            Debug.Log("[ArenaTrapSetupTool] Đã thiết lập 4 bức tường ArenaBounds (North, South, East, West).");
        }

        private static void CreateWall(Transform parent, string wallName, Vector3 localPos, Vector3 boxSize)
        {
            GameObject wall = new GameObject(wallName);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;
            wall.transform.localRotation = Quaternion.identity;
            wall.transform.localScale = Vector3.one;

            BoxCollider col = wall.AddComponent<BoxCollider>();
            col.size = boxSize;
            col.center = Vector3.zero;
            col.isTrigger = false; // Tường cản vật lý ngăn rớt khỏi võ đài
        }

        private static void SetupCenterRotatingTrap(Material trapMat)
        {
            // Tìm và xóa phiên bản cũ trên scene nếu có
            GameObject existingTrap = GameObject.Find("CenterRotatingTrap");
            if (existingTrap != null)
            {
                Object.DestroyImmediate(existingTrap);
            }

            // Tạo GameObject gốc tại (0, 0.5, 0)
            GameObject trapGo = new GameObject("CenterRotatingTrap");
            trapGo.transform.position = new Vector3(0f, 0.5f, 0f);
            trapGo.transform.rotation = Quaternion.identity;

            // Rigidbody kinematic để Unity PhysX tính toán Trigger va chạm tối ưu
            Rigidbody rb = trapGo.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = trapGo.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            // Tạo 2 thanh xà ngang (Blades) chữ thập bằng Cube (Scale 12, 0.4, 0.8) ngang dọc, gắn BoxCollider (isTrigger = true)
            GameObject blade1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade1.name = "Blade_Horizontal";
            blade1.transform.SetParent(trapGo.transform, false);
            blade1.transform.localPosition = Vector3.zero;
            blade1.transform.localRotation = Quaternion.identity;
            blade1.transform.localScale = new Vector3(12f, 0.4f, 0.8f);

            BoxCollider col1 = blade1.GetComponent<BoxCollider>();
            col1.isTrigger = true;

            Renderer rend1 = blade1.GetComponent<Renderer>();
            if (rend1 != null) rend1.sharedMaterial = trapMat;

            GameObject blade2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade2.name = "Blade_Vertical";
            blade2.transform.SetParent(trapGo.transform, false);
            blade2.transform.localPosition = Vector3.zero;
            blade2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            blade2.transform.localScale = new Vector3(12f, 0.4f, 0.8f);

            BoxCollider col2 = blade2.GetComponent<BoxCollider>();
            col2.isTrigger = true;

            Renderer rend2 = blade2.GetComponent<Renderer>();
            if (rend2 != null) rend2.sharedMaterial = trapMat;

            // Gắn component CenterRotatingTrap
            CenterRotatingTrap trapComp = trapGo.AddComponent<CenterRotatingTrap>();

            // Cấu hình Serialized Properties
            SerializedObject so = new SerializedObject(trapComp);
            SerializedProperty bladeColsProp = so.FindProperty("bladeColliders");
            if (bladeColsProp != null)
            {
                bladeColsProp.arraySize = 2;
                bladeColsProp.GetArrayElementAtIndex(0).objectReferenceValue = col1;
                bladeColsProp.GetArrayElementAtIndex(1).objectReferenceValue = col2;
            }

            SerializedProperty bladeVisualsProp = so.FindProperty("bladeVisuals");
            if (bladeVisualsProp != null)
            {
                bladeVisualsProp.arraySize = 2;
                bladeVisualsProp.GetArrayElementAtIndex(0).objectReferenceValue = blade1;
                bladeVisualsProp.GetArrayElementAtIndex(1).objectReferenceValue = blade2;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Lưu Prefab để tái sử dụng
            string prefabPath = "Assets/Prefabs/Environment/CenterRotatingTrap.prefab";
            PrefabUtility.SaveAsPrefabAsset(trapGo, prefabPath);
            Debug.Log($"[ArenaTrapSetupTool] Đã tạo Prefab CenterRotatingTrap tại: {prefabPath}");
        }
    }
}
