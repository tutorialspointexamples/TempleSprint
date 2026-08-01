using UnityEngine;

namespace TempleSprint
{
    public enum BiomeId
    {
        JungleRuins = 0,
        DesertTombs = 1,
        IceCaverns = 2,
        CaveMines = 3,
        VolcanicCrater = 4
    }

    /// <summary>Biome unlock + visual palette for Jungle / Desert / Ice / Cave / Volcano.</summary>
    public static class BiomeSystem
    {
        public static BiomeId Current { get; private set; } = BiomeId.JungleRuins;

        public static bool IsUnlocked(BiomeId id)
        {
            var m = MetaProgress.Ensure().Data;
            if (id == BiomeId.JungleRuins) return true;
            if (id == BiomeId.DesertTombs) return m.desertUnlocked || m.totalRuns >= 3 || m.totalDistance >= 500f;
            if (id == BiomeId.IceCaverns) return m.iceUnlocked || m.totalRuns >= 8 || m.totalDistance >= 2000f;
            if (id == BiomeId.CaveMines) return m.caveUnlocked || m.totalRuns >= 12 || m.totalDistance >= 3500f || m.relics >= 12;
            if (id == BiomeId.VolcanicCrater) return m.volcanoUnlocked || m.totalRuns >= 18 || m.totalDistance >= 6000f || m.relics >= 18;
            return false;
        }

        public static void Select(BiomeId id)
        {
            if (!IsUnlocked(id)) id = BiomeId.JungleRuins;
            Current = id;
            PlayerPrefs.SetInt("TempleSprint.Biome", (int)id);
            PlayerPrefs.Save();
            ApplyLighting();
        }

        public static void LoadSaved()
        {
            var id = (BiomeId)PlayerPrefs.GetInt("TempleSprint.Biome", 0);
            Select(IsUnlocked(id) ? id : BiomeId.JungleRuins);
        }

        public static void ApplyLighting()
        {
            switch (Current)
            {
                case BiomeId.DesertTombs:
                    RenderSettings.ambientLight = new Color(0.72f, 0.58f, 0.35f);
                    break;
                case BiomeId.IceCaverns:
                    RenderSettings.ambientLight = new Color(0.45f, 0.58f, 0.72f);
                    break;
                case BiomeId.CaveMines:
                    RenderSettings.ambientLight = new Color(0.28f, 0.3f, 0.34f);
                    break;
                case BiomeId.VolcanicCrater:
                    RenderSettings.ambientLight = new Color(0.62f, 0.32f, 0.18f);
                    break;
                default:
                    RenderSettings.ambientLight = new Color(0.42f, 0.5f, 0.42f);
                    break;
            }
            NatureBackdrop.Instance?.ApplyBiomeLook();
        }

        public static string DisplayName(BiomeId id) => id switch
        {
            BiomeId.DesertTombs => "Desert Tombs",
            BiomeId.IceCaverns => "Ice Caverns",
            BiomeId.CaveMines => "Cave Mines",
            BiomeId.VolcanicCrater => "Volcanic Crater",
            _ => "Jungle Ruins"
        };

