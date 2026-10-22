#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>Sprint 8 - 8.3: tạo VfxLibrary, VfxManager trong scene và gắn CombatVfxController lên Player.</summary>
    public static class VfxSetupTool
    {
        private const string LibraryPath = "Assets/Data/VfxLibrary.asset";

        [MenuItem("Vanguard/Graphics/Setup VFX Manager")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");

            VfxLibrarySO library = AssetDatabase.LoadAssetAtPath<VfxLibrarySO>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrarySO>();
                library.entries = new[]
                {
                    new VfxLibrarySO.Entry { id = VfxId.DashTrail, poolSize = 1, lifetime = 1f },
                    new VfxLibrarySO.Entry { id = VfxId.Slash, poolSize = 4, lifetime = 0.6f },
                    new VfxLibrarySO.Entry { id = VfxId.SoulBurst, poolSize = 2, lifetime = 1.5f },
                    new VfxLibrarySO.Entry { id = VfxId.Impact, poolSize = 12, lifetime = 0.6f },
                };
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            VfxManager manager = Object.FindAnyObjectByType<VfxManager>();
            if (manager == null)
            {
                GameObject go = new GameObject("VfxManager");
                Undo.RegisterCreatedObjectUndo(go, "Setup VFX Manager");
                manager = go.AddComponent<VfxManager>();
            }
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("library").objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject player = GameObject.Find("Player");
            if (player != null && player.GetComponent<CombatVfxController>() == null)
            {
                Undo.AddComponent<CombatVfxController>(player);
            }

            int missing = 0;
            foreach (VfxLibrarySO.Entry e in library.entries)
            {
                if (e.asset == null) { missing++; Debug.Log($"[VFX] Chưa gán graph cho {e.id}."); }
            }

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            Selection.activeObject = library;
            Debug.Log($"[VFX] Đã tạo VfxManager + CombatVfxController. Graph còn thiếu: {missing}/{library.entries.Length}. Gán VisualEffectAsset vào Assets/Data/VfxLibrary.");
        }
    }
}
#endif
