using UnityEngine;

namespace TempleSprint
{
    public enum CosmeticSlot
    {
        Hat = 0,
        Pet = 1,
        Cape = 2,
        Scarf = 3
    }

    /// <summary>Original hats, pets, capes & scarves for locker cosmetics (genre parity, not Imangi IP).</summary>
    public static class CosmeticRoster
    {
        public struct CosmeticDef
        {
            public string id;
            public string displayName;
            public CosmeticSlot slot;
            public int gemCost;
            public Color color;
            /// <summary>Legacy helper — true only for hat slot.</summary>
            public bool isHat => slot == CosmeticSlot.Hat;
        }

        public static readonly CosmeticDef[] Hats =
        {
            new CosmeticDef { id = "hat_none", displayName = "No Hat", slot = CosmeticSlot.Hat, gemCost = 0, color = Color.clear },
            new CosmeticDef { id = "hat_reed_cap", displayName = "Reed Cap", slot = CosmeticSlot.Hat, gemCost = 40, color = new Color(0.55f, 0.35f, 0.18f) },
            new CosmeticDef { id = "hat_canopy", displayName = "Canopy Helm", slot = CosmeticSlot.Hat, gemCost = 90, color = new Color(0.2f, 0.55f, 0.28f) },
            new CosmeticDef { id = "hat_ember", displayName = "Ember Band", slot = CosmeticSlot.Hat, gemCost = 120, color = new Color(0.95f, 0.4f, 0.15f) },
            new CosmeticDef { id = "hat_lotus_crown", displayName = "Lotus Crown", slot = CosmeticSlot.Hat, gemCost = 80, color = new Color(0.95f, 0.75f, 0.9f) },
            new CosmeticDef { id = "hat_sun_veil", displayName = "Sun Veil", slot = CosmeticSlot.Hat, gemCost = 85, color = new Color(0.95f, 0.78f, 0.25f) },
            new CosmeticDef { id = "hat_frost_circlet", displayName = "Frost Circlet", slot = CosmeticSlot.Hat, gemCost = 85, color = new Color(0.7f, 0.9f, 1f) }
        };

        public static readonly CosmeticDef[] Pets =
        {
            new CosmeticDef { id = "pet_none", displayName = "No Pet", slot = CosmeticSlot.Pet, gemCost = 0, color = Color.clear },
            new CosmeticDef { id = "pet_glowbug", displayName = "Glowbug", slot = CosmeticSlot.Pet, gemCost = 60, color = new Color(0.85f, 1f, 0.35f) },
            new CosmeticDef { id = "pet_frostpup", displayName = "Frost Pup", slot = CosmeticSlot.Pet, gemCost = 110, color = new Color(0.65f, 0.85f, 1f) },
            new CosmeticDef { id = "pet_ashling", displayName = "Ashling", slot = CosmeticSlot.Pet, gemCost = 140, color = new Color(1f, 0.45f, 0.2f) },
            new CosmeticDef { id = "pet_lotus_moth", displayName = "Lotus Moth", slot = CosmeticSlot.Pet, gemCost = 95, color = new Color(0.9f, 0.65f, 0.95f) },
            new CosmeticDef { id = "pet_sand_skitter", displayName = "Sand Skitter", slot = CosmeticSlot.Pet, gemCost = 95, color = new Color(0.85f, 0.65f, 0.3f) },
            new CosmeticDef { id = "pet_ice_wisp", displayName = "Ice Wisp", slot = CosmeticSlot.Pet, gemCost = 95, color = new Color(0.75f, 0.95f, 1f) }
        };

        public static readonly CosmeticDef[] Capes =
        {
            new CosmeticDef { id = "cape_none", displayName = "No Cape", slot = CosmeticSlot.Cape, gemCost = 0, color = Color.clear },
            new CosmeticDef { id = "cape_reed", displayName = "Reed Cape", slot = CosmeticSlot.Cape, gemCost = 70, color = new Color(0.45f, 0.32f, 0.18f) },
            new CosmeticDef { id = "cape_canopy", displayName = "Canopy Cloak", slot = CosmeticSlot.Cape, gemCost = 110, color = new Color(0.18f, 0.48f, 0.28f) },
            new CosmeticDef { id = "cape_ember", displayName = "Ember Mantle", slot = CosmeticSlot.Cape, gemCost = 140, color = new Color(0.85f, 0.28f, 0.12f) },
            new CosmeticDef { id = "cape_night", displayName = "Night Veil", slot = CosmeticSlot.Cape, gemCost = 100, color = new Color(0.18f, 0.2f, 0.38f) }
        };

