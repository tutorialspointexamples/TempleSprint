#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TempleSprint.EditorTools
{
    /// <summary>Imports Kenney CC0 character and builds Resources/Characters/CartoonRunner prefab.</summary>
    public static class CartoonRunnerSetup
    {
        const string ArtDir = "Assets/_Project/Art/Characters";
        const string ResDir = "Assets/_Project/Resources/Characters";
        const string PrefabPath = ResDir + "/CartoonRunner.prefab";
        const string ControllerPath = ArtDir + "/CartoonRunner.controller";
        const string ResControllerPath = ResDir + "/CartoonRunnerAnim.controller";
        const string ModelPath = ArtDir + "/characterMedium.fbx";
        const string RunPath = ArtDir + "/run.fbx";
        const string IdlePath = ArtDir + "/idle.fbx";
        const string JumpPath = ArtDir + "/jump.fbx";
        const string SkinPath = ArtDir + "/survivorMaleB.png";

        [MenuItem("Temple Sprint/Setup Cartoon Runner")]
        public static void MenuSetup()
        {
            if (Ensure())
                Debug.Log("[CartoonRunner] Setup complete → " + PrefabPath);
            else
                Debug.LogError("[CartoonRunner] Setup FAILED");
        }

        public static bool Ensure()
        {
            if (!File.Exists(Path.GetFullPath(ModelPath)) || !File.Exists(Path.GetFullPath(RunPath)))
            {
                Debug.LogError("[CartoonRunner] Missing FBX under " + ArtDir);
                return false;
            }

            Directory.CreateDirectory(Path.GetFullPath(ResDir));
            AssetDatabase.Refresh();

            // Each FBX gets its OWN Humanoid avatar (CreateFromThisModel).
            // Muscle-space retargeting then plays run/idle/jump on characterMedium.
            // CopyFromOther fails on this Kenney pack (hierarchy mismatch).
            ConfigureHumanoid(ModelPath, loop: false);
            ConfigureHumanoid(IdlePath, loop: true);
            ConfigureHumanoid(RunPath, loop: true);
            ConfigureHumanoid(JumpPath, loop: false);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(IdlePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RunPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(JumpPath, ImportAssetOptions.ForceUpdate);

            var mediumAvatar = FindAvatar(ModelPath);
            if (mediumAvatar == null || !mediumAvatar.isValid || !mediumAvatar.isHuman)
            {
                Debug.LogError("[CartoonRunner] characterMedium Humanoid Avatar missing/invalid. isHuman=" +
                               (mediumAvatar != null && mediumAvatar.isHuman));
                return false;
            }
            Debug.Log("[CartoonRunner] Medium humanoid OK: " + mediumAvatar.name);

            var idleClip = FindClip(IdlePath);
            var runClip = FindClip(RunPath);
            var jumpClip = FindClip(JumpPath);
            if (runClip == null)
            {
                Debug.LogError("[CartoonRunner] No run AnimationClip in " + RunPath + " (dumping assets)");
                DumpAssets(RunPath);
                return false;
            }
            Debug.Log($"[CartoonRunner] Clips idle={(idleClip != null ? idleClip.name : "null")} run={runClip.name} jump={(jumpClip != null ? jumpClip.name : "null")}");

            SetClipLoop(idleClip, true);
            SetClipLoop(runClip, true);
            SetClipLoop(jumpClip, false);

            var controller = BuildController(idleClip, runClip, jumpClip);
            if (File.Exists(Path.GetFullPath(ResControllerPath)))
                AssetDatabase.DeleteAsset(ResControllerPath);
            AssetDatabase.CopyAsset(ControllerPath, ResControllerPath);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
            {
                Debug.LogError("[CartoonRunner] Failed to load " + ModelPath);
                return false;
            }

            var instance = Object.Instantiate(source);
            instance.name = "CartoonRunner";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            int rendCount = CountRenderers(instance);
            Debug.Log($"[CartoonRunner] characterMedium renderers={rendCount}");
            if (rendCount == 0)
            {
                Debug.LogError("[CartoonRunner] characterMedium has 0 renderers");
                Object.DestroyImmediate(instance);
                return false;
            }

            float height = MeasureRendererHeight(instance);
            if (height > 0.05f && height < 10f)
                instance.transform.localScale = Vector3.one * (1.85f / height);
            else
                instance.transform.localScale = Vector3.one * 1.2f;

            foreach (var legacy in instance.GetComponentsInChildren<Animation>(true))
                Object.DestroyImmediate(legacy);

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = mediumAvatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;

            ApplySkin(instance);
            ForceSkinnedVisible(instance);

            animator.Play("Run", 0, 0.25f);
            animator.Update(0.02f);
            float animH = MeasureRendererHeight(instance);
            Debug.Log($"[CartoonRunner] Sampled Run height={animH:0.00}");
            if (animH > 5f || animH < 0.4f)
            {
                Debug.LogError($"[CartoonRunner] Humanoid sample still bad ({animH:0.00})");
                Object.DestroyImmediate(instance);
                return false;
            }

            animator.Play("Run", 0, 0f);
            animator.Update(0f);

            if (File.Exists(Path.GetFullPath(PrefabPath)))
                AssetDatabase.DeleteAsset(PrefabPath);

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var check = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            int checkR = check != null ? CountRenderers(check) : 0;
            var checkAnim = check != null ? check.GetComponent<Animator>() : null;
            bool hasCtrl = checkAnim != null && checkAnim.runtimeAnimatorController != null;
            Debug.Log($"[CartoonRunner] Saved prefab renderers={checkR} animator={(hasCtrl ? 1 : 0)}");
            return check != null && checkR > 0 && hasCtrl;
        }

        static void DumpAssets(string fbxPath)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (a != null)
                    Debug.Log($"  asset type={a.GetType().Name} name={a.name}");
            }
        }

        static int CountRenderers(GameObject go)
        {
            if (go == null) return 0;
            return go.GetComponentsInChildren<Renderer>(true).Length;
        }

        static Avatar FindAvatar(string fbxPath)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (a is Avatar av) return av;
            }
            return null;
        }

        static void ConfigureHumanoid(string path, bool loop)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            ApplyClipLocks(importer, loop);
            importer.SaveAndReimport();
        }

        static void ApplyClipLocks(ModelImporter importer, bool loop)
        {
            try
            {
                var defaults = importer.defaultClipAnimations;
                if (defaults == null || defaults.Length == 0) return;
                for (int i = 0; i < defaults.Length; i++)
                {
                    defaults[i].loopTime = loop;
                    defaults[i].lockRootRotation = true;
                    defaults[i].lockRootHeightY = true;
                    defaults[i].lockRootPositionXZ = true;
                    defaults[i].keepOriginalOrientation = true;
                    defaults[i].keepOriginalPositionY = true;
                    defaults[i].keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = defaults;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CartoonRunner] clip lock setup: " + e.Message);
            }
        }

        static void SetClipLoop(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static AnimationClip FindClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip best = null;
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    string n = clip.name.ToLowerInvariant();
                    if (n.Contains("run") || n.Contains("idle") || n.Contains("jump") || n.Contains("mixamo"))
                        return clip;
                    best ??= clip;
                }
            }
            return best;
        }

        static AnimatorController BuildController(AnimationClip idle, AnimationClip run, AnimationClip jump)
        {
            if (File.Exists(Path.GetFullPath(ControllerPath)))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Running", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Slide", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle", new Vector3(200, 0, 0));
            idleState.motion = idle != null ? idle : run;
            var runState = sm.AddState("Run", new Vector3(200, 80, 0));
            runState.motion = run;
            var jumpState = sm.AddState("Jump", new Vector3(420, 40, 0));
            jumpState.motion = jump != null ? jump : run;
            var slideState = sm.AddState("Slide", new Vector3(200, 160, 0));
            slideState.motion = jump != null ? jump : run;
            sm.defaultState = runState;

            var toRun = idleState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.If, 0, "Running");
            toRun.hasExitTime = false; toRun.duration = 0.1f;

            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");
            toIdle.hasExitTime = false; toIdle.duration = 0.1f;

            var anyJump = sm.AddAnyStateTransition(jumpState);
            anyJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            anyJump.hasExitTime = false; anyJump.duration = 0.05f;

            var jumpToRun = jumpState.AddTransition(runState);
            jumpToRun.AddCondition(AnimatorConditionMode.If, 0, "Running");
            jumpToRun.hasExitTime = true; jumpToRun.exitTime = 0.85f; jumpToRun.duration = 0.1f;

            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");
            jumpToIdle.hasExitTime = true; jumpToIdle.exitTime = 0.85f; jumpToIdle.duration = 0.1f;

            var runToSlide = runState.AddTransition(slideState);
            runToSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            runToSlide.hasExitTime = false; runToSlide.duration = 0.08f;

            var idleToSlide = idleState.AddTransition(slideState);
            idleToSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            idleToSlide.hasExitTime = false; idleToSlide.duration = 0.08f;

            var slideToRun = slideState.AddTransition(runState);
            slideToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "Slide");
            slideToRun.AddCondition(AnimatorConditionMode.If, 0, "Running");
            slideToRun.hasExitTime = false; slideToRun.duration = 0.1f;

            var slideToIdle = slideState.AddTransition(idleState);
            slideToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Slide");
            slideToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");
            slideToIdle.hasExitTime = false; slideToIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static float MeasureRendererHeight(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return 0f;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f && r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                    b = smr.sharedMesh.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                minY = Mathf.Min(minY, b.min.y);
                maxY = Mathf.Max(maxY, b.max.y);
                any = true;
            }
            return any ? Mathf.Max(0.01f, maxY - minY) : 0f;
        }

        static void ApplySkin(GameObject go)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinPath);
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null) return;

            string matPath = ArtDir + "/CartoonRunnerSkin.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "CartoonRunnerSkin" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            }
            var tint = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            mat.color = tint;
            EditorUtility.SetDirty(mat);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.sharedMaterial = mat;
            }
        }

        static void ForceSkinnedVisible(GameObject go)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                smr.enabled = true;
                if (smr.sharedMesh != null)
                    smr.localBounds = smr.sharedMesh.bounds;
            }
        }
    }
}
#endif
