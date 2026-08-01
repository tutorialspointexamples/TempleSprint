using UnityEngine;
using UnityEngine.UI;

namespace TempleSprint
{
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        Canvas _canvas;
        GameObject _boot, _menu, _hud, _post, _upgrade, _locker, _shop, _missions, _settings, _info, _tutorial;
        Text _menuCurrency, _hudScore, _hudCoins, _hudPower, _hudCombo, _hudGhost, _postSummary, _infoBody, _tutorialText, _upgradeInfo, _missionBody;
        GameObject _hudComboFrame, _hudGhostFrame;
        Image _hudPowerFill;
        Button _btnReviveAd, _btnReviveGem;
        bool _bootDone, _starterQueued;
        float _bootTimer = 0.45f;
        float _comboPulse;

        void Awake()
        {
            Instance = this;
            UiFactory.EnsureEventSystem();
            _canvas = UiFactory.CreateCanvas("GameCanvas", transform);
            ApplyTextScale();

            // Short boot — semi-transparent so the bridge world is never a solid green wipe
            _boot = Panel("Boot", new Color(0.05f, 0.08f, 0.07f, 0.55f));
            AddChromeBar(_boot, true);
            AddChromeBar(_boot, false);
            Title(_boot, "TEMPLE SPRINT", 64, 0.72f, 0.92f);
            Sub(_boot, "Loading…", 28, 0.55f, 0.68f);

            // Menu lets the 3D causeway show through; gold PLAY is the hero control
            _menu = Panel("Menu", new Color(0.04f, 0.07f, 0.06f, 0.38f));
            AddChromeBar(_menu, true);
            AddChromeBar(_menu, false);
            Title(_menu, "TEMPLE SPRINT", 56, 0.82f, 0.96f);
            _menuCurrency = Sub(_menu, "", 24, 0.74f, 0.82f);
            Btn(_menu, "EASY", new Vector2(-280, 90), () => GameManager.Instance?.StartRun(RunDifficulty.Easy));
            Btn(_menu, "MEDIUM", new Vector2(0, 90), () => GameManager.Instance?.StartRun(RunDifficulty.Medium));
            Btn(_menu, "HARD", new Vector2(280, 90), () => GameManager.Instance?.StartRun(RunDifficulty.Hard));
            Btn(_menu, "UPGRADES", new Vector2(-280, -40), ShowUpgrades);
            Btn(_menu, "LOCKER", new Vector2(0, -40), ShowLocker);
            Btn(_menu, "SHOP", new Vector2(280, -40), ShowShop);
            Btn(_menu, "MISSIONS", new Vector2(-280, -130), ShowMissions);
            Btn(_menu, "SETTINGS", new Vector2(0, -130), ShowSettings);
            Btn(_menu, "MORE", new Vector2(280, -130), ShowMore);
            Sub(_menu, "Choose a difficulty to start  ·  SPACE = Medium", 22, 0.08f, 0.16f);

            _hud = new GameObject("HUD");
            _hud.transform.SetParent(_canvas.transform, false);
            Stretch(_hud.AddComponent<RectTransform>());

            // Temple Run layout: score + coins top-right, power ring top-left, pause bottom-right
            _hudScore = UiFactory.CreateHudPlaque(
                _hud.transform, "Score",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -36f), new Vector2(280f, 88f), "0");
            _hudCoins = UiFactory.CreateHudPlaque(
                _hud.transform, "Coins",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -140f), new Vector2(240f, 72f), "◆ 0");
            var powerRing = UiFactory.CreatePowerRing(
                _hud.transform, "Power",
                new Vector2(0f, 1f), new Vector2(36f, -36f), 132f);
            _hudPowerFill = powerRing.fill;
            _hudPower = powerRing.label;
            _hudCombo = UiFactory.CreateHudPlaque(
                _hud.transform, "Combo",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(220f, 64f), "");
            _hudComboFrame = _hudCombo != null ? _hudCombo.transform.parent.parent.gameObject : null;
            if (_hudComboFrame != null) _hudComboFrame.SetActive(false);
            _hudGhost = UiFactory.CreateHudPlaque(
                _hud.transform, "Ghost",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(280f, 56f), "");
            _hudGhostFrame = _hudGhost != null ? _hudGhost.transform.parent.parent.gameObject : null;
            if (_hudGhostFrame != null) _hudGhostFrame.SetActive(false);
            UiFactory.CreateCircleButton(
                _hud.transform, "Pause", "Ⅱ",
                new Vector2(1f, 0f), new Vector2(-40f, 40f), 96f,
                () =>
                {
                    if (Time.timeScale > 0.1f) { Time.timeScale = 0f; Toast("Paused"); }
                    else { Time.timeScale = 1f; Toast("Resumed"); }
                });

            _post = Panel("Post", new Color(0.05f, 0.08f, 0.06f, 0.72f));
            Title(_post, "RUN OVER", 48, 0.78f, 0.94f);
            _postSummary = Sub(_post, "", 24, 0.48f, 0.74f);
            Btn(_post, "RETRY", new Vector2(0, 40), () =>
            {
                RunSession.Instance?.FinalizeAndBank();
                GameManager.Instance?.StartRun(GameManager.Instance.CurrentDifficulty);
            });
            _btnReviveAd = Btn(_post, "REVIVE (AD)", new Vector2(0, -50), TryAdRevive);
            _btnReviveGem = Btn(_post, "REVIVE (5 GEMS)", new Vector2(0, -140), TryGemRevive);
            Btn(_post, "2x COINS (AD)", new Vector2(0, -230), TryDoubleCoins);
            Btn(_post, "SHARE", new Vector2(0, -320), ShareRun);
            Btn(_post, "UPGRADES", new Vector2(-200, -410), ShowUpgrades);
            Btn(_post, "MENU", new Vector2(200, -410), () => { RunSession.Instance?.FinalizeAndBank(); GameManager.Instance?.EnterMainMenu(); });

            _upgrade = Panel("Upgrades", new Color(0.07f, 0.12f, 0.1f, 0.82f));
            Title(_upgrade, "UPGRADES", 44, 0.82f, 0.96f);
            _upgradeInfo = Sub(_upgrade, "", 22, 0.55f, 0.8f);
            Btn(_upgrade, "MAGNET RADIUS", new Vector2(-160, 80), () => { MetaProgress.Ensure().TryBuyMagnet(); RefreshUpgrade(); });
            Btn(_upgrade, "COIN MULTIPLIER", new Vector2(160, 80), () => { MetaProgress.Ensure().TryBuyCoinMultiplier(); RefreshUpgrade(); });
            Btn(_upgrade, "STARTING SHIELD", new Vector2(-160, -10), () => { MetaProgress.Ensure().TryBuyRevive(); RefreshUpgrade(); });
            Btn(_upgrade, "ENERGY FILL", new Vector2(160, -10), () => { MetaProgress.Ensure().TryBuyEnergyFill(); RefreshUpgrade(); });
            Btn(_upgrade, "MAGNET TIME", new Vector2(-160, -100), () => { MetaProgress.Ensure().TryBuyMagnetDuration(); RefreshUpgrade(); });
            Btn(_upgrade, "BOOST TIME", new Vector2(160, -100), () => { MetaProgress.Ensure().TryBuyBoostDuration(); RefreshUpgrade(); });
            Btn(_upgrade, "BACK", new Vector2(0, -220), BackToMenuOrPost);

            _locker = Panel("Locker", new Color(0.08f, 0.12f, 0.14f, 0.82f));
            Title(_locker, "LOCKER", 44, 0.82f, 0.96f);
            BuildLockerButtons();
            Btn(_locker, "BACK", new Vector2(0, -390), () => ShowMainMenu());

            _shop = Panel("Shop", new Color(0.1f, 0.1f, 0.14f, 0.82f));
            Title(_shop, "SHOP", 44, 0.82f, 0.96f);
            Btn(_shop, "GEM PACK S", new Vector2(0, 120), () => ShopService.BuyGemPack(0, Toast));
            Btn(_shop, "GEM PACK M", new Vector2(0, 40), () => ShopService.BuyGemPack(1, Toast));
            Btn(_shop, "REMOVE ADS", new Vector2(0, -40), () => ShopService.BuyRemoveAds(Toast));
            Btn(_shop, "STARTER PACK", new Vector2(0, -120), () => ShopService.BuyStarterPack(Toast));
            Btn(_shop, "SEASON PASS", new Vector2(0, -200), () => ShopService.BuyBattlePass(Toast));
            Btn(_shop, "CATALOG", new Vector2(0, -280), () => ShowInfo(ShopService.Catalog()));
            Btn(_shop, "BACK", new Vector2(0, -360), () => ShowMainMenu());

            _missions = Panel("Missions", new Color(0.08f, 0.14f, 0.12f, 0.82f));
            Title(_missions, "MISSIONS", 44, 0.82f, 0.96f);
            _missionBody = Sub(_missions, "", 22, 0.4f, 0.74f);
            Btn(_missions, "CLAIM 1", new Vector2(-160, -40), () => { MissionSystem.TryClaim(0); ShowMissions(); });
            Btn(_missions, "CLAIM 2", new Vector2(160, -40), () => { MissionSystem.TryClaim(1); ShowMissions(); });
            Btn(_missions, "CLAIM 3", new Vector2(0, -120), () => { MissionSystem.TryClaim(2); ShowMissions(); });
            Btn(_missions, "CLAIM ARTIFACT", new Vector2(0, -200), () =>
            {
                Toast(ArtifactHuntSystem.TryClaim() ? "Artifact claimed!" : "Hunt incomplete / claimed");
                ShowMissions();
            });
            Btn(_missions, "BACK", new Vector2(0, -260), () => ShowMainMenu());

            _settings = Panel("Settings", new Color(0.09f, 0.1f, 0.12f, 0.82f));
            Title(_settings, "SETTINGS", 44, 0.82f, 0.96f);
            Btn(_settings, "SWIPE SENS +", new Vector2(-200, 80), () => { GameSettings.SwipeSensitivity = Mathf.Min(2f, GameSettings.SwipeSensitivity + 0.25f); Toast($"Sens {GameSettings.SwipeSensitivity:0.00}"); });
            Btn(_settings, "SWIPE SENS -", new Vector2(200, 80), () => { GameSettings.SwipeSensitivity = Mathf.Max(0.5f, GameSettings.SwipeSensitivity - 0.25f); Toast($"Sens {GameSettings.SwipeSensitivity:0.00}"); });
            Btn(_settings, "TOGGLE TILT", new Vector2(0, 0), () => { GameSettings.TiltEnabled = !GameSettings.TiltEnabled; Toast(GameSettings.TiltEnabled ? "Tilt ON" : "Tilt OFF"); });
            Btn(_settings, "TEXT SCALE", new Vector2(0, -80), () => { GameSettings.TextScale = GameSettings.TextScale >= 1.25f ? 1f : GameSettings.TextScale + 0.25f; ApplyTextScale(); Toast($"Text x{GameSettings.TextScale:0.00}"); });
            Btn(_settings, "QUALITY CYCLE", new Vector2(-200, -160), () => { GameSettings.GraphicsQuality = (GameSettings.GraphicsQuality + 1) % 3; Toast("Quality " + GameSettings.GraphicsQuality); });
            Btn(_settings, "TOGGLE SFX", new Vector2(200, -160), () => { GameSettings.SfxEnabled = !GameSettings.SfxEnabled; Toast(GameSettings.SfxEnabled ? "SFX ON" : "SFX OFF"); });
            Btn(_settings, "SIGN-IN GOOGLE", new Vector2(-200, -240), () => Toast(AuthService.SignInAppleOrGoogleStub("google")));
            Btn(_settings, "SIGN-IN APPLE", new Vector2(200, -240), () => Toast(AuthService.SignInAppleOrGoogleStub("apple")));
            Btn(_settings, "CLOUD PULL", new Vector2(0, -320), () => Toast(CloudSaveService.Pull()));
            Btn(_settings, "BACK", new Vector2(0, -400), () => ShowMainMenu());

            _info = Panel("Info", new Color(0.06f, 0.08f, 0.1f, 0.82f));
            Title(_info, "INFO", 44, 0.82f, 0.96f);
            _infoBody = Sub(_info, "", 22, 0.35f, 0.78f);
            Btn(_info, "BACK", new Vector2(0, -360), () => ShowMainMenu());

            _tutorial = new GameObject("Tutorial");
            _tutorial.transform.SetParent(_canvas.transform, false);
            var tr = _tutorial.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.08f, 0.55f);
            tr.anchorMax = new Vector2(0.92f, 0.72f);
            _tutorial.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            _tutorialText = UiFactory.CreateText(_tutorial.transform, "Tip", "", 32, TextAnchor.MiddleCenter, Color.white);
            _tutorial.SetActive(false);

            ShowOnly(_boot);
        }

        void Update()
        {
            if (!_bootDone)
            {
                // Auto-run owns boot — do not yank back to menu over a live run
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Running)
                {
                    _bootDone = true;
                    return;
                }

                _bootTimer -= Time.unscaledDeltaTime;
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    _bootTimer = 0f;
                if (_bootTimer <= 0f)
                {
                    _bootDone = true;
                    DailyLoginService.ClaimStatus();
                    if (GameManager.Instance != null && GameManager.Instance.State != GameState.Running)
                        GameManager.Instance.EnterMainMenu();
                    if (_starterQueued) Toast("Starter Pack available in Shop!");
                }
            }
            else if (_menu != null && _menu.activeSelf && GameManager.Instance != null
                     && GameManager.Instance.State == GameState.MainMenu)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    GameManager.Instance.StartRun(RunDifficulty.Medium);
            }

            if (_hud != null && _hud.activeSelf && RunSession.Instance != null)
            {
                string diff = DifficultyDirector.Instance != null
                    ? DifficultyDirector.Instance.Profile.DisplayName
                    : "RUN";
                _hudScore.text = RunSession.Instance.Score.ToString("N0");
                _hudCoins.text = "◆  " + RunSession.Instance.CoinsThisRun;
                if (PowerUpController.Instance != null)
                {
                    float fill = PowerUpController.Instance.EnergyFill01;
                    if (_hudPowerFill != null)
                    {
                        _hudPowerFill.fillAmount = fill;
                        _hudPowerFill.color = PowerUpController.Instance.EnergyReady
                            ? Color.Lerp(new Color(1f, 0.9f, 0.35f), new Color(1f, 0.55f, 0.15f),
                                0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f))
                            : new Color(1f, 0.82f, 0.28f, 1f);
                    }
                    if (PowerUpController.Instance.EnergyReady)
                        _hudPower.text = "READY";
                    else if (!string.IsNullOrEmpty(PowerUpController.Instance.ActiveLabel)
                             && PowerUpController.Instance.ActiveLabel.IndexOf('·') >= 0)
                        _hudPower.text = Mathf.FloorToInt(fill * 100f) + "%";
                    else
                        _hudPower.text = Mathf.FloorToInt(fill * 100f) + "%";
                }
                else
                {
                    if (_hudPowerFill != null) _hudPowerFill.fillAmount = 0f;
                    _hudPower.text = diff;
                }

                if (_hudCombo != null && _hudComboFrame != null)
                {
                    int combo = RunSession.Instance.Combo;
                    if (combo > 0)
                    {
                        _hudComboFrame.SetActive(true);
                        _hudCombo.text = $"COMBO x{RunSession.Instance.ComboMultiplier:0.0}";
                        var rt = _hudComboFrame.GetComponent<RectTransform>();
                        if (_comboPulse > 0f)
                        {
                            _comboPulse -= Time.unscaledDeltaTime;
                            float s = 1f + _comboPulse * 0.35f;
                            if (rt != null) rt.localScale = new Vector3(s, s, 1f);
                        }
                        else if (rt != null)
                            rt.localScale = Vector3.one;
                    }
                    else
                    {
                        _hudComboFrame.SetActive(false);
                        var rt = _hudComboFrame.GetComponent<RectTransform>();
                        if (rt != null) rt.localScale = Vector3.one;
                    }
                }

                if (_hudGhost != null && _hudGhostFrame != null)
                {
                    var ghost = GhostRivalRunner.Instance;
                    if (ghost != null && ghost.IsRacing && !string.IsNullOrEmpty(ghost.HudLabel))
                    {
                        _hudGhostFrame.SetActive(true);
                        _hudGhost.text = ghost.HudLabel;
                    }
                    else
                        _hudGhostFrame.SetActive(false);
                }
            }
        }

        public void PulseCombo(int combo, float mult)
        {
            if (_hudCombo == null || _hudComboFrame == null) return;
            _comboPulse = 0.35f;
            _hudComboFrame.SetActive(combo > 0);
            _hudCombo.text = $"COMBO x{mult:0.0}";
        }

        public void QueueStarterPackOffer() => _starterQueued = true;

        public void ShowMainMenu()
        {
            var m = MetaProgress.Ensure().Data;
            _menuCurrency.text = $"Coins {m.bankedCoins} · Gems {m.gems} · Relics {m.relics}\n{CharacterRoster.SelectedDisplayName} · {BiomeSystem.DisplayName(BiomeSystem.Current)}";
            ShowOnly(_menu);
        }

        public void ShowHud() => ShowOnly(_hud);

        public void ShowPostRun(RunEndPayload p)
        {
            bool canRevive = RunSession.Instance != null
                             && !RunSession.Instance.ReviveUsed
                             && !RunSession.Instance.IsFinalized;
            bool adsOff = MetaProgress.Ensure().Data.adsRemoved;
            if (_btnReviveAd != null) _btnReviveAd.gameObject.SetActive(canRevive && !adsOff);
            if (_btnReviveGem != null) _btnReviveGem.gameObject.SetActive(canRevive);

            _postSummary.text =
                $"{p.deathReason}\n\nScore {p.score}\nCoins +{p.coinsEarned}  Gems +{p.gemsEarned}  Relics +{p.relicsEarned}\n" +
                $"Distance {p.distance:0}m  Near-misses {p.nearMisses}\n" +
                $"Mode {(GameManager.Instance != null ? DifficultyProfile.For(GameManager.Instance.CurrentDifficulty).DisplayName : "?")}\n" +
                (p.doubledCoins ? "Coins doubled\n" : "") +
                GhostRunService.Status();
            ShowOnly(_post);
        }

        public void ShowTutorial(string msg)
        {
            _tutorialText.text = msg;
            _tutorial.SetActive(true);
        }

        public void HideTutorial() => _tutorial.SetActive(false);

        void ShowUpgrades()
        {
            RefreshUpgrade();
            ShowOnly(_upgrade);
        }

        void RefreshUpgrade()
        {
            var m = MetaProgress.Ensure();
            string revive = m.Data.reviveLevel >= 1 ? "OWNED" : m.ReviveUpgradeCost + " coins";
            _upgradeInfo.text = $"Bank {m.Data.bankedCoins}\nMagnet Lv {m.Data.magnetRadiusLevel} ({m.MagnetUpgradeCost}) · Time Lv {m.Data.magnetDurationLevel}\n" +
                                $"Coin Mult Lv {m.Data.coinMultiplierLevel} x{m.CoinMultiplier:0.00} ({m.CoinUpgradeCost})\n" +
                                $"Energy Fill Lv {m.Data.energyFillLevel} · Boost Time Lv {m.Data.boostDurationLevel}\nShield start: {revive}";
        }

        void ShowLocker()
        {
            ShowOnly(_locker);
        }

        void BuildLockerButtons()
        {
            float y = 120f;
            foreach (var c in CharacterRoster.All)
            {
                var id = c.id;
                Btn(_locker, c.displayName, new Vector2(0, y), () =>
                {
                    var unlocked = MetaProgress.Ensure().GetUnlockedCharacters().Contains(id);
                    if (!unlocked)
                        Toast(CharacterRoster.TryUnlock(id) ? "Unlocked!" : $"Need {c.gemCost} gems");
                    else
                    {
                        Toast(CharacterRoster.Select(id) ? "Selected " + c.displayName : "Locked");
                        PlayerController.Instance?.ApplyCharacterColors();
                    }
                });
                y -= 70f;
            }
            Btn(_locker, "JUNGLE", new Vector2(-220, -240), () => { BiomeSystem.Select(BiomeId.JungleRuins); Toast("Jungle Ruins"); });
            Btn(_locker, "DESERT", new Vector2(0, -240), () =>
            {
                if (!BiomeSystem.IsUnlocked(BiomeId.DesertTombs)) { Toast("Unlock via runs/relics/distance"); return; }
                BiomeSystem.Select(BiomeId.DesertTombs); Toast("Desert Tombs");
            });
            Btn(_locker, "ICE", new Vector2(220, -240), () =>
            {
                if (!BiomeSystem.IsUnlocked(BiomeId.IceCaverns)) { Toast("Unlock via runs/relics/distance"); return; }
                BiomeSystem.Select(BiomeId.IceCaverns); Toast("Ice Caverns");
            });
            Btn(_locker, "CAVE", new Vector2(-120, -310), () =>
            {
                if (!BiomeSystem.IsUnlocked(BiomeId.CaveMines)) { Toast("Unlock Cave Mines via runs/relics"); return; }
                BiomeSystem.Select(BiomeId.CaveMines); Toast("Cave Mines");
            });
            Btn(_locker, "VOLCANO", new Vector2(120, -310), () =>
            {
                if (!BiomeSystem.IsUnlocked(BiomeId.VolcanicCrater)) { Toast("Unlock Volcano via runs/relics"); return; }
                BiomeSystem.Select(BiomeId.VolcanicCrater); Toast("Volcanic Crater");
            });

            float cy = -370f;
            foreach (var h in CosmeticRoster.Hats)
            {
                if (h.id == "hat_none") continue;
                // Hide off-season cosmetics unless already owned.
                if (CosmeticRoster.IsSeasonal(h.id) && !CosmeticRoster.IsFeaturedThisWeek(h.id)
                    && !MetaProgress.Ensure().HasCosmetic(h.id))
                    continue;
                var id = h.id;
                var label = (CosmeticRoster.IsFeaturedThisWeek(id) ? "★ " : "") + h.displayName;
                var cost = h.gemCost;
                Btn(_locker, "HAT:" + label, new Vector2(-160, cy), () =>
                {
                    if (!MetaProgress.Ensure().HasCosmetic(id))
                        Toast(CosmeticRoster.TryUnlock(id, true) ? "Hat unlocked!" : $"Need {cost} gems / not in season");
                    else
                    {
                        CosmeticRoster.SelectHat(id);
                        PlayerController.Instance?.ApplyCharacterColors();
                        Toast("Hat: " + label);
                    }
                });
                cy -= 55f;
            }
            cy = -370f;
            foreach (var p in CosmeticRoster.Pets)
            {
                if (p.id == "pet_none") continue;
                if (CosmeticRoster.IsSeasonal(p.id) && !CosmeticRoster.IsFeaturedThisWeek(p.id)
                    && !MetaProgress.Ensure().HasCosmetic(p.id))
                    continue;
                var id = p.id;
                var label = (CosmeticRoster.IsFeaturedThisWeek(id) ? "★ " : "") + p.displayName;
                var cost = p.gemCost;
                Btn(_locker, "PET:" + label, new Vector2(160, cy), () =>
                {
                    if (!MetaProgress.Ensure().HasCosmetic(id))
                        Toast(CosmeticRoster.TryUnlock(id, false) ? "Pet unlocked!" : $"Need {cost} gems / not in season");
                    else
                    {
                        CosmeticRoster.SelectPet(id);
                        PlayerController.Instance?.ApplyCharacterColors();
                        Toast("Pet: " + label);
                    }
                });
                cy -= 55f;
            }
        }

        void ShowShop() => ShowOnly(_shop);

        void ShowMissions()
        {
            if (_missionBody != null)
                _missionBody.text = MissionSystem.Summary() + "\n\n" + ArtifactHuntSystem.Status() +
                                    "\n\nAchievements:\n" + AchievementSystem.ListUnlocked() +
                                    "\n\n" + BattlePassService.Status() +
                                    "\n\n" + EventService.SeasonalLockerBlurb();
            ShowOnly(_missions);
        }

        void ShowSettings() => ShowOnly(_settings);

        void ShowMore()
        {
            ShowInfo(EventService.Status() + "\n\n" + LeaderboardService.GlobalBoard() + "\n\n" +
                     LeaderboardService.FriendsBoard() + "\n\n" + GhostRunService.Status() +
                     "\n\nSDK: UGS=" + SdkBootstrap.UgsReady + " Ads=" + SdkBootstrap.AdsReady + " IAP=" + SdkBootstrap.IapReady +
                     "\nColorblind: shapes+colors on hazards\nCylinder=safe branch, Cube=risk");
        }

        void ShowInfo(string body)
        {
            _infoBody.text = body;
            ShowOnly(_info);
        }

        void TryAdRevive()
        {
            MonetizationService.WatchRewardedRevive(ok =>
            {
                if (ok)
                {
                    GameManager.Instance?.ContinueAfterRevive();
                    Toast("Revived via rewarded ad");
                }
                else Toast("Revive unavailable");
            });
        }

        void TryGemRevive()
        {
            if (MonetizationService.ReviveWithGems())
            {
                GameManager.Instance?.ContinueAfterRevive();
                Toast("Revived for 5 gems");
            }
            else Toast("Need 5 gems / already used");
        }

        void TryDoubleCoins()
        {
            MonetizationService.WatchDoubleCoins(ok =>
            {
                Toast(ok ? "Coins doubled!" : "Already doubled / ad failed");
                if (GameManager.Instance?.LastPayload != null)
                    ShowPostRun(GameManager.Instance.LastPayload);
            });
        }

        void ShareRun()
        {
            var p = GameManager.Instance?.LastPayload;
            string card = p == null ? "Temple Sprint" : $"Temple Sprint: {p.score} pts, {p.distance:0}m — {BiomeSystem.DisplayName(BiomeSystem.Current)}";
            GUIUtility.systemCopyBuffer = card;
            AnalyticsService.Track("share_card", 1);
            Toast("Run summary copied to clipboard");
        }

        void BackToMenuOrPost()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.PostRun && GameManager.Instance.LastPayload != null)
                ShowPostRun(GameManager.Instance.LastPayload);
            else ShowMainMenu();
        }

        void Toast(string msg) => ShowTutorial(msg);

        void ApplyTextScale()
        {
            var scaler = _canvas != null ? _canvas.GetComponent<CanvasScaler>() : null;
            if (scaler == null) return;
            bool portrait = Screen.height >= Screen.width;
            Vector2 bas = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.referenceResolution = bas / Mathf.Max(0.75f, GameSettings.TextScale);
            scaler.matchWidthOrHeight = portrait ? 0.6f : 0.5f;
        }

        void AddChromeBar(GameObject parent, bool top)
        {
            var bar = new GameObject(top ? "TopBar" : "BottomBar");
            bar.transform.SetParent(parent.transform, false);
            var img = bar.AddComponent<Image>();
            img.color = top
                ? new Color(0.08f, 0.1f, 0.09f, 0.78f)
                : new Color(0.06f, 0.08f, 0.07f, 0.82f);
            var rt = img.rectTransform;
            if (top)
            {
                rt.anchorMin = new Vector2(0f, 0.78f);
                rt.anchorMax = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.28f);
            }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Button BigPlayBtn(GameObject parent, UnityEngine.Events.UnityAction action)
        {
            var b = UiFactory.CreateButton(parent.transform, "PLAY", "PLAY", new Vector2(0f, 70f), new Vector2(420f, 96f));
            b.onClick.AddListener(action);
            return b;
        }

        void ShowOnly(GameObject panel)
        {
            _boot.SetActive(panel == _boot);
            _menu.SetActive(panel == _menu);
            _hud.SetActive(panel == _hud);
            _post.SetActive(panel == _post);
            _upgrade.SetActive(panel == _upgrade);
            _locker.SetActive(panel == _locker);
            _shop.SetActive(panel == _shop);
            _missions.SetActive(panel == _missions);
            _settings.SetActive(panel == _settings);
            _info.SetActive(panel == _info);
            if (panel != _hud) HideTutorial();
        }

        GameObject Panel(string name, Color c)
        {
            // Stone-tablet chrome for menus; keep HUD overlay lightweight.
            return UiFactory.CreateStonePanel(_canvas.transform, name, c).gameObject;
        }

        Text Title(GameObject parent, string t, int size, float amin, float amax)
        {
            var text = UiFactory.CreateText(parent.transform, "Title", t, size, TextAnchor.UpperCenter, new Color(0.95f, 0.82f, 0.35f));
            text.rectTransform.anchorMin = new Vector2(0.05f, amin);
            text.rectTransform.anchorMax = new Vector2(0.95f, amax);
            return text;
        }

        Text Sub(GameObject parent, string t, int size, float amin, float amax)
        {
            var text = UiFactory.CreateText(parent.transform, "Sub", t, size, TextAnchor.MiddleCenter, Color.white);
            text.rectTransform.anchorMin = new Vector2(0.06f, amin);
            text.rectTransform.anchorMax = new Vector2(0.94f, amax);
            return text;
        }

        Text CornerText(GameObject parent, string name, TextAnchor anchor)
        {
            var text = UiFactory.CreateText(parent.transform, name, "", 34, anchor, Color.white);
            if (anchor == TextAnchor.UpperLeft)
            {
                text.rectTransform.anchorMin = new Vector2(0.04f, 0.82f);
                text.rectTransform.anchorMax = new Vector2(0.5f, 0.98f);
            }
            else
            {
                text.rectTransform.anchorMin = new Vector2(0.5f, 0.82f);
                text.rectTransform.anchorMax = new Vector2(0.96f, 0.98f);
                text.color = new Color(1f, 0.85f, 0.3f);
            }
            return text;
        }

        Button Btn(GameObject parent, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            var b = UiFactory.CreateButton(parent.transform, label, label, pos, new Vector2(240f, 58f));
            b.onClick.AddListener(action);
            return b;
        }

        void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
