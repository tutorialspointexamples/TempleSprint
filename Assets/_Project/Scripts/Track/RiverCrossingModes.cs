using UnityEngine;

namespace TempleSprint
{
    public enum RiverCrossingMode
    {
        Jump = 0,
        Boat = 1,
        Rope = 2,
        Swim = 3
    }

    public enum TraversalMode
    {
        None = 0,
        Boat = 1,
        Rope = 2,
        Vine = 3,
        WaterDunk = 4,
        Zipline = 5,
        MineCart = 6,
        Swim = 7,
        IceSurf = 8,
        WallRun = 9,
        LedgeGrab = 10,
        CanopyRope = 11,
        WaterfallPlunge = 12,
        WaterSlide = 13
    }

    /// <summary>Per-tile river crossing metadata; never destroyed while IsOccupied.</summary>
    public class RiverCrossingMarker : MonoBehaviour
    {
        public RiverCrossingMode Mode;
        public float ChannelStart;
        public float ChannelEnd;
        public float PathStartDistance;
        public RunDifficulty Difficulty;
        public bool IsOccupied { get; private set; }
        public bool Completed { get; private set; }

        public float ChannelLength => ChannelEnd - ChannelStart;

        public void Configure(RiverCrossingMode mode, float channelStart, float channelEnd,
            float pathStart, RunDifficulty difficulty)
        {
            Mode = mode;
            ChannelStart = channelStart;
            ChannelEnd = channelEnd;
            PathStartDistance = pathStart;
            Difficulty = difficulty;
            IsOccupied = false;
            Completed = false;
        }

        public void SetOccupied(bool occupied) => IsOccupied = occupied;

        public void MarkCompleted()
        {
            Completed = true;
            IsOccupied = false;
        }

        void OnDisable()
        {
            IsOccupied = false;
        }
    }

    /// <summary>Auto-mount trigger at the near bank of a boat crossing.</summary>
    public class RiverBoatMount : MonoBehaviour
    {
        public RiverCrossingMarker Marker;
        public RiverBoatRide Ride;
        bool _used;

        void OnDisable() => _used = false;

        void OnTriggerEnter(Collider other)
        {
            if (_used || Marker == null || Ride == null) return;
            if (!IsPlayer(other)) return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _used = true;
            player.BeginBoatRide(Marker, Ride);
        }

        static bool IsPlayer(Collider other) =>
            other.GetComponent<PlayerController>() != null
            || other.GetComponentInParent<PlayerController>() != null;
    }

    /// <summary>Visible boat that tracks path progress while the player is mounted.</summary>
    public class RiverBoatRide : MonoBehaviour
    {
        public RiverCrossingMarker Marker;
        public float DeckHeight = 0.35f;
        Transform _hull;

        public void BuildVisual(Transform parent)
        {
            _hull = new GameObject("BoatHull").transform;
            _hull.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Hull";
            body.transform.SetParent(_hull, false);
            body.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            body.transform.localScale = new Vector3(1.8f, 0.35f, 3.4f);
            body.GetComponent<Renderer>().sharedMaterial = JunglePalette.Leather;
            Object.Destroy(body.GetComponent<Collider>());

            var bow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bow.transform.SetParent(_hull, false);
            bow.transform.localPosition = new Vector3(0f, 0.28f, 1.5f);
            bow.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            bow.transform.localScale = new Vector3(1.2f, 0.25f, 1.1f);
            bow.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            Object.Destroy(bow.GetComponent<Collider>());

            for (int side = -1; side <= 1; side += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.transform.SetParent(_hull, false);
                rail.transform.localPosition = new Vector3(side * 0.85f, 0.42f, 0f);
                rail.transform.localScale = new Vector3(0.12f, 0.35f, 3.0f);
                rail.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                Object.Destroy(rail.GetComponent<Collider>());
            }
        }

