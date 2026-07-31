using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace TempleSprint
{
    /// <summary>
    /// Boots services for local play. Ads/IAP use Editor stubs by default so gameplay
    /// always runs. Real AdMob/IAP live in SdkProductionAdsIap.cs (TEMPLE_USE_MOBILE_SDKS).
    /// </summary>
    public class SdkBootstrap : MonoBehaviour
    {
        public static SdkBootstrap Instance { get; private set; }
        public static bool UgsReady { get; private set; }
        public static bool AdsReady { get; private set; }
        public static bool IapReady { get; private set; }

        SdkConfig _config;
        AdRuntime _ads;
        IapRuntime _iap;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("SdkBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<SdkBootstrap>();
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
            _config = SdkConfig.Get();
            CrashReportingService.Install();
            _ads = new AdRuntime(_config);
            _iap = new IapRuntime(_config);
            StartCoroutine(InitRoutine());
        }

        IEnumerator InitRoutine()
        {
            var ugs = InitUgsAsync();
            while (!ugs.IsCompleted) yield return null;
            // InitUgsAsync owns UgsReady — do not overwrite (Editor skip returns RanToCompletion)

            var rc = RemoteConfigService.FetchAsync(_config);
            while (!rc.IsCompleted) yield return null;

            _ads.Initialize(() => AdsReady = true);
            _iap.Initialize(() => IapReady = true);
            yield return null;
            _ads.Preload();
            Debug.Log($"[SDK] Ready — UGS={UgsReady} Ads={AdsReady} IAP={IapReady} (Editor stubs for ads/IAP)");
        }

        async Task InitUgsAsync()
        {
            try
            {
                // Local Editor testing does not need a linked Unity project ID.
                // Skip UGS here so Hub/cloud setup mistakes don't look like game failures.
#if UNITY_EDITOR
                if (!Application.isBatchMode)
                {
                    UgsReady = false;
                    Debug.Log("[SDK] Editor local play — UGS skipped. Ads/IAP stubs active.");
                    return;
                }
#endif
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                try { Unity.Services.Analytics.AnalyticsService.Instance.StartDataCollection(); }
                catch (Exception ae) { Debug.LogWarning("[SDK] Analytics: " + ae.Message); }

                UgsReady = true;
                Debug.Log("[SDK] UGS ready. PlayerId=" + AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SDK] UGS unavailable (link project in Services for cloud). Local play OK. " + e.Message);
                UgsReady = false;
            }
        }

        public AdRuntime Ads => _ads;
        public IapRuntime Iap => _iap;
        public SdkConfig Config => _config;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Local/Editor ad stub — always rewards in Editor for testing.</summary>
    public class AdRuntime
    {
        readonly SdkConfig _config;

        public AdRuntime(SdkConfig config) => _config = config;

        public void Initialize(Action onReady) => onReady?.Invoke();
        public void Preload() { }

        public void ShowRewarded(Action onReward, Action onFail = null)
        {
            if (MetaProgress.Ensure().Data.adsRemoved)
            {
                onFail?.Invoke();
                return;
            }
            Debug.Log("[SDK] Rewarded ad stub — granting reward");
            AnalyticsServiceFacade.Track("ad_rewarded_complete", 1);
            onReward?.Invoke();
        }

        public bool ShowInterstitial()
        {
            if (MetaProgress.Ensure().Data.adsRemoved) return false;
            Debug.Log("[SDK] Interstitial ad stub");
            AnalyticsServiceFacade.Track("ad_interstitial_shown", 1);
            return true;
        }
    }

    /// <summary>Local/Editor IAP stub — grants products without store when useTestAdsAndIap.</summary>
    public class IapRuntime
    {
        readonly SdkConfig _config;
        bool _ready;

        public IapRuntime(SdkConfig config) => _config = config;

        public void Initialize(Action onReady)
        {
            _ready = true;
            onReady?.Invoke();
        }

        public void Purchase(string productId, Action<bool, string> callback)
        {
            if (!_ready)
            {
                callback?.Invoke(false, "IAP not ready");
                return;
            }
            if (_config.useTestAdsAndIap || Application.isEditor)
            {
                Grant(productId);
                AnalyticsServiceFacade.Track("iap_success", productId.GetHashCode());
                callback?.Invoke(true, productId + " (local stub)");
                return;
            }
            callback?.Invoke(false, "Enable TEMPLE_USE_MOBILE_SDKS + store catalog for real IAP");
        }

        void Grant(string productId)
        {
            var m = MetaProgress.Ensure();
            var c = _config;
            if (productId == c.productGemsS) m.AddGems(40);
            else if (productId == c.productGemsM) m.AddGems(250);
            else if (productId == c.productGemsL) m.AddGems(1200);
            else if (productId == c.productRemoveAds)
            {
                m.Data.adsRemoved = true;
                m.Save();
            }
            else if (productId == c.productStarterPack)
            {
                if (!m.Data.starterPackBought)
                {
                    m.Data.starterPackBought = true;
                    m.BankCoins(500);
                    m.AddGems(50);
                    m.UnlockCharacter("desert_runner");
                    m.Save();
                }
            }
            else if (productId == c.productBattlePass)
            {
                m.Data.battlePassOwned = true;
                m.Save();
            }
        }
    }

    public static class CrashReportingService
    {
        static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error)
                    AnalyticsServiceFacade.Track("client_error", condition.GetHashCode());
            };
        }

        public static void LogException(Exception e)
        {
            Debug.LogException(e);
            AnalyticsServiceFacade.Track("exception", e.GetType().Name.GetHashCode());
        }
    }

    public static class AnalyticsServiceFacade
    {
        public static void Track(string name, int value)
        {
            Debug.Log($"[Analytics] {name}={value}");
            try
            {
                if (!SdkBootstrap.UgsReady) return;
                if (UnityServices.State != ServicesInitializationState.Initialized) return;
                Unity.Services.Analytics.AnalyticsService.Instance.RecordEvent(name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] " + e.Message);
            }
        }
    }
}