        public static readonly CosmeticDef[] Scarves =
        {
            new CosmeticDef { id = "scarf_none", displayName = "No Scarf", slot = CosmeticSlot.Scarf, gemCost = 0, color = Color.clear },
            new CosmeticDef { id = "scarf_sand", displayName = "Sand Wrap", slot = CosmeticSlot.Scarf, gemCost = 55, color = new Color(0.88f, 0.68f, 0.35f) },
            new CosmeticDef { id = "scarf_frost", displayName = "Frost Stole", slot = CosmeticSlot.Scarf, gemCost = 90, color = new Color(0.7f, 0.88f, 1f) },
            new CosmeticDef { id = "scarf_vine", displayName = "Vine Band", slot = CosmeticSlot.Scarf, gemCost = 75, color = new Color(0.32f, 0.55f, 0.28f) },
            new CosmeticDef { id = "scarf_ash", displayName = "Ash Ribbon", slot = CosmeticSlot.Scarf, gemCost = 95, color = new Color(0.55f, 0.22f, 0.15f) }
        };

        /// <summary>Seasonal cosmetics featured in the current live event week.</summary>
        public static string[] FeaturedSeasonalIds => EventService.WeekIndex % 3 switch
        {
            0 => new[] { "hat_lotus_crown", "pet_lotus_moth", "cape_canopy", "scarf_vine" },
            1 => new[] { "hat_sun_veil", "pet_sand_skitter", "cape_reed", "scarf_sand" },
            _ => new[] { "hat_frost_circlet", "pet_ice_wisp", "cape_night", "scarf_frost" }
        };

        public static bool IsSeasonal(string id)
        {
            return id == "hat_lotus_crown" || id == "hat_sun_veil" || id == "hat_frost_circlet"
                   || id == "pet_lotus_moth" || id == "pet_sand_skitter" || id == "pet_ice_wisp"
                   || id == "cape_night" || id == "scarf_ash";
        }

        public static bool IsFeaturedThisWeek(string id)
        {
            foreach (var f in FeaturedSeasonalIds)
                if (f == id) return true;
            return false;
        }

        public static bool IsNoneId(string id) =>
            id == "hat_none" || id == "pet_none" || id == "cape_none" || id == "scarf_none";

        /// <summary>Glowbug = magnet pulse, Frost Pup = shield chirp, Ashling = coin bonus; seasonals share nearest passive.</summary>
        public static class PetPassives
        {
            public static bool MagnetPulse => SelectedPetId is "pet_glowbug" or "pet_lotus_moth";
            public static bool ShieldChirp => SelectedPetId is "pet_frostpup" or "pet_ice_wisp";
            public static bool CoinBonus => SelectedPetId is "pet_ashling" or "pet_sand_skitter";
            public static float CoinMult => CoinBonus ? 1.12f : 1f;
            public static float MagnetPulseInterval => 7.5f;
            public static float MagnetPulseDuration => 2.2f;
            public static float MagnetPulseRadiusBonus => 1.6f;
            public static float ShieldChirpInterval => 18f;
        }

        public static string SelectedHatId
        {
            get => MetaProgress.Ensure().Data.selectedHat ?? "hat_none";
            set
            {
                MetaProgress.Ensure().Data.selectedHat = value;
                MetaProgress.Ensure().Save();
            }
        }

        public static string SelectedPetId
        {
            get => MetaProgress.Ensure().Data.selectedPet ?? "pet_none";
            set
            {
                MetaProgress.Ensure().Data.selectedPet = value;
                MetaProgress.Ensure().Save();
            }
        }

        public static string SelectedCapeId
        {
            get => MetaProgress.Ensure().Data.selectedCape ?? "cape_none";
            set
            {
                MetaProgress.Ensure().Data.selectedCape = value;
                MetaProgress.Ensure().Save();
            }
        }

