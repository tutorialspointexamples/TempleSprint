using UnityEngine;

namespace TempleSprint
{
    /// <summary>Temple ruins materials — stone, water, foliage, explorer, gold.</summary>
    public static class JunglePalette
    {
        static Material _stone, _stoneMoss, _foliage, _foliageDark, _foliageLight, _undergrowth;
        static Material _path, _gold, _goldBright;
        static Material _hazard, _player, _guardian, _accent, _skin, _shirt, _pants, _shoes;
        static Material _hair, _eyeWhite, _eyeDark, _grass, _dirt, _bark, _water, _leather, _rope;
        static Material _riverWater, _fallingWater, _foam;
        static Material _ember, _flame, _flameCore, _smoke, _charcoal;

        public static Material Stone => _stone ??= TexturedStone();
        public static Material StoneMoss => _stoneMoss ??= Mat(new Color(0.32f, 0.38f, 0.26f), 0.35f);
        public static Material Foliage => _foliage ??= Mat(new Color(0.18f, 0.48f, 0.2f), 0.5f);
        public static Material FoliageDark => _foliageDark ??= Mat(new Color(0.1f, 0.28f, 0.12f), 0.55f);
        public static Material FoliageLight => _foliageLight ??= Mat(new Color(0.34f, 0.62f, 0.26f), 0.45f);
        public static Material Undergrowth => _undergrowth ??= Mat(new Color(0.14f, 0.34f, 0.16f), 0.6f);
        public static Material Path => _path ??= TexturedPath();
        public static Material Gold => _gold ??= Mat(new Color(1f, 0.78f, 0.18f), 0.45f, 0.65f);
        public static Material GoldBright => _goldBright ??= Emissive(new Color(1f, 0.85f, 0.25f), new Color(1.4f, 1f, 0.2f));
        public static Material Hazard => _hazard ??= Mat(new Color(0.72f, 0.2f, 0.1f), 0.35f);
        public static Material Player => _player ??= Mat(new Color(0.78f, 0.58f, 0.35f));
        public static Material Guardian => _guardian ??= Mat(new Color(0.12f, 0.08f, 0.1f), 0.4f);
        public static Material Accent => _accent ??= Mat(new Color(0.22f, 0.42f, 0.36f));
        // Cartoon explorer palette — saturated so the runner reads on the stone bridge
        public static Material Skin => _skin ??= Mat(new Color(0.92f, 0.72f, 0.55f), 0.5f);
        public static Material Shirt => _shirt ??= Mat(new Color(0.92f, 0.82f, 0.45f), 0.4f); // pale yellow/tan
        public static Material Pants => _pants ??= Mat(new Color(0.18f, 0.38f, 0.18f), 0.35f); // forest green
        public static Material Shoes => _shoes ??= Mat(new Color(0.28f, 0.14f, 0.08f), 0.25f);
        public static Material Hair => _hair ??= Mat(new Color(0.85f, 0.12f, 0.08f), 0.25f); // bright red
        public static Material EyeWhite => _eyeWhite ??= Mat(new Color(0.95f, 0.95f, 0.95f));
        public static Material EyeDark => _eyeDark ??= Mat(new Color(0.08f, 0.08f, 0.1f));
        public static Material Grass => _grass ??= Mat(new Color(0.22f, 0.42f, 0.18f), 0.65f);
        public static Material Dirt => _dirt ??= Mat(new Color(0.35f, 0.26f, 0.16f), 0.85f);
        public static Material Bark => _bark ??= Mat(new Color(0.32f, 0.2f, 0.1f), 0.75f);
        public static Material Water => _water ??= TexturedWater();
        public static Material RiverWater => _riverWater ??= MakeRiverWater();
        public static Material FallingWater => _fallingWater ??= MakeFallingWater();
        public static Material Foam => _foam ??= Mat(new Color(0.92f, 0.97f, 1f), 0.75f, 0.05f);
        public static Material Leather => _leather ??= Mat(new Color(0.42f, 0.24f, 0.12f), 0.3f);
        public static Material Rope => _rope ??= Mat(new Color(0.28f, 0.42f, 0.18f), 0.4f);
        public static Material Ember => _ember ??= Emissive(new Color(0.55f, 0.12f, 0.04f), new Color(1.8f, 0.35f, 0.05f));
        public static Material Flame => _flame ??= Emissive(new Color(1f, 0.45f, 0.08f), new Color(2.4f, 0.7f, 0.05f));
        public static Material FlameCore => _flameCore ??= Emissive(new Color(1f, 0.85f, 0.35f), new Color(2.8f, 1.6f, 0.2f));
        public static Material Smoke => _smoke ??= Mat(new Color(0.28f, 0.28f, 0.3f), 0.15f);
        public static Material Charcoal => _charcoal ??= Mat(new Color(0.12f, 0.1f, 0.09f), 0.2f);

        static Material TexturedPath()
        {
            var m = Mat(new Color(0.62f, 0.54f, 0.42f), 0.3f);
            ApplyTex(m, Resources.Load<Texture2D>("Nature/temple_path"), new Vector2(1.4f, 2.8f));
            return m;
        }