        public void SyncToPlayer(PlayerController player)
        {
            if (player == null || Marker == null) return;
            float local = player.PathDistance - Marker.PathStartDistance;
            float t = Mathf.InverseLerp(Marker.ChannelStart, Marker.ChannelEnd, local);
            t = Mathf.Clamp01(t);

            var tile = GetComponentInParent<TrackTile>();
            if (tile == null) return;
            var pose = tile.SampleAtPathDistance(player.PathDistance);
            Vector3 pos = pose.position + pose.Right * player.LaneOffset;
            pos.y = DeckHeight;
            if (_hull != null)
            {
                _hull.position = pos + pose.Forward * -0.2f;
                _hull.rotation = Quaternion.Euler(0f, pose.yaw, Mathf.Sin(Time.time * 4f) * 3f);
            }
        }

        public bool ReachedFarBank(PlayerController player)
        {
            if (player == null || Marker == null) return false;
            float local = player.PathDistance - Marker.PathStartDistance;
            return local >= Marker.ChannelEnd - 0.35f;
        }
    }

    /// <summary>Auto-grab rope swing across the river channel.</summary>
    public class RiverRopeSwing : MonoBehaviour
    {
        public RiverCrossingMarker Marker;
        public float SwingDuration = 1.15f;
        public float ApexHeight = 2.6f;
        public float ReleaseWindowStart = 0.52f;
        public float ReleaseWindowEnd = 0.82f;

        Transform _ropeVisual;
        bool _grabbed;

        void OnDisable()
        {
            _grabbed = false;
        }

        public void BuildVisual(Transform parent, float grabZ, float landZ)
        {
            var nearPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nearPost.name = "RopeAnchorNear";
            nearPost.transform.SetParent(parent, false);
            nearPost.transform.localPosition = new Vector3(0f, 1.6f, grabZ);
            nearPost.transform.localScale = new Vector3(0.28f, 1.6f, 0.28f);
            nearPost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            Object.Destroy(nearPost.GetComponent<Collider>());

            var farPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            farPost.name = "RopeAnchorFar";
            farPost.transform.SetParent(parent, false);
            farPost.transform.localPosition = new Vector3(0f, 1.6f, landZ);
            farPost.transform.localScale = new Vector3(0.28f, 1.6f, 0.28f);
            farPost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            Object.Destroy(farPost.GetComponent<Collider>());

            _ropeVisual = new GameObject("SwingRope").transform;
            _ropeVisual.SetParent(parent, false);
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.transform.SetParent(_ropeVisual, false);
            rope.transform.localPosition = new Vector3(0f, -1.2f, 0f);
            rope.transform.localScale = new Vector3(0.08f, 1.3f, 0.08f);
            rope.GetComponent<Renderer>().sharedMaterial = JunglePalette.Rope;
            Object.Destroy(rope.GetComponent<Collider>());

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.transform.SetParent(_ropeVisual, false);
            handle.transform.localPosition = new Vector3(0f, -2.35f, 0f);
            handle.transform.localScale = new Vector3(0.55f, 0.12f, 0.12f);
            handle.GetComponent<Renderer>().sharedMaterial = JunglePalette.Leather;
            Object.Destroy(handle.GetComponent<Collider>());
        }

        void OnTriggerEnter(Collider other)
        {
            if (_grabbed || Marker == null) return;
            if (other.GetComponent<PlayerController>() == null
                && other.GetComponentInParent<PlayerController>() == null)
                return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _grabbed = true;
            player.BeginRopeSwing(Marker, this);
        }

        public void UpdateRopeVisual(float t, Vector3 worldPos)
        {
            if (_ropeVisual == null) return;
            _ropeVisual.position = worldPos + Vector3.up * 2.4f;
            float sway = Mathf.Lerp(-18f, 18f, t);
            _ropeVisual.rotation = Quaternion.Euler(0f, 0f, sway);
        }

        public bool InReleaseWindow(float t) => t >= ReleaseWindowStart && t <= ReleaseWindowEnd;
    }

    /// <summary>Dive into the river channel and swim under floating debris.</summary>
    public class RiverSwimMount : MonoBehaviour
    {
        public RiverCrossingMarker Marker;
        public float SwimDepth = -1.15f;
        bool _used;

        void OnDisable() => _used = false;

        void OnTriggerEnter(Collider other)
        {
            if (_used || Marker == null) return;
            if (other.GetComponent<PlayerController>() == null
                && other.GetComponentInParent<PlayerController>() == null)
                return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _used = true;
            player.BeginSwim(Marker, SwimDepth);
        }
    }
}
