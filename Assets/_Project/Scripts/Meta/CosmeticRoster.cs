using UnityEngine;

namespace TempleSprint
{
    /// <summary>Original hats & pets for locker cosmetics (genre parity, not Imangi IP).</summary>
    public static class CosmeticRoster
    {
        public struct CosmeticDef
        {
            public string id;
            public string displayName;
            public bool isHat;
            public int gemCost;
            public Color color;
        }

        public static readonly CosmeticDef[] Hats =
        {
            new CosmeticDef { id = "hat_none", displayName = "No Hat", isHat = true, gemCost = 0, color = Color.clear },
            new CosmeticDef { id = "hat_reed_cap", displayName = "Reed Cap", isHat = true, gemCost = 40, color = new Color(0.55f, 0.35f, 0.18f) },
            new CosmeticDef { id = "hat_canopy", displayName = "Canopy Helm", isHat = true, gemCost = 90, color = new Color(0.2f, 0.55f, 0.28f) },
            new CosmeticDef { id = "hat_ember", displayName = "Ember Band", isHat = true, gemCost = 120, color = new Color(0.95f, 0.4f, 0.15f) }
        };

        public static readonly CosmeticDef[] Pets =
        {
            new CosmeticDef { id = "pet_none", displayName = "No Pet", isHat = false, gemCost = 0, color = Color.clear },
            new CosmeticDef { id = "pet_glowbug", displayName = "Glowbug", isHat = false, gemCost = 60, color = new Color(0.85f, 1f, 0.35f) },
            new CosmeticDef { id = "pet_frostpup", displayName = "Frost Pup", isHat = false, gemCost = 110, color = new Color(0.65f, 0.85f, 1f) },
            new CosmeticDef { id = "pet_ashling", displayName = "Ashling", isHat = false, gemCost = 140, color = new Color(1f, 0.45f, 0.2f) }
        };

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

        public static bool TryUnlock(string id, bool isHat)
        {
            var list = isHat ? Hats : Pets;
            foreach (var c in list)
            {
                if (c.id != id) continue;
                if (MetaProgress.Ensure().HasCosmetic(id)) return false;
                if (c.gemCost > 0 && !MetaProgress.Ensure().SpendGems(c.gemCost)) return false;
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

        /// <summary>Rebuilds lightweight hat/pet props on the runner visual.</summary>
        public static void ApplyToRunner(Transform runnerRoot)
        {
            if (runnerRoot == null) return;
            var existing = runnerRoot.Find("CosmeticRoot");
            if (existing != null)
                Object.Destroy(existing.gameObject);

            var root = new GameObject("CosmeticRoot").transform;
            root.SetParent(runnerRoot, false);

            var hat = GetHat(SelectedHatId);
            if (hat.id != "hat_none" && hat.color.a > 0.01f)
            {
                var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                brim.name = "Hat";
                brim.transform.SetParent(root, false);
                brim.transform.localPosition = new Vector3(0f, 1.72f, 0f);
                brim.transform.localScale = new Vector3(0.55f, 0.12f, 0.55f);
                brim.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(hat.color, 0.35f);
                Object.Destroy(brim.GetComponent<Collider>());

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.transform.SetParent(root, false);
                crown.transform.localPosition = new Vector3(0f, 1.9f, 0f);
                crown.transform.localScale = new Vector3(0.42f, 0.28f, 0.42f);
                crown.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(hat.color * 1.1f, 0.4f);
                Object.Destroy(crown.GetComponent<Collider>());
            }

            var pet = GetPet(SelectedPetId);
            if (pet.id != "pet_none" && pet.color.a > 0.01f)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Pet";
                body.transform.SetParent(root, false);
                body.transform.localPosition = new Vector3(0.55f, 0.85f, -0.35f);
                body.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                body.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(pet.color, 0.45f);
                Object.Destroy(body.GetComponent<Collider>());

                var trail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                trail.transform.SetParent(root, false);
                trail.transform.localPosition = new Vector3(0.7f, 0.7f, -0.55f);
                trail.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
                trail.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(pet.color * 0.85f, 0.5f);
                Object.Destroy(trail.GetComponent<Collider>());
            }
        }
    }
}
