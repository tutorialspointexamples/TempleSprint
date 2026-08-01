using System;
using UnityEngine;

namespace TempleSprint
{
    public enum MissionType { CollectCoins, UsePowerUps, DodgeObstacles, RunDistance, CollectGems }

    public static class MissionSystem
    {
        static MissionType[] _slotTypes = new MissionType[3];
        static string[] _titles = new string[3];
        static int[] _targets = new int[3];

        static readonly (MissionType type, string titleFmt, int min, int max, int step)[] MissionPool =
        {
            (MissionType.CollectCoins, "Collect {0} coins", 150, 400, 50),
            (MissionType.UsePowerUps, "Use {0} power-ups", 2, 6, 1),
            (MissionType.DodgeObstacles, "Dodge {0} obstacles", 10, 30, 5),
            (MissionType.RunDistance, "Run {0}m in a day", 300, 1200, 100),
            (MissionType.CollectGems, "Collect {0} gems", 2, 8, 1),
        };

        public static void EnsureDaily()
        {
            var meta = MetaProgress.Ensure();
            var m = meta.Data;
            int seed = DateTime.UtcNow.DayOfYear + DateTime.UtcNow.Year * 1000;
            if (m.missionDaySeed != seed)
            {
                m.missionDaySeed = seed;
                m.missionProgress0 = m.missionProgress1 = m.missionProgress2 = 0;
                m.missionClaimed0 = m.missionClaimed1 = m.missionClaimed2 = false;
                meta.Save();
            }
            // Rotating daily trio — seeded so every UTC day reshuffles types + targets.
            var rng = new System.Random(seed ^ 0x5F3759DF);
            var picked = new bool[MissionPool.Length];
            for (int slot = 0; slot < 3; slot++)
            {
                int choice = -1;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    int i = rng.Next(MissionPool.Length);
                    if (picked[i]) continue;
                    choice = i;
                    break;
                }
                if (choice < 0)
                {
                    for (int i = 0; i < picked.Length; i++)
                        if (!picked[i]) { choice = i; break; }
                }
                picked[choice] = true;
                var def = MissionPool[choice];
                int steps = Mathf.Max(1, (def.max - def.min) / def.step);
                int target = def.min + rng.Next(steps + 1) * def.step;
                _slotTypes[slot] = def.type;
                _targets[slot] = target;
                _titles[slot] = string.Format(def.titleFmt, target);
            }
        }

        public static string GetTitle(int index) { EnsureDaily(); return _titles[Mathf.Clamp(index, 0, 2)]; }
        public static int GetTarget(int index) { EnsureDaily(); return _targets[Mathf.Clamp(index, 0, 2)]; }

        public static int GetProgress(int index)
        {
            EnsureDaily();
            var m = MetaProgress.Ensure().Data;
            return index switch { 0 => m.missionProgress0, 1 => m.missionProgress1, 2 => m.missionProgress2, _ => 0 };
        }

        public static bool IsClaimed(int index)
        {
            var m = MetaProgress.Ensure().Data;
            return index switch { 0 => m.missionClaimed0, 1 => m.missionClaimed1, 2 => m.missionClaimed2, _ => true };
        }

        public static void Report(MissionType type, int amount)
        {
            if (amount <= 0) return;
            EnsureDaily();
            var meta = MetaProgress.Ensure();
            var m = meta.Data;
            for (int i = 0; i < 3; i++)
            {
                if (_slotTypes[i] != type) continue;
                int next = Mathf.Min(_targets[i], GetProgress(i) + amount);
                if (i == 0) m.missionProgress0 = next;
                if (i == 1) m.missionProgress1 = next;
                if (i == 2) m.missionProgress2 = next;
            }
            meta.Save();
        }

        public static bool TryClaim(int index)
        {
            EnsureDaily();
            if (IsClaimed(index) || GetProgress(index) < GetTarget(index)) return false;
            var meta = MetaProgress.Ensure();
            var m = meta.Data;
            if (index == 0) m.missionClaimed0 = true;
            if (index == 1) m.missionClaimed1 = true;
            if (index == 2) m.missionClaimed2 = true;
            meta.BankCoins(50 + index * 25);
            meta.AddGems(index == 2 ? 2 : 1);
            return true;
        }

