using UnityEngine;

namespace TempleSprint
{
    public class DynamicHazard : MonoBehaviour
    {
        public enum Kind { Pendulum, CrumblingFloor, ArrowTrap, ClosingGate, CollapsingBridge }

        public Kind HazardKind;
        public bool RequiresJump;
        public bool RequiresSlide;
        public string DeathMessage = "Hit a hazard";

        float _t;
        Vector3 _origin;
        float _amp = 2.2f;
        bool _triggered;
        Renderer _rend;

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
