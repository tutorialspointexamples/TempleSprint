# Temple Sprint

Unity 6 URP endless runner (local Editor play works without cloud / mobile SDKs).

## Required software (local play)

| Software | Status needed |
|----------|----------------|
| **Unity Hub** | Installed |
| **Unity Editor 6000.5.6f1** | Installed under Hub → Installs |
| **This project folder** | `Desktop\Temple Run` |

Android / iOS modules are **optional** (only for device builds).

## Fastest way to run

Double-click on Desktop:

- **`Play Temple Sprint.bat`** — opens the Editor on Main scene  
- **`Fix Hub and Play Temple Sprint.bat`** — re-registers the project in Hub, then opens the Editor  

In Unity: wait for compile → press the Editor **Play** button → click **PLAY** in the game menu.

### If Hub shows “No projects”

1. Quit Unity Hub completely (system tray → Quit)  
2. Run **`Fix Hub and Play Temple Sprint.bat`**  
3. Or in Hub: **Add → Add project from disk** → select `C:\Users\dagar\OneDrive\Desktop\Temple Run`

## Controls

WASD / arrows — lanes, jump (air hop), slide. Space — equipped power-up (needs full energy). Mouse drag — swipe. Coins fill the power meter.

## Biomes & stages

Jungle Ruins, Desert Tombs, Ice Caverns, Cave Mines, Volcanic Crater — each with distinct fog, ground, mist, textured path/stone, and roadside props.
Turns are banked quarter-circle curves (not sharp L corners). Special crossings: river (boat/rope/jump/swim + shore foam), fire (jump/vine/dunk), zipline, mine cart, ice surf, wall run, ledge grab, jungle tree bridge.
Chase pack: Idol Beast with carved idol plate; rolling boulders in cave/volcano/desert. Near-misses build a combo score multiplier.
Characters have original silhouette kits + traversal poses (swim, zipline hang, wall lean, ledge hang). Relics and gems are multi-part idols.
Locker: original characters, hats & pets (with pet passives), plus rotating seasonal cosmetics.
Live ops: weekly event biome bias, artifact hunt, mission claims. HUD uses carved stone-tablet plaques.

## Optional (cloud / store)

See [Docs/SDK_SETUP.md](Docs/SDK_SETUP.md) for UGS / AdMob / IAP. Not required for local testing.
