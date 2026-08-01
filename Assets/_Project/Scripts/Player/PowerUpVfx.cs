using UnityEngine;

namespace TempleSprint
{
    /// <summary>Procedural magnet swirl / shield bubble / pickup starburst meshes.</summary>
    public class PowerUpVfx : MonoBehaviour
    {
        public enum Style { MagnetSwirl, ShieldBubble, PickupStarburst }

        Style _style;
        Transform[] _bits;
        float[] _bitBaseSize;
        float _phase;
        Transform _follow;
        float _tierScale = 1f;
        float _lifetime = -1f;
        float _age;
        float _orbitRadius = 0.55f;

        public static PowerUpVfx AttachMagnetSwirl(Transform parent, bool worldOrbit = false, int tier = 0)
        {
            var root = new GameObject("MagnetSwirl");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            var vfx = root.AddComponent<PowerUpVfx>();
            vfx._style = Style.MagnetSwirl;
            vfx._follow = worldOrbit ? parent : null;
            vfx._tierScale = 1f + Mathf.Max(0, tier) * 0.12f;
            float radius = (worldOrbit ? 1.35f : 0.55f) * (1f + Mathf.Max(0, tier) * 0.04f);
            vfx._orbitRadius = radius;
            vfx.BuildMagnetBits(radius, vfx._tierScale);
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

        public static PowerUpVfx SpawnPickupStarburst(Vector3 worldPos, PowerUpType type, int tier = 0)
        {
            var root = new GameObject("PickupStarburst");
            root.transform.position = worldPos + Vector3.up * 0.4f;
            var vfx = root.AddComponent<PowerUpVfx>();
            vfx._style = Style.PickupStarburst;
            vfx._tierScale = 1f + Mathf.Max(0, tier) * 0.1f;
            vfx._lifetime = 0.4f;
            vfx.BuildStarburst(type, vfx._tierScale);
            return vfx;
        }

        void BuildMagnetBits(float radius, float thickness)
        {
            int count = 6 + Mathf.Clamp(Mathf.RoundToInt((thickness - 1f) * 8f), 0, 4);
            _bits = new Transform[count];
            _bitBaseSize = new float[count];
            for (int i = 0; i < _bits.Length; i++)
            {
                var bit = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                bit.name = "MagnetBit";
                bit.transform.SetParent(transform, false);
                float baseSize = (i % 2 == 0 ? 0.18f : 0.14f) * thickness;
                _bitBaseSize[i] = baseSize;
                bit.transform.localScale = Vector3.one * baseSize;
                bit.GetComponent<Renderer>().sharedMaterial =
                    i % 2 == 0 ? JunglePalette.Accent : JunglePalette.GoldBright;
                bit.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(bit.GetComponent<Collider>());
                _bits[i] = bit.transform;
                float ang = i * Mathf.PI * 2f / _bits.Length;
                _bits[i].localPosition = new Vector3(Mathf.Cos(ang) * radius, 0.1f, Mathf.Sin(ang) * radius);
            }

            // Trail ribbons thicken with magnet upgrade tier.
            int trails = Mathf.Clamp(2 + Mathf.RoundToInt((thickness - 1f) * 6f), 2, 5);
            for (int i = 0; i < trails; i++)
            {
                var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trail.name = "MagnetTrail_" + i;
                trail.transform.SetParent(transform, false);
                float ang = i * Mathf.PI * 2f / trails;
                trail.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius * 0.7f, 0.05f, Mathf.Sin(ang) * radius * 0.7f);
                trail.transform.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                trail.transform.localScale = new Vector3(0.06f * thickness, 0.04f, 0.45f * thickness);
                trail.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(new Color(0.35f, 0.75f, 0.95f, 0.55f), 0.2f, 0.1f);
                trail.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(trail.GetComponent<Collider>());
            }
        }

        void BuildShieldShells(float scale)
        {
            _bits = new Transform[2];
            _bitBaseSize = new float[2];
            for (int i = 0; i < _bits.Length; i++)
            {
                var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shell.name = "ShieldShell";
                shell.transform.SetParent(transform, false);
                float s = scale * (1f + i * 0.18f);
                _bitBaseSize[i] = s;
                shell.transform.localScale = Vector3.one * s;
                var mat = JunglePalette.Mat(new Color(0.45f, 0.78f, 0.95f, 0.55f), 0.85f, 0.15f);
                shell.GetComponent<Renderer>().sharedMaterial = mat;
                shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(shell.GetComponent<Collider>());
                _bits[i] = shell.transform;
            }
        }

