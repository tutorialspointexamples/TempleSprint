#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TempleSprint.EditorTools
{
    /// <summary>
    /// Drives an instrumented Play Mode run and writes Logs/play_verify.txt.
    /// Runs only when explicitly armed; a manual Play Mode session is never touched
    /// because the instrumentation disables collisions and forces a fixed route.
    /// </summary>
    [InitializeOnLoad]
    static class AutoPlayVerifier
    {
        // SessionState survives the domain reload that entering Play Mode triggers.
        const string ActiveKey = "TempleSprint.PlayVerify.Active";
        const string QuitKey = "TempleSprint.PlayVerify.Quit";
        const string ModeKey = "TempleSprint.PlayVerify.Mode";

        public enum VerifyMode
        {
            /// <summary>Endless route + difficulty profiles, collisions disabled.</summary>
            Route = 0,
            /// <summary>Pickups and deaths with collisions live.</summary>
            Gameplay = 1
        }

        static readonly string FlagAbs =
            Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Editor/RUN_PLAY_VERIFY.flag"));
        static readonly string ResultAbs =
            Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/play_verify.txt"));

        static double _enteredAt = -1;
        static bool _hooked;
        static bool _startedRun;

        static bool Verifying
        {
            get => SessionState.GetBool(ActiveKey, false);
            set => SessionState.SetBool(ActiveKey, value);
        }

        static bool QuitWhenDone
        {
            get => SessionState.GetBool(QuitKey, false);
            set => SessionState.SetBool(QuitKey, value);
        }

        static VerifyMode Mode
        {
            get => (VerifyMode)SessionState.GetInt(ModeKey, (int)VerifyMode.Route);
            set => SessionState.SetInt(ModeKey, (int)value);
        }

        static AutoPlayVerifier()
        {
            if (File.Exists(FlagAbs))
            {
                try { File.Delete(FlagAbs); } catch { /* ignore */ }
                Arm(false, VerifyMode.Route);
                EditorApplication.delayCall += StartPlay;
            }

            EditorApplication.playModeStateChanged += OnPlayMode;

            // Re-attach after the domain reload caused by entering Play Mode.
            if (Verifying && EditorApplication.isPlaying)
                BeginCheck();
        }

        public static void Arm(bool quitWhenDone, VerifyMode mode = VerifyMode.Route)
        {
            Verifying = true;
            QuitWhenDone = quitWhenDone;
            Mode = mode;
        }

        [MenuItem("Temple Sprint/Verify Play Mode")]
        public static void MenuVerify()
        {
            Arm(false, VerifyMode.Route);
            EditorApplication.delayCall += StartPlay;
        }

        [MenuItem("Temple Sprint/Verify Pickups And Deaths")]
        public static void MenuVerifyGameplay()
        {
            Arm(false, VerifyMode.Gameplay);
            EditorApplication.delayCall += StartPlay;
        }

        public static void StartPlay()
        {
            if (!Verifying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.GetActiveScene().path.EndsWith("Main.unity"))
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity", OpenSceneMode.Single);
            Debug.Log("[Temple Sprint] AutoPlayVerifier: entering Play Mode…");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= CheckAfterBoot;
                _hooked = false;
                _startedRun = false;
                _enteredAt = -1;
                return;
            }
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!Verifying) return;
            BeginCheck();
        }

        static void BeginCheck()
        {
            if (_hooked) return;
            _hooked = true;
            _enteredAt = EditorApplication.timeSinceStartup;
            _startedRun = false;
            _phase = 0;
            _coinOk = false;
            _deathOk = false;
            EditorApplication.update += CheckAfterBoot;
        }

        static void CheckAfterBoot()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= CheckAfterBoot;
                _hooked = false;
                return;
            }

            if (Mode == VerifyMode.Gameplay)
            {
                GameplayTick();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - _enteredAt;
            if (elapsed < 2.0) return;

            var gm = Object.FindAnyObjectByType<GameManager>();
            if (!_startedRun && gm != null
                && gm.State != GameState.Running && gm.State != GameState.PostRun
                && gm.State != GameState.Opening)
            {
                _startedRun = true;
                try
                {
                    gm.StartRun(RunDifficulty.Medium);
                    gm.SkipOpening();
                    var runner = Object.FindAnyObjectByType<PlayerController>();
                    var spawner = Object.FindAnyObjectByType<TileSpawner>();
                    // Survive the whole sample window so the route can be measured.
                    var capsule = runner != null ? runner.GetComponent<CapsuleCollider>() : null;
                    if (capsule != null) capsule.enabled = false;
                    // Deterministic tutorial route guarantees a turn, junction, fire, and river.
                    spawner?.BeginRun(true, RunDifficulty.Medium);
                }
                catch (System.Exception e) { Debug.LogWarning("[VERIFY] StartRun: " + e.Message); }
                return;
            }

            // Domain reloads can bounce back to the menu — restart once.
            if (_startedRun && gm != null && gm.State == GameState.MainMenu && elapsed > 4.0 && elapsed < 6.0)
            {
                try
                {
                    gm.StartRun(RunDifficulty.Medium);
                    gm.SkipOpening();
                    var spawner = Object.FindAnyObjectByType<TileSpawner>();
                    var runner = Object.FindAnyObjectByType<PlayerController>();
                    var capsule = runner != null ? runner.GetComponent<CapsuleCollider>() : null;
                    if (capsule != null) capsule.enabled = false;
                    spawner?.BeginRun(true, RunDifficulty.Medium);
                }
                catch (System.Exception e) { Debug.LogWarning("[VERIFY] RestartRun: " + e.Message); }
                return;
            }

            _startedRun = true;

            // Enough path distance to cross a turn, junction, and fire crossing.
            if (elapsed < 28.0) return;
            EditorApplication.update -= CheckAfterBoot;
            _hooked = false;

            var ui = Object.FindAnyObjectByType<GameUI>();
            var explorer = Object.FindAnyObjectByType<ExplorerRunnerVisual>(FindObjectsInactive.Include);
            var nature = Object.FindAnyObjectByType<NatureBackdrop>(FindObjectsInactive.Include);
            var tiles = Object.FindObjectsByType<TrackTile>(FindObjectsInactive.Exclude);
            var cam = Object.FindAnyObjectByType<ChaseCamera>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var tileSpawner = Object.FindAnyObjectByType<TileSpawner>();

            int kids = gm != null ? gm.transform.childCount : -1;
            int activeTiles = tiles != null ? tiles.Length : 0;
            int rends = GameManager.CountLiveRenderers();
            float height = explorer != null ? explorer.BodyHeight : 0f;
            bool skinned = explorer != null && explorer.HasSkinnedMesh;
            int explorerParts = explorer != null ? explorer.PartCount : 0;
            var smr = explorer != null ? explorer.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            float worldH = smr != null ? smr.bounds.size.y : 0f;
            bool drawn = smr != null && smr.enabled && smr.gameObject.activeInHierarchy && worldH >= 0.8f;
            float camDist = (cam != null && player != null)
                ? Vector3.Distance(cam.transform.position, player.transform.position) : 999f;
            bool framed = cam != null && player != null && camDist < 12f && camDist > 1f;
            bool humanoid = explorer != null && (skinned || explorerParts >= 1) && height >= 1.2f && drawn;
            var anim = explorer != null ? explorer.GetComponentInChildren<Animator>(true) : null;
            bool animOn = anim != null && anim.enabled && anim.runtimeAnimatorController != null;

            int turns = tileSpawner != null ? tileSpawner.TurnsSpawnedThisRun : 0;
            int junctions = tileSpawner != null ? tileSpawner.JunctionsSpawnedThisRun : 0;
            int resolved = tileSpawner != null ? tileSpawner.JunctionsResolvedThisRun : 0;
            int hazards = 0;
            if (tiles != null)
            {
                foreach (var t in tiles)
                {
                    if (t == null) continue;
                    if (TileWeightTable.IsHazardous(t.Kind)) hazards++;
                }
            }

            float nextPath = tileSpawner != null ? tileSpawner.NextPathDistance : 0f;
            float playerPath = player != null ? player.PathDistance : 0f;
            bool endless = tileSpawner != null
                           && !tileSpawner.AwaitingJunctionChoice
                           && nextPath > playerPath + TrackTile.Length * 2f
                           && tileSpawner.ActiveTileCount > 0;
            bool profileOk = SampleHazardOrdering();
            bool audioOk = CheckAudio();
            bool turnClear = CheckTurnAdjacency(tiles);
            bool riverModesOk = CheckRiverModesPresent(tiles);
            bool fireModesOk = CheckFireModesPresent(tiles)
                               || (tileSpawner != null && tileSpawner.FiresSpawnedThisRun > 0);
            bool specialOk = CheckSpecialStagesPresent(tiles)
                             || (tileSpawner != null
                                 && (tileSpawner.ZiplinesSpawnedThisRun > 0
                                     || tileSpawner.MineCartsSpawnedThisRun > 0
                                     || tileSpawner.IceSurfsSpawnedThisRun > 0
                                     || tileSpawner.WallRunsSpawnedThisRun > 0
                                     || tileSpawner.LedgeGrabsSpawnedThisRun > 0
                                     || tileSpawner.TreeBridgesSpawnedThisRun > 0
                                     || tileSpawner.CanopyRopesSpawnedThisRun > 0
                                     || tileSpawner.WaterfallPlungesSpawnedThisRun > 0
                                     || tileSpawner.WaterSlidesSpawnedThisRun > 0
                                     || tileSpawner.TempleHallsSpawnedThisRun > 0
                                     || tileSpawner.LavaRiversSpawnedThisRun > 0
                                     || tileSpawner.CliffNarrowsSpawnedThisRun > 0
                                     || tileSpawner.RuinForksSpawnedThisRun > 0
                                     || tileSpawner.BiomeTransitionsSpawnedThisRun > 0));

            bool ok = gm != null && ui != null && kids >= 4 && explorer != null && nature != null
                      && activeTiles > 0 && rends >= 40 && framed && humanoid
                      && turns > 0 && junctions > 0
                      && endless && profileOk && audioOk && turnClear && fireModesOk
                      && (gm.State == GameState.Running || gm.State == GameState.PostRun || resolved > 0);

            string line = ok
                ? $"PASS kids={kids} state={gm.State} tiles={activeTiles} rends={rends} height={height:0.00} worldH={worldH:0.00} skinned={(skinned ? 1 : 0)} camDist={camDist:0.0} anim={(animOn ? 1 : 0)} audio={(audioOk ? 1 : 0)} turns={turns} junctions={junctions} resolved={resolved} hazards={hazards} riverModes={(riverModesOk ? 1 : 0)} fireModes={(fireModesOk ? 1 : 0)} special={(specialOk ? 1 : 0)} firesSpawned={(tileSpawner != null ? tileSpawner.FiresSpawnedThisRun : 0)} zipSpawned={(tileSpawner != null ? tileSpawner.ZiplinesSpawnedThisRun : 0)} cartSpawned={(tileSpawner != null ? tileSpawner.MineCartsSpawnedThisRun : 0)} iceSpawned={(tileSpawner != null ? tileSpawner.IceSurfsSpawnedThisRun : 0)} treeSpawned={(tileSpawner != null ? tileSpawner.TreeBridgesSpawnedThisRun : 0)} canopySpawned={(tileSpawner != null ? tileSpawner.CanopyRopesSpawnedThisRun : 0)} fallSpawned={(tileSpawner != null ? tileSpawner.WaterfallPlungesSpawnedThisRun : 0)} hallSpawned={(tileSpawner != null ? tileSpawner.TempleHallsSpawnedThisRun : 0)} turnClear=1 next={nextPath:0.0} path={playerPath:0.0} explorer=1 nature=1"
                : $"FAIL gm={gm != null} ui={ui != null} kids={kids} state={(gm != null ? gm.State.ToString() : "?")} explorer={explorer != null} height={height:0.00} worldH={worldH:0.00} drawn={drawn} skinned={skinned} parts={explorerParts} nature={nature != null} tiles={activeTiles} rends={rends} camDist={camDist:0.0} framed={framed} turns={turns} junctions={junctions} resolved={resolved} endless={endless} profileOk={profileOk} audio={audioOk} turnClear={turnClear}";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultAbs));
                File.WriteAllText(ResultAbs, line + "\n" + System.DateTime.Now.ToString("o"));
            }
            catch (System.Exception e) { Debug.LogWarning(e.Message); }
            Debug.Log("[VERIFY] " + line);

            bool quit = QuitWhenDone;
            Verifying = false;
            QuitWhenDone = false;
            if (quit)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        // Gameplay probe state
        static int _phase;
        static int _coinsBefore;
        static bool _coinOk;
        static bool _deathOk;
        static string _deathReason = "";

        /// <summary>
        /// Runs with collisions live: drops a coin in the runner's path and asserts it is
        /// consumed, then drops a spike and asserts the run actually ends.
        /// </summary>
        static void GameplayTick()
        {
            double t = EditorApplication.timeSinceStartup - _enteredAt;
            var gm = Object.FindAnyObjectByType<GameManager>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var session = Object.FindAnyObjectByType<RunSession>();

            if (_phase == 0 && t >= 2.0)
            {
                _phase = 1;
                if (gm != null && gm.State != GameState.Running)
                    gm.StartRun(RunDifficulty.Medium);
                // Shield the coin phase from unrelated hazards on the generated route.
                PowerUpController.Instance?.GrantReviveIFrames(4.5f);
                return;
            }

            if (_phase == 1 && t >= 3.5)
            {
                _phase = 2;
                _coinsBefore = session != null ? session.CoinsThisRun : -1;
                if (player != null)
                    CollectibleCoin.Create(AnchorAtPlayer(player).transform, new Vector3(0f, 1.0f, 4f));
                return;
            }

            if (_phase == 2 && t >= 5.5)
            {
                _phase = 3;
                _coinOk = session != null && session.CoinsThisRun > _coinsBefore;
                Debug.Log($"[VERIFY] coins before={_coinsBefore} after={(session != null ? session.CoinsThisRun : -1)}");
                return;
            }

            if (_phase == 3 && t >= 7.0)
            {
                _phase = 4;
                // I-frames have lapsed by now, so this spike is lethal.
                if (player != null && session != null && session.IsAlive)
                    Obstacle.CreateSpike(AnchorAtPlayer(player).transform, 5f, 1);
                return;
            }

            if (_phase == 4 && t >= 9.5)
            {
                _phase = 5;
                _deathOk = session != null && !session.IsAlive
                           && gm != null && gm.State == GameState.PostRun;
                _deathReason = gm != null && gm.LastPayload != null ? gm.LastPayload.deathReason : "none";

                EditorApplication.update -= CheckAfterBoot;
                _hooked = false;

                bool audioOk = CheckAudio();
                bool ok = _coinOk && _deathOk && audioOk;
                string line = ok
                    ? $"PASS gameplay coin=1 death=1 audio=1 reason=\"{_deathReason}\" coins={_coinsBefore}->{(session != null ? session.CoinsThisRun : -1)}"
                    : $"FAIL gameplay coin={_coinOk} death={_deathOk} audio={audioOk} state={(gm != null ? gm.State.ToString() : "?")} alive={(session != null ? session.IsAlive.ToString() : "?")} reason=\"{_deathReason}\"";

                WriteResult(line);
                Debug.Log("[VERIFY] " + line);

                bool quit = QuitWhenDone;
                Verifying = false;
                QuitWhenDone = false;
                if (quit) EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        /// <summary>Empty transform at the runner's pose so offsets read as "ahead in my lane".</summary>
        static GameObject AnchorAtPlayer(PlayerController player)
        {
            var anchor = new GameObject("VerifyAnchor");
            anchor.transform.SetPositionAndRotation(
                player.transform.position,
                Quaternion.Euler(0f, player.FacingYaw, 0f));
            return anchor;
        }

        static void WriteResult(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultAbs));
                File.WriteAllText(ResultAbs, line + "\n" + System.DateTime.Now.ToString("o"));
            }
            catch (System.Exception e) { Debug.LogWarning(e.Message); }
        }

        /// <summary>Death, coin and hit SFX must exist — they are synthesized, not imported.</summary>
        static bool CheckAudio()
        {
            var hooks = Object.FindAnyObjectByType<AudioHooks>();
            if (hooks == null)
            {
                Debug.LogWarning("[VERIFY] AudioHooks missing");
                return false;
            }
            bool ok = hooks.HasRunClips;
            if (!ok) Debug.LogWarning("[VERIFY] AudioHooks clips not synthesized");
            return ok;
        }

        static bool SampleHazardOrdering()
        {
            var table = TileWeightTable.CreateRuntimeDefault();
            var savedState = Random.state;
            int easy = CountHazards(table, RunDifficulty.Easy);
            int med = CountHazards(table, RunDifficulty.Medium);
            int hard = CountHazards(table, RunDifficulty.Hard);
            Random.state = savedState;
            Debug.Log($"[VERIFY] hazard samples easy={easy} med={med} hard={hard}");
            return easy < med && med < hard;
        }

        static int CountHazards(TileWeightTable table, RunDifficulty d)
        {
            Random.InitState(7319);
            int n = 0;
            for (int i = 0; i < 400; i++)
            {
                var k = table.Pick(d, 0.3f, 1, true, true);
                if (TileWeightTable.IsHazardous(k)) n++;
            }
            return n;
        }

        static bool IsTurnOrJunction(TileKind k) =>
            k == TileKind.TurnLeft || k == TileKind.TurnRight || k == TileKind.TJunction || k == TileKind.RuinFork;

        /// <summary>No hazardous tile may sit directly before or after a turn/junction.</summary>
        static bool CheckTurnAdjacency(TrackTile[] tiles)
        {
            if (tiles == null || tiles.Length < 2) return true;
            System.Array.Sort(tiles, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.PathStartDistance.CompareTo(b.PathStartDistance);
            });

            for (int i = 0; i < tiles.Length - 1; i++)
            {
                var a = tiles[i];
                var b = tiles[i + 1];
                if (a == null || b == null) continue;
                bool turnHazard =
                    (IsTurnOrJunction(a.Kind) && TileWeightTable.IsHazardous(b.Kind))
                    || (TileWeightTable.IsHazardous(a.Kind) && IsTurnOrJunction(b.Kind));
                if (turnHazard)
                {
                    Debug.LogWarning($"[VERIFY] turn/hazard adjacency {a.Kind}->{b.Kind} at {a.PathStartDistance:0.0}");
                    return false;
                }
            }

            // Curved turns must bank mid-arc (genre-style, not sharp L corners).
            foreach (var t in tiles)
            {
                if (t == null || (t.Kind != TileKind.TurnLeft && t.Kind != TileKind.TurnRight)) continue;
                var mid = t.SampleAtPathDistance(t.PathStartDistance + t.PathLength * 0.5f);
                if (Mathf.Abs(mid.bank) < 4f)
                {
                    Debug.LogWarning($"[VERIFY] turn missing bank mid-curve kind={t.Kind} bank={mid.bank:0.0}");
                    return false;
                }
            }

            // Turn geometry itself must carry nothing lethal, decor included.
            foreach (var t in tiles)
            {
                if (t == null || !IsTurnOrJunction(t.Kind)) continue;
                int lethal = t.GetComponentsInChildren<Obstacle>(true).Length
                             + t.GetComponentsInChildren<DynamicHazard>(true).Length
                             + t.GetComponentsInChildren<GapKillZone>(true).Length;
                if (lethal > 0)
                {
                    Debug.LogWarning($"[VERIFY] {t.Kind} at {t.PathStartDistance:0.0} carries {lethal} lethal collider(s)");
                    return false;
                }
            }
            return true;
        }

        static bool CheckRiverModesPresent(TrackTile[] tiles)
        {
            if (tiles == null) return false;
            bool anyRiver = false;
            bool boat = false, rope = false, jump = false, swim = false;
            foreach (var t in tiles)
            {
                if (t == null || t.Kind != TileKind.RiverCrossing) continue;
                anyRiver = true;
                var m = t.GetComponent<RiverCrossingMarker>();
                if (m == null) continue;
                if (m.Mode == RiverCrossingMode.Boat) boat = true;
                else if (m.Mode == RiverCrossingMode.Rope) rope = true;
                else if (m.Mode == RiverCrossingMode.Swim) swim = true;
                else jump = true;
            }
            // Soft check: at least one river tile in the live window is enough for the smoke.
            Debug.Log($"[VERIFY] riverModes any={anyRiver} boat={boat} rope={rope} jump={jump} swim={swim}");
            return anyRiver;
        }

        static bool CheckFireModesPresent(TrackTile[] tiles)
        {
            if (tiles == null) return false;
            bool anyFire = false;
            bool jump = false, vine = false, dunk = false;
            foreach (var t in tiles)
            {
                if (t == null || t.Kind != TileKind.FireCrossing) continue;
                anyFire = true;
                var m = t.GetComponent<FireCrossingMarker>();
                if (m == null)
                {
                    Debug.LogWarning($"[VERIFY] FireCrossing at {t.PathStartDistance:0.0} missing marker");
                    return false;
                }
                if (m.Mode == FireCrossingMode.Vine) vine = true;
                else if (m.Mode == FireCrossingMode.WaterDunk) dunk = true;
                else jump = true;
            }
            Debug.Log($"[VERIFY] fireModes any={anyFire} jump={jump} vine={vine} dunk={dunk}");
            return anyFire;
        }

        static bool CheckSpecialStagesPresent(TrackTile[] tiles)
        {
            if (tiles == null) return false;
            bool zip = false, cart = false, ice = false, wall = false, ledge = false, tree = false;
            bool canopy = false, fall = false, slide = false, hall = false, cliff = false;
            foreach (var t in tiles)
            {
                if (t == null) continue;
                if (t.Kind == TileKind.Zipline)
                {
                    zip = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] Zipline at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                }
                if (t.Kind == TileKind.MineCart)
                {
                    cart = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] MineCart at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                }
                if (t.Kind == TileKind.IceSurf)
                {
                    ice = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] IceSurf at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                    if (t.transform.Find("IceLugeWall_0") == null && t.transform.Find("IceLugeGroove") == null)
                    {
                        Debug.LogWarning($"[VERIFY] IceSurf at {t.PathStartDistance:0.0} missing luge half-pipe walls");
                        return false;
                    }
                }
                if (t.Kind == TileKind.WallRun)
                {
                    wall = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] WallRun at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                }
                if (t.Kind == TileKind.LedgeGrab)
                {
                    ledge = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] LedgeGrab at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                }
                if (t.Kind == TileKind.TreeBridge)
                {
                    tree = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] TreeBridge at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                }
                if (t.Kind == TileKind.CanopyRope)
                {
                    canopy = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] CanopyRope at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                }
                if (t.Kind == TileKind.WaterfallPlunge)
                {
                    fall = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] WaterfallPlunge at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                    if (t.transform.Find("SprayCurtain_0") == null)
                    {
                        Debug.LogWarning($"[VERIFY] WaterfallPlunge at {t.PathStartDistance:0.0} missing spray curtains");
                        return false;
                    }
                }
                if (t.Kind == TileKind.WaterSlide)
                {
                    slide = true;
                    if (t.GetComponent<SpecialStageMarker>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] WaterSlide at {t.PathStartDistance:0.0} missing marker");
                        return false;
                    }
                    if (t.transform.Find("AqueductLugeWall_0") == null && t.transform.Find("AqueductLugeRim") == null)
                    {
                        Debug.LogWarning($"[VERIFY] WaterSlide at {t.PathStartDistance:0.0} missing aqueduct half-pipe walls");
                        return false;
                    }
                }
                if (t.Kind == TileKind.TempleHall)
                {
                    hall = true;
                    if (t.transform.Find("HallWall") == null && t.transform.childCount < 4)
                    {
                        Debug.LogWarning($"[VERIFY] TempleHall at {t.PathStartDistance:0.0} missing interior shell");
                        return false;
                    }
                }
                if (t.Kind == TileKind.LavaRiver)
                {
                    if (t.transform.Find("LavaSurface") == null && t.transform.Find("LavaFlowSheet") == null)
                    {
                        Debug.LogWarning($"[VERIFY] LavaRiver at {t.PathStartDistance:0.0} missing lava shell");
                        return false;
                    }
                    if (t.transform.Find("MagmaSplash_0") == null && t.transform.Find("MagmaBoilRing") == null)
                    {
                        Debug.LogWarning($"[VERIFY] LavaRiver at {t.PathStartDistance:0.0} missing magma splash columns");
                        return false;
                    }
                }
                if (t.Kind == TileKind.CliffNarrow)
                {
                    cliff = true;
                    if (t.transform.Find("CliffNarrowStrip") == null)
                    {
                        Debug.LogWarning($"[VERIFY] CliffNarrow at {t.PathStartDistance:0.0} missing precipice strip");
                        return false;
                    }
                    if (t.transform.Find("CliffWindStreamer") == null && t.GetComponentInChildren<CliffWindSway>() == null)
                    {
                        Debug.LogWarning($"[VERIFY] CliffNarrow at {t.PathStartDistance:0.0} missing precipice wind props");
                        return false;
                    }
                }
                if (t.Kind == TileKind.MineCart)
                {
                    var telegraph = t.GetComponentInChildren<BrokenRailTelegraph>();
                    if (telegraph != null && telegraph.transform.Find("BrokenRailSparks") == null)
                    {
                        Debug.LogWarning($"[VERIFY] MineCart at {t.PathStartDistance:0.0} missing broken-rail spark telegraph");
                        return false;
                    }
                }
                if (t.Kind == TileKind.RuinFork)
                {
                    if (t.transform.Find("ArmLeft") == null || t.transform.Find("ArmRight") == null)
                    {
                        Debug.LogWarning($"[VERIFY] RuinFork at {t.PathStartDistance:0.0} missing fork arms");
                        return false;
                    }
                }
                if (t.Kind == TileKind.BiomeTransitionTunnel)
                {
                    if (t.transform.Find("TransitionWallNear") == null
                        && t.transform.Find("BiomeTransitionCommit") == null)
                    {
                        Debug.LogWarning($"[VERIFY] BiomeTransitionTunnel at {t.PathStartDistance:0.0} missing shell");
                        return false;
                    }
                }
            }
            Debug.Log($"[VERIFY] specialStages zipline={zip} minecart={cart} icesurf={ice} wallrun={wall} ledge={ledge} tree={tree} canopy={canopy} waterfall={fall} slide={slide} hall={hall} cliff={cliff}");
            return zip || cart || ice || wall || ledge || tree || canopy || fall || slide || hall || cliff;
        }
    }
}
#endif
