using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Services.Authentication;
using UgsCloudSave = Unity.Services.CloudSave.CloudSaveService;

namespace TempleSprint
{
    public static class AuthService
    {
        public static bool IsGuest => MetaProgress.Ensure().Data.guestAccount;

        public static string ContinueAsGuest()
        {
            var m = MetaProgress.Ensure();
            m.Data.guestAccount = true;
            if (string.IsNullOrEmpty(m.Data.playerName) || m.Data.playerName.StartsWith("Guest"))
                m.Data.playerName = "Guest" + UnityEngine.Random.Range(100, 999);
            m.Save();
            SdkBootstrap.Ensure();
            return "Playing as guest (UGS anonymous)";
        }

        public static async void SignInPlatformAsync(string provider, Action<string> onDone)
        {
            SdkBootstrap.Ensure();
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                var m = MetaProgress.Ensure();
                m.Data.guestAccount = false;
                m.Data.playerName = provider == "apple" ? "ApplePlayer" : "GooglePlayer";
                m.Save();
                await CloudSaveService.PushAsync();
                AnalyticsService.Track("auth_" + provider, 1);
                onDone?.Invoke($"Signed in ({provider}). PlayerId={AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                CrashReportingService.LogException(e);
                onDone?.Invoke("Sign-in failed: " + e.Message);
            }
        }

        public static string SignInAppleOrGoogleStub(string provider)
        {
            SignInPlatformAsync(provider, msg => Debug.Log("[Auth] " + msg));
            return $"Started {provider} sign-in (UGS). Wire Apple/Google ID tokens for production store auth.";
        }
    }

    public static class CloudSaveService
    {
        const string Key = "meta_v2";

        public static void PushLocal(MetaSaveData data)
        {
            // Cloud push is explicit (bank / settings) — not on every local Save
        }

        public static async Task PushAsync()
        {
            try
            {
                if (!SdkBootstrap.UgsReady) return;
                if (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized)
                    return;
                var json = JsonUtility.ToJson(MetaProgress.Ensure().Data);
                var data = new Dictionary<string, object> { { Key, json } };
                await UgsCloudSave.Instance.Data.Player.SaveAsync(data);
                Debug.Log("[SDK] Cloud save pushed");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SDK] Cloud push failed: " + e.Message);
            }
        }

        public static string Pull()
        {
            PullAsync();
            return "Cloud pull started…";
        }

        public static async void PullAsync()
        {
            try
            {
                if (!SdkBootstrap.UgsReady)
                {
                    Debug.LogWarning("[SDK] Cloud pull skipped — UGS not ready");
                    return;
                }
                var keys = new HashSet<string> { Key };
                var result = await UgsCloudSave.Instance.Data.Player.LoadAsync(keys);
                if (result.TryGetValue(Key, out var item))
                {
                    var json = item.Value.GetAsString();
                    var data = JsonUtility.FromJson<MetaSaveData>(json);
                    if (data != null)
                    {
                        var local = MetaProgress.Ensure();
                        local.Data.bankedCoins = Mathf.Max(local.Data.bankedCoins, data.bankedCoins);
                        local.Data.gems = Mathf.Max(local.Data.gems, data.gems);
                        local.Data.relics = Mathf.Max(local.Data.relics, data.relics);
                        local.Data.bestScore = Mathf.Max(local.Data.bestScore, data.bestScore);
                        local.Data.adsRemoved = local.Data.adsRemoved || data.adsRemoved;
                        local.Data.battlePassOwned = local.Data.battlePassOwned || data.battlePassOwned;
                        local.Save();
                        Debug.Log("[SDK] Cloud save merged");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SDK] Cloud pull failed: " + e.Message);
            }
        }
    }

    [Serializable]
    public class RemoteConfigDto
    {
        public float event_coin_bonus = 1.1f;
        public string event_name = "";
        public float base_speed = 10f;
    }

    public static class RemoteConfigService
    {
        static bool _loaded;
        static RemoteConfigDto _dto = new RemoteConfigDto();

        public static void Refresh()
        {
            _loaded = true;
            if (string.IsNullOrEmpty(_dto.event_name))
                _dto.event_coin_bonus = 1.1f + (DateTimeOffset.UtcNow.Day % 3) * 0.05f;
            AnalyticsService.Track("remote_config_refresh", 1);
        }

        public static async Task FetchAsync(SdkConfig config)
        {
            _loaded = true;
            string url = config != null ? config.remoteConfigUrl : "";
            if (string.IsNullOrEmpty(url))
            {
                Refresh();
                return;
            }
            try
            {
                using var req = UnityWebRequest.Get(url);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var parsed = JsonUtility.FromJson<RemoteConfigDto>(req.downloadHandler.text);
                    if (parsed != null) _dto = parsed;
                    Debug.Log("[SDK] Remote config fetched from URL");
                }
                else
                {
                    Debug.LogWarning("[SDK] Remote config HTTP fail: " + req.error);
                    Refresh();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SDK] Remote config: " + e.Message);
                Refresh();
            }
        }

        public static float GetFloat(string key, float fallback)
        {
            if (!_loaded) Refresh();
            if (key == "event_coin_bonus") return _dto.event_coin_bonus;
            if (key == "base_speed") return _dto.base_speed;
            return fallback;
        }

        public static string GetString(string key, string fallback)
        {
            if (!_loaded) Refresh();
            if (key == "event_name") return _dto.event_name ?? fallback;
            return fallback;
        }
    }

    public static class AnalyticsService
    {
        public static void Track(string name, int value) => AnalyticsServiceFacade.Track(name, value);
    }

    public static class AntiCheatService
    {
        public static bool ValidateScore(int score, float distance, int coins)
        {
            float maxPlausible = distance * 15f + coins * 20f + 500f;
            bool ok = score <= maxPlausible + 50f;
            if (!ok) AnalyticsService.Track("anticheat_flag", score);
            return ok;
        }
    }
}
