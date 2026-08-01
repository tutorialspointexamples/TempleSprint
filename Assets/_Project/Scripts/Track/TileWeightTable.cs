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
            || kind == TileKind.LavaRiver
            || kind == TileKind.Zipline
            || kind == TileKind.MineCart
            || kind == TileKind.IceSurf
            || kind == TileKind.WallRun
            || kind == TileKind.LedgeGrab
            || kind == TileKind.TreeBridge
            || kind == TileKind.CanopyRope
            || kind == TileKind.WaterfallPlunge
            || kind == TileKind.WaterSlide
            || kind == TileKind.TempleHall
            || kind == TileKind.CliffNarrow;

        public TileKind Pick(RunDifficulty difficulty, float obstacleBias, int difficultyTier, bool allowTurn, bool allowHazard)
        {
            float straight, coin, obstacle, gap, branch, dyn, env, river, fire, lava, zipline, minecart, icesurf, wallrun, ledge, tree, canopy, waterfall, slide, hall, cliff, turnL, turnR, junction, ruinFork;
            float riverBias = BiomeSystem.RiverBias;
            float fireBias = BiomeSystem.FireBias;
            float lavaBias = BiomeSystem.LavaRiverBias;
            float zipBias = BiomeSystem.ZiplineBias;
            float cartBias = BiomeSystem.MineCartBias;
            float iceBias = BiomeSystem.IceSurfBias;
            float wallBias = BiomeSystem.WallRunBias;
            float ledgeBias = BiomeSystem.LedgeGrabBias;
            float treeBias = BiomeSystem.TreeBridgeBias;
            float canopyBias = BiomeSystem.CanopyRopeBias;
            float fallBias = BiomeSystem.WaterfallPlungeBias;
            float slideBias = BiomeSystem.WaterSlideBias;
            float hallBias = BiomeSystem.TempleHallBias;
            float cliffBias = BiomeSystem.CliffNarrowBias;
            float forkBias = BiomeSystem.RuinForkBias;

            switch (difficulty)
            {
                case RunDifficulty.Easy:
                    straight = 0.19f;
                    coin = 0.13f;
                    obstacle = allowHazard ? 0.05f + obstacleBias * 0.1f : 0f;
                    gap = allowHazard ? 0.03f : 0f;
                    branch = allowHazard ? 0.03f : 0f;
                    dyn = allowHazard ? 0.02f : 0f;
                    env = 0.04f;
                    river = allowHazard ? 0.05f * riverBias : 0f;
                    fire = allowHazard ? 0.04f * fireBias : 0f;
                    lava = allowHazard ? 0.035f * lavaBias : 0f;
                    zipline = allowHazard ? 0.03f * zipBias : 0f;
                    minecart = allowHazard ? 0.03f * cartBias : 0f;
                    icesurf = allowHazard ? 0.03f * iceBias : 0f;
                    wallrun = allowHazard ? 0.028f * wallBias : 0f;
                    ledge = allowHazard ? 0.028f * ledgeBias : 0f;
                    tree = allowHazard ? 0.03f * treeBias : 0f;
                    canopy = allowHazard ? 0.03f * canopyBias : 0f;
                    waterfall = allowHazard ? 0.028f * fallBias : 0f;
                    slide = allowHazard ? 0.03f * slideBias : 0f;
                    hall = allowHazard ? 0.035f * hallBias : 0.015f * hallBias;
                    cliff = allowHazard ? 0.03f * cliffBias : 0f;
                    turnL = allowTurn ? 0.045f : 0f;
                    turnR = allowTurn ? 0.045f : 0f;
                    junction = allowTurn ? 0.025f : 0f;
                    ruinFork = allowTurn ? 0.03f * forkBias : 0f;
                    break;
                case RunDifficulty.Hard:
                    straight = 0.025f - difficultyTier * 0.01f;
                    coin = 0.04f;
                    obstacle = allowHazard ? 0.09f + obstacleBias * 0.28f + difficultyTier * 0.03f : 0f;
                    gap = allowHazard ? 0.04f + difficultyTier * 0.02f : 0f;
                    branch = allowHazard ? 0.035f + difficultyTier * 0.01f : 0f;
                    dyn = allowHazard ? 0.04f + difficultyTier * 0.02f : 0f;
                    env = 0.025f;
                    river = allowHazard ? 0.06f * riverBias + difficultyTier * 0.01f : 0f;
                    fire = allowHazard ? 0.05f * fireBias + difficultyTier * 0.01f : 0f;
                    lava = allowHazard ? 0.05f * lavaBias + difficultyTier * 0.01f : 0f;
                    zipline = allowHazard ? 0.038f * zipBias : 0f;
                    minecart = allowHazard ? 0.038f * cartBias : 0f;
                    icesurf = allowHazard ? 0.038f * iceBias : 0f;
                    wallrun = allowHazard ? 0.038f * wallBias : 0f;
                    ledge = allowHazard ? 0.038f * ledgeBias : 0f;
                    tree = allowHazard ? 0.038f * treeBias : 0f;
                    canopy = allowHazard ? 0.038f * canopyBias : 0f;
                    waterfall = allowHazard ? 0.038f * fallBias : 0f;
                    slide = allowHazard ? 0.04f * slideBias : 0f;
                    hall = allowHazard ? 0.045f * hallBias : 0.015f * hallBias;
                    cliff = allowHazard ? 0.042f * cliffBias + difficultyTier * 0.01f : 0f;
                    turnL = allowTurn ? 0.06f + difficultyTier * 0.01f : 0f;
                    turnR = allowTurn ? 0.06f + difficultyTier * 0.01f : 0f;
                    junction = allowTurn ? 0.04f + difficultyTier * 0.01f : 0f;
                    ruinFork = allowTurn ? 0.045f * forkBias + difficultyTier * 0.01f : 0f;
                    break;
                default: // Medium
                    straight = 0.09f - difficultyTier * 0.015f;
                    coin = 0.075f;
                    obstacle = allowHazard ? 0.065f + obstacleBias * 0.18f + difficultyTier * 0.02f : 0f;
                    gap = allowHazard ? 0.035f + difficultyTier * 0.01f : 0f;
                    branch = allowHazard ? 0.035f : 0f;
                    dyn = allowHazard ? 0.035f + difficultyTier * 0.015f : 0f;
                    env = 0.03f;
                    river = allowHazard ? 0.055f * riverBias : 0f;
                    fire = allowHazard ? 0.05f * fireBias : 0f;
                    lava = allowHazard ? 0.045f * lavaBias : 0f;
                    zipline = allowHazard ? 0.038f * zipBias : 0f;
                    minecart = allowHazard ? 0.038f * cartBias : 0f;
                    icesurf = allowHazard ? 0.038f * iceBias : 0f;
                    wallrun = allowHazard ? 0.035f * wallBias : 0f;
                    ledge = allowHazard ? 0.035f * ledgeBias : 0f;
                    tree = allowHazard ? 0.038f * treeBias : 0f;
                    canopy = allowHazard ? 0.038f * canopyBias : 0f;
                    waterfall = allowHazard ? 0.038f * fallBias : 0f;
                    slide = allowHazard ? 0.04f * slideBias : 0f;
                    hall = allowHazard ? 0.04f * hallBias : 0.015f * hallBias;
                    cliff = allowHazard ? 0.038f * cliffBias : 0f;
                    turnL = allowTurn ? 0.055f : 0f;
                    turnR = allowTurn ? 0.055f : 0f;
                    junction = allowTurn ? 0.035f : 0f;
                    ruinFork = allowTurn ? 0.04f * forkBias : 0f;
                    break;
            }

            float total = Mathf.Max(0.01f, straight) + coin + obstacle + gap + branch + dyn + env
                          + river + fire + lava + zipline + minecart + icesurf + wallrun + ledge + tree
                          + canopy + waterfall + slide + hall + cliff + turnL + turnR + junction + ruinFork;
            float r = Random.value * total;
            float[] w =
            {
                Mathf.Max(0.01f, straight), coin, obstacle, gap, branch, dyn, env,
                river, fire, lava, zipline, minecart, icesurf, wallrun, ledge, tree, canopy, waterfall, slide, hall, cliff,
                turnL, turnR, junction, ruinFork
            };
            TileKind[] kinds =
            {
                TileKind.Straight, TileKind.CoinLane, TileKind.ObstacleCluster, TileKind.HazardGap,
                TileKind.Branch, TileKind.DynamicHazard, TileKind.EnvironmentZone, TileKind.RiverCrossing,
                TileKind.FireCrossing, TileKind.LavaRiver, TileKind.Zipline, TileKind.MineCart, TileKind.IceSurf,
                TileKind.WallRun, TileKind.LedgeGrab, TileKind.TreeBridge, TileKind.CanopyRope,
                TileKind.WaterfallPlunge, TileKind.WaterSlide, TileKind.TempleHall, TileKind.CliffNarrow,
                TileKind.TurnLeft, TileKind.TurnRight, TileKind.TJunction, TileKind.RuinFork
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
