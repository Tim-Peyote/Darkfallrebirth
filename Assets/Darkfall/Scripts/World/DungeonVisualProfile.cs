using UnityEngine;

namespace Darkfall.World
{
    /// <summary>Data-only visual recipe. New biomes can be added without changing DungeonView.</summary>
    public sealed class DungeonVisualProfile
    {
        public string Id { get; private set; }
        public string FloorTexture { get; private set; }
        public string WallTexture { get; private set; }
        public Color FloorTint { get; private set; }
        public Color WallTint { get; private set; }
        public Color ContactShadow { get; private set; }
        public Color FireTint { get; private set; }
        public int[] ClutterProps { get; private set; }
        public int[] StructuralProps { get; private set; }
        public int LightEveryRooms { get; private set; }

        private DungeonVisualProfile() { }

        public static DungeonVisualProfile ForDepth(int depth)
        {
            var chapter = Mathf.Max(0, (depth - 1) / 10);
            switch (chapter % 3)
            {
                case 1:
                    return Create("ember-vaults", new Color(.39f, .32f, .27f), new Color(.28f, .21f, .18f),
                        new Color(.10f, .042f, .022f, .42f), new Color(1f, .26f, .035f, .8f),
                        new[] { 1, 5, 6, 9 }, new[] { 8, 10, 4 }, 2);
                case 2:
                    return Create("drowned-crypt", new Color(.30f, .35f, .36f), new Color(.20f, .25f, .27f),
                        new Color(.018f, .06f, .075f, .46f), new Color(.30f, .72f, .88f, .65f),
                        new[] { 0, 3, 7, 11 }, new[] { 4, 8, 10 }, 3);
                default:
                    return Create("ashen-catacombs", new Color(.39f, .37f, .34f), new Color(.25f, .245f, .235f),
                        new Color(.035f, .025f, .018f, .38f), new Color(1f, .34f, .055f, .72f),
                        new[] { 4, 5, 6, 7 }, new[] { 0, 1, 8, 10 }, 2);
            }
        }

        private static DungeonVisualProfile Create(string id, Color floor, Color wall, Color shadow, Color fire,
            int[] clutter, int[] structural, int lightEvery)
        {
            return new DungeonVisualProfile
            {
                Id = id,
                FloorTexture = "Textures/dungeon-floor-v2",
                WallTexture = "Textures/dungeon-wall-v2",
                FloorTint = floor,
                WallTint = wall,
                ContactShadow = shadow,
                FireTint = fire,
                ClutterProps = clutter,
                StructuralProps = structural,
                LightEveryRooms = lightEvery
            };
        }
    }
}