        public static string SelectedScarfId
        {
            get => MetaProgress.Ensure().Data.selectedScarf ?? "scarf_none";
            set
            {
                MetaProgress.Ensure().Data.selectedScarf = value;
                MetaProgress.Ensure().Save();
            }
        }

        public static string GetSelected(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Hat => SelectedHatId,
            CosmeticSlot.Pet => SelectedPetId,
            CosmeticSlot.Cape => SelectedCapeId,
            CosmeticSlot.Scarf => SelectedScarfId,
            _ => "hat_none"
        };

        public static CosmeticDef[] GetList(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Hat => Hats,
            CosmeticSlot.Pet => Pets,
            CosmeticSlot.Cape => Capes,
            CosmeticSlot.Scarf => Scarves,
            _ => Hats
        };

        public static bool TryUnlock(string id, bool isHat) =>
            TryUnlock(id, isHat ? CosmeticSlot.Hat : CosmeticSlot.Pet);

        public static bool TryUnlock(string id, CosmeticSlot slot)
        {
            var list = GetList(slot);
            foreach (var c in list)
            {
                if (c.id != id) continue;
                if (MetaProgress.Ensure().HasCosmetic(id)) return false;
                if (IsSeasonal(id) && !IsFeaturedThisWeek(id)) return false;
                int cost = c.gemCost;
                if (IsFeaturedThisWeek(id)) cost = Mathf.Max(20, Mathf.RoundToInt(cost * 0.85f));
                if (cost > 0 && !MetaProgress.Ensure().SpendGems(cost)) return false;
                MetaProgress.Ensure().UnlockCosmetic(id);
                return true;
            }
            return false;
        }

        public static bool SelectHat(string id)
        {
            if (!MetaProgress.Ensure().HasCosmetic(id) && id != "hat_none") return false;
            SelectedHatId = id;
            return true;
        }

        public static bool SelectPet(string id)
        {
            if (!MetaProgress.Ensure().HasCosmetic(id) && id != "pet_none") return false;
            SelectedPetId = id;
            return true;
        }

        public static bool SelectCape(string id)
        {
            if (!MetaProgress.Ensure().HasCosmetic(id) && id != "cape_none") return false;
            SelectedCapeId = id;
            return true;
        }

        public static bool SelectScarf(string id)
        {
            if (!MetaProgress.Ensure().HasCosmetic(id) && id != "scarf_none") return false;
            SelectedScarfId = id;
            return true;
        }

        public static bool Select(string id, CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Hat => SelectHat(id),
            CosmeticSlot.Pet => SelectPet(id),
            CosmeticSlot.Cape => SelectCape(id),
            CosmeticSlot.Scarf => SelectScarf(id),
            _ => false
        };

        public static CosmeticDef GetHat(string id)
        {
            foreach (var h in Hats)
                if (h.id == id) return h;
            return Hats[0];
        }

        public static CosmeticDef GetPet(string id)
        {
            foreach (var p in Pets)
                if (p.id == id) return p;
            return Pets[0];
        }

        public static CosmeticDef GetCape(string id)
        {
            foreach (var c in Capes)
                if (c.id == id) return c;
            return Capes[0];
        }

        public static CosmeticDef GetScarf(string id)
        {
            foreach (var s in Scarves)
                if (s.id == id) return s;
            return Scarves[0];
        }

        public static CosmeticDef Get(string id, CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Hat => GetHat(id),
            CosmeticSlot.Pet => GetPet(id),
            CosmeticSlot.Cape => GetCape(id),
            CosmeticSlot.Scarf => GetScarf(id),
            _ => Hats[0]
        };

        public static bool HasEquippedHat() => SelectedHatId != "hat_none";
        public static bool HasEquippedCape => SelectedCapeId != "cape_none";
        public static bool HasEquippedScarf => SelectedScarfId != "scarf_none";

