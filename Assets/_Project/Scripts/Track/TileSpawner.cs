using System.Collections.Generic;
using UnityEngine;

namespace TempleSprint
{
    public class TileSpawner : MonoBehaviour
    {
        public static TileSpawner Instance { get; private set; }

        [SerializeField] int preloadCount = 10;
        [SerializeField] float recycleBehind = 24f;

        readonly Queue<TrackTile> _pool = new Queue<TrackTile>();
        readonly List<TrackTile> _active = new List<TrackTile>();
        PathPose _nextPose;
        int _tilesSpawned;
        int _tilesSinceTurn;
        int _tilesSinceHazard;
        int _tilesSinceRiver;
        int _tilesSinceFire;
        int _defaultBranchFlip;
        Transform _poolRoot;
        Transform _activeRoot;
        bool _tutorialMode;
        bool _awaitingJunctionChoice;
        TrackTile _pendingJunction;
        TileWeightTable _weights;
        RunDifficulty _difficulty = RunDifficulty.Medium;
        RiverCrossingMode _lastRiverMode = RiverCrossingMode.Jump;
        FireCrossingMode _lastFireMode = FireCrossingMode.Jump;

        public bool AwaitingJunctionChoice => _awaitingJunctionChoice;
        public TrackTile PendingJunction => _pendingJunction;
        public float NextPathDistance => _nextPose.pathDistance;
        public int ActiveTileCount => _active.Count;
        public int TurnsSpawnedThisRun { get; private set; }
        public int JunctionsSpawnedThisRun { get; private set; }
        public int JunctionsResolvedThisRun { get; private set; }
        public int RiversSpawnedThisRun { get; private set; }
        public int FiresSpawnedThisRun { get; private set; }

        void Awake()
        {
            Instance = this;
            _weights = TileWeightTable.CreateRuntimeDefault();
            _poolRoot = new GameObject("TilePool").transform;
            _poolRoot.SetParent(transform);
            _activeRoot = new GameObject("ActiveTiles").transform;
            _activeRoot.SetParent(transform);
            for (int i = 0; i < preloadCount + 6; i++)
            {
                var go = new GameObject("TrackTile");
                go.transform.SetParent(_poolRoot);
                go.SetActive(false);
                _pool.Enqueue(go.AddComponent<TrackTile>());
            }
        }

        public void BeginRun(bool tutorial) => BeginRun(tutorial, RunDifficulty.Medium);

        public void BeginRun(bool tutorial, RunDifficulty difficulty)
        {
            ClearActive();
            _tutorialMode = tutorial;
            _difficulty = difficulty;
            _nextPose = new PathPose(Vector3.zero, 0f, 0f);
            _tilesSpawned = 0;
            _tilesSinceTurn = 99;
            _tilesSinceHazard = 99;
            _tilesSinceRiver = 99;
            _tilesSinceFire = 99;
            _awaitingJunctionChoice = false;
            _pendingJunction = null;
            TurnsSpawnedThisRun = 0;
            JunctionsSpawnedThisRun = 0;
            JunctionsResolvedThisRun = 0;
            RiversSpawnedThisRun = 0;
            FiresSpawnedThisRun = 0;
            _lastRiverMode = RiverCrossingMode.Jump;
            _lastFireMode = FireCrossingMode.Jump;
            for (int i = 0; i < preloadCount; i++)
                SpawnNext();
        }

        public void ShowMenuPreview()
        {
            ClearActive();
            _tutorialMode = false;
            _awaitingJunctionChoice = false;
            _pendingJunction = null;
            _nextPose = new PathPose(Vector3.zero, 0f, 0f);
            _tilesSpawned = 0;
            for (int i = 0; i < preloadCount; i++)
            {
                TrackTile tile = _pool.Count > 0 ? _pool.Dequeue() : CreateExtra();
                tile.transform.SetParent(_activeRoot);
                tile.Build(TileKind.Straight, _nextPose, 0f, 0, RunDifficulty.Easy);
                _active.Add(tile);
                _nextPose = tile.ExitPose;
                _tilesSpawned++;
            }
        }

