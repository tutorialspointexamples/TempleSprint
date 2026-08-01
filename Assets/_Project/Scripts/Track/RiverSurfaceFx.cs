using UnityEngine;

namespace TempleSprint
{
    /// <summary>Scrolls river surface UVs so water crossings read as flowing, not static cubes.</summary>
    public class RiverSurfaceScroll : MonoBehaviour
    {
        Material _matA;
        Material _matB;
        Vector2 _offset;

        public void Bind(Renderer primary, Renderer sheet)
        {
            if (primary != null)
            {
                _matA = primary.material;
                primary.sharedMaterial = _matA;
            }
            if (sheet != null)
            {
                _matB = sheet.material;
                sheet.sharedMaterial = _matB;
            }
        }

        void Update()
        {
            _offset.y += Time.deltaTime * 0.95f;
            _offset.x += Time.deltaTime * 0.14f;
            Apply(_matA, _offset);
            Apply(_matB, _offset * 1.45f);
        }

        static void Apply(Material m, Vector2 offset)
        {
            if (m == null) return;
            m.mainTextureOffset = offset;
            if (m.HasProperty("_BaseMap"))
                m.SetTextureOffset("_BaseMap", offset);
        }

        void OnDestroy()
        {
            if (_matA != null) Destroy(_matA);
            if (_matB != null) Destroy(_matB);
        }
    }

    /// <summary>Bobbing foam patches + shore spray for river crossings.</summary>
    public class RiverFoamAnimator : MonoBehaviour
    {
        Transform[] _patches;
        float[] _phase;
        Vector3[] _baseScale;

        public void Build(Transform parent, float riverWidth, float channelLen)
        {
            int count = 14;
            _patches = new Transform[count];
            _phase = new float[count];
            _baseScale = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var foam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foam.name = "FoamPatch_" + i;
                foam.transform.SetParent(parent, false);
                foam.transform.localPosition = new Vector3(
                    Random.Range(-riverWidth * 0.4f, riverWidth * 0.4f),
                    Random.Range(0.02f, 0.14f),
                    Random.Range(-channelLen * 0.45f, channelLen * 0.45f));
                var scale = new Vector3(Random.Range(1.3f, 3.1f), 0.09f, Random.Range(0.45f, 1.1f));
                foam.transform.localScale = scale;
                foam.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                var col = foam.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
                _patches[i] = foam.transform;
                _phase[i] = Random.value * Mathf.PI * 2f;
                _baseScale[i] = scale;
            }
        }