        /// <summary>Rebuilds lightweight cosmetic props on the runner visual (bone sockets when available).</summary>
        public static void ApplyToRunner(Transform runnerRoot, Animator animator = null)
        {
            if (runnerRoot == null) return;
            var existing = runnerRoot.Find("CosmeticRoot");
            if (existing != null)
                Object.Destroy(existing.gameObject);

            var root = new GameObject("CosmeticRoot").transform;
            root.SetParent(runnerRoot, false);

            Transform hatSocket = ResolveBone(animator, HumanBodyBones.Head) ?? root;
            Transform chestSocket = ResolveBone(animator, HumanBodyBones.UpperChest)
                                    ?? ResolveBone(animator, HumanBodyBones.Chest)
                                    ?? root;
            Transform neckSocket = ResolveBone(animator, HumanBodyBones.Neck) ?? chestSocket;

            var hat = GetHat(SelectedHatId);
            if (hat.id != "hat_none" && hat.color.a > 0.01f)
                BuildHat(hatSocket == root ? root : hatSocket, hat, hatSocket == root);

            var cape = GetCape(SelectedCapeId);
            if (cape.id != "cape_none" && cape.color.a > 0.01f)
                BuildCape(chestSocket == root ? root : chestSocket, cape, chestSocket == root);

            var scarf = GetScarf(SelectedScarfId);
            if (scarf.id != "scarf_none" && scarf.color.a > 0.01f)
                BuildScarf(neckSocket == root ? root : neckSocket, scarf, neckSocket == root);

            var pet = GetPet(SelectedPetId);
            if (pet.id != "pet_none" && pet.color.a > 0.01f)
                BuildPetBody(root, pet);
        }

        static Transform ResolveBone(Animator anim, HumanBodyBones bone)
        {
            if (anim == null || !anim.isHuman) return null;
            return anim.GetBoneTransform(bone);
        }

        static void BuildHat(Transform parent, CosmeticDef hat, bool explorerSpace)
        {
            Vector3 brimPos = explorerSpace ? new Vector3(0f, 1.72f, 0f) : new Vector3(0f, 0.18f, 0.02f);
            Vector3 crownPos = explorerSpace ? new Vector3(0f, 1.9f, 0f) : new Vector3(0f, 0.32f, 0.02f);
            float scale = explorerSpace ? 1f : 0.85f;

            var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brim.name = "Hat";
            brim.transform.SetParent(parent, false);
            brim.transform.localPosition = brimPos;
            brim.transform.localScale = new Vector3(0.55f, 0.12f, 0.55f) * scale;
            brim.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(hat.color, 0.35f);
            Object.Destroy(brim.GetComponent<Collider>());

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(parent, false);
            crown.transform.localPosition = crownPos;
            crown.transform.localScale = new Vector3(0.42f, 0.28f, 0.42f) * scale;
            crown.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(hat.color * 1.1f, 0.4f);
            Object.Destroy(crown.GetComponent<Collider>());
        }

        static void BuildCape(Transform parent, CosmeticDef cape, bool explorerSpace)
        {
            var go = new GameObject("CapeFollower");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = explorerSpace ? new Vector3(0f, 1.35f, -0.18f) : new Vector3(0f, 0.05f, -0.12f);
            go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var follower = go.AddComponent<ClothTailFollower>();
            follower.Configure(cape.id, 3, explorerSpace ? 0.55f : 0.42f);

            Color tip = Color.Lerp(cape.color, Color.black, 0.25f);
            for (int i = 0; i < 3; i++)
            {
                float t = i / 2f;
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "CapeSeg" + i;
                panel.transform.SetParent(go.transform, false);
                panel.transform.localPosition = new Vector3(0f, -0.18f - i * 0.28f, -0.04f * i);
                panel.transform.localScale = new Vector3(
                    Mathf.Lerp(0.72f, 0.85f, t),
                    0.32f,
                    0.06f);
                panel.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(Color.Lerp(cape.color, tip, t * 0.55f), 0.35f);
                Object.Destroy(panel.GetComponent<Collider>());
            }
        }