        public static string Summary()
        {
            EnsureDaily();
            return $"{GetTitle(0)}: {GetProgress(0)}/{GetTarget(0)} {(IsClaimed(0) ? "(claimed)" : "")}\n" +
                   $"{GetTitle(1)}: {GetProgress(1)}/{GetTarget(1)} {(IsClaimed(1) ? "(claimed)" : "")}\n" +
                   $"{GetTitle(2)}: {GetProgress(2)}/{GetTarget(2)} {(IsClaimed(2) ? "(claimed)" : "")}";
        }
    }

    public static class AchievementSystem
    {
        public static void Unlock(string id)
        {
            var meta = MetaProgress.Ensure();
            if (meta.HasAchievement(id)) return;
            meta.UnlockAchievement(id);
            meta.BankCoins(25);
            meta.AddGems(1);
            AnalyticsService.Track("achievement", id.GetHashCode());
        }

        public static void EvaluateRun(RunEndPayload p)
        {
            if (p.distance >= 100f) Unlock("distance_100");
            if (p.distance >= 500f) Unlock("distance_500");
            if (p.coinsEarned >= 100) Unlock("coins_100");
            if (p.nearMisses >= 10) Unlock("dodger");
            if (p.relicsEarned >= 1) Unlock("first_relic");
            if (p.gemsEarned >= 1) Unlock("first_gem");
            if (MetaProgress.Ensure().Data.totalRuns >= 10) Unlock("runs_10");
        }

        public static string ListUnlocked()
        {
            var s = MetaProgress.Ensure().Data.unlockedAchievements;
            return string.IsNullOrEmpty(s) ? "(none yet)" : s.Replace(",", ", ");
        }
    }

    public static class DailyLoginService
    {
        public static string ClaimStatus()
        {
            var meta = MetaProgress.Ensure();
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (meta.Data.lastLoginDay == today)
                return $"Streak {meta.Data.loginStreak} — already claimed today";

            string yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            meta.Data.loginStreak = meta.Data.lastLoginDay == yesterday ? meta.Data.loginStreak + 1 : 1;
            meta.Data.lastLoginDay = today;
            int coins = 20 + meta.Data.loginStreak * 5;
            int gems = meta.Data.loginStreak % 7 == 0 ? 5 : 0;
            meta.BankCoins(coins);
            if (gems > 0) meta.AddGems(gems);
            return $"Day {meta.Data.loginStreak}: +{coins} coins" + (gems > 0 ? $" +{gems} gems" : "");
        }
    }

    public static class EventService
    {
        public static int WeekIndex => DateTime.UtcNow.DayOfYear / 7;

        public static string CurrentEventName
        {
            get
            {
                var cfg = RemoteConfigService.GetString("event_name", "");
                if (!string.IsNullOrEmpty(cfg)) return cfg;
                return (WeekIndex % 3) switch
                {
                    0 => "Jungle Festival",
                    1 => "Desert Gold Rush",
                    _ => "Ice Relic Hunt"
                };
            }
        }

        /// <summary>Weekly event biases a biome look + stage mix without replacing unlock progression.</summary>
        public static BiomeId FeaturedBiome => (WeekIndex % 3) switch
        {
            0 => BiomeId.JungleRuins,
            1 => BiomeId.DesertTombs,
            _ => BiomeId.IceCaverns
        };

        public static bool IsFeaturedBiome(BiomeId id) => id == FeaturedBiome;

        /// <summary>Original festival accent for limited-time runway / backdrop dressing.</summary>
        public static Color FestivalAccent => (WeekIndex % 3) switch
        {
            0 => new Color(0.95f, 0.72f, 0.18f),   // Jungle Festival gold
            1 => new Color(0.92f, 0.42f, 0.18f),   // Desert Gold Rush copper
            _ => new Color(0.45f, 0.78f, 0.98f)    // Ice Relic Hunt frost
        };

        public static Color FestivalTrim => (WeekIndex % 3) switch
        {
            0 => new Color(0.28f, 0.62f, 0.32f),
            1 => new Color(0.85f, 0.7f, 0.28f),
            _ => new Color(0.75f, 0.88f, 1f)
        };

        public static float EventCoinBonus => RemoteConfigService.GetFloat("event_coin_bonus", 1.15f);

        public static float EventStageBias(BiomeId id) =>
            id == FeaturedBiome ? 1.25f : 1f;

        public static string SeasonalLockerBlurb()
        {
            var ids = CosmeticRoster.FeaturedSeasonalIds;
            string a = CosmeticRoster.GetHat(ids[0]).displayName;
            string b = CosmeticRoster.GetPet(ids[1]).displayName;
            return $"Seasonal locker: {a} + {b} (discounted this week)";
        }

        public static string Status() =>
            $"Live Event: {CurrentEventName}\nFeatured biome: {BiomeSystem.DisplayName(FeaturedBiome)}\n" +
            $"Event dressing: ON for featured biome runs\n" +
            $"Coin bonus x{EventCoinBonus:0.00}\n{SeasonalLockerBlurb()}\n" +
            ArtifactHuntSystem.Status() + "\nEvent leaderboard resets weekly.";
    }

    /// <summary>Weekly global artifact hunt — gather relics/distance toward a shared-style personal goal.</summary>
    public static class ArtifactHuntSystem
    {
        public static int WeekSeed => DateTime.UtcNow.Year * 100 + EventService.WeekIndex;

        public static string ArtifactName => (EventService.WeekIndex % 3) switch
        {
            0 => "Jade Serpent Idol",
            1 => "Sunstone Scarab",
            _ => "Frozen Heart Reliquary"
        };

        public static int RelicTarget => 3 + (EventService.WeekIndex % 3);
        public static int DistanceTarget => 800 + (EventService.WeekIndex % 4) * 200;

        public static void EnsureWeek()
        {
            var meta = MetaProgress.Ensure();
            var m = meta.Data;
            if (m.artifactWeekSeed == WeekSeed) return;
            m.artifactWeekSeed = WeekSeed;
            m.artifactRelics = 0;
            m.artifactDistance = 0f;
            m.artifactClaimed = false;
            meta.Save();
        }

        public static void ReportRun(RunEndPayload p)
        {
            if (p == null) return;
            EnsureWeek();
            var meta = MetaProgress.Ensure();
            meta.Data.artifactRelics += Mathf.Max(0, p.relicsEarned);
            meta.Data.artifactDistance += Mathf.Max(0f, p.distance);
            meta.Save();
        }

        public static bool IsComplete
        {
            get
            {
                EnsureWeek();
                var m = MetaProgress.Ensure().Data;
                return m.artifactRelics >= RelicTarget && m.artifactDistance >= DistanceTarget;
            }
        }

        public static bool TryClaim()
        {
            EnsureWeek();
            var meta = MetaProgress.Ensure();
            if (meta.Data.artifactClaimed || !IsComplete) return false;
            meta.Data.artifactClaimed = true;
            meta.BankCoins(120);
            meta.AddGems(4);
            meta.AddRelics(1);
            AchievementSystem.Unlock("artifact_hunt");
            return true;
        }

        public static string Status()
        {
            EnsureWeek();
            var m = MetaProgress.Ensure().Data;
            string claim = m.artifactClaimed ? " (claimed)" : IsComplete ? " — READY" : "";
            return $"Artifact Hunt: {ArtifactName}{claim}\n" +
                   $"Relics {m.artifactRelics}/{RelicTarget} · Distance {m.artifactDistance:0}/{DistanceTarget}m";
        }
    }

    public static class BattlePassService
    {
        public static void AddXp(int xp)
        {
            var m = MetaProgress.Ensure();
            m.Data.battlePassXp += Mathf.Max(0, xp);
            while (m.Data.battlePassXp >= 100)
            {
                m.Data.battlePassXp -= 100;
                m.Data.seasonLevel++;
                m.BankCoins(30);
                if (m.Data.battlePassOwned) m.AddGems(1);
            }
            m.Save();
        }

        public static string Status()
        {
            var d = MetaProgress.Ensure().Data;
            return $"Season Lv {d.seasonLevel}  XP {d.battlePassXp}/100\n" +
                   (d.battlePassOwned ? "Premium track active" : "Free track — buy Season Pass in Shop");
        }

        public static void BuyPass(Action<string> onDone) => ShopService.BuyBattlePass(onDone);
    }
}
