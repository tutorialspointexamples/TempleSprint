using UnityEngine;

namespace TempleSprint
{
    [CreateAssetMenu(fileName = "TileWeights", menuName = "Temple Sprint/Tile Weights")]
    public class TileWeightTable : ScriptableObject
    {
        public static TileWeightTable CreateRuntimeDefault() => CreateInstance<TileWeightTable>();

        public static bool IsHazardous(TileKind kind) =>
            kind == TileKind.ObstacleCluster
            || kind == TileKind.HazardGap
            || kind == TileKind.Branch
            || kind == TileKind.DynamicHazard
            || kind == TileKind.RiverCrossing
            || kind == TileKind.FireCrossing
            || kind == TileKind.Zipline
            || kind == TileKind.MineCart
            || kind == TileKind.IceSurf
            || kind == TileKind.WallRun
            || kind == TileKind.LedgeGrab
            || kind == TileKind.TreeBridge
            || kind == TileKind.CanopyRope
            || kind == TileKind.WaterfallPlunge;

        public TileKind Pick(RunDifficulty difficulty, float obstacleBias, int difficultyTier, bool allowTurn, bool allowHazard)
        {
            float straight, coin, obstacle, gap, branch, dyn, env, river, fire, zipline, minecart, icesurf, wallrun, ledge, tree, canopy, waterfall, turnL, turnR, junction;
            float riverBias = BiomeSystem.RiverBias;
            float fireBias = BiomeSystem.FireBias;
            float zipBias = BiomeSystem.ZiplineBias;
            float cartBias = BiomeSystem.MineCartBias;
            float iceBias = BiomeSystem.IceSurfBias;
            float wallBias = BiomeSystem.WallRunBias;
            float ledgeBias = BiomeSystem.LedgeGrabBias;
            float treeBias = BiomeSystem.TreeBridgeBias;
            float canopyBias = BiomeSystem.CanopyRopeBias;
            float fallBias = BiomeSystem.WaterfallPlungeBias;

            switch (difficulty)
            {
                case RunDifficulty.Easy:
                    straight = 0.24f;
                    coin = 0.16f;
                    obstacle = allowHazard ? 0.05f + obstacleBias * 0.1f : 0f;
                    gap = allowHazard ? 0.03f : 0f;
                    branch = allowHazard ? 0.03f : 0f;
                    dyn = allowHazard ? 0.02f : 0f;
                    env = 0.05f;
                    river = allowHazard ? 0.055f * riverBias : 0f;
                    fire = allowHazard ? 0.045f * fireBias : 0f;
                    zipline = allowHazard ? 0.035f * zipBias : 0f;
                    minecart = allowHazard ? 0.035f * cartBias : 0f;
                    icesurf = allowHazard ? 0.035f * iceBias : 0f;
                    wallrun = allowHazard ? 0.03f * wallBias : 0f;
                    ledge = allowHazard ? 0.03f * ledgeBias : 0f;
                    tree = allowHazard ? 0.035f * treeBias : 0f;
                    canopy = allowHazard ? 0.035f * canopyBias : 0f;
                    waterfall = allowHazard ? 0.035f * fallBias : 0f;
                    turnL = allowTurn ? 0.05f : 0f;
                    turnR = allowTurn ? 0.05f : 0f;
                    junction = allowTurn ? 0.03f : 0f;
                    break;
                case RunDifficulty.Hard:
                    straight = 0.04f - difficultyTier * 0.01f;
                    coin = 0.055f;
                    obstacle = allowHazard ? 0.1f + obstacleBias * 0.28f + difficultyTier * 0.03f : 0f;
                    gap = allowHazard ? 0.045f + difficultyTier * 0.02f : 0f;
                    branch = allowHazard ? 0.04f + difficultyTier * 0.01f : 0f;
                    dyn = allowHazard ? 0.045f + difficultyTier * 0.02f : 0f;
                    env = 0.03f;
                    river = allowHazard ? 0.07f * riverBias + difficultyTier * 0.01f : 0f;
                    fire = allowHazard ? 0.06f * fireBias + difficultyTier * 0.01f : 0f;
                    zipline = allowHazard ? 0.045f * zipBias : 0f;
                    minecart = allowHazard ? 0.045f * cartBias : 0f;
                    icesurf = allowHazard ? 0.045f * iceBias : 0f;
                    wallrun = allowHazard ? 0.045f * wallBias : 0f;
                    ledge = allowHazard ? 0.045f * ledgeBias : 0f;
                    tree = allowHazard ? 0.045f * treeBias : 0f;
                    canopy = allowHazard ? 0.045f * canopyBias : 0f;
                    waterfall = allowHazard ? 0.045f * fallBias : 0f;
                    turnL = allowTurn ? 0.07f + difficultyTier * 0.01f : 0f;
                    turnR = allowTurn ? 0.07f + difficultyTier * 0.01f : 0f;
                    junction = allowTurn ? 0.05f + difficultyTier * 0.012f : 0f;
                    break;
                default: // Medium
                    straight = 0.12f - difficultyTier * 0.015f;
                    coin = 0.09f;
                    obstacle = allowHazard ? 0.075f + obstacleBias * 0.18f + difficultyTier * 0.02f : 0f;
                    gap = allowHazard ? 0.04f + difficultyTier * 0.01f : 0f;
                    branch = allowHazard ? 0.04f : 0f;
                    dyn = allowHazard ? 0.04f + difficultyTier * 0.015f : 0f;
                    env = 0.04f;
                    river = allowHazard ? 0.065f * riverBias : 0f;
                    fire = allowHazard ? 0.055f * fireBias : 0f;
                    zipline = allowHazard ? 0.045f * zipBias : 0f;
                    minecart = allowHazard ? 0.045f * cartBias : 0f;
                    icesurf = allowHazard ? 0.045f * iceBias : 0f;
                    wallrun = allowHazard ? 0.04f * wallBias : 0f;
                    ledge = allowHazard ? 0.04f * ledgeBias : 0f;
                    tree = allowHazard ? 0.045f * treeBias : 0f;
                    canopy = allowHazard ? 0.045f * canopyBias : 0f;
                    waterfall = allowHazard ? 0.045f * fallBias : 0f;
                    turnL = allowTurn ? 0.06f : 0f;
                    turnR = allowTurn ? 0.06f : 0f;
                    junction = allowTurn ? 0.04f : 0f;
                    break;
            }

            float total = Mathf.Max(0.01f, straight) + coin + obstacle + gap + branch + dyn + env
                          + river + fire + zipline + minecart + icesurf + wallrun + ledge + tree
                          + canopy + waterfall + turnL + turnR + junction;
            float r = Random.value * total;
            float[] w =
            {
                Mathf.Max(0.01f, straight), coin, obstacle, gap, branch, dyn, env,
                river, fire, zipline, minecart, icesurf, wallrun, ledge, tree, canopy, waterfall,
                turnL, turnR, junction
            };
            TileKind[] kinds =
            {
                TileKind.Straight, TileKind.CoinLane, TileKind.ObstacleCluster, TileKind.HazardGap,
                TileKind.Branch, TileKind.DynamicHazard, TileKind.EnvironmentZone, TileKind.RiverCrossing,
                TileKind.FireCrossing, TileKind.Zipline, TileKind.MineCart, TileKind.IceSurf,
                TileKind.WallRun, TileKind.LedgeGrab, TileKind.TreeBridge, TileKind.CanopyRope,
                TileKind.WaterfallPlunge, TileKind.TurnLeft, TileKind.TurnRight, TileKind.TJunction
            };
            for (int i = 0; i < w.Length; i++)
            {
                if (w[i] <= 0f) continue;
                if (r < w[i]) return kinds[i];
                r -= w[i];
            }
            return TileKind.Straight;
        }

        // Legacy signature for any leftover callers
        public TileKind Pick(float obstacleBias, int difficultyTier, bool allowTurn = true)
        {
            var d = DifficultyDirector.Instance != null
                ? DifficultyDirector.Instance.Current
                : RunDifficulty.Medium;
            return Pick(d, obstacleBias, difficultyTier, allowTurn, true);
        }
    }
}
