using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Juice;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Công cụ Editor thiết lập JuiceManager và CameraShaker trong Scene (Sprint 5 - Card 5.3).
    /// </summary>
    public static class CombatJuiceSetupTool
    {
        [MenuItem("Vanguard/Setup Combat Juiciness & Screen Shake")]
        public static void Setup()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[CombatJuiceSetupTool] Không tìm thấy Main Camera (tag MainCamera) trong Scene!");
                return;
            }

            CameraShaker shaker = mainCamera.GetComponent<CameraShaker>();
            if (shaker == null)
            {
                shaker = mainCamera.gameObject.AddComponent<CameraShaker>();
            }

            JuiceManager manager = Object.FindAnyObjectByType<JuiceManager>();
            if (manager == null)
            {
                manager = new GameObject("JuiceManager").AddComponent<JuiceManager>();
            }

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("cameraShaker").objectReferenceValue = shaker;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(shaker);

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[CombatJuiceSetupTool] ✅ Đã thiết lập JuiceManager, gắn CameraShaker lên '{mainCamera.name}' và lưu Scene!");
        }
    }
}
