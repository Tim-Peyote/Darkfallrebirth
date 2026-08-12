using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    /// <summary>
    /// Loads the authored architecture modules as independent sprites. The source artwork stays
    /// split by biome and role so a single wall, corner or feature can be replaced without
    /// rebuilding an atlas.
    /// </summary>
    public static class ArchitectureSpriteLibrary
    {
        public readonly struct ArchitectureSocketContract
        {
            public readonly Rect Ink;
            public readonly float Baseline;
            public readonly float LeftSocket;
            public readonly float RightSocket;

            public ArchitectureSocketContract(Rect ink, float baseline, float leftSocket, float rightSocket)
            {
                Ink = ink;
                Baseline = baseline;
                LeftSocket = leftSocket;
                RightSocket = rightSocket;
            }
        }
        private const string Root = "Sprites/Environment/Architecture/";
        private const float PixelsPerUnit = 230f;
        private const float ReferenceCanvas = 362f;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Rect> WallInk = new Dictionary<string, Rect>
        {
            { "ashen-catacombs/wall-left", new Rect(61, 33, 249, 296) },
            { "ashen-catacombs/wall-right", new Rect(45, 31, 248, 295) },
            { "charnel-gardens/wall-left", new Rect(56, 25, 257, 289) },
            { "charnel-gardens/wall-right", new Rect(56, 35, 257, 295) },
            { "drowned-crypt/wall-left", new Rect(83, 21, 281, 295) },
            { "drowned-crypt/wall-right", new Rect(84, 24, 254, 292) },
            { "ember-vaults/wall-left", new Rect(89, 19, 227, 272) },
            { "ember-vaults/wall-right", new Rect(64, 19, 230, 273) },
            { "obsidian-sanctum/wall-left", new Rect(99, 0, 263, 315) },
            { "obsidian-sanctum/wall-right", new Rect(71, 0, 263, 315) }
        };

        // Socket positions are measured on the authored 362 px canvas. Every value is an actual
        // connection plane, not a decorative overlap allowance. A validator can therefore reject
        // an asset before it reaches a generated dungeon.
        private static readonly Dictionary<string, ArchitectureSocketContract> SocketContracts =
            new Dictionary<string, ArchitectureSocketContract>
            {
                { "ashen-catacombs/wall-left", new ArchitectureSocketContract(new Rect(61, 33, 249, 296), 33, 61, 310) },
                { "ashen-catacombs/wall-right", new ArchitectureSocketContract(new Rect(45, 36, 248, 295), 36, 45, 293) },
                { "ashen-catacombs/corner-inner", new ArchitectureSocketContract(new Rect(0, 50, 294, 267), 50, 0, 294) },
                { "ashen-catacombs/corner-outer", new ArchitectureSocketContract(new Rect(18, 58, 262, 267), 58, 18, 280) }
            };

        public static bool TryGetSocketContract(string biome, string role, out ArchitectureSocketContract contract) =>
            SocketContracts.TryGetValue(biome + "/" + role, out contract);

        public static bool ValidateSocketContract(string biome, string role, out string error)
        {
            error = null;
            var sprite = Module(biome, role);
            if (sprite == null || sprite.texture == null)
            {
                error = $"missing sprite {biome}/{role}";
                return false;
            }
            if (!TryGetSocketContract(biome, role, out var contract)) return true;
            var textureBounds = new Rect(0f, 0f, sprite.texture.width, sprite.texture.height);
            if (!textureBounds.Contains(contract.Ink.min) || !textureBounds.Contains(contract.Ink.max))
            {
                error = $"ink rectangle escapes canvas for {biome}/{role}";
                return false;
            }
            if (contract.LeftSocket >= contract.RightSocket || contract.Baseline < contract.Ink.yMin - .01f ||
                contract.Baseline > contract.Ink.yMax + .01f)
            {
                error = $"invalid socket order or baseline for {biome}/{role}";
                return false;
            }
            return true;
        }

        public static bool HasBiome(string biome) => Module(biome, "wall-left") != null;

        public static string WallRoleForAxis(string biome, bool vertical)
        {
            // Logical axes are shared by every biome. Drowned source art was exported with its
            // left/right filenames interchanged, so normalize that authoring difference here.
            if (biome == "drowned-crypt") return vertical ? "wall-left" : "wall-right";
            return vertical ? "wall-right" : "wall-left";
        }

        /// <summary>Normalizes how independently-authored right-wall files face the world axis.</summary>
        public static bool FlipForAxis(string biome, string role, bool vertical)
        {
            if (!vertical) return false;
            if (role != "wall-right") return false;
            // Only the original catacomb right wall was exported with reversed handedness.
            // Drowned and charnel already contain a complementary right-facing sprite; mirroring
            // either of them turns a continuous wall run into the transverse "picket fence".
            return biome == "ashen-catacombs";
        }

        /// <summary>
        /// Every biome follows the same logical module grammar. Source files may use a different
        /// canvas size, so compensate for that canvas here instead of leaking biome-specific
        /// spacing constants into the dungeon builder.
        /// </summary>
        public static void Placement(string biome, string role, Sprite sprite, out Vector2 scale,
            out Vector2 offset)
        {
            scale = Vector2.one;
            offset = Vector2.zero;
            if (sprite == null || sprite.texture == null) return;

            var key = biome + "/" + role;
            if (TryGetSocketContract(biome, role, out var socket) && role.StartsWith("corner-"))
            {
                // Corner modules occupy one topology junction. Align the centre of their measured
                // socket span with the logical anchor and seat the measured plinth on the same
                // baseline as the straight wall kit.
                scale = Vector2.one;
                var pivot = new Vector2(sprite.texture.width * .5f, sprite.texture.height * .08f);
                var socketCentre = (socket.LeftSocket + socket.RightSocket) * .5f;
                offset = new Vector2(-(socketCentre - pivot.x) / PixelsPerUnit,
                    -(socket.Baseline - pivot.y) / PixelsPerUnit);
                return;
            }
            var referenceKey = "ashen-catacombs/" + role;
            if (!WallInk.TryGetValue(key, out var ink) || !WallInk.TryGetValue(referenceKey, out var reference))
            {
                scale = new Vector2(ReferenceCanvas / sprite.texture.width,
                    ReferenceCanvas / sprite.texture.height);
                return;
            }

            scale = new Vector2(reference.width / ink.width, reference.height / ink.height);
            // The logical anchor is the first opaque pixel of the plinth. Anchoring to an
            // arbitrary percentage of the source canvas leaves a transparent strip under the
            // module and makes the whole wall read as suspended above the floor.
            var referencePivot = new Vector2(ReferenceCanvas * .5f, reference.yMin);
            var sourcePivot = new Vector2(sprite.texture.width * .5f, sprite.texture.height * .08f);
            var desired = new Vector2(reference.center.x - referencePivot.x,
                reference.yMin - referencePivot.y) / PixelsPerUnit;
            var actual = new Vector2(ink.center.x - sourcePivot.x,
                ink.yMin - sourcePivot.y) / PixelsPerUnit;
            offset = desired - Vector2.Scale(actual, scale);
        }

        public static Sprite Module(string biome, string role)
        {
            var key = biome + "/" + role;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            // The organic charnel pair is the canonical kit. The experimental 02 pair has a
            // different screen-axis contract and is intentionally kept as replaceable source art,
            // not selected by the shared dungeon grammar.
            var version = "-01";
            var texture = Resources.Load<Texture2D>(Root + key + version);
            if (texture == null)
            {
                Cache[key] = null;
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .08f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = biome + " · " + role;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
