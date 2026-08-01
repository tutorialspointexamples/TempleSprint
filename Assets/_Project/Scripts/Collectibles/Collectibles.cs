using UnityEngine;

namespace TempleSprint
{
    public class CollectibleCoin : MonoBehaviour
    {
        bool _collected;
        float _baseY;
        float _phase;

        void Start()
        {
            _baseY = transform.localPosition.y;
            _phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            transform.Rotate(0f, 260f * Time.deltaTime, 0f, Space.World);
            var lp = transform.localPosition;
            lp.y = _baseY + Mathf.Sin(Time.time * 4.5f + _phase) * 0.14f;
            transform.localPosition = lp;
            MagnetPull();
        }

        void MagnetPull()
        {
            if (PowerUpController.Instance == null || !PowerUpController.Instance.MagnetActive || PlayerController.Instance == null)
                return;
            float r = PowerUpController.Instance.MagnetRadius;
            Vector3 p = PlayerController.Instance.transform.position;
            if (Vector3.Distance(transform.position, p) <= r)
                transform.position = Vector3.MoveTowards(transform.position, p + Vector3.up, 25f * Time.deltaTime);
        }

        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            RunSession.Instance?.AddCoins(1);
            AudioHooks.Instance?.PlayCoin();
            gameObject.SetActive(false);
        }