        void BuildStarburst(PowerUpType type, float scale)
        {
            Color tint = type switch
            {
                PowerUpType.Magnet => new Color(0.35f, 0.85f, 1f, 0.95f),
                PowerUpType.Shield => new Color(0.55f, 0.85f, 1f, 0.95f),
                PowerUpType.SpeedBoost => new Color(1f, 0.45f, 0.15f, 0.95f),
                PowerUpType.SlowMo => new Color(0.55f, 0.75f, 1f, 0.95f),
                PowerUpType.ScoreMultiplier => new Color(1f, 0.85f, 0.25f, 0.95f),
                _ => new Color(1f, 0.9f, 0.4f, 0.95f)
            };
            int rays = 10 + Mathf.RoundToInt((scale - 1f) * 6f);
            _bits = new Transform[rays];
            _bitBaseSize = new float[rays];
            var mat = JunglePalette.Mat(tint, 0.4f, 0.2f);
            for (int i = 0; i < rays; i++)
            {
                var ray = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                ray.name = "StarburstRay_" + i;
                ray.transform.SetParent(transform, false);
                float ang = i * Mathf.PI * 2f / rays;
                float len = (0.55f + (i % 3) * 0.12f) * scale;
                ray.transform.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                ray.transform.localPosition = new Vector3(Mathf.Cos(ang) * len * 0.35f, 0.1f, Mathf.Sin(ang) * len * 0.35f);
                float baseSize = (i % 3 == 0 ? 0.16f : 0.1f) * scale;
                _bitBaseSize[i] = baseSize;
                ray.transform.localScale = i % 3 == 0
                    ? Vector3.one * baseSize
                    : new Vector3(0.08f * scale, 0.08f * scale, len);
                ray.GetComponent<Renderer>().sharedMaterial = mat;
                ray.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(ray.GetComponent<Collider>());
                _bits[i] = ray.transform;
            }

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "StarburstCore";
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * (0.35f * scale);
            core.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(core.GetComponent<Collider>());
        }

        void Update()
        {
            _phase += Time.deltaTime;
            if (_lifetime > 0f)
            {
                _age += Time.deltaTime;
                float life = 1f - Mathf.Clamp01(_age / _lifetime);
                transform.localScale = Vector3.one * (0.6f + (1f - life) * 0.9f) * _tierScale;
                if (_age >= _lifetime)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (_style == Style.MagnetSwirl && _bits != null)
            {
                float radius = _follow != null ? PowerUpController.Instance != null
                    ? PowerUpController.Instance.MagnetRadius * 0.22f * _tierScale : 1.2f * _tierScale
                    : _orbitRadius;
                for (int i = 0; i < _bits.Length; i++)
                {
                    if (_bits[i] == null) continue;
                    float ang = _phase * 2.6f + i * Mathf.PI * 2f / _bits.Length;
                    float y = 0.15f + Mathf.Sin(_phase * 4f + i) * 0.12f;
                    _bits[i].localPosition = new Vector3(Mathf.Cos(ang) * radius, y, Mathf.Sin(ang) * radius);
                    _bits[i].Rotate(0f, 180f * Time.deltaTime, 90f * Time.deltaTime, Space.Self);
                    if (_bitBaseSize != null && i < _bitBaseSize.Length)
                        _bits[i].localScale = Vector3.one * _bitBaseSize[i];
                }
            }
            else if (_style == Style.ShieldBubble && _bits != null)
            {
                float pulse = 1f + Mathf.Sin(_phase * 5.5f) * 0.06f;
                for (int i = 0; i < _bits.Length; i++)
                {
                    if (_bits[i] == null) continue;
                    float baseScale = (_bitBaseSize != null ? _bitBaseSize[i] : (i == 0 ? 1.35f : 1.55f)) * pulse;
                    _bits[i].localScale = Vector3.one * baseScale;
                    _bits[i].Rotate(0f, 40f * Time.deltaTime, 0f, Space.Self);
                }
            }
            else if (_style == Style.PickupStarburst && _bits != null)
            {
                transform.Rotate(0f, 220f * Time.deltaTime, 90f * Time.deltaTime, Space.Self);
            }
        }
    }
}