        void ClearActive()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                t.Recycle();
                t.transform.SetParent(_poolRoot);
                _pool.Enqueue(t);
            }
            _active.Clear();
        }

        void Update()
        {
            if (PlayerController.Instance == null || RunSession.Instance == null || !RunSession.Instance.IsAlive)
                return;

            float playerDist = PlayerController.Instance.PathDistance;

            // Auto-resolve junction early enough to pre-spawn the chosen arm
            if (_awaitingJunctionChoice && _pendingJunction != null && !_pendingJunction.JunctionResolved)
            {
                float local = playerDist - _pendingJunction.PathStartDistance;
                float deadline = TrackTile.JunctionApproach * 0.72f;
                if (local >= deadline)
                    AutoResolveJunction();
            }

            if (!_awaitingJunctionChoice)
            {
                int guard = 0;
                float ahead = playerDist + preloadCount * TrackTile.Length * 0.75f;
                while (_nextPose.pathDistance < ahead && guard++ < 24)
                    SpawnNext();
                if (guard >= 24)
                    Debug.LogWarning("[TileSpawner] Spawn loop guard hit — check tile ExitPose");
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var tile = _active[i];
                if (tile.PathStartDistance + tile.PathLength < playerDist - recycleBehind)
                {
                    if (tile == _pendingJunction) continue;
                    // Never destroy an active boat/rope/vine/dunk ride with the pooled tile.
                    var marker = tile.GetComponentInChildren<RiverCrossingMarker>(true);
                    if (marker != null && marker.IsOccupied) continue;
                    var fireMarker = tile.GetComponentInChildren<FireCrossingMarker>(true);
                    if (fireMarker != null && fireMarker.IsOccupied) continue;
                    _active.RemoveAt(i);
                    tile.Recycle();
                    tile.transform.SetParent(_poolRoot);
                    _pool.Enqueue(tile);
                }
            }
        }

        void SpawnNext()
        {
            if (_awaitingJunctionChoice) return;

            TrackTile tile = _pool.Count > 0 ? _pool.Dequeue() : CreateExtra();
            tile.transform.SetParent(_activeRoot);
            var kind = ChooseKind(_tilesSpawned);
            kind = EnforceTurnHazardBuffer(kind);
            float chance = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.ObstacleChance : 0.3f;
            int tier = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.DifficultyTier : 0;
            tile.Build(kind, _nextPose, chance, tier, _difficulty);
            _active.Add(tile);
            _tilesSpawned++;

            if (tile.IsTurn)
                TurnsSpawnedThisRun++;
            if (tile.IsJunction)
                JunctionsSpawnedThisRun++;

            if (tile.IsTurn || tile.IsJunction)
                _tilesSinceTurn = 0;
            else
                _tilesSinceTurn++;

            if (TileWeightTable.IsHazardous(kind))
                _tilesSinceHazard = 0;
            else
                _tilesSinceHazard++;

            if (kind == TileKind.RiverCrossing)
            {
                _tilesSinceRiver = 0;
                RiversSpawnedThisRun++;
            }
            else
                _tilesSinceRiver++;

            if (kind == TileKind.FireCrossing)
            {
                _tilesSinceFire = 0;
                FiresSpawnedThisRun++;
            }
            else
                _tilesSinceFire++;

            if (tile.IsJunction && !tile.JunctionResolved)
            {
                _awaitingJunctionChoice = true;
                _pendingJunction = tile;
                return;
            }

            _nextPose = tile.ExitPose;
        }

        /// <summary>Turns and hazards never sit next to each other.</summary>
        TileKind EnforceTurnHazardBuffer(TileKind kind)
        {
            bool isTurn = kind == TileKind.TurnLeft || kind == TileKind.TurnRight || kind == TileKind.TJunction;
            bool isHazard = TileWeightTable.IsHazardous(kind);

            // Hazard immediately after a turn/junction → safe filler.
            if (isHazard && _tilesSinceTurn == 0)
                return Random.value < 0.5f ? TileKind.Straight : TileKind.CoinLane;

            // Turn/junction immediately after a hazard → safe filler.
            if (isTurn && _tilesSinceHazard == 0)
                return Random.value < 0.5f ? TileKind.Straight : TileKind.CoinLane;

            return kind;
        }

        public RiverCrossingMode PickRiverMode(RunDifficulty difficulty)
        {
            float boat, rope, jump;
            switch (difficulty)
            {
                case RunDifficulty.Easy:
                    boat = 0.45f; rope = 0.20f; jump = 0.35f;
                    break;
                case RunDifficulty.Hard:
                    boat = 0.30f; rope = 0.45f; jump = 0.25f;
                    break;
                default:
                    boat = 0.40f; rope = 0.35f; jump = 0.25f;
                    break;
            }

            // Soft anti-repeat so modes feel varied across a run.
            RiverCrossingMode mode;
            float r = Random.value;
            if (r < boat) mode = RiverCrossingMode.Boat;
            else if (r < boat + rope) mode = RiverCrossingMode.Rope;
            else mode = RiverCrossingMode.Jump;

            if (mode == _lastRiverMode && Random.value < 0.55f)
            {
                if (mode == RiverCrossingMode.Boat) mode = Random.value < 0.5f ? RiverCrossingMode.Rope : RiverCrossingMode.Jump;
                else if (mode == RiverCrossingMode.Rope) mode = Random.value < 0.5f ? RiverCrossingMode.Boat : RiverCrossingMode.Jump;
                else mode = Random.value < 0.5f ? RiverCrossingMode.Boat : RiverCrossingMode.Rope;
            }
            _lastRiverMode = mode;
            return mode;
        }

        public FireCrossingMode PickFireMode(RunDifficulty difficulty)
        {
            float jump, vine, dunk;
            switch (difficulty)
            {
                case RunDifficulty.Easy:
                    jump = 0.40f; vine = 0.20f; dunk = 0.40f;
                    break;
                case RunDifficulty.Hard:
                    jump = 0.25f; vine = 0.45f; dunk = 0.30f;
                    break;
                default:
                    jump = 0.35f; vine = 0.35f; dunk = 0.30f;
                    break;
            }

            FireCrossingMode mode;
            float r = Random.value;
            if (r < jump) mode = FireCrossingMode.Jump;
            else if (r < jump + vine) mode = FireCrossingMode.Vine;
            else mode = FireCrossingMode.WaterDunk;

            if (mode == _lastFireMode && Random.value < 0.55f)
            {
                if (mode == FireCrossingMode.Jump)
                    mode = Random.value < 0.5f ? FireCrossingMode.Vine : FireCrossingMode.WaterDunk;
                else if (mode == FireCrossingMode.Vine)
                    mode = Random.value < 0.5f ? FireCrossingMode.Jump : FireCrossingMode.WaterDunk;
                else
                    mode = Random.value < 0.5f ? FireCrossingMode.Jump : FireCrossingMode.Vine;
            }
            _lastFireMode = mode;
            return mode;
        }

        public bool TryChooseJunction(bool left)
        {
            if (!_awaitingJunctionChoice || _pendingJunction == null || _pendingJunction.JunctionResolved)
                return false;

            _nextPose = _pendingJunction.ResolveJunction(left);
            _awaitingJunctionChoice = false;
            _pendingJunction = null;
            JunctionsResolvedThisRun++;

            for (int i = 0; i < 5; i++)
                SpawnNext();
            return true;
        }

        void AutoResolveJunction()
        {
            if (_pendingJunction == null || _pendingJunction.JunctionResolved) return;
            int lane = PlayerController.Instance != null ? PlayerController.Instance.Lane : 1;
            bool left;
            if (lane <= 0) left = true;
            else if (lane >= 2) left = false;
            else
            {
                left = (_defaultBranchFlip++ % 2) == 0;
            }
            TryChooseJunction(left);
        }

        public TrackTile FindTileAtPathDistance(float pathDist)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null && _active[i].ContainsPathDistance(pathDist))
                    return _active[i];
            }
            TrackTile best = null;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < _active.Count; i++)
            {
                var t = _active[i];
                if (t == null) continue;
                float mid = t.PathStartDistance + t.PathLength * 0.5f;
                float d = Mathf.Abs(mid - pathDist);
                if (d < bestDelta)
                {
                    bestDelta = d;
                    best = t;
                }
            }
            return best;
        }

        TrackTile CreateExtra()
        {
            var go = new GameObject("TrackTile");
            go.transform.SetParent(_poolRoot);
            return go.AddComponent<TrackTile>();
        }

        TileKind ChooseKind(int index)
        {
            var profile = DifficultyProfile.For(_difficulty);
            if (index < 3) return TileKind.Straight;

            if (_tutorialMode)
            {
                if (index == 4) return TileKind.CoinLane;
                if (index == 5) return TileKind.TurnRight;
                // Keep a safe tile between turn and first hazard.
                if (index == 6) return TileKind.Straight;
                if (index == 7) return TileKind.ObstacleCluster;
                if (index == 9) return TileKind.TJunction;
                if (index == 10) return TileKind.CoinLane;
                if (index == 11) return _difficulty == RunDifficulty.Easy ? TileKind.Straight : TileKind.HazardGap;
                if (index == 12) return TileKind.Straight;
                if (index == 13) return TileKind.FireCrossing;
                if (index == 14) return TileKind.Straight;
                if (index == 15) return TileKind.RiverCrossing;
                if (index < 16) return TileKind.Straight;
            }

            // Mutual one-tile buffer: turns and hazards never adjacent.
            bool allowTurn = _tilesSinceTurn >= profile.TurnCooldown && index >= 4 && _tilesSinceHazard >= 1;
            bool allowHazard = _tilesSinceHazard >= profile.HazardTileCooldown && _tilesSinceTurn >= 1;

            // Soft guarantees: river ~8-12 tiles, fire ~9-14 tiles (independent cadences).
            int riverEvery = _difficulty == RunDifficulty.Easy ? 12
                : _difficulty == RunDifficulty.Hard ? 8 : 10;
            int fireEvery = _difficulty == RunDifficulty.Easy ? 14
                : _difficulty == RunDifficulty.Hard ? 9 : 11;
            bool riverDue = allowHazard && _tilesSinceRiver >= riverEvery && index >= 5;
            bool fireDue = allowHazard && _tilesSinceFire >= fireEvery && index >= 5;
            if (riverDue && fireDue)
                return Random.value < 0.5f ? TileKind.RiverCrossing : TileKind.FireCrossing;
            if (riverDue) return TileKind.RiverCrossing;
            if (fireDue) return TileKind.FireCrossing;

            // Early route interest, still respecting cooldowns + adjacency buffer
            if (allowTurn && index == 4 + profile.TurnCooldown)
                return Random.value < 0.5f ? TileKind.TurnLeft : TileKind.TurnRight;
            if (allowTurn && index == 8 + profile.TurnCooldown)
                return TileKind.TJunction;

            float obstacleBias = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.ObstacleChance : 0.3f;
            int tier = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.DifficultyTier : 0;
            return _weights.Pick(_difficulty, obstacleBias, tier, allowTurn, allowHazard);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
