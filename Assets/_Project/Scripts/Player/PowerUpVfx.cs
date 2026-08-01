using UnityEngine;

namespace TempleSprint
{
    /// <summary>Procedural magnet swirl / shield bubble meshes for pickups and active effects.</summary>
    public class PowerUpVfx : MonoBehaviour
    {
        public enum Style { MagnetSwirl, ShieldBubble }

        Style _style;
        Transform[] _bits;
        float _phase;
        Transform _follow;

        public static PowerUpVfx AttachMagnetSwirl(Transform parent, bool worldOrbit = false)
        {
            var root = new GameObject("MagnetSwirl");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            var vfx = root.AddComponent<PowerUpVfx>();
            vfx._style = Style.MagnetSwirl;
            vfx._follow = worldOrbit ? parent : null;
            vfx.BuildMagnetBits(worldOrbit ? 1.35f : 0.55f);
            return vfx;
        }

        public static PowerUpVfx AttachShieldBubble(Transform parent, float scale = 1.35f)
        {
            var root = new GameObject("ShieldBubble");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.up * 0.85f;
            var vfx = root.AddComponent<PowerUpVfx>();
            vfx._style = Style.ShieldBubble;
            vfx.BuildShieldShells(scale);
            return vfx;
        }

        void BuildMagnetBits(float radius)
        {
            _bits = new Transform[6];
            for (int i = 0; i < _bits.Length; i++)
            {
                var bit = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                bit.name = "MagnetBit";
                bit.transform.SetParent(transform, false);
                bit.transform.localScale = Vector3.one * (i % 2 == 0 ? 0.18f : 0.14f);
                bit.GetComponent<Renderer>().sharedMaterial =
                    i % 2 == 0 ? JunglePalette.Accent : JunglePalette.GoldBright;
                bit.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(bit.GetComponent<Collider>());
                _bits[i] = bit.transform;
                float ang = i * Mathf.PI * 2f / _bits.Length;
                _bits[i].localPosition = new Vector3(Mathf.Cos(ang) * radius, 0.1f, Mathf.Sin(ang) * radius);
            }
        }

        void BuildShieldShells(float scale)
        {
            _bits = new Transform[2];
            for (int i = 0; i < _bits.Length; i++)
            {
                var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shell.name = "ShieldShell";
                shell.transform.SetParent(transform, false);
                float s = scale * (1f + i * 0.18f);
                shell.transform.localScale = Vector3.one * s;
                var mat = JunglePalette.Mat(new Color(0.45f, 0.78f, 0.95f, 0.55f), 0.85f, 0.15f);
                shell.GetComponent<Renderer>().sharedMaterial = mat;
                shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(shell.GetComponent<Collider>());
                _bits[i] = shell.transform;
            }
        }

        void Update()
        {
            _phase += Time.deltaTime;
            if (_style == Style.MagnetSwirl && _bits != null)
            {
                float radius = _follow != null ? PowerUpController.Instance != null
                    ? PowerUpController.Instance.MagnetRadius * 0.22f : 1.2f : 0.55f;
                for (int i = 0; i < _bits.Length; i++)
                {
                    if (_bits[i] == null) continue;
                    float ang = _phase * 2.6f + i * Mathf.PI * 2f / _bits.Length;
                    float y = 0.15f + Mathf.Sin(_phase * 4f + i) * 0.12f;
                    _bits[i].localPosition = new Vector3(Mathf.Cos(ang) * radius, y, Mathf.Sin(ang) * radius);
                    _bits[i].Rotate(0f, 180f * Time.deltaTime, 90f * Time.deltaTime, Space.Self);
                }
            }
            else if (_style == Style.ShieldBubble && _bits != null)
            {
                float pulse = 1f + Mathf.Sin(_phase * 5.5f) * 0.06f;
                for (int i = 0; i < _bits.Length; i++)
                {
                    if (_bits[i] == null) continue;
                    float baseScale = (i == 0 ? 1.35f : 1.55f) * pulse;
                    _bits[i].localScale = Vector3.one * baseScale;
                    _bits[i].Rotate(0f, 40f * Time.deltaTime, 0f, Space.Self);
                }
            }
        }
    }
}
