using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TempleSprint
{
    public static class LeaderboardService
    {
        static readonly List<(string name, int score)> Friends = new List<(string, int)>
        {
            ("You", 0), ("Alex", 4200), ("Sam", 3100), ("Riley", 2800), ("Jordan", 1900)
        };

        public static void Submit(int score, float distance)
        {
            Friends[0] = (MetaProgress.Ensure().Data.playerName, Mathf.Max(Friends[0].score, score));
            Friends.Sort((a, b) => b.score.CompareTo(a.score));
            AnalyticsService.Track("leaderboard_submit", score);
        }

        public static string GlobalBoard()
        {
            var best = MetaProgress.Ensure().Data.bestScore;
            return $"Weekly Global\n1. TempleAce — {Mathf.Max(9800, best + 500)}\n2. RuinRunner — {Mathf.Max(7600, best + 200)}\n3. {MetaProgress.Ensure().Data.playerName} — {best}\n(Submit via UGS Leaderboards when enabled)";
        }

        public static string FriendsBoard()
        {
            var sb = new StringBuilder("Friends\n");
            int i = 1;
            foreach (var e in Friends)
            {
                sb.AppendLine($"{i}. {e.name} — {e.score}");
                if (i++ >= 5) break;
            }
            return sb.ToString();
        }
    }

    public static class GhostRunService
    {
        [Serializable]
        class GhostData { public float[] samples; public int score; }

        public static void SaveSample(List<float> samples, int score)
        {
            var meta = MetaProgress.Ensure();
            if (score < meta.Data.bestScore && !string.IsNullOrEmpty(meta.Data.ghostRunJson)) return;
            meta.Data.ghostRunJson = JsonUtility.ToJson(new GhostData { samples = samples.ToArray(), score = score });
            meta.Save();
        }

        public static bool HasGhost => !string.IsNullOrEmpty(MetaProgress.Ensure().Data.ghostRunJson);

        public static string Status() => HasGhost
            ? "Ghost Run ready — race your best distance markers next run."
            : "Complete a run to record a ghost.";
    }

    public static class ShopService
    {
        public static string Catalog()
        {
            var d = MetaProgress.Ensure().Data;
            var c = SdkConfig.Get();
            return "SHOP\n" +
                   $"Gems: {d.gems}  Coins: {d.bankedCoins}\nAds removed: {d.adsRemoved}\n\n" +
                   $"Gem Pack S ({c.productGemsS}) — 40 gems\n" +
                   $"Gem Pack M ({c.productGemsM}) — 250 gems\n" +
                   $"Gem Pack L ({c.productGemsL}) — 1200 gems\n" +
                   $"Remove Ads ({c.productRemoveAds})\n" +
                   $"Starter Pack ({c.productStarterPack})\n" +
                   $"Season Pass ({c.productBattlePass})\n" +
                   $"Event: {EventService.CurrentEventName}";
        }

        public static void BuyGemPack(int tier, Action<string> onDone)
        {
            SdkBootstrap.Ensure();
            var c = SdkConfig.Get();
            string id = tier switch { 0 => c.productGemsS, 1 => c.productGemsM, _ => c.productGemsL };
            var iap = SdkBootstrap.Instance?.Iap;
            if (iap == null)
            {
                onDone?.Invoke("IAP not ready");
                return;
            }
            iap.Purchase(id, (ok, msg) => onDone?.Invoke(ok ? $"Purchased gems ({msg})" : "Purchase failed: " + msg));
        }

        public static void BuyRemoveAds(Action<string> onDone)
        {
            var m = MetaProgress.Ensure();
            if (m.Data.adsRemoved)
            {
                onDone?.Invoke("Ads already removed");
                return;
            }
            if (m.SpendGems(30))
            {
                m.Data.adsRemoved = true;
                m.Save();
                onDone?.Invoke("Ads removed (30 gems)");
                return;
            }
            SdkBootstrap.Ensure();
            SdkBootstrap.Instance?.Iap?.Purchase(SdkConfig.Get().productRemoveAds, (ok, msg) =>
                onDone?.Invoke(ok ? "Ads removed (IAP)" : "Need 30 gems or IAP: " + msg));
        }

        public static void BuyStarterPack(Action<string> onDone)
        {
            if (MetaProgress.Ensure().Data.starterPackBought)
            {
                onDone?.Invoke("Already owned");
                return;
            }
            SdkBootstrap.Ensure();
            SdkBootstrap.Instance?.Iap?.Purchase(SdkConfig.Get().productStarterPack, (ok, msg) =>
                onDone?.Invoke(ok ? "Starter Pack granted" : "Purchase failed: " + msg));
        }

        public static void BuyBattlePass(Action<string> onDone)
        {
            if (MetaProgress.Ensure().Data.battlePassOwned)
            {
                onDone?.Invoke("Pass already owned");
                return;
            }
            SdkBootstrap.Ensure();
            SdkBootstrap.Instance?.Iap?.Purchase(SdkConfig.Get().productBattlePass, (ok, msg) =>
                onDone?.Invoke(ok ? "Season Pass owned" : "Purchase failed: " + msg));
        }
    }

    public static class MonetizationService
    {
        static int _runsSinceInterstitial;

        public static bool CanOfferRewardedRevive =>
            RunSession.Instance != null
            && !RunSession.Instance.IsAlive
            && !RunSession.Instance.ReviveUsed
            && !RunSession.Instance.IsFinalized
            && !MetaProgress.Ensure().Data.adsRemoved;

        public static void WatchRewardedRevive(Action<bool> onDone)
        {
            if (!CanOfferRewardedRevive)
            {
                onDone?.Invoke(false);
                return;
            }
            SdkBootstrap.Ensure();
            var ads = SdkBootstrap.Instance?.Ads;
            if (ads == null)
            {
                onDone?.Invoke(false);
                return;
            }
            ads.ShowRewarded(
                () => onDone?.Invoke(RunSession.Instance != null && RunSession.Instance.TryRevive()),
                () => onDone?.Invoke(false));
        }

        public static void WatchDoubleCoins(Action<bool> onDone)
        {
            var session = RunSession.Instance;
            if (session == null || session.IsAlive || session.IsFinalized)
            {
                onDone?.Invoke(false);
                return;
            }
            if (MetaProgress.Ensure().Data.adsRemoved)
            {
                bool ok = session.ApplyDoubleCoins();
                onDone?.Invoke(ok);
                return;
            }
            SdkBootstrap.Ensure();
            SdkBootstrap.Instance?.Ads?.ShowRewarded(
                () => onDone?.Invoke(RunSession.Instance != null && RunSession.Instance.ApplyDoubleCoins()),
                () => onDone?.Invoke(false));
        }

        public static void OnPostRun()
        {
            _runsSinceInterstitial++;
            if (MetaProgress.Ensure().Data.adsRemoved) return;
            if (_runsSinceInterstitial >= 3)
            {
                SdkBootstrap.Ensure();
                if (SdkBootstrap.Instance?.Ads != null && SdkBootstrap.Instance.Ads.ShowInterstitial())
                    _runsSinceInterstitial = 0;
            }
        }

        public static bool ReviveWithGems()
        {
            if (RunSession.Instance == null || RunSession.Instance.IsAlive || RunSession.Instance.ReviveUsed || RunSession.Instance.IsFinalized)
                return false;
            if (MetaProgress.Ensure().Data.gems < 5) return false;
            if (!RunSession.Instance.TryRevive()) return false;
            MetaProgress.Ensure().SpendGems(5);
            return true;
        }
    }

    public static class AdService
    {
        public static void ShowRewarded(string placement, Action onSuccess, Action onFail = null)
        {
            SdkBootstrap.Ensure();
            AnalyticsService.Track("ad_rewarded_request", placement.GetHashCode());
            SdkBootstrap.Instance?.Ads?.ShowRewarded(onSuccess, onFail);
        }

        public static bool ShowInterstitial()
        {
            SdkBootstrap.Ensure();
            return SdkBootstrap.Instance?.Ads != null && SdkBootstrap.Instance.Ads.ShowInterstitial();
        }
    }
}