        static Material TexturedStone()
        {
            var m = Mat(new Color(0.42f, 0.36f, 0.28f), 0.28f);
            ApplyTex(m, Resources.Load<Texture2D>("Nature/moss_stone"), new Vector2(1.5f, 1.5f));
            return m;
        }

        static Material TexturedWater()
        {
            var m = Mat(new Color(0.12f, 0.28f, 0.22f), 0.85f, 0.15f);
            ApplyTex(m, Resources.Load<Texture2D>("Nature/murky_water"), new Vector2(4f, 8f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
            return m;
        }

        static Material MakeRiverWater()
        {
            var m = Mat(new Color(0.22f, 0.55f, 0.62f), 0.9f, 0.12f);
            ApplyTex(m, Resources.Load<Texture2D>("Nature/murky_water"), new Vector2(3f, 6f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            return m;
        }

        static Material MakeFallingWater()
        {
            var m = Mat(new Color(0.62f, 0.85f, 0.95f), 0.85f, 0.05f);
            ApplyTex(m, Resources.Load<Texture2D>("Nature/murky_water"), new Vector2(1.2f, 4f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
            return m;
        }

        static void ApplyTex(Material m, Texture2D tex, Vector2 tiling)
        {
            if (tex == null) return;
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling);
            }
        }

        static Material Emissive(Color color, Color emission)
        {
            var m = Mat(color, 0.55f, 0.7f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        public static Material Mat(Color color, float smoothness = 0.25f, float metallic = 0f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Diffuse");
            if (shader == null)
            {
                Debug.LogError("[JunglePalette] No usable shader found");
                return null;
            }
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material TintCopy(Material source, Color color)
        {
            var m = new Material(source);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }
    }

    /// <summary>Scrolls shared river/waterfall UVs once per frame for every water mesh.</summary>
    public class WaterFlow : MonoBehaviour
    {
        public static WaterFlow Instance { get; private set; }

        Vector2 _riverOff;
        Vector2 _fallOff;
        static Color _baseRiver;
        static Color _baseFall;
        static Color _baseWater;
        static bool _baseCached;

        public static WaterFlow Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("WaterFlow");
            return go.AddComponent<WaterFlow>();
        }

        void Awake() => Instance = this;

        void Update()
        {
            float dt = Time.deltaTime;
            _riverOff.x += dt * 0.14f;
            _riverOff.y += dt * 0.38f;
            _fallOff.y -= dt * 1.85f;

            ApplyOffset(JunglePalette.RiverWater, _riverOff);
            ApplyOffset(JunglePalette.FallingWater, _fallOff);
            ApplyOffset(JunglePalette.Water, _riverOff * 0.6f);
        }

        static void ApplyOffset(Material m, Vector2 off)
        {
            if (m == null) return;
            m.mainTextureOffset = off;
            if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", off);
        }

        /// <summary>Retint shared water materials so ice/mud/volcanic channels read as distinct biomes.</summary>
        public static void ApplyBiomeTint(BiomeId biome)
        {
            CacheBases();
            Color river = _baseRiver;
            Color fall = _baseFall;
            Color pool = _baseWater;
            switch (biome)
            {
                case BiomeId.IceCaverns:
                    river = new Color(0.55f, 0.78f, 0.92f);
                    fall = new Color(0.75f, 0.92f, 1f);
                    pool = new Color(0.35f, 0.55f, 0.7f);
                    break;
                case BiomeId.DesertTombs:
                    river = new Color(0.35f, 0.48f, 0.42f);
                    fall = new Color(0.55f, 0.7f, 0.6f);
                    pool = new Color(0.22f, 0.32f, 0.28f);
                    break;
                case BiomeId.CaveMines:
                    river = new Color(0.18f, 0.32f, 0.38f);
                    fall = new Color(0.4f, 0.55f, 0.62f);
                    pool = new Color(0.12f, 0.2f, 0.24f);
                    break;
                case BiomeId.VolcanicCrater:
                    river = new Color(0.35f, 0.28f, 0.22f);
                    fall = new Color(0.55f, 0.4f, 0.28f);
                    pool = new Color(0.22f, 0.16f, 0.12f);
                    break;
                case BiomeId.NightSummit:
                    river = new Color(0.2f, 0.35f, 0.55f);
                    fall = new Color(0.45f, 0.65f, 0.85f);
                    pool = new Color(0.12f, 0.2f, 0.35f);
                    break;
            }
            SetColor(JunglePalette.RiverWater, river);
            SetColor(JunglePalette.FallingWater, fall);
            SetColor(JunglePalette.Water, pool);
        }

        static void CacheBases()
        {
            if (_baseCached) return;
            _baseRiver = JunglePalette.RiverWater != null ? JunglePalette.RiverWater.color : new Color(0.22f, 0.55f, 0.62f);
            _baseFall = JunglePalette.FallingWater != null ? JunglePalette.FallingWater.color : new Color(0.62f, 0.85f, 0.95f);
            _baseWater = JunglePalette.Water != null ? JunglePalette.Water.color : new Color(0.12f, 0.28f, 0.22f);
            _baseCached = true;
        }

        static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