        void Update()
        {
            if (_patches == null) return;
            for (int i = 0; i < _patches.Length; i++)
            {
                if (_patches[i] == null) continue;
                _phase[i] += Time.deltaTime * (2.8f + i * 0.08f);
                var lp = _patches[i].localPosition;
                lp.y = 0.07f + Mathf.Sin(_phase[i]) * 0.07f;
                lp.z += Mathf.Sin(_phase[i] * 0.4f) * Time.deltaTime * 0.45f;
                _patches[i].localPosition = lp;
                float pulse = 0.88f + 0.14f * Mathf.Sin(_phase[i] * 1.5f);
                var b = _baseScale[i];
                _patches[i].localScale = new Vector3(b.x * pulse, b.y, b.z * pulse);
            }
        }
    }

    /// <summary>Soft mist wisps over boat/swim river channels.</summary>
    public class RiverMistAnimator : MonoBehaviour
    {
        Transform[] _wisps;
        float[] _phase;
        Vector3[] _origin;

        public void Build(Transform parent, float riverWidth, float channelLen, int count = 8)
        {
            _wisps = new Transform[count];
            _phase = new float[count];
            _origin = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var mist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mist.name = "RiverMist_" + i;
                mist.transform.SetParent(parent, false);
                var origin = new Vector3(
                    Random.Range(-riverWidth * 0.32f, riverWidth * 0.32f),
                    Random.Range(0.4f, 1.6f),
                    Random.Range(-channelLen * 0.4f, channelLen * 0.4f));
                mist.transform.localPosition = origin;
                float s = Random.Range(1.1f, 2.2f);
                mist.transform.localScale = new Vector3(s, s * 0.55f, s);
                mist.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                var col = mist.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
                _wisps[i] = mist.transform;
                _phase[i] = Random.value * Mathf.PI * 2f;
                _origin[i] = origin;
            }
        }

        void Update()
        {
            if (_wisps == null) return;
            for (int i = 0; i < _wisps.Length; i++)
            {
                if (_wisps[i] == null) continue;
                _phase[i] += Time.deltaTime * (0.7f + i * 0.05f);
                var p = _origin[i];
                p.y += Mathf.Sin(_phase[i]) * 0.25f;
                p.x += Mathf.Sin(_phase[i] * 0.6f + i) * 0.2f;
                p.z += Time.deltaTime * 0.35f;
                if (p.z > _origin[i].z + 2.5f) p.z = _origin[i].z - 2.5f;
                _wisps[i].localPosition = p;
                float pulse = 0.85f + 0.2f * Mathf.Sin(_phase[i] * 1.3f);
                _wisps[i].localScale = new Vector3(pulse * 1.6f, pulse * 0.8f, pulse * 1.6f);
            }
        }
    }

    /// <summary>Bobbing translucent heat shimmer for desert runways and fire pits.</summary>
    public class HeatShimmerAnimator : MonoBehaviour
    {
        Transform[] _orbs;
        Vector3[] _origin;
        Vector3[] _baseScale;
        float[] _phase;
        Light _glow;
        float _intensity = 1f;
        float _glowBase = 1.1f;
        Color _glowColor = new Color(1f, 0.72f, 0.35f);

        public void BindExisting(Transform parent)
        {
            if (parent == null) return;
            int n = parent.childCount;
            _orbs = new Transform[n];
            _origin = new Vector3[n];
            _baseScale = new Vector3[n];
            _phase = new float[n];
            for (int i = 0; i < n; i++)
            {
                _orbs[i] = parent.GetChild(i);
                _origin[i] = _orbs[i].localPosition;
                _baseScale[i] = _orbs[i].localScale;
                _phase[i] = Random.value * Mathf.PI * 2f;
            }
        }

        public void Build(Transform parent, int count, float halfWidth, float halfLen, float baseY, Color tint, bool withGlow = true)
        {
            _orbs = new Transform[count];
            _origin = new Vector3[count];
            _baseScale = new Vector3[count];
            _phase = new float[count];
            for (int i = 0; i < count; i++)
            {
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "HeatShimmer_" + i;
                orb.transform.SetParent(parent, false);
                var origin = new Vector3(
                    Random.Range(-halfWidth, halfWidth),
                    baseY + Random.Range(0.4f, 1.8f),
                    Random.Range(-halfLen, halfLen));
                float s = Random.Range(0.8f, 1.6f);
                orb.transform.localPosition = origin;
                orb.transform.localScale = new Vector3(s, s * 0.5f, s);
                orb.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(tint, 0.08f);
                var col = orb.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
                _orbs[i] = orb.transform;
                _origin[i] = origin;
                _baseScale[i] = orb.transform.localScale;
                _phase[i] = Random.value * Mathf.PI * 2f;
            }

            if (withGlow)
            {
                var lightGo = new GameObject("HeatGlow");
                lightGo.transform.SetParent(parent, false);
                lightGo.transform.localPosition = new Vector3(0f, baseY + 1.2f, 0f);
                _glow = lightGo.AddComponent<Light>();
                _glow.type = LightType.Point;
                _glow.color = _glowColor;
                _glow.range = 10f;
                _glow.intensity = _glowBase;
                _glow.shadows = LightShadows.None;
            }
        }

        /// <summary>Scale bob / pulse / glow from biome time-of-day blend.</summary>
        public void ConfigureIntensity(float intensity, Color? glowColor = null)
        {
            _intensity = Mathf.Clamp(intensity, 0.15f, 1.6f);
            if (glowColor.HasValue)
            {
                _glowColor = glowColor.Value;
                if (_glow != null) _glow.color = _glowColor;
            }
            if (_glow != null)
                _glow.intensity = _glowBase * _intensity;
        }

        void Update()
        {
            if (_orbs == null) return;
            float tod = BiomeSystem.TimeOfDayBlend01;
            float intensity = _intensity * Mathf.Lerp(0.65f, 1.15f, tod);
            for (int i = 0; i < _orbs.Length; i++)
            {
                if (_orbs[i] == null) continue;
                _phase[i] += Time.deltaTime * (1.1f + i * 0.07f) * intensity;
                var p = _origin[i];
                p.y += Mathf.Sin(_phase[i]) * 0.22f * intensity;
                p.x += Mathf.Sin(_phase[i] * 0.7f + i) * 0.12f * intensity;
                _orbs[i].localPosition = p;
                float pulse = 0.88f + 0.18f * Mathf.Sin(_phase[i] * 1.4f) * intensity;
                var bs = _baseScale[i];
                float scaleMul = Mathf.Lerp(0.75f, 1.15f, intensity);
                _orbs[i].localScale = new Vector3(bs.x * pulse * scaleMul, bs.y * (0.9f + pulse * 0.15f) * scaleMul, bs.z * pulse * scaleMul);
            }
            if (_glow != null)
                _glow.intensity = (_glowBase * intensity) * (0.85f + Mathf.Sin(Time.time * 2.4f) * 0.2f);
        }
    }

    /// <summary>Rising ember sparks over fire crossings.</summary>
    public class EmberSparkAnimator : MonoBehaviour
    {
        Transform[] _sparks;
        float[] _phase;
        Vector3[] _origin;

        public void Build(Transform parent, float halfWidth, float halfLen)
        {
            int count = 12;
            _sparks = new Transform[count];
            _phase = new float[count];
            _origin = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "EmberSpark_" + i;
                spark.transform.SetParent(parent, false);
                var origin = new Vector3(
                    Random.Range(-halfWidth, halfWidth),
                    Random.Range(0f, 0.4f),
                    Random.Range(-halfLen, halfLen));
                spark.transform.localPosition = origin;
                spark.transform.localScale = Vector3.one * Random.Range(0.12f, 0.28f);
                spark.GetComponent<Renderer>().sharedMaterial =
                    Random.value < 0.45f ? JunglePalette.FlameCore : JunglePalette.Ember;
                var col = spark.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
                _sparks[i] = spark.transform;
                _phase[i] = Random.value;
                _origin[i] = origin;
            }
        }

        void Update()
        {
            if (_sparks == null) return;
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (_sparks[i] == null) continue;
                _phase[i] += Time.deltaTime * (0.35f + i * 0.02f);
                if (_phase[i] > 1f) _phase[i] -= 1f;
                float t = _phase[i];
                var p = _origin[i];
                p.y += t * 2.8f;
                p.x += Mathf.Sin(t * 8f + i) * 0.15f;
                _sparks[i].localPosition = p;
                float s = Mathf.Lerp(0.28f, 0.05f, t);
                _sparks[i].localScale = Vector3.one * s;
            }
        }
    }

    /// <summary>Expanding foam rings after a waterfall plunge pool impact.</summary>
    public class PoolRippleBurst : MonoBehaviour
    {
        Transform[] _rings;
        float _age;
        float _life = 1.35f;
        Vector3[] _baseScale;

        public static PoolRippleBurst Spawn(Transform parent, Vector3 localPos, float width)
        {
            var go = new GameObject("PoolRippleBurst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var burst = go.AddComponent<PoolRippleBurst>();
            burst.Build(width);
            return burst;
        }

        void Build(float width)
        {
            int count = 4;
            _rings = new Transform[count];
            _baseScale = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "PoolRippleRing_" + i;
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.02f + i * 0.015f, 0f);
                ring.transform.localRotation = Quaternion.identity;
                float w = width * (0.22f + i * 0.08f);
                var scale = new Vector3(w, 0.02f, w * 0.72f);
                ring.transform.localScale = scale;
                ring.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(new Color(0.85f, 0.95f, 1f, 0.65f), 0.15f);
                var col = ring.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
                _rings[i] = ring.transform;
                _baseScale[i] = scale;
            }
        }

        void Update()
        {
            if (_rings == null) return;
            _age += Time.deltaTime;
            float u = Mathf.Clamp01(_age / _life);
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] == null) continue;
                float delay = i * 0.08f;
                float t = Mathf.Clamp01((u - delay) / Mathf.Max(0.01f, 1f - delay));
                float expand = 1f + t * (1.6f + i * 0.35f);
                var b = _baseScale[i];
                _rings[i].localScale = new Vector3(b.x * expand, b.y * (1f - t * 0.5f), b.z * expand);
                var rend = _rings[i].GetComponent<Renderer>();
                if (rend != null)
                {
                    var c = new Color(0.85f, 0.95f, 1f, (1f - t) * 0.7f);
                    rend.material.color = c;
                    if (rend.material.HasProperty("_BaseColor"))
                        rend.material.SetColor("_BaseColor", c);
                }
            }

            if (_age >= _life)
                Destroy(gameObject);
        }
    }
}
