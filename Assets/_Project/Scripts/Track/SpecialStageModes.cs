using UnityEngine;

namespace TempleSprint
{
    /// <summary>Cliff zipline, cave mine-cart, icy surf, parkour, canopy, waterfall, and aqueduct stages.</summary>
    public enum SpecialStageKind
    {
        Zipline = 0,
        MineCart = 1,
        IceSurf = 2,
        WallRun = 3,
        LedgeGrab = 4,
        TreeBridge = 5,
        CanopyRope = 6,
        WaterfallPlunge = 7,
        WaterSlide = 8
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
        public float CableSag = 0.55f;
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
            player.BeginZipline(Marker, RideHeight, CableSag);
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
        /// <summary>Dual-track rail offsets (lane 0 / 2). Center lane rides left rail.</summary>
        public float TrackSpacing = PlayerController.LaneWidth;
        Transform _cart;
        Transform _sparks;
        readonly System.Collections.Generic.List<BrokenRailTelegraph> _broken = new();

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

            _sparks = new GameObject("RailSparks").transform;
            _sparks.SetParent(_cart, false);
            _sparks.localPosition = new Vector3(0f, 0.1f, -0.9f);
            for (int i = 0; i < 3; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.transform.SetParent(_sparks, false);
                spark.transform.localPosition = new Vector3((i - 1) * 0.18f, 0f, 0f);
                spark.transform.localScale = Vector3.one * 0.12f;
                spark.GetComponent<Renderer>().sharedMaterial = JunglePalette.Flame;
                Object.Destroy(spark.GetComponent<Collider>());
            }
        }

        /// <summary>Place a broken-rail gap on one dual track; rider must switch before impact.</summary>
        public BrokenRailTelegraph AddBrokenRail(Transform parent, float localZ, int brokenLane)
        {
            var go = new GameObject("BrokenRail_" + brokenLane);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3((brokenLane - 1) * TrackSpacing, 0.2f, localZ);
            var telegraph = go.AddComponent<BrokenRailTelegraph>();
            telegraph.BrokenLane = brokenLane;
            telegraph.LocalZ = localZ;
            telegraph.BuildVisual(go.transform);
            _broken.Add(telegraph);
            return telegraph;
        }

        public void SyncToPlayer(PlayerController player)
        {
            if (player == null || Marker == null || _cart == null) return;
            var tile = GetComponentInParent<TrackTile>();
            if (tile == null) return;
            var pose = tile.SampleAtPathDistance(player.PathDistance);
            // Snap cart to dual-track lanes (0 left / 2 right); middle shares left rail.
            int trackLane = player.Lane == 2 ? 2 : 0;
            float trackX = (trackLane - 1) * TrackSpacing;
            Vector3 pos = pose.position + pose.Right * trackX;
            pos.y = DeckHeight;
            _cart.position = pos + pose.Forward * -0.15f;
            _cart.rotation = Quaternion.Euler(0f, pose.yaw, Mathf.Sin(Time.time * 9f) * 2.5f);
            if (_sparks != null)
            {
                float pulse = 0.8f + Mathf.Abs(Mathf.Sin(Time.time * 18f)) * 0.5f;
                _sparks.localScale = Vector3.one * pulse;
            }

            float local = player.PathDistance - Marker.PathStartDistance;
            for (int i = 0; i < _broken.Count; i++)
            {
                var br = _broken[i];
                if (br == null || br.Consumed) continue;
                float dist = br.LocalZ - local;
                float approach01 = dist > 0f ? 1f - Mathf.Clamp01(dist / 6.5f) : 1f;
                br.PulseTelegraph(approach01);

                // Audio sting + spark telegraph as the gap nears.
                if (!br.Warned && dist > 0f && dist < 5.5f)
                {
                    br.Warned = true;
                    AudioHooks.Instance?.PlayBrokenRailWarn();
                    ChaseCamera.Instance?.PunchFov(0.9f);
                }

                if (local >= br.LocalZ - 0.35f && local <= br.LocalZ + 0.55f)
                {
                    int riderTrack = player.Lane == 2 ? 2 : 0;
                    if (riderTrack == br.BrokenLane)
                    {
                        br.Consumed = true;
                        AudioHooks.Instance?.PlayBrokenRailFall();
                        RunSession.Instance?.EndRun("Fell through a broken rail");
                        return;
                    }
                    br.Consumed = true;
                    AudioHooks.Instance?.PlayNearMiss();
                    RunSession.Instance?.RegisterNearMiss();
                    ChaseCamera.Instance?.PunchFov(1.6f);
                }
            }
        }

