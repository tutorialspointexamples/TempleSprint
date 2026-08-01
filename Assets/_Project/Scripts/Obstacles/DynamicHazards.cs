using UnityEngine;

namespace TempleSprint
{
    public class DynamicHazard : MonoBehaviour
    {
        public enum Kind
        {
            Pendulum, CrumblingFloor, ArrowTrap, ClosingGate, CollapsingBridge, RollingBoulder, SpikeWheel,
            StoneCrusher, CeilingSlam
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

    /// <summary>Environmental modifiers: rain traction, wind drift, darkness fog.</summary>
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

        void Awake() => Instance = this;

        public void ResetEffects()
        {
            RainActive = WindActive = DarknessActive = false;
            DarknessBlend = 0f;
            _rainTimer = _windTimer = _darkTimer = 0f;
            _rainCooldown = _windCooldown = _darkCooldown = 0f;
            _fadeDir = 0f;
            WindDrift = 0f;
            RestoreAmbientFog();
            SnapshotBaseFog();
        }

        public void Tick(float distance)
        {
            float dt = Time.deltaTime;
            if (_rainCooldown > 0f) _rainCooldown -= dt;
            if (_windCooldown > 0f) _windCooldown -= dt;
            if (_darkCooldown > 0f) _darkCooldown -= dt;

            // Frame-rate-independent scheduling: countdown then roll once.
            if (_rainTimer <= 0f && _rainCooldown <= 0f && distance > 80f)
            {
                _rainCooldown = Random.Range(18f, 28f);
                if (Random.value < 0.35f) BeginRain(8f);
            }
            if (_windTimer <= 0f && _windCooldown <= 0f && distance > 120f)
            {
                _windCooldown = Random.Range(20f, 32f);
                if (Random.value < 0.3f) BeginWind(7f);
            }
            if (_darkTimer <= 0f && _darkCooldown <= 0f && !DarknessActive && distance > 160f)
            {
                _darkCooldown = DarknessCooldown;
                float darkChance = BiomeSystem.Current == BiomeId.CaveMines ? 0.55f
                    : BiomeSystem.Current == BiomeId.VolcanicCrater ? 0.3f : 0.22f;
                if (Random.value < darkChance) BeginDarkness(BiomeSystem.Current == BiomeId.CaveMines ? 7f : 5f);
            }

            if (_rainTimer > 0f) { _rainTimer -= dt; if (_rainTimer <= 0f) RainActive = false; }
            if (_windTimer > 0f)
            {
                _windTimer -= dt;
                WindDrift = Mathf.Sin(Time.time * 3f) * 1.8f;
                if (_windTimer <= 0f) { WindActive = false; WindDrift = 0f; }
            }

            TickDarkness(dt);
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

        void BeginRain(float d) { RainActive = true; _rainTimer = d; }
        void BeginWind(float d) { WindActive = true; _windTimer = d; }

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
