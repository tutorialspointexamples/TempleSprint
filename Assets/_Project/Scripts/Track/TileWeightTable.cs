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
            || kind == TileKind.FireCrossing;

        public TileKind Pick(RunDifficulty difficulty, float obstacleBias, int difficultyTier, bool allowTurn, bool allowHazard)
        {
            float straight, coin, obstacle, gap, branch, dyn, env, river, fire, turnL, turnR, junction;

            switch (difficulty)
            {
                case RunDifficulty.Easy:
                    straight = 0.34f;
                    coin = 0.22f;
                    obstacle = allowHazard ? 0.07f + obstacleBias * 0.1f : 0f;
                    gap = allowHazard ? 0.03f : 0f;
                    branch = allowHazard ? 0.04f : 0f;
                    dyn = allowHazard ? 0.02f : 0f;
                    env = 0.07f;
                    river = allowHazard ? 0.08f : 0f;
                    fire = allowHazard ? 0.06f : 0f;
                    turnL = allowTurn ? 0.05f : 0f;
                    turnR = allowTurn ? 0.05f : 0f;
                    junction = allowTurn ? 0.03f : 0f;
                    break;
                case RunDifficulty.Hard:
                    straight = 0.10f - difficultyTier * 0.01f;
                    coin = 0.09f;
                    obstacle = allowHazard ? 0.16f + obstacleBias * 0.30f + difficultyTier * 0.03f : 0f;
                    gap = allowHazard ? 0.07f + difficultyTier * 0.02f : 0f;
                    branch = allowHazard ? 0.06f + difficultyTier * 0.01f : 0f;
                    dyn = allowHazard ? 0.07f + difficultyTier * 0.02f : 0f;
                    env = 0.03f;
                    river = allowHazard ? 0.12f + difficultyTier * 0.01f : 0f;
                    fire = allowHazard ? 0.11f + difficultyTier * 0.01f : 0f;
                    turnL = allowTurn ? 0.08f + difficultyTier * 0.01f : 0f;
                    turnR = allowTurn ? 0.08f + difficultyTier * 0.01f : 0f;
                    junction = allowTurn ? 0.06f + difficultyTier * 0.012f : 0f;
                    break;
                default: // Medium
                    straight = 0.22f - difficultyTier * 0.015f;
                    coin = 0.15f;
                    obstacle = allowHazard ? 0.11f + obstacleBias * 0.20f + difficultyTier * 0.02f : 0f;
                    gap = allowHazard ? 0.05f + difficultyTier * 0.01f : 0f;
                    branch = allowHazard ? 0.05f : 0f;
                    dyn = allowHazard ? 0.05f + difficultyTier * 0.015f : 0f;
                    env = 0.05f;
                    river = allowHazard ? 0.11f : 0f;
                    fire = allowHazard ? 0.09f : 0f;
                    turnL = allowTurn ? 0.06f : 0f;
                    turnR = allowTurn ? 0.06f : 0f;
                    junction = allowTurn ? 0.04f : 0f;
                    break;
            }

            float total = Mathf.Max(0.01f, straight) + coin + obstacle + gap + branch + dyn + env + river + fire + turnL + turnR + junction;
            float r = Random.value * total;
            float[] w = { Mathf.Max(0.01f, straight), coin, obstacle, gap, branch, dyn, env, river, fire, turnL, turnR, junction };
            TileKind[] kinds =
            {
                TileKind.Straight, TileKind.CoinLane, TileKind.ObstacleCluster, TileKind.HazardGap,
                TileKind.Branch, TileKind.DynamicHazard, TileKind.EnvironmentZone, TileKind.RiverCrossing,
                TileKind.FireCrossing,
                TileKind.TurnLeft, TileKind.TurnRight, TileKind.TJunction
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