        /// <summary>Stage bias for special crossings — cave favors carts, volcano favors fire, jungle favors river/zipline.</summary>
        public static float MineCartBias => Current == BiomeId.CaveMines ? 1.8f : Current == BiomeId.IceCaverns ? 1.2f : 1f;
        public static float ZiplineBias => Current == BiomeId.JungleRuins || Current == BiomeId.DesertTombs ? 1.4f : 1f;
        public static float FireBias => Current == BiomeId.VolcanicCrater ? 1.7f : 1f;
        public static float LavaRiverBias =>
            (Current == BiomeId.VolcanicCrater ? 2.1f
                : Current == BiomeId.DesertTombs ? 0.7f
                : Current == BiomeId.CaveMines ? 0.85f
                : 0.45f) * EventService.EventStageBias(Current);
        public static float RuinForkBias =>
            (Current == BiomeId.JungleRuins || Current == BiomeId.DesertTombs ? 1.65f
                : Current == BiomeId.CaveMines ? 1.35f
                : Current == BiomeId.VolcanicCrater ? 1.05f
                : 0.8f) * EventService.EventStageBias(Current);
        public static float RiverBias => Current == BiomeId.JungleRuins || Current == BiomeId.IceCaverns ? 1.3f : 0.85f;
        public static float IceSurfBias => Current == BiomeId.IceCaverns ? 1.9f : Current == BiomeId.CaveMines ? 0.6f : 0.85f;
        public static float SwimBias => Current == BiomeId.JungleRuins || Current == BiomeId.IceCaverns ? 1.35f : 0.9f;
        public static float WallRunBias =>
            (Current == BiomeId.CaveMines || Current == BiomeId.JungleRuins ? 1.55f
                : Current == BiomeId.VolcanicCrater ? 1.25f : 0.9f) * EventService.EventStageBias(Current);
        public static float LedgeGrabBias =>
            (Current == BiomeId.DesertTombs || Current == BiomeId.JungleRuins ? 1.5f
                : Current == BiomeId.IceCaverns ? 1.2f : 0.85f) * EventService.EventStageBias(Current);
        public static float TreeBridgeBias =>
            (Current == BiomeId.JungleRuins ? 1.85f
                : Current == BiomeId.DesertTombs ? 1.15f
                : Current == BiomeId.CaveMines ? 0.7f : 0.9f) * EventService.EventStageBias(Current);
        public static float CanopyRopeBias =>
            (Current == BiomeId.JungleRuins ? 1.9f
                : Current == BiomeId.DesertTombs ? 0.85f
                : Current == BiomeId.IceCaverns ? 0.7f : 0.75f) * EventService.EventStageBias(Current);
        public static float WaterfallPlungeBias =>
            (Current == BiomeId.JungleRuins || Current == BiomeId.IceCaverns ? 1.7f
                : Current == BiomeId.VolcanicCrater ? 0.8f
                : Current == BiomeId.CaveMines ? 0.65f : 1f) * EventService.EventStageBias(Current);
        public static float TempleHallBias =>
            (Current == BiomeId.JungleRuins || Current == BiomeId.DesertTombs ? 1.75f
                : Current == BiomeId.CaveMines ? 1.45f
                : Current == BiomeId.VolcanicCrater ? 1.1f
                : 0.85f) * EventService.EventStageBias(Current);

        public static Material PathMat => BiomePalette.Path(Current);
        public static Material StoneMat => BiomePalette.Stone(Current);
        public static Material AccentMat => BiomePalette.Accent(Current);
        public static Material FoliageMat => BiomePalette.Foliage(Current);
    }

