# Temple Sprint — Local Play & SDK Notes

## Fix blank Game view / console errors

AdMob + Unity IAP packages were causing Editor errors and blocking scripts. They are **removed from the manifest** for local play. Ads/IAP use **Editor stubs** (rewards and purchases still work in Play Mode).

### What to do now

1. In Unity: click **Stop** (exit Play Mode).
2. Wait for the spinner / “Compiling…” to finish — console errors should drop.
3. Open `Assets/_Project/Scenes/Main.unity` — Hierarchy should show **GameManager** and **BootstrapCamera**.
4. Press **Play**.
5. After splash → **PLAY** → swipe/WASD to run.

### Controls

| Key | Action |
|-----|--------|
| A/D or Left/Right | Lane |
| W / Up | Jump |
| S / Down | Slide |
| Space | Equipped boost |
| Mouse drag | Swipe |

## Packages (current)

| Package | Role |
|---------|------|
| UGS Core / Auth / Cloud Save / Analytics | Optional cloud (game runs without linking) |
| Ads / IAP | Local stubs in `SdkBootstrap.cs` |

## Re-adding store SDKs later

Add via Package Manager when you need real stores:

- `com.google.ads.mobile` (OpenUPM)
- `com.unity.purchasing`

Then replace stub `AdRuntime` / `IapRuntime` with real SDK calls. Keep `useTestAdsAndIap = true` on `SdkConfig` until release.