        public bool ReachedFarBank(PlayerController player)
        {
            if (player == null || Marker == null) return false;
            float local = player.PathDistance - Marker.PathStartDistance;
            return local >= Marker.ChannelEnd - 0.35f;
        }
    }

    /// <summary>Glowing gap on one dual-track rail — switch tracks before impact.</summary>
    public class BrokenRailTelegraph : MonoBehaviour
    {
        public int BrokenLane;
        public float LocalZ;
        public bool Consumed;
        public bool Warned;
        Renderer _glow;
        Transform _sparkRoot;
        readonly Transform[] _sparks = new Transform[5];

        public void BuildVisual(Transform parent)
        {
            var gap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gap.name = "BrokenGap";
            gap.transform.SetParent(parent, false);
            gap.transform.localPosition = Vector3.zero;
            gap.transform.localScale = new Vector3(1.4f, 0.12f, 1.8f);
            _glow = gap.GetComponent<Renderer>();
            _glow.sharedMaterial = JunglePalette.Hazard;
            Object.Destroy(gap.GetComponent<Collider>());

            for (int i = 0; i < 2; i++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "BrokenRailSpike";
                spike.transform.SetParent(parent, false);
                spike.transform.localPosition = new Vector3((i == 0 ? -0.55f : 0.55f), 0.35f, 0f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -25f : 25f);
                spike.transform.localScale = new Vector3(0.15f, 0.7f, 0.15f);
                spike.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                Object.Destroy(spike.GetComponent<Collider>());
            }

            // Approach spark telegraph — stronger read before the gap.
            _sparkRoot = new GameObject("BrokenRailSparks").transform;
            _sparkRoot.SetParent(parent, false);
            _sparkRoot.localPosition = new Vector3(0f, 0.25f, -0.9f);
            for (int i = 0; i < _sparks.Length; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "BrokenSpark";
                spark.transform.SetParent(_sparkRoot, false);
                spark.transform.localPosition = new Vector3(
                    (i - 2) * 0.16f, Random.Range(0f, 0.2f), Random.Range(-0.35f, 0.1f));
                spark.transform.localScale = Vector3.one * Random.Range(0.1f, 0.18f);
                spark.GetComponent<Renderer>().sharedMaterial = JunglePalette.FlameCore;
                Object.Destroy(spark.GetComponent<Collider>());
                _sparks[i] = spark.transform;
            }
        }

        public void PulseTelegraph(float approach01 = 0.5f)
        {
            if (_glow == null) return;
            float urgency = Mathf.Clamp01(approach01);
            float pulse = 0.45f + Mathf.Abs(Mathf.Sin(Time.time * (8f + urgency * 10f))) * (0.45f + urgency * 0.35f);
            var c = Color.Lerp(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.95f, 0.35f), pulse);
            _glow.material.color = c;
            if (_glow.material.HasProperty("_BaseColor"))
                _glow.material.SetColor("_BaseColor", c);
            _glow.transform.localScale = new Vector3(1.4f, 0.12f + urgency * 0.08f, 1.8f + urgency * 0.35f);

            if (_sparkRoot != null)
            {
                float sparkPulse = 0.7f + urgency * 0.9f + Mathf.Abs(Mathf.Sin(Time.time * 22f)) * 0.35f;
                _sparkRoot.localScale = Vector3.one * sparkPulse;
                for (int i = 0; i < _sparks.Length; i++)
                {
                    if (_sparks[i] == null) continue;
                    var lp = _sparks[i].localPosition;
                    lp.y = Mathf.Abs(Mathf.Sin(Time.time * (14f + i * 3f))) * (0.15f + urgency * 0.45f);
                    _sparks[i].localPosition = lp;
                }
            }
        }
    }

    /// <summary>Wind sway for precipice mist / streamers on CliffNarrow stages.</summary>
    public class CliffWindSway : MonoBehaviour
    {
        float _amp;
        float _speed;
        float _yawAmp;
        float _phase;
        Vector3 _basePos;
        Quaternion _baseRot;

        public void Configure(float amplitude, float speed, float yawDegrees)
        {
            _amp = amplitude;
            _speed = speed;
            _yawAmp = yawDegrees;
            _phase = Random.value * Mathf.PI * 2f;
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
        }

        void LateUpdate()
        {
            float t = Time.time * _speed + _phase;
            float x = Mathf.Sin(t) * _amp;
            float y = Mathf.Sin(t * 1.37f) * _amp * 0.25f;
            transform.localPosition = _basePos + new Vector3(x, y, 0f);
            transform.localRotation = _baseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.85f) * _yawAmp, Mathf.Sin(t) * 6f);
        }
    }

    /// <summary>Pulsing dual-track switch plate + chevron VFX so lane changes read clearly.</summary>
    public class TrackSwitchPlatePulse : MonoBehaviour
    {
        Renderer _plate;
        Transform _glow;
        readonly Transform[] _chevrons = new Transform[4];
        Vector3 _baseScale;

        public void BuildExtras(Transform tileRoot, float midZ, float trackX)
        {
            _plate = GetComponent<Renderer>();
            _baseScale = transform.localScale;

            var glowGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glowGo.name = "TrackSwitchPlateGlow";
            glowGo.transform.SetParent(tileRoot, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.12f, midZ);
            glowGo.transform.localScale = new Vector3(TrackTile.DeckWidth * 0.9f, 0.04f, 1.55f);
            glowGo.GetComponent<Renderer>().sharedMaterial =
                JunglePalette.Mat(new Color(1f, 0.82f, 0.25f, 0.75f), 0.65f, 0.2f);
            Object.Destroy(glowGo.GetComponent<Collider>());
            _glow = glowGo.transform;

            for (int i = 0; i < _chevrons.Length; i++)
            {
                int side = i < 2 ? -1 : 1;
                int tier = i % 2;
                var chevron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chevron.name = "TrackSwitchChevron";
                chevron.transform.SetParent(tileRoot, false);
                chevron.transform.localPosition = new Vector3(
                    side * trackX * 0.55f,
                    0.18f + tier * 0.08f,
                    midZ + (tier == 0 ? -0.35f : 0.35f));
                chevron.transform.localRotation = Quaternion.Euler(0f, side * 35f, 0f);
                chevron.transform.localScale = new Vector3(0.55f, 0.08f, 0.22f);
                chevron.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
                Object.Destroy(chevron.GetComponent<Collider>());
                _chevrons[i] = chevron.transform;
            }
        }

        void Update()
        {
            float pulse = 0.55f + Mathf.Abs(Mathf.Sin(Time.time * 5.5f)) * 0.45f;
            if (_plate != null)
            {
                var c = Color.Lerp(new Color(0.85f, 0.62f, 0.15f), new Color(1f, 0.95f, 0.45f), pulse);
                _plate.material.color = c;
                if (_plate.material.HasProperty("_BaseColor"))
                    _plate.material.SetColor("_BaseColor", c);
                transform.localScale = new Vector3(
                    _baseScale.x * (0.98f + pulse * 0.04f),
                    _baseScale.y,
                    _baseScale.z * (0.95f + pulse * 0.1f));
            }

            if (_glow != null)
            {
                _glow.localScale = new Vector3(
                    TrackTile.DeckWidth * (0.82f + pulse * 0.12f),
                    0.04f + pulse * 0.03f,
                    1.35f + pulse * 0.45f);
                var rend = _glow.GetComponent<Renderer>();
                if (rend != null)
                {
                    var gc = new Color(1f, 0.78f + pulse * 0.18f, 0.2f, 0.35f + pulse * 0.45f);
                    rend.material.color = gc;
                    if (rend.material.HasProperty("_BaseColor"))
                        rend.material.SetColor("_BaseColor", gc);
                }
            }

            for (int i = 0; i < _chevrons.Length; i++)
            {
                if (_chevrons[i] == null) continue;
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 7f + i * 0.9f)) * 0.12f;
                var lp = _chevrons[i].localPosition;
                lp.y = 0.18f + (i % 2) * 0.08f + bob;
                _chevrons[i].localPosition = lp;
            }
        }
    }

    /// <summary>Auto-mount aqueduct water-slide at the channel lip.</summary>
    public class WaterSlideMount : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public WaterSlideRide Ride;
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
            player.BeginWaterSlide(Marker, Ride);
        }
    }

    public class WaterSlideRide : MonoBehaviour
    {
        public SpecialStageMarker Marker;
        public float DeckHeight = 0.2f;
        Transform _board;
        Material _flowMat;
        Vector2 _uv;

        public void BuildVisual(Transform parent)
        {
            _board = new GameObject("SlideBoard").transform;
            _board.SetParent(parent, false);

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "AqueductBoard";
            plank.transform.SetParent(_board, false);
            plank.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            plank.transform.localScale = new Vector3(1.05f, 0.1f, 2.2f);
            plank.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.55f, 0.42f, 0.28f), 0.25f);
            Object.Destroy(plank.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(_board, false);
            nose.transform.localPosition = new Vector3(0f, 0.1f, 1.0f);
            nose.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            nose.transform.localScale = new Vector3(0.9f, 0.08f, 0.45f);
            nose.GetComponent<Renderer>().sharedMaterial = JunglePalette.Leather;
            Object.Destroy(nose.GetComponent<Collider>());
        }

        public void BindFlowMaterial(Material flow) => _flowMat = flow;

        public void SyncToPlayer(PlayerController player)
        {
            if (player == null || Marker == null || _board == null) return;
            var tile = GetComponentInParent<TrackTile>();
            if (tile == null) return;
            var pose = tile.SampleAtPathDistance(player.PathDistance);
            Vector3 pos = pose.position + pose.Right * player.LaneOffset;
            pos.y = DeckHeight;
            float roll = Mathf.Sin(Time.time * 10f) * 7f + player.LaneOffset * 4f;
            _board.position = pos + pose.Forward * -0.08f;
            _board.rotation = Quaternion.Euler(8f + Mathf.Sin(Time.time * 9f) * 3f, pose.yaw, roll);

            if (_flowMat != null)
            {
                _uv.y -= Time.deltaTime * 2.4f;
                if (_flowMat.HasProperty("_BaseMap"))
                    _flowMat.SetTextureOffset("_BaseMap", _uv);
                _flowMat.mainTextureOffset = _uv;
            }
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
