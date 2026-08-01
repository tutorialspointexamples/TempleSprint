using UnityEngine;
using System;
using System.Collections;

namespace TempleSprint
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public RunDifficulty CurrentDifficulty { get; private set; } = RunDifficulty.Medium;

        PlayerController _player;
        GuardianAI _guardian;
        TileSpawner _spawner;
        DifficultyDirector _difficulty;
        RunSession _session;
        PowerUpController _powers;
        ChaseCamera _camera;
        EnvironmentEffects _env;
        GhostRivalRunner _ghost;
        float _tutorialTimer;
        int _tutorialStep = -1;
        RunEndPayload _lastPayload;
        bool _bootstrappedIntoMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            try
            {
                if (FindAnyObjectByType<GameManager>() != null) return;
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
            catch (Exception e)
            {
                Debug.LogError("[GameManager] AutoBootstrap failed: " + e);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try
            {
                BuildWorld();
                Debug.Log("[GameManager] World built OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[GameManager] BuildWorld failed: " + e);
            }
        }

        void BuildWorld()
        {
            try { SdkBootstrap.Ensure(); }
            catch (Exception e)
            {
                Debug.LogWarning("[GameManager] SDK bootstrap skipped: " + e.Message);
            }

            try
            {
                MetaProgress.Ensure();
                RemoteConfigService.Refresh();
                BiomeSystem.LoadSaved();
                MissionSystem.EnsureDaily();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameManager] Meta init: " + e.Message);
            }

            var sun = new GameObject("Sun");
            sun.transform.SetParent(transform);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.88f, 0.65f);
            light.intensity = 1.55f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            var systems = new GameObject("Systems");
            systems.transform.SetParent(transform);
            systems.AddComponent<SwipeInput>();
            systems.AddComponent<AudioHooks>();
            _session = systems.AddComponent<RunSession>();
            _difficulty = systems.AddComponent<DifficultyDirector>();
            _difficulty.Bind(_session);
            _powers = systems.AddComponent<PowerUpController>();
            _spawner = systems.AddComponent<TileSpawner>();
            _env = systems.AddComponent<EnvironmentEffects>();
            WaterFlow.Ensure();

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform);
            _player = playerGo.AddComponent<PlayerController>();

            var guardianGo = new GameObject("Guardian");
            guardianGo.transform.SetParent(transform);
            _guardian = guardianGo.AddComponent<GuardianAI>();

            _ghost = GhostRivalRunner.Ensure(transform);

            var nature = NatureBackdrop.Ensure(transform);
            nature.SetFollow(_player.transform);

            Camera[] cams = FindObjectsByType<Camera>();
            GameObject camGo = null;
            foreach (var c in cams)
            {
                if (camGo == null) camGo = c.gameObject;
                else Destroy(c.gameObject);
            }
            if (camGo == null) camGo = new GameObject("ChaseCamera");
            camGo.name = "ChaseCamera";
            camGo.transform.SetParent(transform);
            foreach (var al in FindObjectsByType<AudioListener>())
            {
                if (al.gameObject != camGo) Destroy(al);
            }
            if (camGo.GetComponent<Camera>() == null) camGo.AddComponent<Camera>();
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            _camera = camGo.GetComponent<ChaseCamera>() ?? camGo.AddComponent<ChaseCamera>();
            _camera.SetTarget(_player.transform);
            _camera.SnapNow();
            nature.AttachVineCurtain(camGo.transform);

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform);
            uiGo.AddComponent<GameUI>();
            UiFactory.EnsureEventSystem();
            var es = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es != null) es.transform.SetParent(transform);

            SetRunActorsVisible(true);
            _spawner?.ShowMenuPreview();
            _player?.ResetAtStart();
            _player?.ApplyCharacterColors();
            _camera?.SnapNow();
            _session.OnRunEnded += HandleRunEnded;
        }

        void Start()
        {
            try
            {
                if (_player != null)
                {
                    _player.BindInput();
                    GameSettings.ApplyToInput();
                }
                AuthService.ContinueAsGuest();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameManager] Start: " + e.Message);
            }
            State = GameState.Boot;
            // Open menu so the player can pick Easy / Medium / Hard
            StartCoroutine(BootToMenu());
        }

        IEnumerator BootToMenu()
        {
            yield return null;
            if (_bootstrappedIntoMenu) yield break;
            _bootstrappedIntoMenu = true;
            if (_player == null || _spawner == null)
            {
                Debug.LogError("[GameManager] Boot aborted — world missing");
                yield break;
            }
            EnterMainMenu();
            Debug.Log("[GameManager] Boot → Main Menu (choose difficulty)");
        }

        public static int CountLiveRenderers()
        {
            var rends = FindObjectsByType<Renderer>();
            int n = 0;
            foreach (var r in rends)
                if (r != null && r.enabled && r.gameObject.activeInHierarchy) n++;
            return n;
        }

        void Update()
        {
            if (State == GameState.Running && _session != null && _session.IsAlive && _player != null && _difficulty != null)
            {
                _player.TickMovement(_difficulty.CurrentSpeed);
                UpdateTutorial();
            }
        }

        public void EnterMainMenu()
        {
            StopCoroutineSafe(_openingCo);
            _openingCo = null;
            State = GameState.MainMenu;
            Time.timeScale = 1f;
            _powers?.ClearTimers();
            SetRunActorsVisible(true);
            _guardian?.Stop();
            if (_guardian != null) _guardian.gameObject.SetActive(false);
            _ghost?.Stop();
            _env?.ResetEffects();
            BiomeSystem.ClearRunTransition();
            AudioHooks.Instance?.StopAmbience();
            _camera?.ClearCinematic();
            GameUI.Instance?.HideOpening();
            _spawner?.ShowMenuPreview();
            _player?.ResetAtStart();
            _player?.ApplyCharacterColors();
            _camera?.SnapNow();
            GameUI.Instance?.ShowMainMenu();
        }

        public void StartRun() => StartRun(CurrentDifficulty);

        public void StartRun(RunDifficulty difficulty)
        {
            if (_player == null || _session == null || _spawner == null || _guardian == null || _difficulty == null)
            {
                Debug.LogError("[GameManager] Cannot StartRun — world not fully built");
                return;
            }

            CurrentDifficulty = difficulty;

            if (!_session.IsAlive && !_session.IsFinalized)
                _session.FinalizeAndBank();

            StopCoroutineSafe(_openingCo);
            _openingSkip = false;
            _openingCo = StartCoroutine(PlayOpeningThenRun(difficulty));
        }

        Coroutine _openingCo;
        bool _openingSkip;

        void StopCoroutineSafe(Coroutine co)
        {
            if (co != null) StopCoroutine(co);
        }

        IEnumerator PlayOpeningThenRun(RunDifficulty difficulty)
        {
            var meta = MetaProgress.Ensure();
            bool tutorial = !meta.Data.tutorialCompleted;
            BiomeSystem.ClearRunTransition();
            BiomeSystem.ApplyLighting();

            State = GameState.Opening;
            SetRunActorsVisible(true);
            if (_guardian != null) _guardian.gameObject.SetActive(true);
            _difficulty.ResetDifficulty(difficulty);
            _powers.ResetForRun();
            _env?.ResetEffects();
            _player.ResetAtStart();
            _player.ApplyCharacterColors();
            _spawner.BeginRun(tutorial, difficulty);
            _ghost?.Stop();
            _guardian.BeginOpeningPose();

            // Pedestal idol ahead of the runner for the theft beat.
            Transform pedestal = IdolVisualFactory.BuildPedestal(_player.transform, new Vector3(0f, 0.05f, 2.4f));
            Transform idol = IdolVisualFactory.BuildStolenIdol(_player.transform, new Vector3(0f, 0.85f, 2.4f), 1.1f);
            _player.BeginOutcomeDeath(RunnerOutcomePose.IdolReach);

            Vector3 camPos = _player.transform.position + _player.transform.right * 2.2f
                             + Vector3.up * 2.1f - _player.transform.forward * 1.2f;
            Vector3 look = _player.transform.position + Vector3.up * 1.2f + _player.transform.forward * 1.6f;
            _camera?.SetCinematicPose(camPos, look, 50f);
            GameUI.Instance?.ShowOpening("IDOL STOLEN!");
            AudioHooks.Instance?.PlayPickup();

            float t = 0f;
            const float openDur = 1.35f;
            bool snatched = false;
            while (t < openDur)
            {
                if (ConsumeOpeningSkip()) break;
                t += Time.unscaledDeltaTime;
                // Idol snatch mid-beat — keep the prop for the early run (genre "stolen treasure").
                if (!snatched && t > 0.55f && idol != null)
                {
                    snatched = true;
                    _player.AttachStolenIdol(idol);
                    idol = null;
                    AudioHooks.Instance?.PlayGem();
                    _camera?.PunchFov(4f);
                }
                yield return null;
            }

            // Skip path still needs the idol on the runner.
            if (!snatched && idol != null)
            {
                _player.AttachStolenIdol(idol);
                idol = null;
            }
            if (pedestal != null) Destroy(pedestal.gameObject);
            _player.ClearOutcomePose();
            _camera?.ClearCinematic();
            GameUI.Instance?.HideOpening();

            // Live run begins after the theft framing.
            _session.Begin();
            _guardian.StartChaseFromOpening();
            _ghost?.BeginRun();
            State = GameState.Running;

            if (meta.ConsumeHeadStartArm())
            {
                _player.AdvanceAfterRevive(28f);
                _powers.GrantHeadStartBurst();
                _camera?.PunchFov(5f);
                GameUI.Instance?.ShowTutorial("HEAD START! Sprint ahead!");
            }

            _camera?.SnapNow();
            _camera?.PunchFov(3f);
            AudioHooks.Instance?.PlayBiomeAmbience(BiomeSystem.Current);
            GameUI.Instance?.ShowHud();
            AnalyticsService.Track("run_start", (int)difficulty);

            if (tutorial)
            {
                _tutorialStep = 0;
                _tutorialTimer = 0f;
                GameUI.Instance?.ShowTutorial("Swipe LEFT / RIGHT · WASD / Arrows");
            }
            else
            {
                _tutorialStep = -1;
                GameUI.Instance?.HideTutorial();
            }

            _openingCo = null;
        }

        bool ConsumeOpeningSkip()
        {
            if (_openingSkip) return true;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)
                || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                _openingSkip = true;
                return true;
            }
            return false;
        }

        public void SkipOpening() => _openingSkip = true;

        public void ContinueAfterRevive()
        {
            State = GameState.Running;
            Time.timeScale = 1f;
            _env?.ResetEffects();
            _powers?.GrantReviveIFrames(2.25f);
            if (_powers != null) _powers.Activate(PowerUpType.Shield, true);
            if (_player != null)
            {
                _player.ClearStumble();
                _player.AdvanceAfterRevive(6f);
            }
            if (_guardian != null)
            {
                _guardian.gameObject.SetActive(true);
                _guardian.BeginRun();
            }
            _camera?.SnapNow();
            _camera?.PunchFov(3.5f);
            GameUI.Instance?.ShowHud();
            GameUI.Instance?.ShowTutorial("SAVED! Shield up — keep running!");
        }

        void UpdateTutorial()
        {
            if (_tutorialStep < 0) return;
            _tutorialTimer += Time.deltaTime;
            float d = _session.Distance;

            if (_tutorialStep == 0 && d > 25f)
            {
                _tutorialStep = 1;
                GameUI.Instance?.ShowTutorial("Swipe UP / W to jump · Space for boost");
            }
            else if (_tutorialStep == 1 && d > 55f)
            {
                _tutorialStep = 2;
                GameUI.Instance?.ShowTutorial("Swipe DOWN / S to slide");
            }
            else if (_tutorialStep == 2 && d > 90f)
            {
                _tutorialStep = 3;
                GameUI.Instance?.ShowTutorial("The Guardian chases you — don't stumble!");
            }
            else if (_tutorialStep == 3 && d > 130f)
            {
                _tutorialStep = 4;
                _tutorialTimer = 0f;
                var meta = MetaProgress.Ensure();
                meta.MarkTutorialComplete();
                meta.BankCoins(50);
                meta.AddGems(3);
                meta.UnlockCharacter("jungle_ace");
                GameUI.Instance?.ShowTutorial("+50 coins, +3 gems, Jungle Ace unlocked!");
            }
            else if (_tutorialStep == 4 && _tutorialTimer > 2.5f)
            {
                _tutorialStep = -1;
                GameUI.Instance?.HideTutorial();
            }
        }

        void HandleRunEnded(RunEndPayload payload)
        {
            _lastPayload = payload;
            State = GameState.PostRun;
            _guardian.Stop();
            _ghost?.Stop();
            Time.timeScale = 1f;
            _powers?.ClearTimers();
            _env?.ResetEffects();
            AudioHooks.Instance?.StopAmbience();
            MonetizationService.OnPostRun();
            GameUI.Instance?.ShowPostRun(payload);
        }

        public RunEndPayload LastPayload => _lastPayload;

        void SetRunActorsVisible(bool visible)
        {
            if (_player != null) _player.gameObject.SetActive(visible);
            if (_guardian != null) _guardian.gameObject.SetActive(visible);
            if (_spawner != null) _spawner.gameObject.SetActive(visible);
        }

        void OnDestroy()
        {
            if (_session != null) _session.OnRunEnded -= HandleRunEnded;
            if (Instance == this) Instance = null;
        }
    }
}