        static void BuildScarf(Transform parent, CosmeticDef scarf, bool explorerSpace)
        {
            var wrap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wrap.name = "ScarfWrap";
            wrap.transform.SetParent(parent, false);
            wrap.transform.localPosition = explorerSpace ? new Vector3(0f, 1.52f, 0.02f) : new Vector3(0f, 0.02f, 0.04f);
            wrap.transform.localScale = explorerSpace
                ? new Vector3(0.55f, 0.12f, 0.38f)
                : new Vector3(0.38f, 0.08f, 0.22f);
            wrap.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(scarf.color, 0.4f);
            Object.Destroy(wrap.GetComponent<Collider>());

            var go = new GameObject("ScarfTail");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = explorerSpace ? new Vector3(0.18f, 1.35f, -0.12f) : new Vector3(0.1f, -0.05f, -0.08f);
            var follower = go.AddComponent<ClothTailFollower>();
            follower.Configure(scarf.id, 2, explorerSpace ? 0.35f : 0.22f);

            for (int i = 0; i < 2; i++)
            {
                var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tail.name = "ScarfSeg" + i;
                tail.transform.SetParent(go.transform, false);
                tail.transform.localPosition = new Vector3(0.02f * i, -0.12f - i * 0.2f, -0.04f * i);
                tail.transform.localRotation = Quaternion.Euler(12f + i * 8f, 8f, 10f);
                tail.transform.localScale = new Vector3(0.12f, 0.28f, 0.07f);
                tail.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(Color.Lerp(scarf.color, Color.white, i * 0.15f), 0.4f);
                Object.Destroy(tail.GetComponent<Collider>());
            }
        }

