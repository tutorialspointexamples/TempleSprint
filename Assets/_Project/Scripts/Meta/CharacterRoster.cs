using UnityEngine;

namespace TempleSprint
{
    public static class CharacterRoster
    {
        public struct CharacterDef
        {
            public string id;
            public string displayName;
            public string passive;
            public int gemCost;
            public Color color;
        }

        public static readonly CharacterDef[] All =
        {
            new CharacterDef { id = "scout_default", displayName = "Guy Explorer", passive = "None", gemCost = 0, color = new Color(0.92f, 0.82f, 0.45f) },
            new CharacterDef { id = "desert_runner", displayName = "Desert Runner", passive = "+5% coins in Desert", gemCost = 80, color = new Color(0.85f, 0.55f, 0.28f) },
            new CharacterDef { id = "ice_wraith", displayName = "Ice Wraith", passive = "Slight magnet bonus", gemCost = 120, color = new Color(0.55f, 0.75f, 0.9f) },
            new CharacterDef { id = "jungle_ace", displayName = "Jungle Ace", passive = "Tutorial clear bonus", gemCost = 60, color = new Color(0.4f, 0.55f, 0.35f) }
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
                return 1f;
            }
        }

        public static float PassiveMagnetBonus => GetSelected().id == "ice_wraith" ? 0.5f : 0f;
    }
}
