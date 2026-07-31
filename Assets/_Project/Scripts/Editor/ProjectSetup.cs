#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TempleSprint.EditorTools
{
    public static class ProjectSetup
    {
        const string UrpAssetPath = "Assets/_Project/Settings/URP-TempleSprint.asset";
        const string RendererPath = "Assets/_Project/Settings/URP-Renderer.asset";
        const string MainScene = "Assets/_Project/Scenes/Main.unity";
        const string SdkConfigPath = "Assets/_Project/Resources/SdkConfig.asset";

        [MenuItem("Temple Sprint/Setup Project")]
        public static void Setup()
        {
            EnsureFolders();
            EnsureUrp();
            EnsureSdkConfig();
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScene, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Temple Sprint setup complete. See Docs/SDK_SETUP.md for AdMob/UGS/IAP dashboard steps.");
        }

        [MenuItem("Temple Sprint/Open Main Scene")]
        public static void OpenMain()
        {
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        }

        /// <summary>Open Main and enter Play Mode â€” use this from local launchers.</summary>
        [MenuItem("Temple Sprint/Play Now")]
        public static void OpenAndPlay()
        {
            CartoonRunnerSetup.Ensure();
            if (!EditorSceneManager.GetActiveScene().path.EndsWith("Main.unity"))
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.EnterPlaymode();
        }

        /// <summary>Headless smoke: compile, open Main, Play, auto-run, write Logs/play_verify.txt, quit.</summary>
        public static void BatchSmokePlay()
        {
            try
            {
                Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")));
                CartoonRunnerSetup.Ensure();
                if (!EditorSceneManager.GetActiveScene().path.EndsWith("Main.unity"))
                    EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

                // AutoPlayVerifier owns the in-play assertions: entering Play Mode
                // reloads the domain, which would drop an EditorApplication.update
                // callback registered from here.
                AutoPlayVerifier.Arm(true, AutoPlayVerifier.VerifyMode.Route);
                EditorApplication.EnterPlaymode();
            }
            catch (System.Exception e)
            {
                WriteSmoke("FAIL exception=" + e.Message);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Headless check that pickups are consumed and hazards end the run.</summary>
        public static void BatchGameplayCheck()
        {
            try
            {
                Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")));
                CartoonRunnerSetup.Ensure();
                if (!EditorSceneManager.GetActiveScene().path.EndsWith("Main.unity"))
                    EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

                AutoPlayVerifier.Arm(true, AutoPlayVerifier.VerifyMode.Gameplay);
                EditorApplication.EnterPlaymode();
            }
            catch (System.Exception e)
            {
                WriteSmoke("FAIL exception=" + e.Message);
                EditorApplication.Exit(1);
            }
        }


        static void WriteSmoke(string line)
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/play_verify.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, line + "\n" + System.DateTime.Now.ToString("o"));
            }
            catch (System.Exception e) { Debug.LogWarning(e.Message); }
        }

        [MenuItem("Temple Sprint/Create SDK Config")]
        public static void EnsureSdkConfig()
        {
            CreateFolder("Assets/_Project", "Resources");
            var existing = AssetDatabase.LoadAssetAtPath<SdkConfig>(SdkConfigPath);
            if (existing == null)
            {
                var cfg = ScriptableObject.CreateInstance<SdkConfig>();
                AssetDatabase.CreateAsset(cfg, SdkConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created " + SdkConfigPath + " with Google test ad unit IDs.");
            }
        }

        static void EnsureFolders()
        {
            CreateFolder("Assets", "_Project");
            CreateFolder("Assets/_Project", "Settings");
            CreateFolder("Assets/_Project", "Scenes");
            CreateFolder("Assets/_Project", "Scripts");
            CreateFolder("Assets/_Project", "Prefabs");
            CreateFolder("Assets/_Project", "Art");
            CreateFolder("Assets/_Project", "ScriptableObjects");
            CreateFolder("Assets/_Project", "Audio");
            CreateFolder("Assets/_Project", "Resources");
            CreateFolder("Assets", "Docs");
        }

        static void CreateFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureUrp()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, UrpAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Temple Sprint/Reimport Project Scripts")]
        public static void ReimportScripts()
        {
            AssetDatabase.ImportAsset("Assets/_Project/Scripts", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("[Temple Sprint] Reimported Assets/_Project/Scripts â€” wait for compile, then Play.");
        }

        /// <summary>Batchmode smoke test: open Main, enter Play, assert GameManager+UI exist.</summary>
        public static void VerifyPlayBoot()
        {
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            if (!EditorApplication.isPlaying)
                EditorApplication.EnterPlaymode();

            double start = EditorApplication.timeSinceStartup;
            EditorApplication.update += WaitAndCheck;

            void WaitAndCheck()
            {
                if (!EditorApplication.isPlaying) return;
                if (EditorApplication.timeSinceStartup - start < 2.5) return;
                EditorApplication.update -= WaitAndCheck;

                var gm = Object.FindAnyObjectByType<GameManager>();
                var ui = Object.FindAnyObjectByType<GameUI>();
                int kids = gm != null ? gm.transform.childCount : -1;
                bool ok = gm != null && ui != null && kids >= 4;
                string msg = ok
                    ? $"[VERIFY] PASS GameManager kids={kids} UI={ui.name} State={gm.State}"
                    : $"[VERIFY] FAIL gm={(gm != null)} ui={(ui != null)} kids={kids}";
                Debug.Log(msg);
                if (EditorApplication.isPlaying)
                    EditorApplication.ExitPlaymode();

                // Defer quit so ExitPlaymode can settle
                EditorApplication.delayCall += () =>
                {
                    if (ok) EditorApplication.Exit(0);
                    else EditorApplication.Exit(1);
                };
            }
        }
    }

    [InitializeOnLoad]
    static class PlayModeBootGuard
    {
        static PlayModeBootGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            // AutoPlayVerifier owns Logs/play_verify.txt when armed — do not clobber it.
            if (SessionState.GetBool("TempleSprint.PlayVerify.Active", false)) return;
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("TempleSprint.PlayVerify.Active", false)) return;
                var gm = Object.FindAnyObjectByType<GameManager>();
                var ui = Object.FindAnyObjectByType<GameUI>();
                int kids = gm != null ? gm.transform.childCount : -1;
                bool ok = gm != null && ui != null && kids >= 4;
                string line = ok
                    ? $"PASS kids={kids} state={gm.State} ui={ui.name}"
                    : $"FAIL gm={gm != null} ui={ui != null} kids={kids}";
                try
                {
                    System.IO.Directory.CreateDirectory("Logs");
                    System.IO.File.WriteAllText("Logs/play_verify.txt", line + "\n" + System.DateTime.Now.ToString("o"));
                }
                catch { /* ignore */ }
                if (gm == null)
                    Debug.LogError("[Temple Sprint] No GameManager after Enter Play â€” scripts may be missing. Run Temple Sprint â†’ Reimport Project Scripts.");
                else
                    Debug.Log($"[Temple Sprint] Boot OK â€” {line}");
            };
        }
    }
}
#endif