        static void BuildPetBody(Transform root, CosmeticDef pet)
        {
            var followerGo = new GameObject("PetFollower");
            followerGo.transform.SetParent(root, false);
            followerGo.transform.localPosition = new Vector3(0.7f, 0.95f, -0.55f);
            var follower = followerGo.AddComponent<PetFollower>();
            follower.Configure(pet.id, pet.color);

            void Prim(PrimitiveType p, string name, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null)
            {
                var go = GameObject.CreatePrimitive(p);
                go.name = name;
                go.transform.SetParent(followerGo.transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
                go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(c, 0.4f);
                Object.Destroy(go.GetComponent<Collider>());
            }

            switch (pet.id)
            {
                case "pet_frostpup":
                    Prim(PrimitiveType.Sphere, "Body", new Vector3(0f, 0.05f, 0f), new Vector3(0.42f, 0.36f, 0.48f), pet.color);
                    Prim(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.22f, 0.22f), Vector3.one * 0.28f, pet.color * 1.05f);
                    Prim(PrimitiveType.Cube, "EarL", new Vector3(-0.12f, 0.38f, 0.18f), new Vector3(0.08f, 0.18f, 0.08f), pet.color);
                    Prim(PrimitiveType.Cube, "EarR", new Vector3(0.12f, 0.38f, 0.18f), new Vector3(0.08f, 0.18f, 0.08f), pet.color);
                    Prim(PrimitiveType.Sphere, "Tail", new Vector3(0f, 0.1f, -0.28f), Vector3.one * 0.16f, Color.Lerp(pet.color, Color.white, 0.25f));
                    break;
                case "pet_ashling":
                    Prim(PrimitiveType.Sphere, "Core", Vector3.zero, Vector3.one * 0.32f, pet.color);
                    Prim(PrimitiveType.Sphere, "Flame", new Vector3(0f, 0.22f, 0f), Vector3.one * 0.2f, new Color(1f, 0.7f, 0.25f));
                    Prim(PrimitiveType.Sphere, "Spark", new Vector3(0.18f, 0.1f, -0.1f), Vector3.one * 0.12f, pet.color * 0.9f);
                    break;
                case "pet_lotus_moth":
                    Prim(PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(0.22f, 0.18f, 0.3f), pet.color);
                    Prim(PrimitiveType.Cube, "WingL", new Vector3(-0.22f, 0.08f, 0f), new Vector3(0.28f, 0.04f, 0.35f),
                        Color.Lerp(pet.color, Color.white, 0.3f), new Vector3(0f, 0f, 25f));
                    Prim(PrimitiveType.Cube, "WingR", new Vector3(0.22f, 0.08f, 0f), new Vector3(0.28f, 0.04f, 0.35f),
                        Color.Lerp(pet.color, Color.white, 0.3f), new Vector3(0f, 0f, -25f));
                    break;
                case "pet_sand_skitter":
                    Prim(PrimitiveType.Capsule, "Body", Vector3.zero, new Vector3(0.22f, 0.16f, 0.4f), pet.color);
                    Prim(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.08f, 0.22f), Vector3.one * 0.2f, pet.color * 1.1f);
                    Prim(PrimitiveType.Cube, "LegL", new Vector3(-0.16f, -0.08f, 0.05f), new Vector3(0.06f, 0.16f, 0.06f), pet.color);
                    Prim(PrimitiveType.Cube, "LegR", new Vector3(0.16f, -0.08f, 0.05f), new Vector3(0.06f, 0.16f, 0.06f), pet.color);
                    break;
                case "pet_ice_wisp":
                    Prim(PrimitiveType.Sphere, "Core", Vector3.zero, Vector3.one * 0.3f, pet.color);
                    Prim(PrimitiveType.Sphere, "Halo", new Vector3(0f, 0.05f, 0f), Vector3.one * 0.42f,
                        Color.Lerp(pet.color, Color.white, 0.45f));
                    Prim(PrimitiveType.Cube, "Shard", new Vector3(0.12f, 0.22f, -0.05f), new Vector3(0.08f, 0.22f, 0.08f),
                        pet.color, new Vector3(0f, 0f, 20f));
                    break;
                default:
                    Prim(PrimitiveType.Sphere, "Body", Vector3.zero, Vector3.one * 0.34f, pet.color);
                    Prim(PrimitiveType.Sphere, "Glow", new Vector3(0f, 0.05f, 0f), Vector3.one * 0.22f,
                        Color.Lerp(pet.color, Color.white, 0.35f));
                    Prim(PrimitiveType.Cube, "WingL", new Vector3(-0.16f, 0.1f, 0f), new Vector3(0.16f, 0.03f, 0.2f),
                        pet.color * 0.9f, new Vector3(0f, 0f, 30f));
                    Prim(PrimitiveType.Cube, "WingR", new Vector3(0.16f, 0.1f, 0f), new Vector3(0.16f, 0.03f, 0.2f),
                        pet.color * 0.9f, new Vector3(0f, 0f, -30f));
                    break;
            }
        }
    }

    /// <summary>Keeps equipped pets bobbing/orbiting beside the runner so they stay camera-visible.</summary>
    public class PetFollower : MonoBehaviour
    {
        string _petId;
        Vector3 _home;
        float _phase;

        public void Configure(string petId, Color color)
        {
            _petId = petId ?? "pet_glowbug";
            _home = transform.localPosition;
            _phase = Random.value * Mathf.PI * 2f;
            _ = color;
        }

        void LateUpdate()
        {
            _phase += Time.deltaTime * (_petId is "pet_glowbug" or "pet_lotus_moth" or "pet_ice_wisp" ? 4.2f : 3.2f);
            float bob = Mathf.Sin(_phase) * 0.12f;
            float side = Mathf.Sin(_phase * 0.55f) * 0.1f;
            float back = -0.05f + Mathf.Abs(Mathf.Sin(_phase * 0.35f)) * 0.08f;
            float heightBias = _petId is "pet_frostpup" or "pet_sand_skitter" ? -0.15f : 0.08f;
            transform.localPosition = _home + new Vector3(side, bob + heightBias, back);

            float flap = Mathf.Sin(_phase * 2.2f) * 18f;
            transform.localRotation = Quaternion.Euler(
                Mathf.Sin(_phase) * 8f,
                12f + Mathf.Sin(_phase * 0.4f) * 10f,
                flap * (_petId.Contains("moth") || _petId.Contains("glowbug") ? 1f : 0.25f));
        }
    }

    /// <summary>Lightweight cloth sway for cape/scarf segments without Unity Cloth.</summary>
    public class ClothTailFollower : MonoBehaviour
    {
        float _phase;
        float _amp;
        Quaternion _baseRot;

        public void Configure(string id, int segments, float amplitude)
        {
            _ = id;
            _ = segments;
            _amp = Mathf.Clamp(amplitude, 0.1f, 1f);
            _phase = Random.value * Mathf.PI * 2f;
            _baseRot = transform.localRotation;
        }

        void LateUpdate()
        {
            float speed = PlayerController.Instance != null && RunSession.Instance != null && RunSession.Instance.IsAlive
                ? 9f : 3.2f;
            _phase += Time.deltaTime * speed;
            float pitch = Mathf.Sin(_phase) * (6f + _amp * 10f);
            float yaw = Mathf.Sin(_phase * 0.7f) * (4f + _amp * 6f);
            float roll = Mathf.Cos(_phase * 1.1f) * (3f + _amp * 4f);
            transform.localRotation = _baseRot * Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
