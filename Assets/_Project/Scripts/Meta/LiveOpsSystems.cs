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
            // Fixed daily trio for clarity
            _slotTypes[0] = MissionType.CollectCoins; _titles[0] = "Collect 200 coins"; _targets[0] = 200;
            _slotTypes[1] = MissionType.UsePowerUps; _titles[1] = "Use 3 power-ups"; _targets[1] = 3;
            _slotTypes[2] = MissionType.DodgeObstacles; _titles[2] = "Dodge 15 obstacles"; _targets[2] = 15;
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
        public static string CurrentEventName
        {
            get
            {
                var cfg = RemoteConfigService.GetString("event_name", "");
                if (!string.IsNullOrEmpty(cfg)) return cfg;
                int week = DateTime.UtcNow.DayOfYear / 7;
                return (week % 3) switch
                {
                    0 => "Jungle Festival",
                    1 => "Desert Gold Rush",
                    _ => "Ice Relic Hunt"
                };
            }
        }

        public static float EventCoinBonus => RemoteConfigService.GetFloat("event_coin_bonus", 1.1f);

        public static string Status() =>
            $"Live Event: {CurrentEventName}\nCoin bonus x{EventCoinBonus:0.00}\nEvent leaderboard resets weekly.";
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
