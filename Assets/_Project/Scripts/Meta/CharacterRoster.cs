using UnityEngine;

namespace TempleSprint
{
    public enum CharacterActiveSkill
    {
        None = 0,
        SandDash = 1,
        FrostMagnetBurst = 2,
        CanopyVault = 3,
        MinerHeadlamp = 4,
        EmberShieldPulse = 5
    }

    public static class CharacterRoster
    {
        public struct CharacterDef
        {
            public string id;
            public string displayName;
            public string passive;
            public string activeDescription;
            public CharacterActiveSkill activeSkill;
            public int gemCost;
            public Color color;
        }

        public static readonly CharacterDef[] All =
        {
            new CharacterDef
            {
                id = "scout_default", displayName = "Scout Reed", passive = "None",
                activeDescription = "None", activeSkill = CharacterActiveSkill.None,
                gemCost = 0, color = new Color(0.92f, 0.82f, 0.45f)
            },
            new CharacterDef
            {
                id = "desert_runner", displayName = "Sand Strider", passive = "+5% coins in Desert",
                activeDescription = "Sand Dash — short boost burst",
                activeSkill = CharacterActiveSkill.SandDash,
                gemCost = 80, color = new Color(0.85f, 0.55f, 0.28f)
            },
            new CharacterDef
            {
                id = "ice_wraith", displayName = "Frost Courier", passive = "Slight magnet bonus",
                activeDescription = "Frost Magnet — wide coin pull",
                activeSkill = CharacterActiveSkill.FrostMagnetBurst,
                gemCost = 120, color = new Color(0.55f, 0.75f, 0.9f)
            },
            new CharacterDef
            {
                id = "jungle_ace", displayName = "Canopy Ace", passive = "Tutorial clear bonus",
                activeDescription = "Canopy Vault — free air hop",
                activeSkill = CharacterActiveSkill.CanopyVault,
                gemCost = 60, color = new Color(0.4f, 0.55f, 0.35f)
            },
            new CharacterDef
            {
                id = "cave_miner", displayName = "Tunnel Runner", passive = "+8% coins in Cave Mines",
                activeDescription = "Headlamp — clear darkness fog",
                activeSkill = CharacterActiveSkill.MinerHeadlamp,
                gemCost = 140, color = new Color(0.65f, 0.55f, 0.4f)
            },
            new CharacterDef
            {
                id = "ember_scout", displayName = "Ember Scout", passive = "Brief shield pulse in Volcano",
                activeDescription = "Ember Pulse — grant a shield",
                activeSkill = CharacterActiveSkill.EmberShieldPulse,
                gemCost = 160, color = new Color(0.95f, 0.45f, 0.2f)
            }
        };

        public static string SelectedId
        {
            get => MetaProgress.Ensure().Data.selectedCharacter ?? "scout_default";
            set
            {
                MetaProgress.Ensure().Data.selectedCharacter = value;
                MetaProgress.Ensure().Save();
            }
        }

        public static CharacterDef GetSelected()
        {
            foreach (var c in All)
                if (c.id == SelectedId) return c;
            return All[0];
        }

        public static string SelectedDisplayName => GetSelected().displayName;
        public static CharacterActiveSkill ActiveSkill => GetSelected().activeSkill;
        public static string ActiveSkillLabel
        {
            get
            {
                var c = GetSelected();
                return c.activeSkill == CharacterActiveSkill.None ? "" : c.activeDescription;
            }
        }

        public static bool TryUnlock(string id)
        {
            foreach (var c in All)
            {
                if (c.id != id) continue;
                if (MetaProgress.Ensure().GetUnlockedCharacters().Contains(id)) return false;
                if (c.gemCost > 0 && !MetaProgress.Ensure().SpendGems(c.gemCost)) return false;
                MetaProgress.Ensure().UnlockCharacter(id);
                return true;
            }
            return false;
        }

        public static bool Select(string id)
        {
            if (!MetaProgress.Ensure().GetUnlockedCharacters().Contains(id)) return false;
            SelectedId = id;
            return true;
        }

        public static float PassiveCoinMult
        {
            get
            {
                var c = GetSelected();
                if (c.id == "desert_runner" && BiomeSystem.Current == BiomeId.DesertTombs) return 1.05f;
                if (c.id == "cave_miner" && BiomeSystem.Current == BiomeId.CaveMines) return 1.08f;
                return 1f;
            }
        }

        public static float PassiveMagnetBonus => GetSelected().id == "ice_wraith" ? 0.5f : 0f;

        public static bool EmberStartShield =>
            GetSelected().id == "ember_scout" && BiomeSystem.Current == BiomeId.VolcanicCrater;
    }
}
