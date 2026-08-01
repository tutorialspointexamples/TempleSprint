using UnityEngine;

namespace TempleSprint
{
    public class DynamicHazard : MonoBehaviour
    {
        public enum Kind
        {
            Pendulum, CrumblingFloor, ArrowTrap, ClosingGate, CollapsingBridge, RollingBoulder, SpikeWheel,
            StoneCrusher, CeilingSlam, LogPendulum, VineSweep
        }

        public Kind HazardKind;
        public bool RequiresJump;
        public bool RequiresSlide;
        public string DeathMessage = "Hit a hazard";

        float _t;
        Vector3 _origin;
        float _amp = 2.2f;
        bool _triggered;
        Renderer _rend;
        Renderer _telegraphRend;
        Vector3 _slamHigh;
        Vector3 _slamLow;
        float _telegraphSeconds = 0.65f;
        float _slamInterval = 1.85f;
        Transform _swingPivot;
        float _swingSpeed = 1.6f;
        float _swingAngle = 55f;
        float _swingPhase;

        void Start()
        {
            _origin = transform.localPosition;
            _rend = GetComponent<Renderer>();
        }

        void Update()
        {
            _t += Time.deltaTime;
            switch (HazardKind)
            {
                case Kind.Pendulum:
                    transform.localPosition = _origin + Vector3.right * (Mathf.Sin(_t * 2.2f) * _amp);
                    break;
                case Kind.ClosingGate:
                    float open = Mathf.PingPong(_t * 1.4f, 1.6f);
                    transform.localScale = new Vector3(Mathf.Max(0.4f, open), transform.localScale.y, transform.localScale.z);
                    break;
                case Kind.ArrowTrap:
                    transform.localPosition = _origin + Vector3.forward * (-Mathf.Repeat(_t * 8f, 10f));
                    break;
                case Kind.CrumblingFloor:
                    if (_triggered)
                    {
                        transform.localPosition += Vector3.down * (6f * Time.deltaTime);
                        if (transform.localPosition.y < -4f) gameObject.SetActive(false);
                    }
                    break;
                case Kind.CollapsingBridge:
                    if (_triggered)
                    {
                        transform.Rotate(80f * Time.deltaTime, 0f, 0f);
                        transform.localPosition += Vector3.down * (4f * Time.deltaTime);
                    }
                    break;
                case Kind.RollingBoulder:
                    // Rolls toward the runner along the tile (negative local Z).
                    transform.localPosition += Vector3.back * (7.5f * Time.deltaTime);
                    transform.Rotate(420f * Time.deltaTime, 0f, 0f, Space.Self);
                    if (transform.localPosition.z < -2f) gameObject.SetActive(false);
                    TryBoulderNearMiss();
                    break;
                case Kind.SpikeWheel:
                    // Spinning spiked disk that sweeps across lanes — jump or lane-dodge.
                    transform.Rotate(0f, 0f, 360f * Time.deltaTime, Space.Self);
                    float sweep = Mathf.Sin(_t * 1.7f) * _amp;
                    transform.localPosition = new Vector3(_origin.x + sweep, _origin.y, _origin.z);
                    TrySpikeWheelNearMiss();
                    break;
                case Kind.StoneCrusher:
                    TickCrusher();
                    break;
                case Kind.CeilingSlam:
                    TickCeilingSlam();
                    break;
                case Kind.LogPendulum:
                case Kind.VineSweep:
                    TickSwingArc();
                    break;
            }
        }

        void TickSwingArc()
        {
            if (_swingPivot == null) return;
            float ang = Mathf.Sin(_t * _swingSpeed + _swingPhase) * _swingAngle;
            _swingPivot.localRotation = Quaternion.Euler(0f, 0f, ang);
            // Telegraph pulses hottest as the mass nears the deck.
            float danger = 1f - Mathf.Clamp01(Mathf.Abs(ang) / Mathf.Max(1f, _swingAngle));
            PulseTelegraph(danger);
            TrySwingNearMiss(ang);
        }

        void TrySwingNearMiss(float ang)
        {
            var player = PlayerController.Instance;
            if (player == null || RunSession.Instance == null || !RunSession.Instance.IsAlive) return;
            // Near the lowest point of the arc, dodging out of the swept lane counts once.
            if (_triggered || Mathf.Abs(ang) > 18f) return;
            Vector3 tip = _swingPivot.TransformPoint(new Vector3(0f, -2f, 0f));
            float dx = Mathf.Abs(tip.x - player.transform.position.x);
            float dz = Vector3.Dot(tip - player.transform.position,
                Quaternion.Euler(0f, player.FacingYaw, 0f) * Vector3.forward);
            if (dz > -1.0f && dz < 2.0f && dx > 1.15f && dx < 3.4f)
            {
                _triggered = true;
                RunSession.Instance.RegisterNearMiss();
                ChaseCamera.Instance?.PunchFov(1.5f);
            }
        }