    public static class BiomePalette
    {
        static Material Make(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");
            var m = new Material(shader) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        public static Material Path(BiomeId b)
        {
            if (b == BiomeId.JungleRuins) return JunglePalette.Path;
            Color c = b switch
            {
                BiomeId.DesertTombs => new Color(0.78f, 0.62f, 0.35f),
                BiomeId.IceCaverns => new Color(0.72f, 0.82f, 0.9f),
                BiomeId.CaveMines => new Color(0.38f, 0.36f, 0.34f),
                BiomeId.VolcanicCrater => new Color(0.42f, 0.28f, 0.22f),
                _ => new Color(0.62f, 0.54f, 0.42f)
            };
            var m = Make(c);
            // Original authored path texture, tinted per biome (not a TR2 asset).
            ApplyTex(m, Resources.Load<Texture2D>("Nature/temple_path"), new Vector2(1.4f, 2.8f), c);
            return m;
        }

        public static Material Stone(BiomeId b)
        {
            if (b == BiomeId.JungleRuins) return JunglePalette.Stone;
            Color c = b switch
            {
                BiomeId.DesertTombs => new Color(0.55f, 0.4f, 0.25f),
                BiomeId.IceCaverns => new Color(0.55f, 0.65f, 0.75f),
                BiomeId.CaveMines => new Color(0.32f, 0.34f, 0.38f),
                BiomeId.VolcanicCrater => new Color(0.35f, 0.22f, 0.18f),
                _ => new Color(0.42f, 0.36f, 0.28f)
            };
            var m = Make(c);
            ApplyTex(m, Resources.Load<Texture2D>("Nature/moss_stone"), new Vector2(1.5f, 1.5f), c);
            return m;
        }

        static void ApplyTex(Material m, Texture2D tex, Vector2 tiling, Color tint)
        {
            if (m == null || tex == null) return;
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            m.color = tint;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        }

        public static Material Accent(BiomeId b) => b switch
        {
            BiomeId.DesertTombs => Make(new Color(0.85f, 0.45f, 0.15f)),
            BiomeId.IceCaverns => Make(new Color(0.35f, 0.7f, 0.85f)),
            BiomeId.CaveMines => Make(new Color(0.75f, 0.55f, 0.2f)),
            BiomeId.VolcanicCrater => Make(new Color(1f, 0.4f, 0.12f)),
            _ => JunglePalette.Accent
        };

        public static Material Foliage(BiomeId b) => b switch
        {
            BiomeId.DesertTombs => Make(new Color(0.65f, 0.5f, 0.2f)),
            BiomeId.IceCaverns => Make(new Color(0.85f, 0.92f, 0.98f)),
            BiomeId.CaveMines => Make(new Color(0.22f, 0.24f, 0.28f)),
            BiomeId.VolcanicCrater => Make(new Color(0.28f, 0.18f, 0.12f)),
            _ => JunglePalette.Foliage
        };
    }

    /// <summary>Classic Temple Run swamp-temple: water under causeway, cliffs, fog, vine curtain.</summary>
    public class NatureBackdrop : MonoBehaviour
    {
        public static NatureBackdrop Instance { get; private set; }

        Transform _follow;
        Transform[] _billboards;
        Transform[] _treeClusters;
        Transform _scenicRoot;
        Renderer _waterRend;
        Renderer _groundRend;
        Material _waterMatInstance;
        Material _groundMatInstance;
        Transform _vineCurtain;
        float _treeSpacing = 16f;
        Vector2 _waterOffset;

        public static NatureBackdrop Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NatureBackdrop");
            go.transform.SetParent(parent, false);
            return go.AddComponent<NatureBackdrop>();
        }

        void Awake()
        {
            Instance = this;
            Build();
            ApplyBiomeLook();
        }

        public void SetFollow(Transform follow) => _follow = follow;

        void Build()
        {
            BuildSky();
            BuildForestGround();
            BuildDistantHills();
            BuildTreeRing();
            BuildFillLight();
        }

        void BuildSky()
        {
            ApplyBiomeSkybox(BiomeId.JungleRuins);

            DynamicGI.UpdateEnvironment();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = new Color(0.52f, 0.62f, 0.66f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.32f, 0.24f);
        }

        static string SkyResource(BiomeId id) => id switch
        {
            BiomeId.DesertTombs => "Nature/sky_desert",
            BiomeId.IceCaverns => "Nature/sky_ice",
            BiomeId.CaveMines => "Nature/sky_cave",
            BiomeId.VolcanicCrater => "Nature/sky_volcano",
            _ => "Nature/sky_jungle"
        };

        void ApplyBiomeSkybox(BiomeId id)
        {
            var skyTex = Resources.Load<Texture2D>(SkyResource(id))
                         ?? Resources.Load<Texture2D>("Nature/temple_ruins_sky")
                         ?? Resources.Load<Texture2D>("Nature/jungle_sky");
            if (skyTex == null) return;

            var panoramic = Shader.Find("Skybox/Panoramic")
                            ?? Shader.Find("Skybox/Cubemap")
                            ?? Shader.Find("Skybox/6 Sided");
            if (panoramic == null) return;

            var box = RenderSettings.skybox != null && RenderSettings.skybox.shader == panoramic
                ? RenderSettings.skybox
                : new Material(panoramic);
            box.SetTexture("_MainTex", skyTex);
            if (box.HasProperty("_Tex")) box.SetTexture("_Tex", skyTex);
            RenderSettings.skybox = box;
        }

