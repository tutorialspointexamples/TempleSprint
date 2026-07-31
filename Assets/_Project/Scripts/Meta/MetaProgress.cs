using System;
using System.Collections.Generic;
using UnityEngine;

namespace TempleSprint
{
    [Serializable]
    public class MetaSaveData
    {
        public int bankedCoins;
        public int gems;
        public int relics;
        public int magnetRadiusLevel;
        public int coinMultiplierLevel;
        public int reviveLevel;
        public bool tutorialCompleted;
        public int totalRuns;
        public float totalDistance;
        public int totalCoinsEarned;
        public int totalObstaclesDodged;
        public bool desertUnlocked;
        public bool iceUnlocked;
        public bool caveUnlocked;
        public bool volcanoUnlocked;
        public string unlockedCharacters = "scout_default";
        public string selectedCharacter = "scout_default";
        public int loginStreak;
        public string lastLoginDay = "";
        public bool adsRemoved;
        public bool starterPackBought;
        public bool battlePassOwned;
        public int battlePassXp;
        public int seasonLevel;
        public bool guestAccount = true;
        public string playerName = "Runner";
        public string cloudSaveJson = "";
        public string unlockedAchievements = "";
        public int missionDaySeed;
        public int missionProgress0;
        public int missionProgress1;
        public int missionProgress2;
        public bool missionClaimed0;
        public bool missionClaimed1;
        public bool missionClaimed2;
        public int bestScore;
        public string ghostRunJson = "";
    }

    public class MetaProgress
    {
        const string SaveKey = "TempleSprint.Meta.v2";

        public MetaSaveData Data { get; private set; } = new MetaSaveData();
        public static MetaProgress Instance { get; private set; }

        public static MetaProgress Ensure()
        {
            if (Instance == null)
            {
                Instance = new MetaProgress();
                Instance.Load();
            }
            return Instance;
        }

        public void Load()
        {
            string key = PlayerPrefs.HasKey(SaveKey) ? SaveKey : "TempleSprint.Meta";
            if (PlayerPrefs.HasKey(key))
            {
                try { Data = JsonUtility.FromJson<MetaSaveData>(PlayerPrefs.GetString(key)) ?? new MetaSaveData(); }
                catch { Data = new MetaSaveData(); }
            }
            else Data = new MetaSaveData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            CloudSaveService.PushLocal(Data);
        }

        public int MagnetRadiusBonus => Data.magnetRadiusLevel;
        public float CoinMultiplier => 1f + Data.coinMultiplierLevel * 0.15f;
        public int ReviveCharges => Mathf.Min(1, Data.reviveLevel);
        public int MagnetUpgradeCost => 80 + Data.magnetRadiusLevel * 60;
        public int CoinUpgradeCost => 100 + Data.coinMultiplierLevel * 75;
        public int ReviveUpgradeCost => Data.reviveLevel >= 1 ? -1 : 250;

        public bool TryBuyMagnet()
        {
            if (Data.bankedCoins < MagnetUpgradeCost) return false;
            Data.bankedCoins -= MagnetUpgradeCost;
            Data.magnetRadiusLevel++;
            Save();
            return true;
        }

        public bool TryBuyCoinMultiplier()
        {
            if (Data.bankedCoins < CoinUpgradeCost) return false;
            Data.bankedCoins -= CoinUpgradeCost;
            Data.coinMultiplierLevel++;
            Save();
            return true;
        }

        public bool TryBuyRevive()
        {
            if (Data.reviveLevel >= 1 || Data.bankedCoins < ReviveUpgradeCost) return false;
            Data.bankedCoins -= ReviveUpgradeCost;
            Data.reviveLevel = 1;
            Save();
            return true;
        }

        public void BankCoins(int amount)
        {
            Data.bankedCoins += Mathf.Max(0, amount);
            Data.totalCoinsEarned += Mathf.Max(0, amount);
            Save();
        }

        public void AddGems(int amount)
        {
            Data.gems += Mathf.Max(0, amount);
            Save();
        }

        public void AddRelics(int amount)
        {
            Data.relics += Mathf.Max(0, amount);
            if (Data.relics >= 3) Data.desertUnlocked = true;
            if (Data.relics >= 8) Data.iceUnlocked = true;
            if (Data.relics >= 12) Data.caveUnlocked = true;
            if (Data.relics >= 18) Data.volcanoUnlocked = true;
            Save();
        }

        public bool SpendGems(int amount)
        {
            if (Data.gems < amount) return false;
            Data.gems -= amount;
            Save();
            return true;
        }

        public void MarkTutorialComplete()
        {
            Data.tutorialCompleted = true;
            Save();
        }

        public void IncrementRuns()
        {
            Data.totalRuns++;
            if (Data.totalRuns >= 3) Data.desertUnlocked = true;
            if (Data.totalRuns >= 8) Data.iceUnlocked = true;
            if (Data.totalRuns >= 12) Data.caveUnlocked = true;
            if (Data.totalRuns >= 18) Data.volcanoUnlocked = true;
            Save();
            AnalyticsService.Track("run_complete", Data.totalRuns);
        }

        public void AddDistance(float d)
        {
            Data.totalDistance += d;
            if (Data.totalDistance >= 500f) Data.desertUnlocked = true;
            if (Data.totalDistance >= 2000f) Data.iceUnlocked = true;
            if (Data.totalDistance >= 3500f) Data.caveUnlocked = true;
            if (Data.totalDistance >= 6000f) Data.volcanoUnlocked = true;
            Save();
        }

        public void RegisterBestScore(int score)
        {
            if (score > Data.bestScore)
            {
                Data.bestScore = score;
                Save();
            }
        }

        public HashSet<string> GetUnlockedCharacters()
        {
            var set = new HashSet<string>();
            foreach (var p in (Data.unlockedCharacters ?? "scout_default").Split(','))
                if (!string.IsNullOrEmpty(p)) set.Add(p.Trim());
            set.Add("scout_default");
            return set;
        }

        public bool UnlockCharacter(string id)
        {
            var set = GetUnlockedCharacters();
            if (!set.Add(id)) return false;
            var list = new System.Collections.Generic.List<string>(set);
            list.Sort();
            Data.unlockedCharacters = string.Join(",", list);
            Save();
            return true;
        }

        public bool HasAchievement(string id) =>
            ("," + (Data.unlockedAchievements ?? "") + ",").Contains("," + id + ",");

        public void UnlockAchievement(string id)
        {
            if (HasAchievement(id)) return;
            Data.unlockedAchievements = string.IsNullOrEmpty(Data.unlockedAchievements)
                ? id
                : Data.unlockedAchievements + "," + id;
            Save();
        }
    }
}
