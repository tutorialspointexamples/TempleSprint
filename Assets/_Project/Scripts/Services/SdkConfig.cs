using UnityEngine;

namespace TempleSprint
{
    /// <summary>
    /// Production SDK IDs. Replace test AdMob / product IDs before store submission.
    /// Create via: Assets → Create → Temple Sprint → SDK Config
    /// </summary>
    [CreateAssetMenu(fileName = "SdkConfig", menuName = "Temple Sprint/SDK Config")]
    public class SdkConfig : ScriptableObject
    {
        [Header("Remote Config")]
        [Tooltip("Optional HTTPS JSON endpoint: {\"event_coin_bonus\":1.15,\"event_name\":\"…\",\"base_speed\":10}")]
        public string remoteConfigUrl = "";

        [Header("Mode")]
        [Tooltip("When true, Editor/dev builds use test ads and simulated IAP without charging.")]
        public bool useTestAdsAndIap = true;

        [Header("AdMob — Android")]
        public string androidAppId = "ca-app-pub-3940256099942544~3347511713";
        public string androidRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public string androidInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";

        [Header("AdMob — iOS")]
        public string iosAppId = "ca-app-pub-3940256099942544~1458002511";
        public string iosRewardedUnitId = "ca-app-pub-3940256099942544/1712485313";
        public string iosInterstitialUnitId = "ca-app-pub-3940256099942544/4411468910";

        [Header("IAP Product IDs (must match stores)")]
        public string productGemsS = "temple_sprint_gems_s";
        public string productGemsM = "temple_sprint_gems_m";
        public string productGemsL = "temple_sprint_gems_l";
        public string productRemoveAds = "temple_sprint_remove_ads";
        public string productStarterPack = "temple_sprint_starter_pack";
        public string productBattlePass = "temple_sprint_battle_pass";

        public string RewardedUnitId =>
#if UNITY_IOS
            iosRewardedUnitId;
#else
            androidRewardedUnitId;
#endif

        public string InterstitialUnitId =>
#if UNITY_IOS
            iosInterstitialUnitId;
#else
            androidInterstitialUnitId;
#endif

        public string AppId =>
#if UNITY_IOS
            iosAppId;
#else
            androidAppId;
#endif

        static SdkConfig _runtime;

        public static SdkConfig Get()
        {
            if (_runtime != null) return _runtime;
            _runtime = Resources.Load<SdkConfig>("SdkConfig");
            if (_runtime == null)
            {
                _runtime = CreateInstance<SdkConfig>();
                _runtime.name = "SdkConfig (Runtime Default — Test IDs)";
            }
            return _runtime;
        }
    }
}