        /// <summary>Forest floor stretching to the fog line; the causeway is raised above it.</summary>
        void BuildForestGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ForestGround";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(0f, GroundY - 1f, 0f);
            ground.transform.localScale = new Vector3(320f, 2f, 320f);
            _groundRend = ground.GetComponent<Renderer>();
            _groundMatInstance = new Material(JunglePalette.Grass);
            _groundRend.sharedMaterial = _groundMatInstance;
            Object.Destroy(ground.GetComponent<Collider>());

            // Slow-drifting mist just above the floor, reusing the scrolling water material.
            var mist = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mist.name = "GroundMist";
            mist.transform.SetParent(transform, false);
            mist.transform.localPosition = new Vector3(0f, GroundY + 0.6f, 0f);
            mist.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mist.transform.localScale = new Vector3(300f, 300f, 1f);
            _waterRend = mist.GetComponent<Renderer>();
            _waterMatInstance = new Material(JunglePalette.Water);
            _waterMatInstance.color = new Color(0.35f, 0.42f, 0.36f, 1f);
            _waterRend.sharedMaterial = _waterMatInstance;
            _waterRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(mist.GetComponent<Collider>());
        }

        void BuildDistantHills()
        {
            // Facing-relative distant hills — never parented to the follow-root in world Z.
            if (_scenicRoot == null)
            {
                _scenicRoot = new GameObject("ForestScatter").transform;
                _scenicRoot.SetParent(transform, false);
            }

            var hillsTex = Resources.Load<Texture2D>("Nature/jungle_hills");
            _billboards = new Transform[4];
            for (int i = 0; i < _billboards.Length; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                var board = GameObject.CreatePrimitive(PrimitiveType.Quad);
                board.name = "HillBillboard_" + i;
                board.transform.SetParent(_scenicRoot, false);
                board.transform.localScale = new Vector3(60f, 28f, 1f);
                Object.Destroy(board.GetComponent<Collider>());
                var mat = UnlitMat(new Color(0.3f, 0.42f, 0.28f));
                if (hillsTex != null)
                {
                    mat.mainTexture = hillsTex;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", hillsTex);
                }
                var r = board.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _billboards[i] = board.transform;
                PlaceHill(board.transform, side, 70f + (i / 2) * 40f);
            }
        }

        // Deep-forest layer beyond the per-tile planting; kept clear of the run corridor.
        const float RingNear = 28f;
        const float RingFar = 52f;
        const float CorridorClearance = 18f;
        const float HillLateral = 48f;
        const float GroundY = -3.8f;

        void BuildTreeRing()
        {
            if (_scenicRoot == null)
            {
                _scenicRoot = new GameObject("ForestScatter").transform;
                _scenicRoot.SetParent(transform, false);
            }

            _treeClusters = new Transform[14];
            for (int i = 0; i < _treeClusters.Length; i++)
            {
                var cluster = new GameObject("TreeCluster_" + i).transform;
                cluster.SetParent(_scenicRoot, false);
                int count = Random.Range(2, 5);
                for (int t = 0; t < count; t++)
                {
                    SpawnTree(cluster,
                        new Vector3(Random.Range(-3.5f, 3.5f), 0f, Random.Range(-3.5f, 3.5f)),
                        Random.Range(1.2f, 2.0f));
                }
                _treeClusters[i] = cluster;
                PlaceAhead(cluster, (i % 2 == 0) ? -1f : 1f, (i / 2) * _treeSpacing);
            }
        }

