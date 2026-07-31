using UnityEngine;

namespace TempleSprint
{
    public enum RunDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    public struct DifficultyProfile
    {
        public float BaseSpeed;
        public float MaxSpeed;
        public float SpeedRampPer100m;
        public float BaseObstacleChance;
        public float MaxObstacleChance;
        public int MaxTier;
        public int HazardTileCooldown;
        public int TurnCooldown;
        public float MaxObstaclesInCluster;
        public string DisplayName;

        public static DifficultyProfile For(RunDifficulty d) => d switch
        {
            RunDifficulty.Easy => new DifficultyProfile
            {
                DisplayName = "EASY",
                BaseSpeed = 8f,
                MaxSpeed = 16f,
                SpeedRampPer100m = 0.7f,
                BaseObstacleChance = 0.08f,
                MaxObstacleChance = 0.25f,
                MaxTier = 1,
                HazardTileCooldown = 3,
                TurnCooldown = 5,
                MaxObstaclesInCluster = 1
            },
            RunDifficulty.Hard => new DifficultyProfile
            {
                DisplayName = "HARD",
                BaseSpeed = 12f,
                MaxSpeed = 28f,
                SpeedRampPer100m = 1.5f,
                BaseObstacleChance = 0.28f,
                MaxObstacleChance = 0.70f,
                MaxTier = 5,
                HazardTileCooldown = 1,
                TurnCooldown = 3,
                MaxObstaclesInCluster = 3
            },
            _ => new DifficultyProfile
            {
                DisplayName = "MEDIUM",
                BaseSpeed = 10f,
                MaxSpeed = 22f,
                SpeedRampPer100m = 1.1f,
                BaseObstacleChance = 0.16f,
                MaxObstacleChance = 0.45f,
                MaxTier = 3,
                HazardTileCooldown = 2,
                TurnCooldown = 4,
                MaxObstaclesInCluster = 2
            }
        };
    }

    /// <summary>Raises speed and obstacle density from distance, bounded by Easy/Medium/Hard.</summary>
    public class DifficultyDirector : MonoBehaviour
    {
        public static DifficultyDirector Instance { get; private set; }

        public RunDifficulty Current { get; private set; } = RunDifficulty.Medium;
        public DifficultyProfile Profile { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float ObstacleChance { get; private set; }
        public int DifficultyTier { get; private set; }

        RunSession _session;

        void Awake()
        {
            Instance = this;
            Profile = DifficultyProfile.For(RunDifficulty.Medium);
            CurrentSpeed = Profile.BaseSpeed;
            ObstacleChance = Profile.BaseObstacleChance;
        }

        public void Bind(RunSession session) => _session = session;

        public void ResetDifficulty() => ResetDifficulty(Current);

        public void ResetDifficulty(RunDifficulty difficulty)
        {
            Current = difficulty;
            Profile = DifficultyProfile.For(difficulty);
            float remoteBase = RemoteConfigService.GetFloat("base_speed", Profile.BaseSpeed);
            CurrentSpeed = Mathf.Clamp(remoteBase, Profile.BaseSpeed, Profile.MaxSpeed);
            ObstacleChance = Profile.BaseObstacleChance;
            DifficultyTier = 0;
        }

        void Update()
        {
            if (_session == null || !_session.IsAlive) return;
            float d = _session.Distance;
            CurrentSpeed = Mathf.Min(
                Profile.MaxSpeed,
                Profile.BaseSpeed + (d / 100f) * Profile.SpeedRampPer100m);
            ObstacleChance = Mathf.Lerp(
                Profile.BaseObstacleChance,
                Profile.MaxObstacleChance,
                Mathf.Clamp01(d / 800f));
            DifficultyTier = Mathf.Clamp(Mathf.FloorToInt(d / 150f), 0, Profile.MaxTier);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
