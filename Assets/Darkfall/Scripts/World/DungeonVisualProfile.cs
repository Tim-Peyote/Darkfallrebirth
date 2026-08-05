using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Data-only visual recipe. New biomes can be added without changing DungeonView.</summary>
    public sealed class DungeonVisualProfile
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public int Chapter { get; private set; }
        public string FloorTexture { get; private set; }
        public string WallTexture { get; private set; }
        public Color FloorTint { get; private set; }
        public Color WallTint { get; private set; }
        public Color ContactShadow { get; private set; }
        public Color FireTint { get; private set; }
        public Color DecorTint { get; private set; }
        public int[] ClutterProps { get; private set; }
        public int[] StructuralProps { get; private set; }
        public int LightEveryRooms { get; private set; }
        public float DecorDensity { get; private set; }

        private DungeonVisualProfile() { }

        public static DungeonVisualProfile ForDepth(int depth)
        {
            var chapter = Mathf.Max(0, (depth - 1) / 10);
            switch (chapter % 5)
            {
                case 1:
                    return Create(chapter, "ember-vaults", "ОБГОРЕВШИЕ ХРАНИЛИЩА",
                        "Textures/Biomes/ember-floor", "Textures/Biomes/ember-wall",
                        new Color(.52f, .43f, .36f), new Color(.39f, .31f, .27f),
                        new Color(.10f, .042f, .022f, .42f), new Color(1f, .26f, .035f, .8f),
                        new Color(1f, .78f, .62f), new[] { 0, 2, 3, 5, 8, 11 }, new[] { 1, 4, 6, 7, 9, 10 }, 2, 1.12f);
                case 2:
                    return Create(chapter, "drowned-crypt", "ЗАТОПЛЕННАЯ КРИПТА",
                        "Textures/Biomes/drowned-floor", "Textures/Biomes/drowned-wall",
                        new Color(.42f, .49f, .50f), new Color(.31f, .39f, .42f),
                        new Color(.018f, .06f, .075f, .46f), new Color(.30f, .72f, .88f, .65f),
                        new Color(.67f, .82f, .80f), new[] { 0, 2, 3, 5, 8, 11 }, new[] { 1, 4, 6, 7, 9, 10 }, 3, 1.2f);
                case 3:
                    return Create(chapter, "charnel-gardens", "ТЛЕННЫЕ САДЫ",
                        "Textures/Biomes/charnel-floor", "Textures/Biomes/charnel-wall",
                        new Color(.34f, .43f, .29f), new Color(.25f, .31f, .22f),
                        new Color(.025f, .065f, .018f, .48f), new Color(.54f, .92f, .22f, .62f),
                        new Color(.68f, .80f, .55f), new[] { 0, 2, 3, 5, 8, 11 }, new[] { 1, 4, 6, 7, 9, 10 }, 3, 1.38f);
                case 4:
                    return Create(chapter, "obsidian-sanctum", "ОБСИДИАНОВЫЙ САНКТУМ",
                        "Textures/Biomes/obsidian-floor", "Textures/Biomes/obsidian-wall",
                        new Color(.34f, .30f, .40f), new Color(.23f, .20f, .29f),
                        new Color(.055f, .022f, .075f, .52f), new Color(.72f, .28f, 1f, .7f),
                        new Color(.76f, .65f, .88f), new[] { 0, 2, 3, 5, 8, 11 }, new[] { 1, 4, 6, 7, 9, 10 }, 2, 1.22f);
                default:
                    return Create(chapter, "ashen-catacombs", "ПЕПЕЛЬНЫЕ КАТАКОМБЫ",
                        "Textures/dungeon-floor-v2", "Textures/dungeon-wall-v2",
                        new Color(.39f, .37f, .34f), new Color(.25f, .245f, .235f),
                        new Color(.035f, .025f, .018f, .38f), new Color(1f, .34f, .055f, .72f),
                        Color.white, new[] { 2, 3, 5, 7, 8, 11 }, new[] { 0, 1, 4, 6, 9, 10 }, 2, 1f);
            }
        }

        private static DungeonVisualProfile Create(int chapter, string id, string displayName,
            string floorTexture, string wallTexture, Color floor, Color wall, Color shadow, Color fire,
            Color decorTint, int[] clutter, int[] structural, int lightEvery, float decorDensity)
        {
            return new DungeonVisualProfile
            {
                Id = id,
                DisplayName = displayName,
                Chapter = chapter,
                FloorTexture = floorTexture,
                WallTexture = wallTexture,
                FloorTint = floor,
                WallTint = wall,
                ContactShadow = shadow,
                FireTint = fire,
                DecorTint = decorTint,
                ClutterProps = clutter,
                StructuralProps = structural,
                LightEveryRooms = lightEvery,
                DecorDensity = decorDensity
            };
        }
    }
}