        void PulseTelegraph(float warn01)
        {
            if (_telegraphRend == null) return;
            float pulse = 0.45f + 0.55f * Mathf.Clamp01(warn01);
            var c = Color.Lerp(JunglePalette.GoldBright.color, JunglePalette.Hazard.color, warn01);
            c.a = 1f;
            _telegraphRend.material.color = c;
            if (_telegraphRend.material.HasProperty("_BaseColor"))
                _telegraphRend.material.SetColor("_BaseColor", c);
            _telegraphRend.transform.localScale = new Vector3(
                1.55f + pulse * 0.25f, 0.06f, 1.55f + pulse * 0.15f);
        }

        void TickCrusher()
        {
            float cycle = Mathf.Repeat(_t, _slamInterval);
            float warn = Mathf.Clamp01(cycle / _telegraphSeconds);
            PulseTelegraph(warn);

            bool crushing = cycle > _telegraphSeconds && cycle < _telegraphSeconds + 0.4f;
            float u = crushing
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_telegraphSeconds, _telegraphSeconds + 0.4f, cycle))
                : 0f;
            transform.localPosition = Vector3.Lerp(_slamHigh, _slamLow, u);

            // Lane dodge during the slam counts once as a near-miss.
            if (crushing && !_triggered)
            {
                var player = PlayerController.Instance;
                if (player == null || RunSession.Instance == null || !RunSession.Instance.IsAlive) return;
                float dx = Mathf.Abs(transform.position.x - player.transform.position.x);
                float dz = Vector3.Dot(transform.position - player.transform.position,
                    Quaternion.Euler(0f, player.FacingYaw, 0f) * Vector3.forward);
                if (dz > -0.7f && dz < 1.4f && dx > 1.05f && dx < 2.9f)
                {
                    _triggered = true;
                    RunSession.Instance.RegisterNearMiss();
                    ChaseCamera.Instance?.PunchFov(1.6f);
                }
            }
        }

        void TickCeilingSlam()
        {
            float cycle = Mathf.Repeat(_t, _slamInterval);
            float warn = Mathf.Clamp01(cycle / _telegraphSeconds);
            PulseTelegraph(warn);

            bool down = cycle >= _telegraphSeconds && cycle < _telegraphSeconds + 0.34f;
            float u = down
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_telegraphSeconds, _telegraphSeconds + 0.34f, cycle))
                : 0f;
            // Rise quickly after slam so the open window is readable.
            if (!down && cycle >= _telegraphSeconds + 0.34f)
            {
                float rise = Mathf.InverseLerp(_telegraphSeconds + 0.34f, _slamInterval, cycle);
                u = 1f - Mathf.SmoothStep(0f, 1f, rise);
            }
            transform.localPosition = Vector3.Lerp(_slamHigh, _slamLow, u);

            // Sliding under the open window during slam telegraph counts as near-miss.
            if (down && !_triggered)
            {
                var player = PlayerController.Instance;
                if (player == null || RunSession.Instance == null || !RunSession.Instance.IsAlive) return;
                float dx = Mathf.Abs(transform.position.x - player.transform.position.x);
                float dz = Vector3.Dot(transform.position - player.transform.position,
                    Quaternion.Euler(0f, player.FacingYaw, 0f) * Vector3.forward);
                if (dz > -0.6f && dz < 1.3f && dx < 1.0f && player.IsSliding)
                {
                    _triggered = true;
                    RunSession.Instance.RegisterNearMiss();
                    ChaseCamera.Instance?.PunchFov(1.7f);
                }
            }
        }

        static GameObject BuildTelegraphPlate(Transform parent, float x, float localZ, out Renderer rend)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "HazardTelegraph";
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = new Vector3(x, 0.07f, localZ);
            plate.transform.localScale = new Vector3(1.55f, 0.06f, 1.55f);
            plate.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            Object.Destroy(plate.GetComponent<Collider>());
            rend = plate.GetComponent<Renderer>();
            return plate;
        }

        void TrySpikeWheelNearMiss()
        {
            var player = PlayerController.Instance;
            if (player == null || RunSession.Instance == null || !RunSession.Instance.IsAlive) return;
            float dx = Mathf.Abs(transform.position.x - player.transform.position.x);
            float dz = Vector3.Dot(transform.position - player.transform.position,
                Quaternion.Euler(0f, player.FacingYaw, 0f) * Vector3.forward);
            if (!_triggered && dz > -0.8f && dz < 1.6f && dx > 1.0f && dx < 2.8f && !player.IsJumping)
            {
                _triggered = true;
                RunSession.Instance.RegisterNearMiss();
                ChaseCamera.Instance?.PunchFov(1.5f);
            }
            // Jumping over the wheel also counts once.
            if (!_triggered && dz > -0.6f && dz < 1.2f && dx < 1.0f && player.IsJumping)
            {
                _triggered = true;
                RunSession.Instance.RegisterNearMiss();
                ChaseCamera.Instance?.PunchFov(1.8f);
            }
        }

        void TryBoulderNearMiss()
        {
            var player = PlayerController.Instance;
            if (player == null || RunSession.Instance == null || !RunSession.Instance.IsAlive) return;
            float dx = Mathf.Abs(transform.position.x - player.transform.position.x);
            float dz = Vector3.Dot(transform.position - player.transform.position,
                Quaternion.Euler(0f, player.FacingYaw, 0f) * Vector3.forward);
            // Same corridor, adjacent lane dodge counted as near-miss once.
            if (!_triggered && dz > -1.2f && dz < 2.2f && dx > 1.1f && dx < 3.2f)
            {
                _triggered = true;
                RunSession.Instance.RegisterNearMiss();
                ChaseCamera.Instance?.PunchFov(1.4f);
            }
        }

        public void TriggerCrumble()
        {
            _triggered = true;
            if (_rend != null) _rend.sharedMaterial = JunglePalette.Hazard;
        }

        public static DynamicHazard CreatePendulum(Transform parent, float localZ, int lane)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pendulum";
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, 1.4f, localZ);
            go.transform.localScale = Vector3.one * 1.1f;
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            var col = go.GetComponent<Collider>();
            col.isTrigger = true;
            // Colorblind shape cue: sphere = dodge laterally / time
            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.Pendulum;
            h.DeathMessage = "Hit by a pendulum";
            var obs = go.AddComponent<Obstacle>();
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        public static DynamicHazard CreateArrow(Transform parent, float localZ, int lane)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Arrow";
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, 1f, localZ + 4f);
            go.transform.localScale = new Vector3(0.25f, 0.25f, 1.2f);
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            go.GetComponent<Collider>().isTrigger = true;
            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.ArrowTrap;
            h.DeathMessage = "Struck by an arrow trap";
            var obs = go.AddComponent<Obstacle>();
            obs.DeathMessage = h.DeathMessage;
            // Cube elongated = shape+color coding for colorblind
            return h;
        }

        public static DynamicHazard CreateGate(Transform parent, float localZ)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Gate";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, localZ);
            go.transform.localScale = new Vector3(1.2f, 2.2f, 0.4f);
            go.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            go.GetComponent<Collider>().isTrigger = true;
            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.ClosingGate;
            h.DeathMessage = "Crushed by a closing gate";
            var obs = go.AddComponent<Obstacle>();
            obs.RequiresSlide = true;
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        public static DynamicHazard CreateCrumbling(Transform parent, float localZ, int lane)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CrumblingFloor";
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, 0.05f, localZ);
            go.transform.localScale = new Vector3(1.8f, 0.2f, 2.5f);
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            go.GetComponent<Collider>().isTrigger = true;
            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.CrumblingFloor;
            h.DeathMessage = "Floor crumbled";
            var obs = go.AddComponent<Obstacle>();
            obs.RequiresJump = true;
            obs.DeathMessage = h.DeathMessage;
            var trigger = go.AddComponent<CrumbleTrigger>();
            trigger.lane = lane;
            trigger.localZ = localZ;
            return h;
        }

        public static DynamicHazard CreateRollingBoulder(Transform parent, float localZ, int lane)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "RollingBoulder";
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, 0.85f, localZ);
            go.transform.localScale = Vector3.one * 1.55f;
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.42f, 0.36f, 0.3f), 0.2f);
            go.GetComponent<Collider>().isTrigger = true;

            // Cracks for silhouette readability (colorblind-safe shape cue: big sphere).
            for (int i = 0; i < 3; i++)
            {
                var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crack.transform.SetParent(go.transform, false);
                crack.transform.localPosition = new Vector3(
                    Mathf.Cos(i * 2.1f) * 0.15f, Mathf.Sin(i * 1.7f) * 0.15f, 0.42f);
                crack.transform.localScale = new Vector3(0.55f, 0.06f, 0.08f);
                crack.transform.localRotation = Quaternion.Euler(0f, 0f, i * 40f);
                crack.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                Object.Destroy(crack.GetComponent<Collider>());
            }

            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.RollingBoulder;
            h.DeathMessage = "Crushed by a rolling boulder";
            var obs = go.AddComponent<Obstacle>();
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        /// <summary>Spinning spiked wheel — flat disk silhouette, distinct from rolling boulders.</summary>
        public static DynamicHazard CreateSpikeWheel(Transform parent, float localZ, int lane)
        {
            var go = new GameObject("SpikeWheel");
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, 1.15f, localZ);

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "WheelHub";
            hub.transform.SetParent(go.transform, false);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hub.transform.localScale = new Vector3(1.5f, 0.12f, 1.5f);
            hub.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            Object.Destroy(hub.GetComponent<Collider>());

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.transform.SetParent(go.transform, false);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rim.transform.localScale = new Vector3(1.85f, 0.06f, 1.85f);
            rim.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(rim.GetComponent<Collider>());

            // Radial spikes — toothy disk silhouette (colorblind-safe vs sphere boulder).
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f;
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.transform.SetParent(go.transform, false);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                spike.transform.localPosition = Quaternion.Euler(0f, 0f, ang) * new Vector3(0.95f, 0f, 0f);
                spike.transform.localScale = new Vector3(0.55f, 0.14f, 0.14f);
                spike.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
                Object.Destroy(spike.GetComponent<Collider>());
            }

            var hit = go.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.radius = 1.05f;
            hit.center = Vector3.zero;

            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.SpikeWheel;
            h.RequiresJump = true;
            h.DeathMessage = "Caught by a spike wheel";
            h._amp = 2.0f;
            var obs = go.AddComponent<Obstacle>();
            obs.RequiresJump = true;
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        /// <summary>Vertical stone press with floor telegraph — lane-dodge during the slam window.</summary>
        public static DynamicHazard CreateStoneCrusher(Transform parent, float localZ, int lane)
        {
            float x = (lane - 1) * PlayerController.LaneWidth;
            var go = new GameObject("StoneCrusher");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 2.55f, localZ);

            BuildTelegraphPlate(parent, x, localZ, out var telegraph);

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "CrusherBlock";
            block.transform.SetParent(go.transform, false);
            block.transform.localPosition = Vector3.zero;
            block.transform.localScale = new Vector3(1.7f, 1.1f, 1.7f);
            block.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            Object.Destroy(block.GetComponent<Collider>());

            // Carved teeth for silhouette (colorblind-safe vs gate/boulder).
            for (int i = -1; i <= 1; i++)
            {
                var tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tooth.transform.SetParent(go.transform, false);
                tooth.transform.localPosition = new Vector3(i * 0.45f, -0.7f, 0f);
                tooth.transform.localScale = new Vector3(0.28f, 0.45f, 1.2f);
                tooth.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                Object.Destroy(tooth.GetComponent<Collider>());
            }

            var hit = go.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.size = new Vector3(1.55f, 1.4f, 1.55f);
            hit.center = new Vector3(0f, -0.15f, 0f);

            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.StoneCrusher;
            h.DeathMessage = "Crushed by moving stones";
            h._telegraphRend = telegraph;
            h._slamHigh = new Vector3(x, 2.55f, localZ);
            h._slamLow = new Vector3(x, 0.85f, localZ);
            h._telegraphSeconds = 0.7f;
            h._slamInterval = 1.9f;
            var obs = go.AddComponent<Obstacle>();
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        /// <summary>Ceiling slab that telegraph-warns then slams — slide under the open window.</summary>
        public static DynamicHazard CreateCeilingSlam(Transform parent, float localZ, int lane)
        {
            float x = (lane - 1) * PlayerController.LaneWidth;
            var go = new GameObject("CeilingSlam");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 3.1f, localZ);

            BuildTelegraphPlate(parent, x, localZ, out var telegraph);

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "SlamSlab";
            slab.transform.SetParent(go.transform, false);
            slab.transform.localPosition = Vector3.zero;
            slab.transform.localScale = new Vector3(2.1f, 0.45f, 1.9f);
            slab.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            Object.Destroy(slab.GetComponent<Collider>());

            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            tip.transform.localScale = new Vector3(1.6f, 0.35f, 0.35f);
            tip.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            Object.Destroy(tip.GetComponent<Collider>());

            var hit = go.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.size = new Vector3(1.9f, 1.2f, 1.6f);
            hit.center = new Vector3(0f, -0.35f, 0f);

            var h = go.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.CeilingSlam;
            h.RequiresSlide = true;
            h.DeathMessage = "Flattened by a ceiling trap";
            h._telegraphRend = telegraph;
            h._slamHigh = new Vector3(x, 3.1f, localZ);
            h._slamLow = new Vector3(x, 1.05f, localZ);
            h._telegraphSeconds = 0.6f;
            h._slamInterval = 1.75f;
            var obs = go.AddComponent<Obstacle>();
            obs.RequiresSlide = true;
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        /// <summary>Ceiling-hung log that swings in a readable lateral arc — jump or lane-dodge.</summary>
        public static DynamicHazard CreateLogPendulum(Transform parent, float localZ, int lane)
        {
            float x = (lane - 1) * PlayerController.LaneWidth;
            var root = new GameObject("LogPendulum");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, 3.35f, localZ);

            // Arc telegraph shadow on the deck.
            BuildTelegraphPlate(parent, x, localZ, out var telegraph);

            var pivot = new GameObject("SwingPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = Vector3.zero;

            // Rope / chain links
            for (int i = 0; i < 3; i++)
            {
                var link = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                link.name = "Chain_" + i;
                link.transform.SetParent(pivot, false);
                link.transform.localPosition = new Vector3(0f, -0.45f - i * 0.42f, 0f);
                link.transform.localScale = new Vector3(0.08f, 0.18f, 0.08f);
                link.GetComponent<Renderer>().sharedMaterial = JunglePalette.Rope;
                Object.Destroy(link.GetComponent<Collider>());
            }

            // Horizontal log at the tip of the swing (collider rides the arc).
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Log";
            log.transform.SetParent(pivot, false);
            log.transform.localPosition = new Vector3(0f, -1.85f, 0f);
            log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            log.transform.localScale = new Vector3(0.55f, 1.15f, 0.55f);
            log.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            var logCol = log.GetComponent<Collider>();
            logCol.isTrigger = true;

            // End caps for silhouette readability.
            foreach (float sx in new[] { -1.15f, 1.15f })
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.transform.SetParent(log.transform, false);
                cap.transform.localPosition = new Vector3(0f, sx, 0f);
                cap.transform.localScale = new Vector3(1.05f, 0.35f, 1.05f);
                cap.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                Object.Destroy(cap.GetComponent<Collider>());
            }

            var h = root.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.LogPendulum;
            h.RequiresJump = true;
            h.DeathMessage = "Swung into by a log";
            h._swingPivot = pivot;
            h._swingSpeed = 1.55f;
            h._swingAngle = 58f;
            h._swingPhase = Random.Range(0f, Mathf.PI * 2f);
            h._telegraphRend = telegraph;
            var obs = log.AddComponent<Obstacle>();
            obs.RequiresJump = true;
            obs.Lane = lane;
            obs.DeathMessage = h.DeathMessage;
            return h;
        }

        /// <summary>Leafy vine that sweeps across lanes on a long rope — slide or dodge the tip.</summary>
        public static DynamicHazard CreateVineSweep(Transform parent, float localZ, int lane)
        {
            float x = (lane - 1) * PlayerController.LaneWidth;
            var root = new GameObject("VineSweep");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, 3.55f, localZ);

            BuildTelegraphPlate(parent, x, localZ, out var telegraph);

            var pivot = new GameObject("VinePivot").transform;
            pivot.SetParent(root.transform, false);

            // Tapered vine rope
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.name = "VineRope";
            rope.transform.SetParent(pivot, false);
            rope.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            rope.transform.localScale = new Vector3(0.07f, 1.15f, 0.07f);
            rope.GetComponent<Renderer>().sharedMaterial = JunglePalette.FoliageDark;
            Object.Destroy(rope.GetComponent<Collider>());

            // Leaf cluster / thorn tip (collider rides the arc with the vine).
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "VineTip";
            tip.transform.SetParent(pivot, false);
            tip.transform.localPosition = new Vector3(0f, -2.25f, 0f);
            tip.transform.localScale = new Vector3(0.85f, 1.1f, 0.85f);
            tip.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foliage;
            var tipCol = tip.GetComponent<Collider>();
            tipCol.isTrigger = true;

            for (int i = 0; i < 4; i++)
            {
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.transform.SetParent(tip.transform, false);
                float ang = i * 90f + 20f;
                leaf.transform.localRotation = Quaternion.Euler(25f, ang, 15f);
                leaf.transform.localPosition = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.45f, 0.1f, 0f);
                leaf.transform.localScale = new Vector3(0.55f, 0.08f, 0.28f);
                leaf.GetComponent<Renderer>().sharedMaterial = JunglePalette.FoliageLight;
                Object.Destroy(leaf.GetComponent<Collider>());
            }

            var h = root.AddComponent<DynamicHazard>();
            h.HazardKind = Kind.VineSweep;
            h.RequiresSlide = true;
            h.DeathMessage = "Snagged by a swinging vine";
            h._swingPivot = pivot;
            h._swingSpeed = 1.85f;
            h._swingAngle = 68f;
            h._swingPhase = Random.Range(0f, Mathf.PI * 2f);
            h._telegraphRend = telegraph;
            var obs = tip.AddComponent<Obstacle>();
            obs.RequiresSlide = true;
            obs.Lane = lane;
            obs.DeathMessage = h.DeathMessage;
            return h;
        }
    }

    public class CrumbleTrigger : MonoBehaviour
    {
        public int lane;
        public float localZ;
        bool _spawnedKill;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerController>() == null && other.GetComponentInParent<PlayerController>() == null)
                return;
            var h = GetComponent<DynamicHazard>();
            h?.TriggerCrumble();
            if (!_spawnedKill)
            {
                _spawnedKill = true;
                StartCoroutine(SpawnKillDelayed());
            }
        }

        System.Collections.IEnumerator SpawnKillDelayed()
        {
            yield return new WaitForSeconds(0.55f);
            // Only collapsing bridges open a real pit — crumbling jump pads sit on solid deck
            var h = GetComponent<DynamicHazard>();
            if (h == null || h.HazardKind != DynamicHazard.Kind.CollapsingBridge) yield break;
            if (transform.parent != null)
                GapKillZone.Create(transform.parent, localZ, lane);
        }
    }

    /// <summary>Environmental modifiers: rain traction, wind drift, darkness fog + weather sheets.</summary>
    public class EnvironmentEffects : MonoBehaviour
    {
        public static EnvironmentEffects Instance { get; private set; }

        public bool RainActive { get; private set; }
        public bool WindActive { get; private set; }
        public bool DarknessActive { get; private set; }
        /// <summary>0..1 blend used by the player lantern and fog ramp.</summary>
        public float DarknessBlend { get; private set; }
        public float WindDrift { get; private set; }

        const float DarkFogDensity = 0.020f;
        static readonly Color DarkFogColor = new Color(0.24f, 0.28f, 0.32f);
        const float FadeDuration = 0.8f;
        const float DarknessCooldown = 35f;

        float _rainTimer, _windTimer, _darkTimer;
        float _rainCooldown, _windCooldown, _darkCooldown;
        float _baseFogDensity = 0.012f;
        Color _baseFogColor = new Color(0.55f, 0.72f, 0.55f);
        float _fadeDir; // +1 fading in, -1 fading out, 0 idle
        ParticleSystem _precipPs;
        ParticleSystem _windPs;
        BiomeWeatherProfile _profile = BiomeWeatherProfile.For(BiomeId.JungleRuins);
        Transform _follow;

        void Awake()
        {
            Instance = this;
            BuildWeatherVisuals();
            RefreshBiomeProfile(BiomeSystem.Current);
        }

        public void ResetEffects()
        {
            RainActive = WindActive = DarknessActive = false;
            DarknessBlend = 0f;
            _rainTimer = _windTimer = _darkTimer = 0f;
            _rainCooldown = _windCooldown = _darkCooldown = 0f;
            _fadeDir = 0f;
            WindDrift = 0f;
            SetRainVisual(false);
            SetWindVisual(false);
            RestoreAmbientFog();
            SnapshotBaseFog();
        }

        public void RefreshBiomeProfile(BiomeId biome)
        {
            _profile = BiomeWeatherProfile.For(biome);
            ApplyProfileToSystems();
            if (RainActive) SetRainVisual(true);
            if (WindActive) SetWindVisual(true);
        }

        public void Tick(float distance)
        {
            float dt = Time.deltaTime;
            if (_rainCooldown > 0f) _rainCooldown -= dt;
            if (_windCooldown > 0f) _windCooldown -= dt;
            if (_darkCooldown > 0f) _darkCooldown -= dt;

            FollowPlayer();

            // Frame-rate-independent scheduling: countdown then roll once.
            float rainChance = _profile.rainChance;
            float windChance = _profile.windChance;
            if (_rainTimer <= 0f && _rainCooldown <= 0f && distance > 80f)
            {
                _rainCooldown = Random.Range(18f, 28f);
                if (Random.value < rainChance) BeginRain(8f);
            }
            if (_windTimer <= 0f && _windCooldown <= 0f && distance > 120f)
            {
                _windCooldown = Random.Range(20f, 32f);
                if (Random.value < windChance) BeginWind(7f);
            }
            if (_darkTimer <= 0f && _darkCooldown <= 0f && !DarknessActive && distance > 160f)
            {
                _darkCooldown = DarknessCooldown;
                float darkChance = BiomeSystem.Current == BiomeId.CaveMines ? 0.55f
                    : BiomeSystem.Current == BiomeId.VolcanicCrater ? 0.3f : 0.22f;
                if (Random.value < darkChance) BeginDarkness(BiomeSystem.Current == BiomeId.CaveMines ? 7f : 5f);
            }

            if (_rainTimer > 0f)
            {
                _rainTimer -= dt;
                if (_rainTimer <= 0f)
                {
                    RainActive = false;
                    SetRainVisual(false);
                }
            }
            if (_windTimer > 0f)
            {
                _windTimer -= dt;
                WindDrift = Mathf.Sin(Time.time * 3f) * 1.8f * _profile.windStrength;
                if (_windTimer <= 0f)
                {
                    WindActive = false;
                    WindDrift = 0f;
                    SetWindVisual(false);
                }
            }

            TickDarkness(dt);
        }

        void FollowPlayer()
        {
            if (PlayerController.Instance == null) return;
            _follow = PlayerController.Instance.transform;
            Vector3 p = _follow.position + Vector3.up * 4.5f + _follow.forward * 2f;
            if (_precipPs != null) _precipPs.transform.position = p;
            if (_windPs != null)
                _windPs.transform.SetPositionAndRotation(
                    _follow.position + Vector3.up * 1.8f - _follow.right * 1.5f,
                    Quaternion.LookRotation(_follow.right + _follow.forward * 0.15f, Vector3.up));
        }

        void BuildWeatherVisuals()
        {
            var root = new GameObject("WeatherFx").transform;
            root.SetParent(transform, false);

            _precipPs = CreateSheetSystem(root, "PrecipSheet", 180);
            _windPs = CreateSheetSystem(root, "WindSheet", 90);
            ApplyProfileToSystems();
            SetRainVisual(false);
            SetWindVisual(false);
        }

        static ParticleSystem CreateSheetSystem(Transform parent, string name, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.9f;
            main.startSize = 0.08f;
            main.startSpeed = 8f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 0.2f, 10f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;
            renderer.velocityScale = 0.12f;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit")
                                            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                            ?? Shader.Find("Sprites/Default")
                                            ?? Shader.Find("Diffuse"));
            return ps;
        }

        void ApplyProfileToSystems()
        {
            if (_profile.precipRate <= 0f && _profile.windRate <= 0f) return;
            if (_precipPs != null)
            {
                var main = _precipPs.main;
                main.startColor = _profile.precipColor;
                main.startSpeed = _profile.precipSpeed;
                main.startSize = _profile.precipSize;
                main.gravityModifier = _profile.precipGravity;
                var emission = _precipPs.emission;
                emission.rateOverTime = RainActive ? _profile.precipRate : 0f;
                var renderer = _precipPs.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.material != null)
                    renderer.material.color = _profile.precipColor;
            }
            if (_windPs != null)
            {
                var main = _windPs.main;
                main.startColor = _profile.windColor;
                main.startSpeed = _profile.windSpeed;
                main.startSize = _profile.windSize;
                main.gravityModifier = 0.05f;
                main.startLifetime = 0.7f;
                var emission = _windPs.emission;
                emission.rateOverTime = WindActive ? _profile.windRate : 0f;
                var renderer = _windPs.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.lengthScale = 8f;
                    if (renderer.material != null) renderer.material.color = _profile.windColor;
                }
            }
        }

        void SetRainVisual(bool on)
        {
            if (_precipPs == null) return;
            var emission = _precipPs.emission;
            emission.rateOverTime = on ? Mathf.Max(8f, _profile.precipRate) : 0f;
            if (on)
            {
                ApplyProfileToSystems();
                if (!_precipPs.isPlaying) _precipPs.Play();
            }
            else if (_precipPs.isPlaying) _precipPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void SetWindVisual(bool on)
        {
            if (_windPs == null) return;
            var emission = _windPs.emission;
            emission.rateOverTime = on ? Mathf.Max(6f, _profile.windRate) : 0f;
            if (on)
            {
                ApplyProfileToSystems();
                if (!_windPs.isPlaying) _windPs.Play();
            }
            else if (_windPs.isPlaying) _windPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void TickDarkness(float dt)
        {
            if (_darkTimer > 0f)
            {
                _darkTimer -= dt;
                if (_darkTimer <= 0f)
                {
                    DarknessActive = false;
                    _fadeDir = -1f;
                    _darkCooldown = DarknessCooldown;
                }
            }

            if (_fadeDir > 0f)
            {
                DarknessBlend = Mathf.Min(1f, DarknessBlend + dt / FadeDuration);
                if (DarknessBlend >= 1f) _fadeDir = 0f;
                ApplyDarkFogBlend();
            }
            else if (_fadeDir < 0f)
            {
                DarknessBlend = Mathf.Max(0f, DarknessBlend - dt / FadeDuration);
                ApplyDarkFogBlend();
                if (DarknessBlend <= 0f)
                {
                    _fadeDir = 0f;
                    RestoreAmbientFog();
                    SnapshotBaseFog();
                }
            }
        }

        void BeginRain(float d)
        {
            RainActive = true;
            _rainTimer = d;
            SetRainVisual(true);
        }

        void BeginWind(float d)
        {
            WindActive = true;
            _windTimer = d;
            SetWindVisual(true);
        }

        void BeginDarkness(float d)
        {
            // Already dark or fading: do not overwrite / extend.
            if (DarknessActive || DarknessBlend > 0.05f)
                return;

            SnapshotBaseFog();
            DarknessActive = true;
            _darkTimer = d;
            _fadeDir = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            ApplyDarkFogBlend();
        }

        void SnapshotBaseFog()
        {
            _baseFogDensity = RenderSettings.fogDensity;
            _baseFogColor = RenderSettings.fogColor;
            if (_baseFogDensity < 0.001f) _baseFogDensity = 0.012f;
        }

        void ApplyDarkFogBlend()
        {
            float t = DarknessBlend;
            RenderSettings.fogDensity = Mathf.Lerp(_baseFogDensity, DarkFogDensity, t);
            RenderSettings.fogColor = Color.Lerp(_baseFogColor, DarkFogColor, t);
        }

        /// <summary>Restore jungle/desert/ice ambient fog after darkness or resets.</summary>
        public static void RestoreAmbientFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            if (NatureBackdrop.Instance != null)
                NatureBackdrop.Instance.ApplyBiomeLook();
            else
            {
                RenderSettings.fogDensity = 0.012f;
                RenderSettings.fogColor = new Color(0.55f, 0.72f, 0.55f);
            }
        }

        public void ForceRain(float seconds = 8f) => BeginRain(seconds);
        public void ForceWind(float seconds = 7f) => BeginWind(seconds);
        public void ForceDarkness(float seconds = 6f)
        {
            // Environment-zone triggers bypass the random cooldown but not an active darkness.
            if (DarknessActive || DarknessBlend > 0.05f) return;
            _darkCooldown = 0f;
            BeginDarkness(seconds);
        }

        /// <summary>Tunnel Runner headlamp / lantern skill — snap darkness off.</summary>
        public void ClearDarkness()
        {
            DarknessActive = false;
            _darkTimer = 0f;
            _fadeDir = -1f;
            DarknessBlend = 0f;
            RestoreAmbientFog();
            SnapshotBaseFog();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Fires rain/wind/darkness when the runner actually enters an EnvironmentZone tile.</summary>
    public class EnvironmentZonePulse : MonoBehaviour
    {
        bool _fired;

        void OnTriggerEnter(Collider other)
        {
            if (_fired) return;
            if (other.GetComponentInParent<PlayerController>() == null
                && other.GetComponent<PlayerController>() == null)
                return;

            _fired = true;
            var env = EnvironmentEffects.Instance;
            if (env == null) return;

            // Darkness is rarer than rain/wind so long runs stay readable.
            float roll = Random.value;
            if (roll < 0.45f) env.ForceRain(7f);
            else if (roll < 0.8f) env.ForceWind(6f);
            else env.ForceDarkness(5f);
        }
    }
}