        public static CollectibleCoin Create(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("Coin");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Diamond";
            go.transform.SetParent(root.transform, false);
            go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            go.transform.localScale = new Vector3(0.42f, 0.42f, 0.16f);
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            Object.Destroy(go.GetComponent<Collider>());

            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.transform.SetParent(root.transform, false);
            tip.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            tip.transform.localScale = new Vector3(0.26f, 0.26f, 0.1f);
            tip.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            Object.Destroy(tip.GetComponent<Collider>());

            // Soft glow shell
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = Vector3.one * 0.55f;
            var gr = glow.GetComponent<Renderer>();
            gr.sharedMaterial = JunglePalette.GoldBright;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(glow.GetComponent<Collider>());

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.55f;
            return root.AddComponent<CollectibleCoin>();
        }
    }

    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpType Type;
        bool _collected;

        void Update() => transform.Rotate(0f, 120f * Time.deltaTime, 0f);

        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            int tier = 0;
            var meta = MetaProgress.Ensure();
            if (meta != null)
            {
                tier = Type switch
                {
                    PowerUpType.Magnet => meta.Data.magnetRadiusLevel,
                    PowerUpType.SpeedBoost => meta.Data.boostDurationLevel,
                    PowerUpType.Shield => meta.Data.reviveLevel,
                    _ => Mathf.Max(meta.Data.magnetRadiusLevel, meta.Data.coinMultiplierLevel)
                };
            }
            PowerUpVfx.SpawnPickupStarburst(transform.position, Type, tier);
            PowerUpController.Instance?.Activate(Type);
            AudioHooks.Instance?.PlayPower();
            gameObject.SetActive(false);
        }

        public static PowerUpPickup Create(Transform parent, Vector3 localPos, PowerUpType type)
        {
            PrimitiveType prim = type switch
            {
                PowerUpType.SpeedBoost => PrimitiveType.Capsule,
                PowerUpType.SlowMo => PrimitiveType.Cylinder,
                PowerUpType.Shield => PrimitiveType.Cube,
                _ => PrimitiveType.Sphere
            };
            var go = GameObject.CreatePrimitive(prim);
            go.name = "PowerUp_" + type;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.7f;
            var mat = type switch
            {
                PowerUpType.Magnet => JunglePalette.Accent,
                PowerUpType.Shield => JunglePalette.Stone,
                PowerUpType.SpeedBoost => JunglePalette.Hazard,
                PowerUpType.SlowMo => BiomeSystem.AccentMat,
                _ => JunglePalette.Gold
            };
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().isTrigger = true;
            if (type == PowerUpType.Magnet)
                PowerUpVfx.AttachMagnetSwirl(go.transform);
            else if (type == PowerUpType.Shield)
                PowerUpVfx.AttachShieldBubble(go.transform, 0.95f);
            var p = go.AddComponent<PowerUpPickup>();
            p.Type = type;
            return p;
        }
    }

    public class GemPickup : MonoBehaviour
    {
        bool _collected;
        void Update() => transform.Rotate(0f, 200f * Time.deltaTime, 0f);

        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            RunSession.Instance?.AddGems(1);
            AudioHooks.Instance?.PlayGem();
            gameObject.SetActive(false);
        }

        public static GemPickup Create(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("GemIdol");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Crystal";
            crystal.transform.SetParent(root.transform, false);
            crystal.transform.localRotation = Quaternion.Euler(45f, 35f, 20f);
            crystal.transform.localScale = new Vector3(0.38f, 0.55f, 0.38f);
            crystal.GetComponent<Renderer>().sharedMaterial = JunglePalette.Accent;
            Object.Destroy(crystal.GetComponent<Collider>());

            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            tip.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            tip.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            tip.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            Object.Destroy(tip.GetComponent<Collider>());

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = Vector3.one * 0.7f;
            var gr = glow.GetComponent<Renderer>();
            gr.sharedMaterial = JunglePalette.Accent;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(glow.GetComponent<Collider>());

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;
            return root.AddComponent<GemPickup>();
        }
    }

    public class RelicPickup : MonoBehaviour
    {
        bool _collected;
        void Update() => transform.Rotate(40f * Time.deltaTime, 90f * Time.deltaTime, 0f);

        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            RunSession.Instance?.AddRelic(1);
            AchievementSystem.Unlock("first_relic");
            AudioHooks.Instance?.PlayPickup();
            gameObject.SetActive(false);
        }

        public static RelicPickup Create(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("IdolRelic");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            // Pedestal
            var baseBlock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseBlock.name = "Pedestal";
            baseBlock.transform.SetParent(root.transform, false);
            baseBlock.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            baseBlock.transform.localScale = new Vector3(0.55f, 0.12f, 0.55f);
            baseBlock.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(baseBlock.GetComponent<Collider>());

            // Carved torso
            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "IdolTorso";
            torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            torso.transform.localScale = new Vector3(0.42f, 0.55f, 0.28f);
            torso.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            Object.Destroy(torso.GetComponent<Collider>());

            // Headdress / crest
            var crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crest.name = "Headdress";
            crest.transform.SetParent(root.transform, false);
            crest.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            crest.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            crest.transform.localScale = new Vector3(0.28f, 0.28f, 0.12f);
            crest.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            Object.Destroy(crest.GetComponent<Collider>());

            // Eye gems
            foreach (float sx in new[] { -0.1f, 0.1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(root.transform, false);
                eye.transform.localPosition = new Vector3(sx, 0.38f, 0.16f);
                eye.transform.localScale = Vector3.one * 0.1f;
                eye.GetComponent<Renderer>().sharedMaterial = JunglePalette.Accent;
                Object.Destroy(eye.GetComponent<Collider>());
            }

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.65f;
            return root.AddComponent<RelicPickup>();
        }
    }

    /// <summary>Roadside idol urn — smash to spray a coin fountain (genre breakable containers).</summary>
    public class BreakableIdol : MonoBehaviour
    {
        public enum IdolKind { CoinUrn, GemIdol }

        public IdolKind Kind = IdolKind.CoinUrn;
        bool _smashed;

        void Update()
        {
            if (_smashed) return;
            transform.Rotate(0f, 35f * Time.deltaTime, 0f, Space.World);
        }

        public void Smash()
        {
            if (_smashed) return;
            _smashed = true;
            AudioHooks.Instance?.PlaySmash();
            ChaseCamera.Instance?.PunchFov(1.1f);
            RunSession.Instance?.RegisterNearMiss();

            int burst = Kind == IdolKind.GemIdol ? 4 : 8;
            for (int i = 0; i < burst; i++)
            {
                float ang = i * (360f / burst) + Random.Range(-12f, 12f);
                Vector3 local = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.35f, 0.2f, 0f);
                var coin = CollectibleCoin.Create(transform.parent, transform.localPosition + local + Vector3.up * 0.4f);
                var burstFx = coin.gameObject.AddComponent<CoinFountainBurst>();
                burstFx.Launch(
                    Quaternion.Euler(0f, ang, 0f) * new Vector3(Random.Range(1.2f, 2.4f), Random.Range(4.5f, 7f), Random.Range(0.2f, 1.2f)));
            }

            if (Kind == IdolKind.GemIdol || Random.value < 0.22f)
            {
                var gem = GemPickup.Create(transform.parent, transform.localPosition + Vector3.up * 0.6f);
                gem.gameObject.AddComponent<CoinFountainBurst>().Launch(new Vector3(
                    Random.Range(-1.2f, 1.2f), Random.Range(5f, 7.5f), Random.Range(0.4f, 1.5f)));
            }

            // Shatter husk briefly, then hide.
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                r.material.color = Color.Lerp(r.material.color, JunglePalette.Charcoal.color, 0.55f);
            }
            gameObject.SetActive(false);
        }

        public static BreakableIdol Create(Transform parent, Vector3 localPos, IdolKind kind = IdolKind.CoinUrn)
        {
            var root = new GameObject(kind == IdolKind.GemIdol ? "BreakableGemIdol" : "BreakableCoinUrn");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Plinth";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            pedestal.transform.localScale = new Vector3(0.7f, 0.1f, 0.7f);
            pedestal.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            Object.Destroy(pedestal.GetComponent<Collider>());

            if (kind == IdolKind.CoinUrn)
            {
                var pot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pot.name = "UrnBody";
                pot.transform.SetParent(root.transform, false);
                pot.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                pot.transform.localScale = new Vector3(0.75f, 0.95f, 0.75f);
                pot.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
                Object.Destroy(pot.GetComponent<Collider>());

                var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rim.transform.SetParent(root.transform, false);
                rim.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                rim.transform.localScale = new Vector3(0.55f, 0.08f, 0.55f);
                rim.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
                Object.Destroy(rim.GetComponent<Collider>());

                // Coin glints peeking from the mouth.
                for (int i = 0; i < 3; i++)
                {
                    var glint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    glint.transform.SetParent(root.transform, false);
                    glint.transform.localPosition = new Vector3((i - 1) * 0.12f, 1.05f, 0.05f);
                    glint.transform.localRotation = Quaternion.Euler(45f, i * 25f, 0f);
                    glint.transform.localScale = new Vector3(0.16f, 0.16f, 0.05f);
                    glint.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
                    Object.Destroy(glint.GetComponent<Collider>());
                }
            }
            else
            {
                var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
                torso.name = "IdolBody";
                torso.transform.SetParent(root.transform, false);
                torso.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                torso.transform.localScale = new Vector3(0.5f, 0.75f, 0.35f);
                torso.GetComponent<Renderer>().sharedMaterial = JunglePalette.Accent;
                Object.Destroy(torso.GetComponent<Collider>());

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                head.transform.localScale = Vector3.one * 0.42f;
                head.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
                Object.Destroy(head.GetComponent<Collider>());

                var crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crest.transform.SetParent(root.transform, false);
                crest.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                crest.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                crest.transform.localScale = new Vector3(0.22f, 0.22f, 0.1f);
                crest.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
                Object.Destroy(crest.GetComponent<Collider>());
            }

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.7f;
            col.center = new Vector3(0f, 0.55f, 0f);
            var idol = root.AddComponent<BreakableIdol>();
            idol.Kind = kind;
            return idol;
        }
    }

    /// <summary>Short upward burst for coins sprayed from smashed idols.</summary>
    public class CoinFountainBurst : MonoBehaviour
    {
        Vector3 _vel;
        float _life = 0.85f;
        bool _active;

        public void Launch(Vector3 velocity)
        {
            _vel = velocity;
            _active = true;
            _life = 0.85f;
        }

        void Update()
        {
            if (!_active) return;
            _life -= Time.deltaTime;
            _vel.y -= 18f * Time.deltaTime;
            transform.position += _vel * Time.deltaTime;
            if (_life <= 0f || transform.position.y < 0.2f)
            {
                var lp = transform.localPosition;
                lp.y = Mathf.Max(0.9f, lp.y);
                transform.localPosition = lp;
                _active = false;
                Destroy(this);
            }
        }
    }
}
