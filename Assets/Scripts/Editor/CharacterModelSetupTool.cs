#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vanguard.Graphics;

namespace Vanguard.EditorTools
{
    /// <summary>
    /// Sprint 7 (7.1/7.2): cấu hình import FBX (Generic + Avatar dùng chung), tạo AnimatorController,
    /// material NeonLit và gắn model vào Player / prefab quái, thay cho placeholder.
    /// </summary>
    public static class CharacterModelSetupTool
    {
        private struct StateSpec
        {
            public string state;
            public string clipFile;
            public float targetDuration; // 0 = giữ nguyên tốc độ clip
            public StateSpec(string state, string clipFile, float targetDuration)
            {
                this.state = state; this.clipFile = clipFile; this.targetDuration = targetDuration;
            }
        }

        private class CharSpec
        {
            public string id, folder, mainFile, targetPath;
            public bool isPlayer;
            public float height;
            public string[] loopClips;
            public StateSpec[] states;
            public System.Func<ArtPaletteSO, Color> color;
        }

        private static readonly CharSpec[] Specs =
        {
            new CharSpec
            {
                id = "Player", folder = "Player", mainFile = "Player", isPlayer = true, height = 2.2f,
                loopClips = new[] { "Idle", "Run" },
                color = p => p.playerCyan,
                states = new[]
                {
                    new StateSpec("Idle", "Idle", 0), new StateSpec("Run", "Run", 0),
                    new StateSpec("Dash", "Dash", 0.35f), new StateSpec("Combo", "Combo", 1.2f),
                    new StateSpec("SoulBurst", "SoulBurst", 1f), new StateSpec("Death", "Death", 1.5f),
                }
            },
            new CharSpec
            {
                id = "Crawler", folder = "Crawler", mainFile = "Crawler", targetPath = "Assets/Prefabs/Enemies/Crawler.prefab", height = 1.3f,
                loopClips = new[] { "Walk" },
                color = p => p.enemyRed,
                states = new[]
                {
                    new StateSpec("Idle", "Walk", 0), new StateSpec("Move", "Walk", 0),
                    new StateSpec("Attack", "Attack", 0.9f), new StateSpec("Hit", "Hit", 0.35f), new StateSpec("Death", "Death", 1.2f),
                }
            },
            new CharSpec
            {
                id = "VoidCaster", folder = "VoidCaster", mainFile = "VoidCaster", targetPath = "Assets/Prefabs/Enemies/VoidCaster.prefab", height = 2.6f,
                loopClips = new[] { "Idle" },
                color = p => p.voidPurple,
                states = new[]
                {
                    new StateSpec("Idle", "Idle", 0), new StateSpec("Move", "Idle", 0),
                    new StateSpec("Attack", "Attack", 1.0f), new StateSpec("Hit", "Hit", 0.35f), new StateSpec("Death", "Death", 1.2f),
                }
            },
            new CharSpec
            {
                id = "VolatileHusk", folder = "VolatileHusk", mainFile = "VolatileHusk", targetPath = "Assets/Prefabs/Enemies/VolatileHusk.prefab", height = 1.8f,
                loopClips = new[] { "Run" },
                color = p => p.huskOrange,
                states = new[]
                {
                    new StateSpec("Idle", "Run", 0), new StateSpec("Move", "Run", 0),
                    new StateSpec("Attack", "Charge", 1.0f), new StateSpec("Hit", "Run", 0.3f), new StateSpec("Death", "Explode", 1.0f),
                }
            },
        };

