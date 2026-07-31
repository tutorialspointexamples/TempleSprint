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
            _offset.y += Time.deltaTime * 0.55f;
            _offset.x += Time.deltaTime * 0.08f;
            Apply(_matA, _offset);
            Apply(_matB, _offset * 1.35f);
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
            int count = 10;
            _patches = new Transform[count];
            _phase = new float[count];
            _baseScale = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var foam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foam.name = "FoamPatch_" + i;
                foam.transform.SetParent(parent, false);
                foam.transform.localPosition = new Vector3(
                    Random.Range(-riverWidth * 0.38f, riverWidth * 0.38f),
                    Random.Range(0.02f, 0.12f),
                    Random.Range(-channelLen * 0.42f, channelLen * 0.42f));
                var scale = new Vector3(Random.Range(1.2f, 2.8f), 0.08f, Random.Range(0.4f, 0.95f));
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
                _phase[i] += Time.deltaTime * (2.2f + i * 0.07f);
                var lp = _patches[i].localPosition;
                lp.y = 0.06f + Mathf.Sin(_phase[i]) * 0.05f;
                lp.z += Mathf.Sin(_phase[i] * 0.35f) * Time.deltaTime * 0.25f;
                _patches[i].localPosition = lp;
                float pulse = 0.9f + 0.1f * Mathf.Sin(_phase[i] * 1.4f);
                var b = _baseScale[i];
                _patches[i].localScale = new Vector3(b.x * pulse, b.y, b.z * pulse);
            }
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
}
