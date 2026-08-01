using System;
using System.Collections.Generic;
using UnityEngine;

namespace TempleSprint
{
    public class RunEndPayload
    {
        public int score;
        public int coinsEarned;
        public int gemsEarned;
        public int relicsEarned;
        public float distance;
        public int nearMisses;
        public string deathReason;
        public bool doubledCoins;
    }

    public class RunSession : MonoBehaviour
    {
        public static RunSession Instance { get; private set; }

        public int Score { get; private set; }
        public int CoinsThisRun { get; private set; }
        public int GemsThisRun { get; private set; }
        public int RelicsThisRun { get; private set; }
        public float Distance { get; private set; }
        public int NearMisses { get; private set; }
        public int Combo { get; private set; }
        public float ComboMultiplier => Combo <= 0 ? 1f : Mathf.Clamp(1f + Combo * 0.25f, 1f, 5f);
        public bool IsAlive { get; private set; } = true;
        public bool ReviveUsed { get; private set; }
        public bool IsFinalized { get; private set; }

        public event Action<RunEndPayload> OnRunEnded;

        float _scoreAccumulator;
        float _comboTimer;
        float _elapsed;
        MetaProgress _meta;
        readonly List<float> _ghostTimes = new List<float>();
        readonly List<float> _ghostDistances = new List<float>();
        readonly List<int> _ghostLanes = new List<int>();
        RunEndPayload _pending;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _meta = MetaProgress.Ensure();
        }

        public void Begin()
        {
            Score = CoinsThisRun = GemsThisRun = RelicsThisRun = 0;
            Distance = 0f;
            NearMisses = 0;
            Combo = 0;
            IsAlive = true;
            ReviveUsed = false;
            IsFinalized = false;
            _scoreAccumulator = 0f;
            _comboTimer = 0f;
            _elapsed = 0f;
            _ghostTimes.Clear();
            _ghostDistances.Clear();
            _ghostLanes.Clear();
            _pending = null;
        }

        public void Tick(float deltaDistance, float speed)
        {
            if (!IsAlive) return;
            Distance += deltaDistance;
            _elapsed += Time.deltaTime;
            if (_comboTimer > 0f)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f) Combo = 0;
            }
            float comboScore = ComboMultiplier;
            _scoreAccumulator += deltaDistance * (1f + speed * 0.05f) * comboScore;
            Score = Mathf.FloorToInt(_scoreAccumulator) + CoinsThisRun * 10 + GemsThisRun * 50 + RelicsThisRun * 100;
            if (_ghostDistances.Count == 0 || Distance - _ghostDistances[_ghostDistances.Count - 1] >= 2f)
            {
                _ghostTimes.Add(_elapsed);
                _ghostDistances.Add(Distance);
                _ghostLanes.Add(PlayerController.Instance != null ? PlayerController.Instance.Lane : 1);
            }
        }

        public void AddCoins(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            float mult = _meta.CoinMultiplier * CharacterRoster.PassiveCoinMult
                         * EventService.EventCoinBonus * CosmeticRoster.PetPassives.CoinMult
                         * ComboMultiplier;
            if (PowerUpController.Instance != null)
                mult *= PowerUpController.Instance.ScoreMultiplier;
            int gained = Mathf.Max(1, Mathf.RoundToInt(amount * mult));
            CoinsThisRun += gained;
            Score += gained * 10;
            PowerUpController.Instance?.AddEnergyFromCoins(gained);
        }

        public void AddGems(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            GemsThisRun += amount;
            Score += amount * 50;
            MissionSystem.Report(MissionType.CollectGems, amount);
        }

        public void AddRelic(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            RelicsThisRun += amount;
            Score += amount * 100;
        }

        public void RegisterNearMiss()
        {
            NearMisses++;
            Combo = Mathf.Min(Combo + 1, 16);
            _comboTimer = 2.6f;
            MissionSystem.Report(MissionType.DodgeObstacles, 1);
            _meta.Data.totalObstaclesDodged++;
            AudioHooks.Instance?.PlayNearMiss();
            GameUI.Instance?.PulseCombo(Combo, ComboMultiplier);
        }

        public bool TryRevive()
        {
            if (IsAlive || ReviveUsed || IsFinalized) return false;
            ReviveUsed = true;
            IsAlive = true;
            _pending = null;
            return true;
        }

        public void EndRun(string reason, bool allowRevivePrompt = true)
        {
            if (!IsAlive) return;
            IsAlive = false;
            PowerUpController.Instance?.ClearTimers();
            Time.timeScale = 1f;
            Debug.Log("[RunSession] EndRun: " + reason);

            if (AudioHooks.Instance != null)
            {
                bool fell = reason != null && reason.Contains("Fell");
                if (fell) AudioHooks.Instance.PlayFall();
                else AudioHooks.Instance.PlayDeath();
            }

            if (!AntiCheatService.ValidateScore(Score, Distance, CoinsThisRun))
            {
                Score = Mathf.Min(Score, Mathf.FloorToInt(Distance * 2f));
                AnalyticsService.Track("anticheat_flag", Score);
            }

            _pending = new RunEndPayload
            {
                score = Score,
                coinsEarned = CoinsThisRun,
                gemsEarned = GemsThisRun,
                relicsEarned = RelicsThisRun,
                distance = Distance,
                nearMisses = NearMisses,
                deathReason = reason,
                doubledCoins = false
            };
            OnRunEnded?.Invoke(_pending);
        }

        public void FinalizeAndBank()
        {
            if (IsFinalized) return;
            IsFinalized = true;
            var payload = _pending ?? new RunEndPayload
            {
                score = Score,
                coinsEarned = CoinsThisRun,
                gemsEarned = GemsThisRun,
                relicsEarned = RelicsThisRun,
                distance = Distance,
                nearMisses = NearMisses,
                deathReason = "Finished"
            };

            _meta.BankCoins(payload.coinsEarned);
            _meta.AddGems(payload.gemsEarned);
            _meta.AddRelics(payload.relicsEarned);
            _meta.AddDistance(payload.distance);
            _meta.RegisterBestScore(payload.score);
            _meta.IncrementRuns();
            GhostRunService.SaveTimedSample(_ghostTimes, _ghostDistances, _ghostLanes, payload.score);
            LeaderboardService.Submit(payload.score, payload.distance);
            BattlePassService.AddXp(Mathf.FloorToInt(payload.distance / 10f) + payload.coinsEarned);
            // Missions: coins only here (not per-pickup) to avoid double count
            MissionSystem.Report(MissionType.CollectCoins, payload.coinsEarned);
            MissionSystem.Report(MissionType.RunDistance, Mathf.FloorToInt(payload.distance));
            AchievementSystem.EvaluateRun(payload);
            ArtifactHuntSystem.ReportRun(payload);
            AnalyticsService.Track("run_end", payload.score);
            _ = CloudSaveService.PushAsync();

            if (_meta.Data.totalRuns == 2 && !_meta.Data.starterPackBought)
                GameUI.Instance?.QueueStarterPackOffer();
        }

        public bool ApplyDoubleCoins()
        {
            if (_pending != null && _pending.doubledCoins) return false;
            if (CoinsThisRun <= 0 && (_pending == null || _pending.coinsEarned <= 0)) return false;
            int bonus = _pending != null ? _pending.coinsEarned : CoinsThisRun;
            CoinsThisRun += bonus;
            if (_pending != null)
            {
                _pending.coinsEarned = CoinsThisRun;
                _pending.doubledCoins = true;
            }
            if (IsFinalized) _meta.BankCoins(bonus);
            return true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