        [MenuItem("Vanguard/Graphics/Setup Character Models")]
        public static void SetupAll()
        {
            ArtPaletteSO palette = AssetDatabase.LoadAssetAtPath<ArtPaletteSO>("Assets/Data/ArtPalette.asset");
            if (palette == null) palette = ScriptableObject.CreateInstance<ArtPaletteSO>();

            Shader neon = Shader.Find("Vanguard/NeonLit");
            if (neon == null) { Debug.LogError("[CharModels] Không tìm thấy shader Vanguard/NeonLit."); return; }

            EnsureFolder("Assets/Animation");

            foreach (CharSpec spec in Specs)
            {
                string mainPath = $"Assets/Models/{spec.folder}/{spec.mainFile}.fbx";
                GameObject mainAsset = AssetDatabase.LoadAssetAtPath<GameObject>(mainPath);
                if (mainAsset == null) { Debug.LogError($"[CharModels] Thiếu {mainPath}"); continue; }

                Avatar avatar = ConfigureMain(mainPath);
                Dictionary<string, AnimationClip> clips = ConfigureClips(spec, avatar);
                AnimatorController controller = BuildController(spec, clips);
                Material[] mats = BuildMaterials(spec, mainAsset, neon, spec.color(palette));
                float bottomHeight = MeasureHeight(mainAsset, out float minY);

                if (spec.isPlayer)
                {
                    GameObject player = GameObject.Find("Player");
                    if (player == null) { Debug.LogError("[CharModels] Không tìm thấy GameObject 'Player' trong scene."); continue; }
                    Apply(player, spec, mainAsset, avatar, controller, mats, bottomHeight, minY, true);
                    EditorSceneManager.MarkSceneDirty(player.scene);
                }
                else
                {
                    GameObject contents = PrefabUtility.LoadPrefabContents(spec.targetPath);
                    Apply(contents, spec, mainAsset, avatar, controller, mats, bottomHeight, minY, false);
                    PrefabUtility.SaveAsPrefabAsset(contents, spec.targetPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                Debug.Log($"[CharModels] Đã gắn model cho {spec.id}.");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[CharModels] Hoàn tất. Bấm Play để kiểm tra.");
        }

        private static Avatar ConfigureMain(string path)
        {
            ModelImporter imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        }

        private static Dictionary<string, AnimationClip> ConfigureClips(CharSpec spec, Avatar avatar)
        {
            Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
            foreach (string clipFile in spec.states.Select(s => s.clipFile).Distinct())
            {
                string path = $"Assets/Models/{spec.folder}/{clipFile}.fbx";
                ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { Debug.LogWarning($"[CharModels] Thiếu clip {path}"); continue; }

                bool loop = spec.loopClips.Contains(clipFile);
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;

                ModelImporterClipAnimation[] defaults = imp.defaultClipAnimations;
                if (defaults.Length > 0)
                {
                    ModelImporterClipAnimation c = defaults[0];
                    c.name = clipFile;
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    imp.clipAnimations = new[] { c };
                }
                imp.SaveAndReimport();

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(a => !a.name.StartsWith("__preview"));
                if (clip != null) result[clipFile] = clip;
            }
            return result;
        }

        private static AnimatorController BuildController(CharSpec spec, Dictionary<string, AnimationClip> clips)
        {
            string path = $"Assets/Animation/{spec.id}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            bool first = true;
            foreach (StateSpec s in spec.states)
            {
                if (!clips.TryGetValue(s.clipFile, out AnimationClip clip)) continue;
                AnimatorState st = sm.AddState(s.state);
                st.motion = clip;
                st.speed = s.targetDuration > 0f && clip.length > 0.01f ? clip.length / s.targetDuration : 1f;
                if (first) { sm.defaultState = st; first = false; }
            }
            return controller;
        }

        private static Material[] BuildMaterials(CharSpec spec, GameObject mainAsset, Shader neon, Color faction)
        {
            SkinnedMeshRenderer[] smrs = mainAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            List<Material> originals = smrs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
            if (originals.Count == 0) originals.Add(null);

            List<Material> result = new List<Material>();
            for (int i = 0; i < originals.Count; i++)
            {
                string path = $"Assets/Materials/M_{spec.id}_{i}.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(neon);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = neon;

                Material src = originals[i];
                Texture tex = src != null && src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
                if (tex == null && src != null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", tex != null ? Color.white : new Color(0.1f, 0.06f, 0.14f, 1f));
                m.SetColor("_EmissionColor", faction * 0.25f);
                m.SetColor("_RimColor", faction);
                m.SetColor("_FlashColor", new Color(4f, 4f, 4f, 1f));
                EditorUtility.SetDirty(m);
                result.Add(m);
            }
            return result.ToArray();
        }

        private static float MeasureHeight(GameObject asset, out float minY)
        {
            Renderer[] rs = asset.GetComponentsInChildren<Renderer>(true);
            minY = 0f;
            if (rs.Length == 0) return 1f;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            minY = b.min.y;
            return Mathf.Max(0.01f, b.size.y);
        }

        private static float ColliderBottomLocal(GameObject root)
        {
            Collider c = root.GetComponent<Collider>();
            switch (c)
            {
                case CapsuleCollider cap: return cap.center.y - cap.height * 0.5f;
                case SphereCollider sph: return sph.center.y - sph.radius;
                case BoxCollider box: return box.center.y - box.size.y * 0.5f;
                default: return 0f;
            }
        }

        private static void Apply(GameObject root, CharSpec spec, GameObject mainAsset, Avatar avatar,
            AnimatorController controller, Material[] mats, float assetHeight, float assetMinY, bool isScene)
        {
            Transform old = root.transform.Find("Model");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Gỡ mesh placeholder trên root (giữ Collider/Rigidbody/NavMeshAgent)
            MeshRenderer placeholderRenderer = root.GetComponent<MeshRenderer>();
            if (placeholderRenderer != null) Object.DestroyImmediate(placeholderRenderer);
            MeshFilter placeholderFilter = root.GetComponent<MeshFilter>();
            if (placeholderFilter != null) Object.DestroyImmediate(placeholderFilter);

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(mainAsset, root.transform);
            model.name = "Model";

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            StateAnimator stateAnimator = model.GetComponent<StateAnimator>();
            if (stateAnimator == null) stateAnimator = model.AddComponent<StateAnimator>();

            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] slots = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = mats[Mathf.Min(i, mats.Length - 1)];
                smr.sharedMaterials = slots;
                smr.updateWhenOffscreen = false;
            }

            Vector3 rs = root.transform.lossyScale;
            float s = spec.height / assetHeight;
            model.transform.localScale = new Vector3(s / rs.x, s / rs.y, s / rs.z);
            float bottom = ColliderBottomLocal(root);
            model.transform.localPosition = new Vector3(0f, bottom - assetMinY * s / rs.y, 0f);
            model.transform.localRotation = Quaternion.identity;

            if (spec.isPlayer)
            {
                PlayerAnimationDriver driver = root.GetComponent<PlayerAnimationDriver>();
                if (driver == null) driver = root.AddComponent<PlayerAnimationDriver>();
                SetRef(driver, "model", stateAnimator);
            }
            else
            {
                EnemyAnimationDriver driver = root.GetComponent<EnemyAnimationDriver>();
                if (driver == null) driver = root.AddComponent<EnemyAnimationDriver>();
                SetRef(driver, "model", stateAnimator);

                Vanguard.Enemy.EnemyBase enemy = root.GetComponent<Vanguard.Enemy.EnemyBase>();
                if (enemy != null)
                {
                    SerializedObject so = new SerializedObject(enemy);
                    SerializedProperty linger = so.FindProperty("deathLingerTime");
                    if (linger != null) { linger.floatValue = 1.2f; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
            }
        }

        private static void SetRef(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
        }
    }
}
#endif
