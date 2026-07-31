using UnityEngine;

namespace TempleSprint
{
    /// <summary>Cliff zipline, cave mine-cart, icy surf, parkour, canopy, and waterfall stages.</summary>
    public enum SpecialStageKind
    {
        Zipline = 0,
        MineCart = 1,
        IceSurf = 2,
        WallRun = 3,
        LedgeGrab = 4,
        TreeBridge = 5,
        CanopyRope = 6,
        WaterfallPlunge = 7
    }

    public class SpecialStageMarker : MonoBehaviour
    {
        public SpecialStageKind Kind;
        public float ChannelStart;
        public float ChannelEnd;
        public float PathStartDistance;
        public RunDifficulty Difficulty;
        public bool IsOccupied { get; private set; }
        public bool Completed { get; private set; }

        public float ChannelLength => ChannelEnd - ChannelStart;

        public void Configure(SpecialStageKind kind, float channelStart, float channelEnd,
            float pathStart, RunDifficulty difficulty)
        {
            Kind = kind;
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

        void OnDisable() => IsOccupied = false;
    }

    /// <summary>Auto-grab zipline mount at the near cliff edge.</summary>
    public class ZiplineMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float RideHeight = 2.4f;
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
            player.BeginZipline(Marker, RideHeight);
        }
    }

    /// <summary>Auto-board mine cart at the tunnel mouth.</summary>
    public class MineCartMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public MineCartRide Ride;
        bool _used;

        void OnDisable() => _used = false;

        void OnTriggerEnter(Collider other)
        {
            if (_used || Marker == null || Ride == null) return;
            if (other.GetComponent<PlayerController>() == null
                && other.GetComponentInParent<PlayerController>() == null)
                return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _used = true;
            player.BeginMineCart(Marker, Ride);
        }
    }

    public class MineCartRide : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float DeckHeight = 0.55f;
        Transform _cart;

        public void BuildVisual(Transform parent)
        {
            _cart = new GameObject("MineCartBody").transform;
            _cart.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CartBed";
            body.transform.SetParent(_cart, false);
            body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            body.transform.localScale = new Vector3(1.6f, 0.35f, 2.4f);
            body.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            Object.Destroy(body.GetComponent<Collider>());

            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(_cart, false);
                wall.transform.localPosition = new Vector3(side * 0.72f, 0.7f, 0f);
                wall.transform.localScale = new Vector3(0.12f, 0.55f, 2.2f);
                wall.GetComponent<Renderer>().sharedMaterial = JunglePalette.Leather;
                Object.Destroy(wall.GetComponent<Collider>());
            }

            for (int i = 0; i < 4; i++)
            {
                float zx = (i % 2 == 0) ? -0.55f : 0.55f;
                float zz = (i < 2) ? -0.7f : 0.7f;
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.transform.SetParent(_cart, false);
                wheel.transform.localPosition = new Vector3(zx, 0.18f, zz);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
                wheel.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
                Object.Destroy(wheel.GetComponent<Collider>());
            }
        }

        public void SyncToPlayer(PlayerController player)
        {
            if (player == null || Marker == null || _cart == null) return;
            var tile = GetComponentInParent<TrackTile>();
            if (tile == null) return;
            var pose = tile.SampleAtPathDistance(player.PathDistance);
            Vector3 pos = pose.position + pose.Right * player.LaneOffset;
            pos.y = DeckHeight;
            _cart.position = pos + pose.Forward * -0.15f;
            _cart.rotation = Quaternion.Euler(0f, pose.yaw, Mathf.Sin(Time.time * 9f) * 2.5f);
        }

        public bool ReachedFarBank(PlayerController player)
        {
            if (player == null || Marker == null) return false;
            float local = player.PathDistance - Marker.PathStartDistance;
            return local >= Marker.ChannelEnd - 0.35f;
        }
    }

    /// <summary>Auto-mount ice board at the snowy slope lip.</summary>
    public class IceSurfMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public IceBoardRide Ride;
        bool _used;

        void OnDisable() => _used = false;

        void OnTriggerEnter(Collider other)
        {
            if (_used || Marker == null || Ride == null) return;
            if (other.GetComponent<PlayerController>() == null
                && other.GetComponentInParent<PlayerController>() == null)
                return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _used = true;
            player.BeginIceSurf(Marker, Ride);
        }
    }

    public class IceBoardRide : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float DeckHeight = 0.28f;
        Transform _board;

        public void BuildVisual(Transform parent)
        {
            _board = new GameObject("IceBoardBody").transform;
            _board.SetParent(parent, false);

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Board";
            plank.transform.SetParent(_board, false);
            plank.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            plank.transform.localScale = new Vector3(1.1f, 0.12f, 2.6f);
            plank.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.78f, 0.88f, 0.95f), 0.15f, 0.55f);
            Object.Destroy(plank.GetComponent<Collider>());

            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.transform.SetParent(_board, false);
            tip.transform.localPosition = new Vector3(0f, 0.14f, 1.15f);
            tip.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.95f, 0.1f, 0.55f);
            tip.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.65f, 0.78f, 0.9f), 0.2f, 0.4f);
            Object.Destroy(tip.GetComponent<Collider>());
        }

        public void SyncToPlayer(PlayerController player)
        {
            if (player == null || Marker == null || _board == null) return;
            var tile = GetComponentInParent<TrackTile>();
            if (tile == null) return;
            var pose = tile.SampleAtPathDistance(player.PathDistance);
            Vector3 pos = pose.position + pose.Right * player.LaneOffset;
            pos.y = DeckHeight;
            float pitch = Mathf.Sin(Time.time * 11f) * 4f;
            _board.position = pos + pose.Forward * -0.1f;
            _board.rotation = Quaternion.Euler(pitch, pose.yaw, Mathf.Sin(Time.time * 8f) * 6f);
        }

        public bool ReachedFarBank(PlayerController player)
        {
            if (player == null || Marker == null) return false;
            float local = player.PathDistance - Marker.PathStartDistance;
            return local >= Marker.ChannelEnd - 0.35f;
        }
    }

    /// <summary>Auto-mount vertical wall-run at the canyon lip.</summary>
    public class WallRunMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float WallHeight = 1.85f;
        public float WallOffset = 2.35f;
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
            player.BeginWallRun(Marker, WallHeight, WallOffset);
        }
    }

    /// <summary>Auto-grab hanging ledge across a ravine.</summary>
    public class LedgeGrabMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float LedgeHeight = 2.1f;
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
            player.BeginLedgeGrab(Marker, LedgeHeight);
        }
    }

    /// <summary>Auto-grab canopy rope swing across the jungle void.</summary>
    public class CanopyRopeMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float RideHeight = 2.35f;
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
            player.BeginCanopyRope(Marker, RideHeight);
        }
    }

    /// <summary>Auto-trigger waterfall plunge into the pool below.</summary>
    public class WaterfallPlungeMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float DiveHeight = 3.6f;
        public float PoolDepth = -0.35f;
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
            player.BeginWaterfallPlunge(Marker, DiveHeight, PoolDepth);
        }
    }
}