        void SpawnTree(Transform parent, Vector3 localPos, float scale = 1f)
        {
            // Distant ring matches selected biome so Desert/Ice/Cave/Volcano don't keep jungle trees.
            Material trunkMat = BiomeSystem.Current switch
            {
                BiomeId.DesertTombs => BiomeSystem.FoliageMat,
                BiomeId.IceCaverns => JunglePalette.Mat(new Color(0.75f, 0.9f, 1f), 0.6f, 0.15f),
                BiomeId.CaveMines => BiomeSystem.StoneMat,
                BiomeId.VolcanicCrater => JunglePalette.Charcoal,
                _ => JunglePalette.Bark
            };
            Material canopyMat = BiomeSystem.FoliageMat;

            float trunkH = Random.Range(3.2f, 4.6f) * scale;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(parent, false);
            trunk.transform.localPosition = localPos + new Vector3(0f, trunkH * 0.5f - 0.3f, 0f);
            trunk.transform.localScale = new Vector3(0.32f * scale, trunkH * 0.5f, 0.32f * scale);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
            Object.Destroy(trunk.GetComponent<Collider>());

            const int tiers = 3;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)(tiers - 1);
                float s = Mathf.Lerp(2.6f, 0.9f, t) * scale * Random.Range(0.92f, 1.1f);
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.transform.SetParent(parent, false);
                canopy.transform.localPosition = localPos
                    + new Vector3(Random.Range(-0.2f, 0.2f), trunkH * 0.62f + i * 1.05f * scale, Random.Range(-0.2f, 0.2f));
                canopy.transform.localScale = new Vector3(s, s * 0.88f, s);
                canopy.GetComponent<Renderer>().sharedMaterial = canopyMat;
                Object.Destroy(canopy.GetComponent<Collider>());
            }
        }

        /// <summary>
        /// Park a prop relative to the runner's current heading. Laying scenery out in
        /// world Z instead put it straight across the path after every turn.
        /// </summary>
        void PlaceAhead(Transform prop, float side, float distanceAhead)
        {
            if (prop == null) return;
            Vector3 origin = _follow != null ? _follow.position : Vector3.zero;
            Vector3 fwd = _follow != null ? Flat(_follow.forward) : Vector3.forward;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            Vector3 world = origin
                            + fwd * distanceAhead
                            + right * (side * Random.Range(RingNear, RingFar));
            world.y = GroundY;
            prop.position = world;
        }

        void PlaceHill(Transform prop, float side, float distanceAhead)
        {
            if (prop == null) return;
            Vector3 origin = _follow != null ? _follow.position : Vector3.zero;
            Vector3 fwd = _follow != null ? Flat(_follow.forward) : Vector3.forward;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            Vector3 world = origin
                            + fwd * distanceAhead
                            + right * (side * HillLateral);
            world.y = GroundY + 10f;
            prop.position = world;
            // Face roughly toward the path centre.
            Vector3 look = origin + fwd * distanceAhead - world;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                prop.rotation = Quaternion.LookRotation(look.normalized);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        void BuildFillLight()
        {
            var fill = new GameObject("FillLight");
            fill.transform.SetParent(transform, false);
            fill.transform.rotation = Quaternion.Euler(15f, 60f, 0f);
            var light = fill.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.65f, 0.8f, 0.9f);
            light.intensity = 0.4f;
            light.shadows = LightShadows.None;
        }

        /// <summary>Near-camera vine framing like classic Temple Run screenshots.</summary>
        public void AttachVineCurtain(Transform cameraTransform)
        {
            if (cameraTransform == null || _vineCurtain != null) return;
            _vineCurtain = new GameObject("VineCurtain").transform;
            _vineCurtain.SetParent(cameraTransform, false);
            // Soft frame only — far corner ropes, never fill the phone Game view
            _vineCurtain.localPosition = new Vector3(0f, 2.2f, 2.4f);
            float[] xs = { -2.2f, 2.2f };
            foreach (float x in xs)
            {
                float len = Random.Range(0.5f, 1.0f);
                var vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vine.transform.SetParent(_vineCurtain, false);
                vine.transform.localPosition = new Vector3(x, -len * 0.25f, Random.Range(0f, 0.2f));
                vine.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
                vine.transform.localScale = new Vector3(0.03f, len * 0.4f, 0.03f);
                vine.GetComponent<Renderer>().sharedMaterial = JunglePalette.FoliageDark;
                vine.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(vine.GetComponent<Collider>());
            }
        }

        public void ApplyBiomeLook()
        {
            Color ground;
            Color mist;
            Color skyTint;
            switch (BiomeSystem.Current)
            {
                case BiomeId.DesertTombs:
                    RenderSettings.fogColor = new Color(0.85f, 0.7f, 0.45f);
                    RenderSettings.fogDensity = 0.009f;
                    RenderSettings.ambientSkyColor = new Color(0.9f, 0.75f, 0.45f);
                    RenderSettings.ambientEquatorColor = new Color(0.7f, 0.58f, 0.38f);
                    RenderSettings.ambientGroundColor = new Color(0.4f, 0.3f, 0.18f);
                    ground = new Color(0.62f, 0.48f, 0.28f);
                    mist = new Color(0.72f, 0.58f, 0.32f, 1f);
                    skyTint = new Color(1f, 0.92f, 0.7f);
                    break;
                case BiomeId.IceCaverns:
                    RenderSettings.fogColor = new Color(0.7f, 0.82f, 0.92f);
                    RenderSettings.fogDensity = 0.012f;
                    RenderSettings.ambientSkyColor = new Color(0.65f, 0.78f, 0.95f);
                    RenderSettings.ambientEquatorColor = new Color(0.55f, 0.65f, 0.78f);
                    RenderSettings.ambientGroundColor = new Color(0.35f, 0.42f, 0.5f);
                    ground = new Color(0.72f, 0.84f, 0.92f);
                    mist = new Color(0.78f, 0.9f, 0.98f, 1f);
                    skyTint = new Color(0.75f, 0.88f, 1f);
                    break;
                case BiomeId.CaveMines:
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.18f, 0.2f, 0.24f);
                    RenderSettings.fogDensity = 0.018f;
                    RenderSettings.ambientSkyColor = new Color(0.28f, 0.32f, 0.38f);
                    RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.28f);
                    RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.14f);
                    ground = new Color(0.22f, 0.22f, 0.24f);
                    mist = new Color(0.16f, 0.18f, 0.22f, 1f);
                    skyTint = new Color(0.35f, 0.38f, 0.45f);
                    break;
                case BiomeId.VolcanicCrater:
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.55f, 0.28f, 0.16f);
                    RenderSettings.fogDensity = 0.014f;
                    RenderSettings.ambientSkyColor = new Color(0.75f, 0.4f, 0.22f);
                    RenderSettings.ambientEquatorColor = new Color(0.5f, 0.28f, 0.18f);
                    RenderSettings.ambientGroundColor = new Color(0.22f, 0.1f, 0.08f);
                    ground = new Color(0.28f, 0.14f, 0.1f);
                    mist = new Color(0.45f, 0.18f, 0.1f, 1f);
                    skyTint = new Color(0.95f, 0.55f, 0.3f);
                    break;
                default:
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.52f, 0.62f, 0.66f);
                    RenderSettings.fogDensity = 0.009f;
                    RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.78f);
                    RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.5f);
                    RenderSettings.ambientGroundColor = new Color(0.28f, 0.32f, 0.24f);
                    ground = new Color(0.22f, 0.42f, 0.18f);
                    mist = new Color(0.35f, 0.42f, 0.36f, 1f);
                    skyTint = new Color(0.85f, 0.92f, 0.95f);
                    break;
            }

            if (_groundMatInstance != null)
            {
                _groundMatInstance.color = ground;
                if (_groundMatInstance.HasProperty("_BaseColor"))
                    _groundMatInstance.SetColor("_BaseColor", ground);
            }
            if (_waterMatInstance != null)
            {
                _waterMatInstance.color = mist;
                if (_waterMatInstance.HasProperty("_BaseColor"))
                    _waterMatInstance.SetColor("_BaseColor", mist);
            }
            ApplyBiomeSkybox(BiomeSystem.Current);
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Tint"))
                RenderSettings.skybox.SetColor("_Tint", skyTint);
            else if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_SkyTint"))
                RenderSettings.skybox.SetColor("_SkyTint", skyTint);
            DynamicGI.UpdateEnvironment();

            // Retint distant hill billboards so each biome reads from the first second of a run.
            if (_billboards != null)
            {
                Color hill = BiomeSystem.Current switch
                {
                    BiomeId.DesertTombs => new Color(0.72f, 0.55f, 0.3f),
                    BiomeId.IceCaverns => new Color(0.65f, 0.78f, 0.88f),
                    BiomeId.CaveMines => new Color(0.22f, 0.24f, 0.28f),
                    BiomeId.VolcanicCrater => new Color(0.4f, 0.18f, 0.12f),
                    _ => new Color(0.3f, 0.42f, 0.28f)
                };
                foreach (var b in _billboards)
                {
                    if (b == null) continue;
                    var r = b.GetComponent<Renderer>();
                    if (r == null || r.sharedMaterial == null) continue;
                    var m = r.material;
                    m.color = hill;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hill);
                }
            }
        }

        void LateUpdate()
        {
            if (_follow == null && PlayerController.Instance != null)
                _follow = PlayerController.Instance.transform;
            if (_follow == null) return;

            Vector3 p = _follow.position;
            p.y = transform.position.y;
            transform.position = p;

            // Scroll murky water UVs (cached instance — do not use .material each frame)
            if (_waterMatInstance != null)
            {
                _waterOffset.y += Time.deltaTime * 0.04f;
                _waterOffset.x += Time.deltaTime * 0.015f;
                _waterMatInstance.mainTextureOffset = _waterOffset;
                if (_waterMatInstance.HasProperty("_BaseMap"))
                    _waterMatInstance.SetTextureOffset("_BaseMap", _waterOffset);
            }

            // Forest scatter lives in stable world space so it does not ride the root.
            if (_scenicRoot != null)
            {
                _scenicRoot.position = Vector3.zero;
                _scenicRoot.rotation = Quaternion.identity;
            }

            Vector3 fwd = Flat(_follow.forward);
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            if (_treeClusters != null)
            {
                foreach (var cluster in _treeClusters)
                {
                    if (cluster == null) continue;
                    Vector3 delta = cluster.position - _follow.position;
                    delta.y = 0f;
                    float along = Vector3.Dot(delta, fwd);
                    float lateral = Vector3.Dot(delta, right);

                    bool behind = along < -30f;
                    // Relocate any cluster that a turn swings into the corridor — even near ones.
                    bool inCorridor = along > -5f && Mathf.Abs(lateral) < CorridorClearance;
                    if (behind || inCorridor)
                        PlaceAhead(cluster, lateral < 0f ? -1f : 1f, Random.Range(55f, 95f));
                }
            }

            if (_billboards != null)
            {
                for (int i = 0; i < _billboards.Length; i++)
                {
                    var b = _billboards[i];
                    if (b == null) continue;
                    Vector3 delta = b.position - _follow.position;
                    delta.y = 0f;
                    float along = Vector3.Dot(delta, fwd);
                    float lateral = Vector3.Dot(delta, right);
                    float side = (i % 2 == 0) ? -1f : 1f;
                    bool behind = along < -40f;
                    bool inCorridor = along > -5f && Mathf.Abs(lateral) < CorridorClearance + 10f;
                    if (behind || inCorridor)
                        PlaceHill(b, side, Random.Range(75f, 120f));
                }
            }
        }

        static Material SkyMat(Texture2D tex, Color fallback)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            }
            else
            {
                m.color = fallback;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", fallback);
            }
            return m;
        }

        static Material UnlitMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
