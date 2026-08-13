using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.World
{
    public sealed class DungeonView : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();
        private DungeonVisualProfile profile;
        private Transform architectureDecor;
        private Transform structuralDecor;
        private Transform lightDecor;
        private Transform clutterDecor;
        private readonly Dictionary<string, Sprite> hazardSprites = new Dictionary<string, Sprite>();
        private readonly List<DecorFootprint> decorFootprints = new List<DecorFootprint>();

        private readonly struct DecorFootprint
        {
            public readonly Vector2 Position;
            public readonly float Radius;

            public DecorFootprint(Vector2 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }

        public void Build(DungeonData data, int depth = 1)
        {
            Clear();
            decorFootprints.Clear();
            profile = DungeonVisualProfile.ForDepth(depth);
            gameObject.name = "Dungeon · " + profile.Id;
            DungeonFloorTileResolver.Resolve(data, data.GenerationInfo != null ? data.GenerationInfo.seed : depth);
            DungeonWallTileResolver.Resolve(data, data.GenerationInfo != null ? data.GenerationInfo.seed : depth);
            var contour = DungeonContour.Build(data);
            architectureDecor = CreateGroup(transform, "Architecture · " + profile.Id);
            BuildContourFloor(contour, data);
            BuildContextFloorTiles(data);
            BuildWallFoundationApron(contour, data);
            BuildHazardSurfaces(data);
            BuildContourWalls(contour, data);
            BuildContourShadows(contour);
            var decorRoot = new GameObject("Decor · " + profile.Id).transform;
            decorRoot.SetParent(transform, false);
            structuralDecor = CreateGroup(decorRoot, "Structural");
            lightDecor = CreateGroup(decorRoot, "Light Sources");
            clutterDecor = CreateGroup(decorRoot, "Clutter");
            ReserveRuntimeScenarioFootprints(data);
            BuildSetPieces(data);
            BuildMiniSets(data);
            BuildDecor(data);
            BuildBiomeEvents(data);
            data.CompleteGenerationStage(DungeonGenerationStage.TileResolution);
        }

        private void ReserveRuntimeScenarioFootprints(DungeonData data)
        {
            // Runtime scenarios are spawned after DungeonView.Build. Reserve their composition
            // here so generic room themes and biome events cannot occupy the same floor space.
            // The missing reservation was why a biome landmark could grow through the healing
            // shrine even though both placements were individually valid.
            foreach (var setPiece in data.SetPieces)
            {
                if (setPiece.Kind == DungeonSetPieceKind.Shrine)
                    RegisterDecor(setPiece.Anchor, 1.65f);
                else if (setPiece.Kind == DungeonSetPieceKind.EventRoom)
                    RegisterDecor(setPiece.Anchor, 1.35f);
                else if (setPiece.Kind == DungeonSetPieceKind.EliteArena)
                    RegisterDecor(setPiece.Anchor, 1.25f);
            }
        }

        private void BuildMiniSets(DungeonData data)
        {
            foreach (var miniSet in data.MiniSets)
            {
                if (miniSet.Kind == DungeonMiniSetKind.HazardBridge) continue;
                if (profile.Id == "ashen-catacombs" && CreateAuthoredAshenMiniSet(data, miniSet)) continue;
                var index = miniSet.Kind == DungeonMiniSetKind.StatueNiche ? 6 :
                    miniSet.Kind == DungeonMiniSetKind.RuinedCorner ? 7 :
                    miniSet.Kind == DungeonMiniSetKind.Colonnade ? 6 :
                    miniSet.Kind == DungeonMiniSetKind.RubbleBlock ? 7 :
                    miniSet.Kind == DungeonMiniSetKind.Campfire ? 2 :
                    miniSet.Kind == DungeonMiniSetKind.Altar ? 8 :
                    miniSet.Kind == DungeonMiniSetKind.SideChapel ? 1 : 5;
                if (miniSet.Kind == DungeonMiniSetKind.Colonnade)
                {
                    CreateProp(data, index, miniSet.Anchor + Vector2.left * 1.35f, .68f,
                        "Mini Set · Colonnade", true, structuralDecor);
                    CreateProp(data, index, miniSet.Anchor + Vector2.right * 1.35f, .68f,
                        "Mini Set · Colonnade", true, structuralDecor);
                }
                else CreateProp(data, index, miniSet.Anchor, miniSet.Mask.width >= 5 ? .78f : .62f,
                    "Mini Set · " + miniSet.Kind, miniSet.Kind != DungeonMiniSetKind.Campfire, structuralDecor);
            }
        }

        private bool CreateAuthoredAshenMiniSet(DungeonData data, DungeonMiniSet miniSet)
        {
            var sprite = MiniSetSpriteLibrary.Get(miniSet.Kind);
            if (sprite == null) return false;
            // A colonnade reserves a composed visual mask, but its arch remains traversable.
            // Treating the anchor as a solid obstacle made the art promise a passage while
            // navigation rejected it.
            var blocks = miniSet.Kind != DungeonMiniSetKind.Campfire &&
                         miniSet.Kind != DungeonMiniSetKind.Colonnade &&
                         miniSet.Kind != DungeonMiniSetKind.StatueNiche;
            var footprintRadius = miniSet.Kind == DungeonMiniSetKind.Colonnade ? 1.15f :
                miniSet.Kind == DungeonMiniSetKind.SideChapel ? 1.05f :
                miniSet.Kind == DungeonMiniSetKind.Campfire ? .48f : .72f;
            if (blocks && !data.TryAddObstaclePreservingRoutes(miniSet.Anchor)) return false;

            var root = new GameObject("Mini Set · Authored " + miniSet.Kind);
            root.transform.SetParent(miniSet.Kind == DungeonMiniSetKind.Campfire ? lightDecor : structuralDecor, false);
            root.transform.position = miniSet.Anchor;
            // A niche is wall-mounted art: move its logical anchor onto the north wall line
            // instead of presenting it as a freestanding monument in the room.
            if (miniSet.Kind == DungeonMiniSetKind.StatueNiche &&
                miniSet.RoomIndex >= 0 && miniSet.RoomIndex < data.Rooms.Count)
            {
                var room = data.Rooms[miniSet.RoomIndex];
                if (!TryResolveNicheWall(data, room.bounds, miniSet.Anchor, out var wallPosition))
                {
                    Destroy(root);
                    return false;
                }
                root.transform.position = wallPosition;
            }
            else if (miniSet.Kind == DungeonMiniSetKind.SideChapel &&
                     miniSet.RoomIndex >= 0 && miniSet.RoomIndex < data.Rooms.Count)
            {
                var room = data.Rooms[miniSet.RoomIndex];
                root.transform.position = new Vector2(miniSet.Anchor.x, room.bounds.yMin + .35f);
            }
            var visual = new GameObject("Projected " + miniSet.Kind);
            visual.transform.SetParent(root.transform, false);
            var scale = miniSet.Mask.width >= 5 ? .78f : .62f;
            if (miniSet.Kind == DungeonMiniSetKind.StatueNiche) scale = .34f;
            else if (miniSet.Kind == DungeonMiniSetKind.Altar) scale = .38f;
            else if (miniSet.Kind == DungeonMiniSetKind.Campfire) scale = .38f;
            visual.transform.localScale = Vector3.one * scale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            // Campfires and room braziers must share one canonical construction. The previous
            // mini-set swapped four complete bowl images, so the metal body changed silhouette
            // every frame while a second, newer brazier existed elsewhere in the same dungeon.
            // Keep one static body and animate only its child flame.
            renderer.sprite = miniSet.Kind == DungeonMiniSetKind.Campfire
                ? EnvironmentSpriteAtlas.Prop(profile.Id, 2)
                : sprite;
            renderer.color = Color.white;
            DarkfallRenderMaterials.MakeLit(renderer);
            visual.AddComponent<IsoVisual>().Initialize(root.transform, 0f,
                miniSet.Kind == DungeonMiniSetKind.StatueNiche ? 1120 : 1004);

            if (blocks)
            {
                var caster = visual.AddComponent<ShadowCaster2D>();
                caster.castsShadows = true;
                caster.selfShadows = false;
                caster.alphaCutoff = .18f;
            }
            if (miniSet.Kind == DungeonMiniSetKind.Campfire)
            {
                visual.transform.localScale = Vector3.one * .58f;
                AddFlame(visual.transform, new Vector2(0, .24f), .56f, 9);
                data.AddLightSource(miniSet.Anchor + new Vector2(0, .18f), profile.FireTint, 4.8f, .14f);
            }
            RegisterDecor(root.transform.position, footprintRadius);
            return true;
        }

        private static bool TryResolveNicheWall(DungeonData data, RectInt bounds, Vector2 preferred,
            out Vector2 position)
        {
            // The authored niche faces the camera and can only sit on one of the two horizontal
            // isometric wall planes. A room bound is not itself proof of a wall: an adjoining
            // corridor may be carved immediately outside it. Require walkable floor on the room
            // side and empty space on the far side for the complete three-cell frontage.
            var candidates = new[]
            {
                new Vector2(Mathf.Clamp(preferred.x, bounds.xMin + 1.5f, bounds.xMax - 1.5f), bounds.yMax),
                new Vector2(Mathf.Clamp(preferred.x, bounds.xMin + 1.5f, bounds.xMax - 1.5f), bounds.yMin)
            };
            foreach (var candidate in candidates)
            {
                var upper = Mathf.Approximately(candidate.y, bounds.yMax);
                var insideY = upper ? bounds.yMax - 1 : bounds.yMin;
                var outsideY = upper ? bounds.yMax : bounds.yMin - 1;
                var centerX = Mathf.FloorToInt(candidate.x);
                var valid = true;
                for (var dx = -1; dx <= 1; dx++)
                    valid &= data.IsFloor(centerX + dx, insideY) && !data.IsFloor(centerX + dx, outsideY);
                if (!valid) continue;
                position = new Vector2(centerX + .5f, candidate.y + (upper ? -.12f : .12f));
                return true;
            }
            position = default;
            return false;
        }

        private void BuildSetPieces(DungeonData data)
        {
            foreach (var setPiece in data.SetPieces)
            {
                var label = "Set Piece · " + setPiece.Kind;
                switch (setPiece.Kind)
                {
                    case DungeonSetPieceKind.Entrance:
                        // Quiet, symmetric vigil around the landing. The centre and the line to
                        // the safety door remain empty, so these marks orient rather than block.
                        CreateProp(data, 2, setPiece.Anchor + new Vector2(-1.05f, .82f), .48f,
                            label + " Vigil", false, lightDecor);
                        CreateProp(data, 2, setPiece.Anchor + new Vector2(1.05f, .82f), .48f,
                            label + " Vigil", false, lightDecor);
                        CreateProp(data, 5, setPiece.Anchor + Vector2.down * .94f, .34f,
                            label + " Threshold Offering", false, clutterDecor);
                        break;
                    case DungeonSetPieceKind.Portal:
                        // The ExitPortal remains the sole interactable. Low non-blocking markers
                        // frame it from behind and make the destination legible before empowerment.
                        CreateProp(data, 5, setPiece.Anchor + new Vector2(-1.08f, .86f), .4f,
                            label + " Pilgrim Remains", false, clutterDecor);
                        CreateProp(data, 5, setPiece.Anchor + new Vector2(1.08f, .86f), .4f,
                            label + " Pilgrim Remains", false, clutterDecor);
                        CreateProp(data, 7, setPiece.Anchor + Vector2.up * 1.02f, .52f,
                            label + " Seal", false, structuralDecor);
                        break;
                    case DungeonSetPieceKind.EliteArena:
                        // Keep the centre clear for the encounter. Four braziers make the arena
                        // legible from every entrance and provide its own combat-light rhythm.
                        CreateProp(data, 2, setPiece.Anchor + new Vector2(-1.05f, -.72f), .58f,
                            label + " Brazier", true, lightDecor);
                        CreateProp(data, 2, setPiece.Anchor + new Vector2(1.05f, -.72f), .58f,
                            label + " Brazier", true, lightDecor);
                        CreateProp(data, 5, setPiece.Anchor + new Vector2(-.92f, .86f), .42f,
                            label + " Remains", false, clutterDecor);
                        CreateProp(data, 5, setPiece.Anchor + new Vector2(.92f, .86f), .42f,
                            label + " Remains", false, clutterDecor);
                        break;
                    case DungeonSetPieceKind.EventRoom:
                        // OssuaryEventScenario owns the animated altar at the semantic anchor.
                        // Keep only the low offerings here so no generic prop overlaps it.
                        CreateProp(data, 5, setPiece.Anchor + new Vector2(-1.02f, -.72f), .42f,
                            label + " Offering", false, clutterDecor);
                        CreateProp(data, 5, setPiece.Anchor + new Vector2(1.02f, -.72f), .42f,
                            label + " Offering", false, clutterDecor);
                        break;
                    case DungeonSetPieceKind.TreasureVault:
                        // The runtime chest owns the centre; authored storage frames it without
                        // competing with the interactable or closing the room's only route.
                        CreateProp(data, 4, setPiece.Anchor + Vector2.left * 1.28f, .88f,
                            label + " Stores", true, structuralDecor);
                        CreateProp(data, 4, setPiece.Anchor + Vector2.right * 1.28f, .88f,
                            label + " Stores", true, structuralDecor);
                        CreateProp(data, 10, setPiece.Anchor + Vector2.up * 1.22f, .82f,
                            label + " Ledger", true, structuralDecor);
                        break;
                    case DungeonSetPieceKind.MimicLair:
                        CreateProp(data, 5, setPiece.Anchor + Vector2.left * .92f, .5f,
                            label + " Victims", false, clutterDecor);
                        CreateProp(data, 5, setPiece.Anchor + Vector2.right * .92f, .5f,
                            label + " Victims", false, clutterDecor);
                        CreateProp(data, 7, setPiece.Anchor + Vector2.up * .82f, .58f,
                            label + " Chains", false, structuralDecor);
                        break;
                    default:
                        // CatacombShrineScenario owns the animated shrine at its semantic anchor.
                        if (setPiece.Kind != DungeonSetPieceKind.Shrine)
                            CreateProp(data, 6, setPiece.Anchor, setPiece.Mask.width >= 5 ? .92f : .78f,
                                label, true, structuralDecor);
                        break;
                }
            }
        }

        private void BuildHazardSurfaces(DungeonData data)
        {
            if (data.Hazards.Count == 0) return;
            var authoredRoot = CreateGroup(transform, "Hazard Modules · " + profile.Id);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var upperVertices = new List<Vector3>();
            var upperTriangles = new List<int>();
            var upperColors = new List<Color>();
            var upperUvs = new List<Vector2>();
            var bankVertices = new List<Vector3>();
            var bankTriangles = new List<int>();
            var bankColors = new List<Color>();
            var bankUvs = new List<Vector2>();
            var upperBankVertices = new List<Vector3>();
            var upperBankTriangles = new List<int>();
            var upperBankColors = new List<Color>();
            var upperBankUvs = new List<Vector2>();
            foreach (var hazard in data.Hazards)
            {
                var x = hazard.Cell.x;
                var y = hazard.Cell.y;
                // Full authored modules are minisets, not Wang tiles: repeating and mirroring them
                // per cell exposes their square footprint. Keep the authored bridge as a landmark,
                // while the river itself and its banks are built as continuous meshes below.
                if (hazard.SafeCrossing) CreateAuthoredHazardModule(authoredRoot, hazard, data);
                var shape = new List<Vector2>
                {
                    new Vector2(x + .14f, y + .03f), new Vector2(x + .86f, y + .03f),
                    new Vector2(x + .97f, y + .14f), new Vector2(x + .97f, y + .86f),
                    new Vector2(x + .86f, y + .97f), new Vector2(x + .14f, y + .97f),
                    new Vector2(x + .03f, y + .86f), new Vector2(x + .03f, y + .14f)
                };
                if ((hazard.Connections & DungeonHazardConnections.West) != 0)
                { shape[6] = new Vector2(x, y + .86f); shape[7] = new Vector2(x, y + .14f); }
                if ((hazard.Connections & DungeonHazardConnections.East) != 0)
                { shape[2] = new Vector2(x + 1, y + .14f); shape[3] = new Vector2(x + 1, y + .86f); }
                if ((hazard.Connections & DungeonHazardConnections.South) != 0)
                { shape[0] = new Vector2(x + .14f, y); shape[1] = new Vector2(x + .86f, y); }
                if ((hazard.Connections & DungeonHazardConnections.North) != 0)
                { shape[4] = new Vector2(x + .86f, y + 1); shape[5] = new Vector2(x + .14f, y + 1); }
                var upper = data.ElevationLevel(x, y) > 0;
                var height = upper
                    ? data.SurfaceHeight(new Vector2(x + .5f, y + .5f)) + .008f
                    : .008f;
                if (upper)
                    AddHazardPolygon(upperVertices, upperTriangles, upperColors, upperUvs, shape,
                        HazardSurfaceColor(hazard), height);
                else
                    AddHazardPolygon(vertices, triangles, colors, uvs, shape,
                        HazardSurfaceColor(hazard), height);

                AddHazardBanks(upper ? upperBankVertices : bankVertices,
                    upper ? upperBankTriangles : bankTriangles,
                    upper ? upperBankColors : bankColors,
                    upper ? upperBankUvs : bankUvs, shape, hazard.Connections, height + .006f);
                if (!hazard.SafeCrossing && hazard.FlowIndex % 4 == 1)
                    data.AddLightSource(hazard.Cell + new Vector2(.5f, .5f), HazardGlowColor(hazard.Kind),
                        hazard.Kind == DungeonHazardKind.Lava ? 3.7f : 3.15f,
                        hazard.Kind == DungeonHazardKind.Lava ? .08f : .035f);
            }
            CreateHazardLayer("Connected Biome Hazards", vertices, triangles, colors, uvs, -9);
            CreateHazardLayer("Connected Upper Biome Hazards", upperVertices, upperTriangles,
                upperColors, upperUvs, 974);
            CreateHazardBankLayer("Continuous Hazard Banks", bankVertices, bankTriangles,
                bankColors, bankUvs, -7);
            CreateHazardBankLayer("Continuous Upper Hazard Banks", upperBankVertices,
                upperBankTriangles, upperBankColors, upperBankUvs, 976);
        }

        // Hue and surface detail live in one seamless material. White vertex color prevents the
        // material from being multiplied by the same biome tint twice and turning into a dark mat.
        private static Color HazardSurfaceColor(DungeonHazardCell hazard) => Color.white;

        private static Color HazardGlowColor(DungeonHazardKind kind)
        {
            switch (kind)
            {
                case DungeonHazardKind.Lava: return new Color(1f, .24f, .045f, .58f);
                case DungeonHazardKind.Brine: return new Color(.08f, .46f, .54f, .24f);
                case DungeonHazardKind.Bile: return new Color(.42f, .56f, .07f, .27f);
                case DungeonHazardKind.VoidRift: return new Color(.46f, .08f, .72f, .34f);
                default: return new Color(.72f, .12f, .045f, .28f);
            }
        }

        private void CreateHazardLayer(string name, List<Vector3> vertices, List<int> triangles,
            List<Color> colors, List<Vector2> uvs, int sortingOrder)
        {
            if (vertices.Count == 0) return;
            var mesh = MakeMesh(name, vertices, triangles, colors, uvs);
            // Hazard color is part of its gameplay readability. It remains self-readable in the
            // ambient darkness while authored banks above it still receive local 2D lighting.
            var kind = HazardKindForBiome(profile.Id);
            var surfaceMaterial = CreateHazardSurfaceMaterial(kind);
            materials.Add(surfaceMaterial);
            var layer = CreateLayer(mesh.name, mesh, surfaceMaterial, sortingOrder);
            layer.AddComponent<HazardSurfaceAnimator>().Initialize(mesh, kind, surfaceMaterial);
        }

        private void CreateHazardBankLayer(string name, List<Vector3> vertices, List<int> triangles,
            List<Color> colors, List<Vector2> uvs, int sortingOrder)
        {
            if (vertices.Count == 0) return;
            var mesh = MakeMesh(name, vertices, triangles, colors, uvs);
            CreateLayer(name, mesh, CreateTexturedMaterial(profile.WallTexture), sortingOrder);
        }

        private void AddHazardBanks(List<Vector3> vertices, List<int> triangles, List<Color> colors,
            List<Vector2> uvs, IReadOnlyList<Vector2> shape, DungeonHazardConnections connections,
            float height)
        {
            var color = Color.Lerp(profile.FloorTint, profile.WallTint, .64f) * .92f;
            for (var segment = 0; segment < shape.Count; segment++)
            {
                var connectionOpening = (segment == 0 &&
                                         (connections & DungeonHazardConnections.South) != 0) ||
                                        (segment == 2 &&
                                         (connections & DungeonHazardConnections.East) != 0) ||
                                        (segment == 4 &&
                                         (connections & DungeonHazardConnections.North) != 0) ||
                                        (segment == 6 &&
                                         (connections & DungeonHazardConnections.West) != 0);
                if (connectionOpening) continue;
                var logicalA = shape[segment];
                var logicalB = shape[(segment + 1) % shape.Count];
                var a = IsoWorld.Project(logicalA) + Vector2.up * height;
                var b = IsoWorld.Project(logicalB) + Vector2.up * height;
                var direction = (b - a).normalized;
                var normal = Vector2.Perpendicular(direction);
                // A two-tone lip visually cuts the liquid into the floor. The inner edge catches
                // light while the outer edge stays grounded; neither creates a detached black
                // strip below the architecture.
                AddHazardBankQuad(vertices, triangles, colors, uvs,
                    a - normal * .058f, b - normal * .058f,
                    b + normal * .046f, a + normal * .046f,
                    color * .54f, color * 1.18f, logicalA, logicalB);
            }
        }

        private static void AddHazardBankQuad(List<Vector3> vertices, List<int> triangles,
            List<Color> colors, List<Vector2> uvs, Vector2 a, Vector2 b, Vector2 d, Vector2 e,
            Color outer, Color inner, Vector2 logicalA, Vector2 logicalB)
        {
            var index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(d); vertices.Add(e);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
            triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);
            colors.Add(outer); colors.Add(outer); colors.Add(inner); colors.Add(inner);
            uvs.Add(logicalA * .18f); uvs.Add(logicalB * .18f);
            uvs.Add(logicalB * .18f + Vector2.up * .07f);
            uvs.Add(logicalA * .18f + Vector2.up * .07f);
        }

        private Material CreateHazardSurfaceMaterial(DungeonHazardKind kind)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = "Seamless " + kind + " Surface",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var baseColor = HazardColor(kind);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                // Periodic functions make opposite texture borders identical. Quantisation keeps
                // the late-90s hand-painted cadence without exposing a square repeat.
                var broad = Mathf.Sin((u + v) * Mathf.PI * 4f + Mathf.Sin(v * Mathf.PI * 2f) * 1.7f);
                var cross = Mathf.Sin((u * 3f - v * 2f) * Mathf.PI * 2f +
                                      Mathf.Cos(u * Mathf.PI * 2f) * 1.35f);
                var grain = Mathf.Sin((u * 9f + v * 7f) * Mathf.PI * 2f) * .18f;
                var field = Mathf.Round((broad * .57f + cross * .31f + grain) * 5f) / 5f;
                var value = 1f + field * SurfaceContrast(kind);
                var highlight = Mathf.Clamp01((field - .28f) * 1.8f) * SurfaceHighlight(kind);
                var color = baseColor * value + HazardGlowColor(kind) * highlight;
                color.a = baseColor.a;
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            runtimeTextures.Add(texture);
            return new Material(DarkfallRenderMaterials.SpriteUnlit)
            {
                name = kind + " Connected Surface Material",
                color = Color.white,
                mainTexture = texture
            };
        }

        private static float SurfaceContrast(DungeonHazardKind kind)
        {
            if (kind == DungeonHazardKind.Lava) return .38f;
            if (kind == DungeonHazardKind.VoidRift) return .31f;
            if (kind == DungeonHazardKind.Bile) return .25f;
            return .2f;
        }

        private static float SurfaceHighlight(DungeonHazardKind kind)
        {
            if (kind == DungeonHazardKind.Lava) return .42f;
            if (kind == DungeonHazardKind.VoidRift) return .26f;
            return .16f;
        }

        private bool CreateAuthoredHazardModule(Transform parent, DungeonHazardCell hazard, DungeonData data)
        {
            var moduleName = HazardModuleName(hazard);
            var folder = HazardResourceFolder(hazard.Kind);
            var cacheKey = folder + "/" + moduleName;
            if (!hazardSprites.TryGetValue(cacheKey, out var sprite))
            {
                var texture = Resources.Load<Texture2D>(cacheKey + "-01");
                if (texture == null) return false;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 345f, 0, SpriteMeshType.Tight);
                hazardSprites[cacheKey] = sprite;
            }
            var module = new GameObject(hazard.Kind + " · " + moduleName);
            module.transform.SetParent(parent, false);
            module.transform.position = IsoWorld.Project(hazard.Cell + new Vector2(.5f, .5f)) +
                                        Vector2.up * (data.SurfaceHeight(hazard.Cell + new Vector2(.5f, .5f)) + .008f);
            var renderer = module.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            ConfigureHazardOrientation(renderer, hazard.Connections, moduleName);
            // The crossing is architecture above both the animated hazard and its bank lip.
            // A negative order let the translucent edge matte and nearby walls paint over it.
            renderer.sortingOrder = data.ElevationLevel(hazard.Cell.x, hazard.Cell.y) > 0 ? 1085 : 1008;
            DarkfallRenderMaterials.MakeLit(renderer);
            return true;
        }

        private static string HazardModuleName(DungeonHazardCell hazard)
        {
            if (hazard.SafeCrossing) return "bridge";
            var mask = hazard.Connections;
            var count = 0;
            if ((mask & DungeonHazardConnections.West) != 0) count++;
            if ((mask & DungeonHazardConnections.East) != 0) count++;
            if ((mask & DungeonHazardConnections.South) != 0) count++;
            if ((mask & DungeonHazardConnections.North) != 0) count++;
            if (count == 0) return "isolated";
            if (count == 1) return "end";
            if (count == 2)
                return ((mask & (DungeonHazardConnections.West | DungeonHazardConnections.East)) ==
                        (DungeonHazardConnections.West | DungeonHazardConnections.East) ||
                        (mask & (DungeonHazardConnections.South | DungeonHazardConnections.North)) ==
                        (DungeonHazardConnections.South | DungeonHazardConnections.North)) ? "straight" : "corner";
            if (count == 3) return "tee";
            return "body-4way";
        }

        private static void ConfigureHazardOrientation(SpriteRenderer renderer,
            DungeonHazardConnections mask, string moduleName)
        {
            // The authored canonical pieces face west (end), west/east (straight),
            // west+north (corner), and west+east+south (tee). Isometric axes are mirrored,
            // never arbitrarily rotated, so pixel scale and connection width remain stable.
            if (moduleName == "straight" || moduleName == "bridge")
            {
                // Straight hazard art follows the flow; a bridge must use the perpendicular
                // isometric axis. Mirroring swaps the two screen-space diagonals.
                var northSouth = (mask & (DungeonHazardConnections.South |
                                           DungeonHazardConnections.North)) != 0;
                renderer.flipX = moduleName == "bridge" ? !northSouth : northSouth;
                return;
            }
            if (moduleName == "end")
            {
                renderer.flipX = (mask & (DungeonHazardConnections.South | DungeonHazardConnections.East)) != 0;
                renderer.flipY = (mask & (DungeonHazardConnections.North | DungeonHazardConnections.East)) != 0;
                return;
            }
            if (moduleName == "corner")
            {
                renderer.flipX = (mask & DungeonHazardConnections.East) != 0;
                renderer.flipY = (mask & DungeonHazardConnections.South) != 0;
                return;
            }
            if (moduleName == "tee")
            {
                renderer.flipX = (mask & DungeonHazardConnections.West) == 0 ||
                                 (mask & DungeonHazardConnections.North) == 0;
                renderer.flipY = (mask & DungeonHazardConnections.South) == 0 ||
                                 (mask & DungeonHazardConnections.East) == 0;
            }
        }

        private static string HazardResourceFolder(DungeonHazardKind kind)
        {
            const string root = "Sprites/Environment/Hazards/";
            switch (kind)
            {
                case DungeonHazardKind.EmberSeep: return root + "ashen-catacombs";
                case DungeonHazardKind.Lava: return root + "ember-vaults";
                case DungeonHazardKind.Brine: return root + "drowned-crypt";
                case DungeonHazardKind.Bile: return root + "charnel-gardens";
                case DungeonHazardKind.VoidRift: return root + "obsidian-sanctum";
                default: return root + "ashen-catacombs";
            }
        }

        private static void AddHazardPolygon(List<Vector3> vertices, List<int> triangles,
            List<Color> colors, List<Vector2> uvs, IReadOnlyList<Vector2> shape, Color color, float height)
        {
            var centerLogical = Vector2.zero;
            for (var i = 0; i < shape.Count; i++) centerLogical += shape[i];
            centerLogical /= shape.Count;
            var center = vertices.Count;
            vertices.Add(IsoWorld.Project(centerLogical) + Vector2.up * height);
            colors.Add(color);
            uvs.Add(centerLogical * .72f);
            for (var i = 0; i < shape.Count; i++)
            {
                vertices.Add(IsoWorld.Project(shape[i]) + Vector2.up * height);
                // Shared logical positions receive identical color and UV values, so adjacent
                // cells become one uninterrupted surface rather than visible polygon facets.
                colors.Add(color);
                uvs.Add(shape[i] * .72f);
            }
            for (var i = 0; i < shape.Count; i++)
            {
                triangles.Add(center);
                triangles.Add(center + 1 + (i + 1) % shape.Count);
                triangles.Add(center + 1 + i);
            }
        }

        private static Color HazardColor(DungeonHazardKind kind)
        {
            switch (kind)
            {
                case DungeonHazardKind.Lava: return new Color(.64f, .075f, .008f, .98f);
                case DungeonHazardKind.Brine: return new Color(.025f, .27f, .32f, .9f);
                case DungeonHazardKind.Bile: return new Color(.21f, .31f, .025f, .92f);
                case DungeonHazardKind.VoidRift: return new Color(.20f, .025f, .39f, .94f);
                default: return new Color(.34f, .04f, .018f, .9f);
            }
        }

        private void BuildContourFloor(DungeonContour contour, DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            const float uvScale = .08f;
            foreach (var polygon in contour.FloorPolygons)
            {
                var index = vertices.Count;
                var center = Vector2.zero;
                for (var i = 0; i < polygon.Length; i++) center += polygon[i];
                center /= polygon.Length;
                var elevation = data.SurfaceHeight(center);
                var level = data.ElevationLevel(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
                var tint = ElevationFloorTint(profile.FloorTint *
                    RandomTint(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y)), level);
                for (var i = 0; i < polygon.Length; i++)
                {
                    var point = IsoWorld.Project(polygon[i]);
                    point.y += elevation;
                    vertices.Add(point);
                    colors.Add(tint);
                    uvs.Add(polygon[i] * uvScale);
                }
                for (var i = 1; i < polygon.Length - 1; i++)
                {
                    triangles.Add(index);
                    triangles.Add(index + i + 1);
                    triangles.Add(index + i);
                }
            }
            var mesh = MakeMesh("Continuous Isometric Floor", vertices, triangles, colors, uvs);
            var material = CreateTexturedMaterial(profile.FloorTexture);
            CreateLayer(mesh.name, mesh, material, -20);
            BuildRaisedFloorCaps(data);
            BuildElevationRisers(data);
            BuildStairRamps(data);
            BuildElevationGuardrails(data);
        }

        private void BuildStairRamps(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            foreach (var feature in data.Architecture)
            {
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs) continue;
                var normal = feature.Vertical ? Vector2.right : Vector2.up;
                var tangent = feature.Vertical ? Vector2.up : Vector2.right;
                var negative = data.ElevationLevel(
                    Mathf.FloorToInt(feature.Position.x - normal.x * .25f),
                    Mathf.FloorToInt(feature.Position.y - normal.y * .25f));
                var positive = data.ElevationLevel(
                    Mathf.FloorToInt(feature.Position.x + normal.x * .25f),
                    Mathf.FloorToInt(feature.Position.y + normal.y * .25f));
                if (negative == positive) continue;
                var lowerDirection = negative < positive ? -normal : normal;
                var lowerLevel = Mathf.Min(negative, positive);
                var upperLevel = Mathf.Max(negative, positive);
                // Raised platforms already use the authored exterior stair module. This composite
                // mesh is the recessed counterpart and must never be drawn on top of that module.
                if (lowerLevel >= 0) continue;
                const float flightDepth = 1.42f;
                // A 2/3-cell authored opening must also produce a 2/3-cell visual flight. The
                // previous hard cap made the wide variant look like the same narrow ramp inside a
                // larger hole.
                var halfWidth = Mathf.Max(.68f, feature.Width * .5f - .12f);
                var upperCenter = feature.Position;
                var lowerCenter = feature.Position + lowerDirection * flightDepth;
                var tint = Color.Lerp(profile.FloorTint, profile.WallTint, .32f) * .78f;
                const int stepCount = 7;
                for (var step = 0; step < stepCount; step++)
                {
                    var nearT = step / (float)stepCount;
                    var farT = (step + 1f) / stepCount;
                    var near = Vector2.Lerp(upperCenter, lowerCenter, nearT);
                    var far = Vector2.Lerp(upperCenter, lowerCenter, farT);
                    var height = Mathf.Lerp(upperLevel, lowerLevel, nearT) * DungeonData.ElevationStepHeight;
                    var nextHeight = Mathf.Lerp(upperLevel, lowerLevel, farT) * DungeonData.ElevationStepHeight;
                    var logical = new[]
                    {
                        near - tangent * halfWidth, near + tangent * halfWidth,
                        far + tangent * halfWidth, far - tangent * halfWidth
                    };
                    var index = vertices.Count;
                    for (var i = 0; i < logical.Length; i++)
                    {
                        vertices.Add(IsoWorld.Project(logical[i]) + Vector2.up * height);
                        colors.Add(tint);
                        uvs.Add(logical[i] * .08f);
                    }
                    triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
                    triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);

                    // The vertical face is what makes the height change readable as stairs rather
                    // than a smooth painted ramp. It also closes every screen-space crack between
                    // consecutive tread planes.
                    var riser = new[]
                    {
                        far - tangent * halfWidth, far + tangent * halfWidth
                    };
                    index = vertices.Count;
                    vertices.Add(IsoWorld.Project(riser[0]) + Vector2.up * height);
                    vertices.Add(IsoWorld.Project(riser[1]) + Vector2.up * height);
                    vertices.Add(IsoWorld.Project(riser[1]) + Vector2.up * nextHeight);
                    vertices.Add(IsoWorld.Project(riser[0]) + Vector2.up * nextHeight);
                    for (var i = 0; i < 4; i++)
                    {
                        colors.Add(profile.WallTint * .68f);
                        uvs.Add(riser[Mathf.Min(i, 1)] * .08f);
                    }
                    triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                    triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
                }
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Elevation Stair Ramps", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.FloorTexture), 974);
            var stairReadability = new Material(DarkfallRenderMaterials.SpriteUnlit)
            {
                color = new Color(.76f, .72f, .64f, .28f),
                mainTexture = Resources.Load<Texture2D>(profile.FloorTexture)
            };
            materials.Add(stairReadability);
            CreateLayer("Elevation Stair Readability", mesh, stairReadability, 975);
        }

        private void BuildContextFloorTiles(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            foreach (var tile in data.ResolvedFloorTiles)
            {
                var index = vertices.Count;
                var x = tile.Cell.x;
                var y = tile.Cell.y;
                var height = data.SurfaceHeight(new Vector2(x + .5f, y + .5f));
                var logical = new[] { new Vector2(x, y), new Vector2(x + 1, y),
                    new Vector2(x + 1, y + 1), new Vector2(x, y + 1) };
                var edgeFactor = tile.Kind == DungeonFloorTileKind.Center ? 1f :
                    tile.Kind == DungeonFloorTileKind.InnerCorner ? .93f : .84f;
                if (tile.Damaged) edgeFactor *= .82f;
                var tint = ElevationFloorTint(profile.FloorTint *
                    RandomTint(x + tile.Variant * 7, y + tile.Variant * 11), data.ElevationLevel(x, y));
                tint = RoomMaterialTint(data, x, y, tint) * edgeFactor;
                // Each variant samples another stable portion of the repeatable source texture.
                // A future authored atlas can replace this UV policy without touching generation.
                var uvOffset = new Vector2(tile.Variant * .271f, tile.Variant * .163f);
                for (var i = 0; i < logical.Length; i++)
                {
                    var point = IsoWorld.Project(logical[i]);
                    point.y += height;
                    vertices.Add(point);
                    colors.Add(tint);
                    uvs.Add(logical[i] * .08f + uvOffset);
                }
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Context Resolved Floor Tiles", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.FloorTexture), -19);
        }

        private void BuildWallFoundationApron(DungeonContour contour, DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            foreach (var segment in contour.Segments)
            {
                var outward = segment.Mask == 1 ? Vector2.down : segment.Mask == 2 ? Vector2.right :
                    segment.Mask == 4 ? Vector2.up : Vector2.left;
                const float foundationDepth = .22f;
                var logical = new[] { segment.From, segment.To,
                    segment.To + outward * foundationDepth, segment.From + outward * foundationDepth };
                var index = vertices.Count;
                var elevation = data.BoundaryHeight((segment.From + segment.To) * .5f);
                for (var i = 0; i < logical.Length; i++)
                {
                    var projected = IsoWorld.Project(logical[i]);
                    projected.y += elevation;
                    vertices.Add(projected);
                    colors.Add(profile.FloorTint * .58f);
                    uvs.Add(logical[i] * .08f);
                }
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Wall Stone Foundation Apron", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.FloorTexture), -18);
        }

        private void BuildElevationGuardrails(DungeonData data)
        {
            if (!ArchitectureSpriteLibrary.HasBiome(profile.Id)) return;
            var moduleIndex = 70000;
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                var level = data.ElevationLevel(x, y);
                var top = level * DungeonData.ElevationStepHeight;
                AddElevationGuardrail(data, x, y, x - 1, y,
                    new Vector2(x, y + .5f), true, top, ref moduleIndex);
                AddElevationGuardrail(data, x, y, x + 1, y,
                    new Vector2(x + 1, y + .5f), true, top, ref moduleIndex);
                AddElevationGuardrail(data, x, y, x, y - 1,
                    new Vector2(x + .5f, y), false, top, ref moduleIndex);
                AddElevationGuardrail(data, x, y, x, y + 1,
                    new Vector2(x + .5f, y + 1), false, top, ref moduleIndex);
            }
        }

        private void AddElevationGuardrail(DungeonData data, int upperX, int upperY, int lowerX, int lowerY,
            Vector2 anchor, bool vertical, float top, ref int moduleIndex)
        {
            // Outer dungeon contours already own their wall. This guardrail exists only where two
            // walkable floors touch at different levels: precisely the case that otherwise reads
            // as a fake open passage despite traversal being blocked.
            if (!data.IsFloor(lowerX, lowerY) || data.ElevationLevel(lowerX, lowerY) >=
                data.ElevationLevel(upperX, upperY) || IsStairRiserOpening(data, anchor, vertical)) return;
            var role = ArchitectureSpriteLibrary.WallRoleForAxis(profile.Id, vertical);
            var flip = ArchitectureSpriteLibrary.FlipForAxis(profile.Id, role, vertical);
            var lowerLevel = data.ElevationLevel(lowerX, lowerY);
            // A sunken room owns walls rooted in its own floor. Keeping them at the main-floor
            // ledge made the floor descend while the whole room silhouette stayed at elevation 0.
            // Raised platforms still need their guard wall on the upper platform.
            var wallElevation = lowerLevel < 0
                ? lowerLevel * DungeonData.ElevationStepHeight
                : top;
            CreateArchitectureModule(role, anchor, flip, .985f, moduleIndex++, wallElevation);
        }

        private void BuildRaisedFloorCaps(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var seamVertices = new List<Vector3>();
            var seamTriangles = new List<int>();
            var seamColors = new List<Color>();
            var seamUvs = new List<Vector2>();
            const float uvScale = .08f;
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                var level = data.ElevationLevel(x, y);
                if (level <= 0) continue;
                var height = level * DungeonData.ElevationStepHeight;
                var index = vertices.Count;
                var logical = new[]
                {
                    new Vector2(x, y), new Vector2(x + 1, y),
                    new Vector2(x + 1, y + 1), new Vector2(x, y + 1)
                };
                foreach (var point in logical)
                {
                    var projected = IsoWorld.Project(point);
                    projected.y += height;
                    vertices.Add(projected);
                    uvs.Add(point * uvScale);
                    colors.Add(ElevationFloorTint(profile.FloorTint * RandomTint(x, y), level));
                }
                // Project() reverses the apparent Y axis. These must be clockwise in projected
                // space or the 2D renderer culls the entire upper plane as a back face.
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);

                // Even an away-facing ledge needs a graphic seam. Without it, an inaccessible
                // upper floor reads as ordinary floor behind a wall because its vertical face is
                // correctly hidden by the cap in this projection.
                if (data.ElevationLevel(x - 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x, y + .5f), true))
                    AddPlatformCapSeam(seamVertices, seamTriangles, seamColors, seamUvs,
                        new Vector2(x, y), new Vector2(x, y + 1), Vector2.right, height);
                if (data.ElevationLevel(x, y - 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y), false))
                    AddPlatformCapSeam(seamVertices, seamTriangles, seamColors, seamUvs,
                        new Vector2(x + 1, y), new Vector2(x, y), Vector2.up, height);
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Raised Platform Upper Floors", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.FloorTexture), 972);
            var seamMesh = MakeMesh("Raised Platform Cap Seams", seamVertices, seamTriangles, seamColors, seamUvs);
            var seamMaterial = new Material(DarkfallRenderMaterials.SpriteLit)
            {
                color = Color.white,
                mainTexture = Resources.Load<Texture2D>(profile.WallTexture)
            };
            materials.Add(seamMaterial);
            CreateLayer(seamMesh.name, seamMesh, seamMaterial, 976);
        }

        private void AddPlatformCapSeam(List<Vector3> vertices, List<int> triangles,
            List<Color> colors, List<Vector2> uvs, Vector2 from, Vector2 to, Vector2 inward, float height)
        {
            var a = IsoWorld.Project(from) + Vector2.up * height;
            var b = IsoWorld.Project(to) + Vector2.up * height;
            var lipTo = IsoWorld.Project(to + inward * .045f) + Vector2.up * height;
            var lipFrom = IsoWorld.Project(from + inward * .045f) + Vector2.up * height;
            var recessTo = IsoWorld.Project(to + inward * .15f) + Vector2.up * height;
            var recessFrom = IsoWorld.Project(from + inward * .15f) + Vector2.up * height;
            var stone = Color.Lerp(profile.WallTint, Color.white, profile.WallReadability) * .54f;
            AddScreenQuad(vertices, triangles, colors, uvs, a, b, lipTo, lipFrom,
                stone, from, to);
            AddScreenQuad(vertices, triangles, colors, uvs, lipFrom, lipTo, recessTo, recessFrom,
                Color.Lerp(profile.WallTint, profile.FloorTint, .35f) * .46f, from, to);
        }

        private void BuildElevationRisers(DungeonData data)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var color = Color.Lerp(profile.WallTint, Color.white, profile.WallReadability) * .72f;
            for (var x = 0; x < data.Width; x++)
            for (var y = 0; y < data.Height; y++)
            {
                var level = data.ElevationLevel(x, y);
                if (!data.IsFloor(x, y)) continue;
                // Only screen-facing (+X/+Y) platform faces are visible in this projection. The
                // -X/-Y faces are behind the upper floor; rendering them in a 2D pipeline makes
                // them appear through that floor as interior walls.
                if (data.ElevationLevel(x + 1, y) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + 1, y + .5f), true))
                {
                    var lower = data.ElevationLevel(x + 1, y) * DungeonData.ElevationStepHeight;
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x + 1, y), new Vector2(x + 1, y + 1),
                        lower, (level - data.ElevationLevel(x + 1, y)) * DungeonData.ElevationStepHeight, color * .84f);
                }
                if (data.ElevationLevel(x, y + 1) < level &&
                    !IsStairRiserOpening(data, new Vector2(x + .5f, y + 1), false))
                {
                    var lower = data.ElevationLevel(x, y + 1) * DungeonData.ElevationStepHeight;
                    AddRiser(vertices, triangles, colors, uvs, new Vector2(x + 1, y + 1), new Vector2(x, y + 1),
                        lower, (level - data.ElevationLevel(x, y + 1)) * DungeonData.ElevationStepHeight, color);
                }
            }
            if (vertices.Count == 0) return;
            var mesh = MakeMesh("Raised Platform Fascias", vertices, triangles, colors, uvs);
            CreateLayer(mesh.name, mesh, CreateTexturedMaterial(profile.WallTexture), 970);
            var texture = Resources.Load<Texture2D>(profile.WallTexture);
            // Shader.Find-only variants can be stripped from a player build even though they are
            // available in Editor. Reuse the URP 2D material path referenced by the renderer asset.
            var readability = new Material(DarkfallRenderMaterials.SpriteUnlit)
            {
                color = new Color(.9f, .9f, .9f, .52f),
                mainTexture = texture
            };
            materials.Add(readability);
            CreateLayer("Raised Platform Readability", mesh, readability, 971);
        }

        private static bool IsStairRiserOpening(DungeonData data, Vector2 midpoint, bool vertical)
        {
            foreach (var feature in data.Architecture)
            {
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs || feature.Vertical != vertical) continue;
                var normalDistance = vertical
                    ? Mathf.Abs(midpoint.x - feature.Position.x)
                    : Mathf.Abs(midpoint.y - feature.Position.y);
                var tangentDistance = vertical
                    ? Mathf.Abs(midpoint.y - feature.Position.y)
                    : Mathf.Abs(midpoint.x - feature.Position.x);
                // Reserve the complete authored opening. The old fixed .51 half-width only
                // removed a one-cell slit, so ordinary guard walls overlapped every 2/3-cell
                // staircase and made the transition look glued over a black hole.
                if (normalDistance < .05f && tangentDistance <= feature.Width * .5f + .05f) return true;
            }
            return false;
        }

        private static void AddRiser(List<Vector3> vertices, List<int> triangles, List<Color> colors,
            List<Vector2> uvs, Vector2 from, Vector2 to, float baseElevation, float height, Color color)
        {
            var lowerFrom = IsoWorld.Project(from) + Vector2.up * baseElevation;
            var lowerTo = IsoWorld.Project(to) + Vector2.up * baseElevation;
            var plinth = height * .18f;
            var frieze = height * .78f;

            // A platform edge is a vertical architectural tile, not a stretched shadow quad.
            // Use the full biome wall texture range and explicit masonry bands so height remains
            // readable even outside local lights.
            AddWallBand(vertices, triangles, colors, uvs, lowerFrom, lowerTo,
                0f, plinth, color * .62f, from, to, 0f, .18f);
            AddWallBand(vertices, triangles, colors, uvs, lowerFrom, lowerTo,
                plinth, frieze, color * .94f, from, to, .18f, .76f);
            AddWallBand(vertices, triangles, colors, uvs, lowerFrom, lowerTo,
                frieze, height, color * 1.22f, from, to, .76f, 1f);

            var upperFrom = lowerFrom + Vector2.up * height;
            var upperTo = lowerTo + Vector2.up * height;
            var edge = upperTo - upperFrom;
            var lip = Vector2.Perpendicular(edge.normalized) * .045f;
            if (lip.y < 0f) lip = -lip;
            AddScreenQuad(vertices, triangles, colors, uvs, upperFrom, upperTo,
                upperTo + lip, upperFrom + lip, color * 1.12f, from, to);
        }

        private void BuildContourWalls(DungeonContour contour, DungeonData data)
        {
            var maximumDepth = data.Width + data.Height;
            var hasArchitecture = ArchitectureSpriteLibrary.HasBiome(profile.Id);
            var wallTexture = Resources.Load<Texture2D>(profile.WallTexture);
            if (wallTexture != null)
            {
                wallTexture.wrapMode = TextureWrapMode.Repeat;
                wallTexture.filterMode = FilterMode.Bilinear;
            }

            // The continuous mesh is only a legacy fallback. Drawing it below a complete authored
            // kit also duplicates the dark plinth painted into every module. The two baselines do
            // not coincide exactly and read as a black air gap, making the wall appear to float.
            if (hasArchitecture)
            {
                BuildArchitectureModules(contour, data);
                BuildThresholdArchitecture(data);
                return;
            }

            var shadowVertices = new List<Vector3>();
            var shadowTriangles = new List<int>();
            var shadowColors = new List<Color>();
            var shadowUvs = new List<Vector2>();
            foreach (var segment in contour.Segments)
                AddContactShadow(shadowVertices, shadowTriangles, shadowColors, shadowUvs,
                    segment.From, segment.To);
            var shadowMesh = MakeMesh("Continuous Wall Contact Shadow", shadowVertices, shadowTriangles,
                shadowColors, shadowUvs);
            var shadowMaterial = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            materials.Add(shadowMaterial);
            CreateLayer(shadowMesh.name, shadowMesh, shadowMaterial, -5);

            for (var depth = -2; depth <= maximumDepth + 2; depth++)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var colors = new List<Color>();
                var uvs = new List<Vector2>();
                foreach (var segment in contour.Segments)
                {
                    var midpoint = (segment.From + segment.To) * .5f;
                    if (Mathf.RoundToInt(midpoint.x + midpoint.y) != depth) continue;
                    var projected = IsoWorld.Project(segment.To) - IsoWorld.Project(segment.From);
                    var shade = projected.x >= 0 ? .88f : .72f;
                    if (Mathf.Abs(projected.x) < .08f) shade = .8f;
                    var readableWall = Color.Lerp(profile.WallTint, Color.white, profile.WallReadability);
                    AddWallFace(vertices, triangles, colors, uvs, segment.From, segment.To,
                        readableWall * shade);
                }
                if (vertices.Count == 0) continue;
                var mesh = MakeMesh("Contour Wall Facades · " + depth, vertices, triangles, colors, uvs);
                var material = new Material(DarkfallRenderMaterials.SpriteLit)
                    { color = Color.white, mainTexture = wallTexture };
                materials.Add(material);
                CreateLayer(mesh.name, mesh, material, 1040 + depth * IsoWorld.DepthPrecision);
                var fillMaterial = new Material(DarkfallRenderMaterials.SpriteUnlit)
                {
                    color = new Color(1f, 1f, 1f, profile.WallFill),
                    mainTexture = wallTexture
                };
                materials.Add(fillMaterial);
                CreateLayer("Wall Texture Fill · " + depth, mesh, fillMaterial,
                    1041 + depth * IsoWorld.DepthPrecision);
            }

        }

        private void BuildArchitectureModules(DungeonContour contour, DungeonData data)
        {
            var moduleIndex = 0;
            foreach (var module in data.ResolvedWallModules)
            {
                if (FeatureReplacesWallModule(data, module.Anchor)) continue;
                var role = module.Kind == DungeonWallModuleKind.Broken ? "wall-broken" :
                    module.Kind == DungeonWallModuleKind.Niche ? "wall-niche" :
                    module.Kind == DungeonWallModuleKind.Arcade ? "arcade" :
                    ArchitectureSpriteLibrary.WallRoleForAxis(profile.Id, module.Vertical);
                var flip = ArchitectureSpriteLibrary.FlipForAxis(profile.Id, role, module.Vertical);
                var visualVariant = IsCornerShoulder(data.ResolvedWallCorners, module) ? 0 : module.Variant;
                var cornerTrim = CornerShoulderTrim(data.ResolvedWallCorners, module);
                CreateArchitectureModule(role, module.Anchor, flip, .985f, moduleIndex++,
                    data.BoundaryHeight(module.Anchor), visualVariant: visualVariant,
                    endpointTrim: cornerTrim);
            }
        }

        private static bool IsCornerShoulder(IReadOnlyList<DungeonResolvedWallCorner> corners,
            DungeonResolvedWallModule module)
        {
            foreach (var corner in corners)
            {
                var delta = module.Anchor - corner.Anchor;
                if (module.Vertical && Mathf.Abs(delta.x) < .01f &&
                    Mathf.Abs(Mathf.Abs(delta.y) - .5f) < .01f) return true;
                if (!module.Vertical && Mathf.Abs(delta.y) < .01f &&
                    Mathf.Abs(Mathf.Abs(delta.x) - .5f) < .01f) return true;
            }
            return false;
        }

        private static int CornerShoulderTrim(IReadOnlyList<DungeonResolvedWallCorner> corners,
            DungeonResolvedWallModule module)
        {
            foreach (var corner in corners)
            {
                var delta = module.Anchor - corner.Anchor;
                var touches = module.Vertical
                    ? Mathf.Abs(delta.x) < .01f && Mathf.Abs(Mathf.Abs(delta.y) - .5f) < .01f
                    : Mathf.Abs(delta.y) < .01f && Mathf.Abs(Mathf.Abs(delta.x) - .5f) < .01f;
                if (touches)
                {
                    var towardsCorner = IsoWorld.Project(corner.Anchor) - IsoWorld.Project(module.Anchor);
                    return towardsCorner.x < 0f ? -1 : 1;
                }
            }
            return 0;
        }


        private static void AddWallWindowObstacle(DungeonData data, Vector2 anchor, bool vertical)
        {
            // arch-open is the authored lancet/window module. Its dark aperture is visual depth,
            // not a doorway: preserve a solid wall plane even if two carved floor regions happen
            // to approach the same contour closely.
            const float span = 1.02f;
            const float depth = .34f;
            data.AddArchitectureObstacle(vertical
                ? new Rect(anchor.x - depth * .5f, anchor.y - span * .5f, depth, span)
                : new Rect(anchor.x - span * .5f, anchor.y - depth * .5f, span, depth));
        }

        private static List<BoundarySpan> BuildBoundarySpans(IReadOnlyList<DungeonContourSegment> segments)
        {
            var units = new List<BoundarySpan>(segments.Count);
            foreach (var segment in segments)
            {
                var vertical = Mathf.Abs(segment.From.x - segment.To.x) < .01f;
                var fixedCoordinate = vertical ? segment.From.x : segment.From.y;
                var first = vertical ? segment.From.y : segment.From.x;
                var second = vertical ? segment.To.y : segment.To.x;
                units.Add(new BoundarySpan(vertical, fixedCoordinate, Mathf.Min(first, second), Mathf.Max(first, second)));
            }
            units.Sort((a, b) =>
            {
                var axis = a.Vertical.CompareTo(b.Vertical);
                if (axis != 0) return axis;
                var fixedResult = a.Fixed.CompareTo(b.Fixed);
                return fixedResult != 0 ? fixedResult : a.Start.CompareTo(b.Start);
            });
            var spans = new List<BoundarySpan>();
            foreach (var unit in units)
            {
                if (spans.Count > 0)
                {
                    var previous = spans[spans.Count - 1];
                    if (previous.Vertical == unit.Vertical && Mathf.Abs(previous.Fixed - unit.Fixed) < .01f &&
                        Mathf.Abs(previous.End - unit.Start) < .01f)
                    {
                        spans[spans.Count - 1] = new BoundarySpan(previous.Vertical, previous.Fixed,
                            previous.Start, unit.End);
                        continue;
                    }
                }
                spans.Add(unit);
            }
            return spans;
        }

        private static bool FeatureReplacesWallModule(DungeonData data, Vector2 point)
        {
            foreach (var feature in data.Architecture)
            {
                // Floor-to-floor thresholds (including closed doors) are already open in the
                // contour. Removing their neighbouring contour modules cuts false side passages
                // beside the jambs. Only an elevation stair replaces its platform guardrail.
                if (feature.Kind != DungeonArchitectureKind.ElevationStairs) continue;
                var normalDistance = feature.Vertical
                    ? Mathf.Abs(point.x - feature.Position.x)
                    : Mathf.Abs(point.y - feature.Position.y);
                var tangentDistance = feature.Vertical
                    ? Mathf.Abs(point.y - feature.Position.y)
                    : Mathf.Abs(point.x - feature.Position.x);
                if (normalDistance <= .62f && tangentDistance <= feature.Width * .5f + .12f) return true;
            }
            return false;
        }

        private readonly struct BoundarySpan
        {
            public readonly bool Vertical;
            public readonly float Fixed;
            public readonly float Start;
            public readonly float End;

            public BoundarySpan(bool vertical, float fixedCoordinate, float start, float end)
            {
                Vertical = vertical;
                Fixed = fixedCoordinate;
                Start = start;
                End = end;
            }
        }

        private int EdgeHash(Vector2 from, Vector2 to, int sections) =>
            (Mathf.RoundToInt((from.x + to.x) * 47f) * 73856093 ^
             Mathf.RoundToInt((from.y + to.y) * 47f) * 19349663 ^
             profile.Chapter * 83492791 ^ sections * 31) & int.MaxValue;

        private void BuildThresholdArchitecture(DungeonData data)
        {
            var featureIndex = 100000;
            foreach (var feature in data.Architecture)
            {
                if (feature.Kind == DungeonArchitectureKind.ClosedDoor)
                {
                    DungeonDoor.Spawn(data, feature, profile.Id, architectureDecor);
                    continue;
                }
                // Ordinary circulation is an empty threshold. The arcade artwork is a small
                // double lancet wall module and must never masquerade as a walk-through gate.
                if (feature.Kind == DungeonArchitectureKind.OpenGate)
                    continue;
                var normal = feature.Vertical ? Vector2.right : Vector2.up;
                var negativeLevel = data.ElevationLevel(
                    Mathf.FloorToInt(feature.Position.x - normal.x * .25f),
                    Mathf.FloorToInt(feature.Position.y - normal.y * .25f));
                var positiveLevel = data.ElevationLevel(
                    Mathf.FloorToInt(feature.Position.x + normal.x * .25f),
                    Mathf.FloorToInt(feature.Position.y + normal.y * .25f));
                var lowerDirection = negativeLevel < positiveLevel ? -normal : normal;
                // Up and down are the same physical flight. Its foot always belongs to the lower
                // platform and its top always terminates at the higher platform; traversal
                // direction must never select another piece of artwork.
                var lowerElevation = Mathf.Min(negativeLevel, positiveLevel) *
                                     DungeonData.ElevationStepHeight;

                // Two- and three-cell openings use the same flight at distinct authored widths.
                // Only screen X changes so the .9 elevation rise remains exact.
                var stairHorizontalScale = feature.Width >= 3 ? 1.66f : 1.24f;
                // The same authored flight serves both directions. Mirror it along its travel
                // axis when the lower landing lies on the opposite side, otherwise a descent
                // reads as another staircase rising out of a hole.
                var stairFlip = Mathf.Min(negativeLevel, positiveLevel) < 0;
                var stairAnchor = feature.Position + lowerDirection * (stairFlip ? .90f : .64f);
                CreateArchitectureModule("stairs", stairAnchor, feature.Vertical, 1.03f,
                    featureIndex++, lowerElevation, stairHorizontalScale, 0, stairFlip);
            }
        }

        private static Color ElevationFloorTint(Color source, int level)
        {
            if (level > 0)
                return Color.Lerp(source, new Color(.76f, .60f, .38f, source.a), .34f) * 1.10f;
            if (level < 0)
                // Height must read before biome hue. A neutral value shift keeps the lower floor
                // recognisably made from the same catacomb stone instead of looking flooded.
                return Color.Lerp(source, new Color(.10f, .10f, .10f, source.a), .38f) * .62f;
            return source;
        }

        private void CreateArchitectureModule(string role, Vector2 anchor, bool flipX, float scale, int index,
            float elevation = 0f, float horizontalScale = -1f, int visualVariant = 0,
            bool reverseTravel = false, int endpointTrim = 0)
        {
            var sprite = ArchitectureSpriteLibrary.Module(profile.Id, role);
            if (sprite == null) return;

            var owner = new GameObject($"{role} · {index}");
            owner.transform.SetParent(architectureDecor, false);
            owner.transform.position = anchor;

            var visual = new GameObject("Projected Architecture");
            visual.transform.SetParent(owner.transform, false);
            ArchitectureSpriteLibrary.Placement(profile.Id, role, sprite, out var moduleScale,
                out var moduleOffset);
            visual.transform.localPosition = reverseTravel ? Vector3.zero : (Vector3)(moduleOffset * scale);
            visual.transform.localScale = new Vector3(
                (horizontalScale > 0f ? horizontalScale : scale) * moduleScale.x,
                scale * moduleScale.y, 1f);
            // Reversing travel is a 180-degree rotation of the complete isometric construction.
            // The imported sprite has a bottom-left pivot, so preserve its visible centre while
            // rotating; otherwise the staircase jumps several cells away from the threshold.
            if (reverseTravel)
            {
                visual.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }

            // Multi-sprite architecture was imported with a bottom-left pivot. A centred runtime
            // view of the same texture rect lets the reversed preset rotate around its actual
            // construction rather than around an arbitrary canvas corner.
            var renderSprite = sprite;
            if (reverseTravel)
            {
                renderSprite = Sprite.Create(sprite.texture, sprite.rect, new Vector2(.5f, .5f),
                    sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
                renderSprite.name = sprite.name + " · centered reverse";
                runtimeSprites.Add(renderSprite);
            }
            else if (endpointTrim != 0)
            {
                var sourceTrim = flipX ? -endpointTrim : endpointTrim;
                var rect = sprite.rect;
                var pivotPixels = new Vector2(sprite.pivot.x, sprite.pivot.y);
                if (sourceTrim < 0)
                {
                    rect.xMin += 18f;
                    pivotPixels.x -= 18f;
                }
                else rect.xMax -= 18f;
                var normalizedPivot = new Vector2(pivotPixels.x / rect.width, pivotPixels.y / rect.height);
                renderSprite = Sprite.Create(sprite.texture, rect, normalizedPivot,
                    sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
                renderSprite.name = sprite.name + " · trimmed corner shoulder";
                runtimeSprites.Add(renderSprite);
            }

            // Authored sprites already contain their own material shading. A restrained unlit pass
            // keeps carved detail legible in the global darkness; the lit pass still receives local
            // torches and player light.
            var readability = visual.AddComponent<SpriteRenderer>();
            readability.sprite = renderSprite;
            readability.flipX = flipX;
            var variantTint = ArchitectureVariantTint(visualVariant);
            readability.color = new Color(.72f * variantTint.r, .72f * variantTint.g,
                .72f * variantTint.b, .30f);
            readability.sortingOrder = 0;
            DarkfallRenderMaterials.MakeEmissive(readability);

            var litObject = new GameObject("Local Light Pass");
            litObject.transform.SetParent(visual.transform, false);
            var lit = litObject.AddComponent<SpriteRenderer>();
            lit.sprite = renderSprite;
            lit.flipX = flipX;
            lit.color = variantTint;
            lit.sortingOrder = 1;
            DarkfallRenderMaterials.MakeLit(lit);

            // Architecture stays on one depth system so the stair remains joined to both platform
            // lips. Traversing actors receive their temporary stair-depth boost in IsoVisual.
            visual.AddComponent<IsoVisual>().Initialize(owner.transform, elevation, 1002, false);
            visual.AddComponent<ArchitectureOcclusionFade>().Initialize(elevation);
        }

        private static Color ArchitectureVariantTint(int variant)
        {
            return (variant % 3) switch
            {
                1 => new Color(.91f, .86f, .78f, 1f),
                2 => new Color(.78f, .83f, .87f, 1f),
                _ => Color.white
            };
        }

        private static Color RoomMaterialTint(DungeonData data, int x, int y, Color source)
        {
            DungeonRoomTheme theme = DungeonRoomTheme.None;
            foreach (var room in data.Rooms)
                if (room.bounds.Contains(new Vector2Int(x, y)))
                {
                    theme = room.theme;
                    break;
                }
            var accent = theme switch
            {
                DungeonRoomTheme.Shrine => new Color(.72f, .60f, .42f, source.a),
                DungeonRoomTheme.Reliquary => new Color(.55f, .49f, .39f, source.a),
                DungeonRoomTheme.Ossuary => new Color(.48f, .50f, .47f, source.a),
                DungeonRoomTheme.Ritual => new Color(.50f, .38f, .36f, source.a),
                DungeonRoomTheme.Arrival => new Color(.52f, .47f, .39f, source.a),
                DungeonRoomTheme.Exit => new Color(.43f, .44f, .47f, source.a),
                _ => source
            };
            return theme == DungeonRoomTheme.None ? source : Color.Lerp(source, accent, .115f);
        }

        private Material CreateTexturedMaterial(string path)
        {
            var material = new Material(DarkfallRenderMaterials.SpriteLit) { color = Color.white };
            var texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                material.mainTexture = texture;
            }
            materials.Add(material);
            return material;
        }

        private void AddContactShadow(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to)
        {
            var a = IsoWorld.Project(from);
            var b = IsoWorld.Project(to);
            var normal = Vector2.Perpendicular((b - a).normalized) * .026f;
            AddScreenQuad(v, t, c, uv, a - normal, b - normal, b + normal, a + normal,
                new Color(.018f, .016f, .014f, .34f), from, to);
        }

        private static void AddScreenQuad(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 a, Vector2 b, Vector2 d, Vector2 e, Color color, Vector2 logicalFrom, Vector2 logicalTo)
        {
            var index = v.Count;
            v.Add(a); v.Add(b); v.Add(d); v.Add(e);
            t.Add(index); t.Add(index + 2); t.Add(index + 1);
            t.Add(index); t.Add(index + 3); t.Add(index + 2);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            var length = Vector2.Distance(logicalFrom, logicalTo) * .16f;
            uv.Add(Vector2.zero); uv.Add(new Vector2(length, 0));
            uv.Add(new Vector2(length, .08f)); uv.Add(new Vector2(0, .08f));
        }

        public void Clear()
        {
            for (var i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in materials) Destroy(material);
            foreach (var texture in runtimeTextures) Destroy(texture);
            foreach (var sprite in runtimeSprites) Destroy(sprite);
            meshes.Clear();
            materials.Clear();
            runtimeTextures.Clear();
            runtimeSprites.Clear();
        }

        private void AddWallFace(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 from, Vector2 to, Color color)
        {
            var baseFrom = IsoWorld.Project(from);
            var baseTo = IsoWorld.Project(to);
            var height = profile.WallHeight;
            var plinth = height * .18f;
            var frieze = height * .78f;
            AddWallBand(v, t, c, uv, baseFrom, baseTo, 0f, plinth, color * .68f, from, to, 0f, .16f);
            AddWallBand(v, t, c, uv, baseFrom, baseTo, plinth, frieze, color, from, to, .16f, .72f);
            AddWallBand(v, t, c, uv, baseFrom, baseTo, frieze, height, color * 1.12f, from, to, .72f, .92f);

            var topFrom = baseFrom + Vector2.up * height;
            var topTo = baseTo + Vector2.up * height;
            var projected = topTo - topFrom;
            var crownOffset = Vector2.Perpendicular(projected.normalized) * .105f;
            if (crownOffset.y < 0f) crownOffset = -crownOffset;
            AddScreenQuad(v, t, c, uv, topFrom, topTo, topTo + crownOffset, topFrom + crownOffset,
                color * .92f, from, to);
        }

        private static void AddWallBand(List<Vector3> v, List<int> t, List<Color> c, List<Vector2> uv,
            Vector2 baseFrom, Vector2 baseTo, float bottom, float top, Color color,
            Vector2 logicalFrom, Vector2 logicalTo, float uvBottom, float uvTop)
        {
            var index = v.Count;
            var lowerFrom = baseFrom + Vector2.up * bottom;
            var lowerTo = baseTo + Vector2.up * bottom;
            var upperFrom = baseFrom + Vector2.up * top;
            var upperTo = baseTo + Vector2.up * top;
            v.Add(lowerFrom);
            v.Add(lowerTo);
            v.Add(upperTo);
            v.Add(upperFrom);
            t.Add(index); t.Add(index + 2); t.Add(index + 1);
            t.Add(index); t.Add(index + 3); t.Add(index + 2);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            var length = Vector2.Distance(logicalFrom, logicalTo) * .22f;
            uv.Add(new Vector2(0, uvBottom)); uv.Add(new Vector2(length, uvBottom));
            uv.Add(new Vector2(length, uvTop)); uv.Add(new Vector2(0, uvTop));
        }


        private void BuildDecor(DungeonData data)
        {
            for (var roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
            {
                var bounds = data.Rooms[roomIndex].bounds;
                var hash = ((bounds.x * 73856093) ^ (bounds.y * 19349663) ^
                            (roomIndex * 83492791) ^ (profile.Chapter * 297121507)) & int.MaxValue;
                if (roomIndex == 0)
                {
                    BuildArrivalDecor(data, bounds, hash);
                    continue;
                }
                if (roomIndex % profile.LightEveryRooms == 1)
                {
                    var lightProp = profile.Id == "ashen-catacombs" ? 2 : (roomIndex % 2 == 0 ? 0 : 8);
                    CreateProp(data, lightProp, new Vector2(bounds.xMin + 1.2f, bounds.yMax - 1.15f),
                        profile.Id == "ashen-catacombs" ? 1f : .72f, "Biome Light", false, lightDecor);
                }

                // Theme rooms are authored compositions, not circular noise. Most rooms remain
                // quiet; selected rooms reserve either a wall bay, a corner, or (rarely) a formal
                // central landmark. The profile only supplies biome art, so the spatial grammar is
                // identical for all five biomes.
                if (roomIndex >= data.Rooms.Count - 1 || bounds.width < 7 || bounds.height < 7 ||
                    hash % 100 >= Mathf.RoundToInt(46f * profile.DecorDensity))
                    continue;

                var theme = data.Rooms[roomIndex].theme;
                var primary = ThemePrimary(theme, hash);
                var formalCentre = theme == DungeonRoomTheme.Ritual || theme == DungeonRoomTheme.Reliquary ||
                                   theme == DungeonRoomTheme.Observatory || theme == DungeonRoomTheme.Forge;
                if (bounds.width >= 11 && bounds.height >= 10 &&
                    (formalCentre || (hash / 31) % 7 == 0))
                    BuildCentralTheme(data, bounds, hash, primary, theme);
                else if (theme == DungeonRoomTheme.Shrine || theme == DungeonRoomTheme.Armory ||
                         theme == DungeonRoomTheme.Cistern || (hash / 17 & 1) == 0)
                    BuildWallTheme(data, bounds, hash, primary, theme);
                else
                    BuildCornerTheme(data, bounds, hash, primary, theme);
            }
        }

        private void BuildBiomeEvents(DungeonData data)
        {
            var eventRoot = CreateGroup(structuralDecor, "Biome Events · " + profile.Id);
            var placed = 0;
            var fallbackRoom = -1;
            var fallbackArea = 0;
            for (var roomIndex = 1; roomIndex < data.Rooms.Count - 1; roomIndex++)
            {
                var room = data.Rooms[roomIndex];
                var bounds = room.bounds;
                if (bounds.width < 7 || bounds.height < 7) continue;
                if (bounds.width * bounds.height > fallbackArea)
                {
                    fallbackArea = bounds.width * bounds.height;
                    fallbackRoom = roomIndex;
                }
                var hash = ((bounds.x * 92837111) ^ (bounds.y * 689287499) ^
                            (roomIndex * 283923481) ^ (profile.Chapter * 104729)) & int.MaxValue;
                // Landmarks are rare enough to remain memorable, but every sufficiently large
                // dungeon receives several. Selection is semantic rather than a recoloured pool.
                if (hash % 100 >= Mathf.RoundToInt(44f * profile.DecorDensity)) continue;
                if (TryBuildBiomeEvent(data, eventRoot, roomIndex, hash)) placed++;
            }

            // A biome identity cannot disappear because all semantic rolls missed. Guarantee one
            // signature composition on every regular floor, while still obeying route clearance.
            if (placed == 0 && fallbackRoom >= 0)
                TryBuildBiomeEvent(data, eventRoot, fallbackRoom,
                    (fallbackRoom * 283923481 ^ profile.Chapter * 104729) & int.MaxValue);
        }

        private bool TryBuildBiomeEvent(DungeonData data, Transform eventRoot, int roomIndex, int hash)
        {
            var room = data.Rooms[roomIndex];
            var bounds = room.bounds;
            var index = BiomeEventIndex(room.theme, hash);
            var center = (Vector2)bounds.center;
            var offset = new Vector2(((hash / 7) % 3 - 1) * .7f, ((hash / 19) % 3 - 1) * .55f);
            var position = center + offset;
            if (ThemeClearance(data, bounds, position, false) < 2.45f) return false;
            var eventScale = index >= 6 ? 1.24f + hash % 4 * .06f : .88f + hash % 4 * .045f;
            var footprintRadius = index >= 6 ? eventScale * .72f : eventScale * .58f;
            if (!CanPlaceDecor(position, footprintRadius)) return false;
            if (!data.TryAddObstaclePreservingRoutes(position)) return false;
            var sprite = BiomeEventSpriteLibrary.Get(profile.Id, index);
            if (sprite == null) return false;

            var root = new GameObject("Biome Event · " + room.theme + " · " + index);
            root.transform.SetParent(eventRoot, false);
            root.transform.position = position;
            var visual = new GameObject("Projected Event");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * eventScale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            DarkfallRenderMaterials.MakeLit(renderer);
            visual.AddComponent<IsoVisual>().Initialize(root.transform, 0f, 1010);
            // Every gameplay-significant landmark must read as active before contact. Large
            // landmarks already breathed; extend the same restrained motion to hostile and
            // luminous small props instead of leaving damaging traps visually frozen.
            if (index >= 6 || IsLuminousBiomeEvent(index) || IsHostileBiomeEvent(index))
                visual.AddComponent<BiomeEventAnimator>().Initialize(profile.Id, index);
            var caster = visual.AddComponent<ShadowCaster2D>();
            caster.castsShadows = true;
            caster.selfShadows = false;
            caster.alphaCutoff = .2f;

            if (IsLuminousBiomeEvent(index))
                data.AddLightSource(position + new Vector2(0, .25f), profile.FireTint, 5.4f, .17f);
            if (IsHostileBiomeEvent(index))
                root.AddComponent<BiomeEventHazard>().Initialize(1.25f, 12f + profile.Chapter * .7f,
                    HazardKindForBiome(profile.Id));
            RegisterDecor(position, footprintRadius);
            BuildBiomeEventComposition(data, bounds, room.theme, position, hash, index);
            return true;
        }

        private void BuildBiomeEventComposition(DungeonData data, RectInt bounds, DungeonRoomTheme theme,
            Vector2 center, int hash, int eventIndex)
        {
            // A signature object is a room composition, not a collectible dropped in empty space.
            // Small non-blocking satellites give it history and function while preserving the
            // combat lanes and the route guarantees owned by the dungeon data.
            var forward = (hash & 1) == 0 ? Vector2.right : Vector2.up;
            if ((hash / 5 & 1) != 0) forward = -forward;
            var side = new Vector2(-forward.y, forward.x);
            var offsets = new[]
            {
                -forward * 1.72f - side * .78f,
                forward * 1.58f + side * .92f,
                side * 1.82f - forward * .28f
            };
            var pool = ThemeDetailPool(theme);
            var count = eventIndex >= 6 && bounds.width >= 9 && bounds.height >= 9 ? 3 : 2;
            for (var member = 0; member < count; member++)
            {
                var position = center + offsets[(member + hash / 17) % offsets.Length];
                if (ThemeClearance(data, bounds, position, false) < 1.45f) continue;
                var propIndex = pool[(hash / (member + 7) + eventIndex * 3 + member * 5) % pool.Length];
                CreateProp(data, propIndex, position, .42f + member % 2 * .08f,
                    "Event Satellite · " + theme, false, clutterDecor);
            }
        }

        private int BiomeEventIndex(DungeonRoomTheme theme, int hash)
        {
            switch (profile.Id)
            {
                case "ember-vaults":
                    if (theme == DungeonRoomTheme.Forge) return Pick(hash, 0, 1, 2, 5, 6, 8, 10);
                    if (theme == DungeonRoomTheme.Ritual) return Pick(hash, 3, 4, 7, 9, 11);
                    if (theme == DungeonRoomTheme.Armory) return Pick(hash, 1, 5, 6, 10, 11);
                    return Pick(hash, 0, 2, 3, 4, 7, 8, 9);
                case "drowned-crypt":
                    if (theme == DungeonRoomTheme.Cistern) return Pick(hash, 1, 4, 5, 6, 7, 8, 10);
                    if (theme == DungeonRoomTheme.Shrine) return Pick(hash, 0, 3, 9, 11);
                    if (theme == DungeonRoomTheme.Ritual) return Pick(hash, 4, 8, 9, 11);
                    if (theme == DungeonRoomTheme.Reliquary) return Pick(hash, 2, 3, 6, 11);
                    return Pick(hash, 0, 1, 2, 5, 7, 10);
                case "charnel-gardens":
                    if (theme == DungeonRoomTheme.Garden) return Pick(hash, 0, 1, 2, 3, 4, 6, 7, 8, 9, 11);
                    if (theme == DungeonRoomTheme.Ritual) return Pick(hash, 5, 8, 10, 11);
                    if (theme == DungeonRoomTheme.Ossuary) return Pick(hash, 5, 7, 10);
                    if (theme == DungeonRoomTheme.Shrine) return Pick(hash, 0, 4, 6, 10);
                    return Pick(hash, 1, 2, 3, 6, 9, 11);
                case "obsidian-sanctum":
                    if (theme == DungeonRoomTheme.Observatory) return Pick(hash, 0, 1, 6, 7, 8, 9, 11);
                    if (theme == DungeonRoomTheme.Ritual) return Pick(hash, 2, 3, 4, 7, 9, 10);
                    if (theme == DungeonRoomTheme.Reliquary) return Pick(hash, 1, 5, 8, 10);
                    return Pick(hash, 2, 5, 6, 8, 9, 11);
                default:
                    if (theme == DungeonRoomTheme.Ossuary) return Pick(hash, 0, 2, 6, 7);
                    if (theme == DungeonRoomTheme.Ritual) return Pick(hash, 1, 3, 8, 10);
                    if (theme == DungeonRoomTheme.Shrine) return Pick(hash, 4, 6, 10, 11);
                    if (theme == DungeonRoomTheme.Reliquary) return Pick(hash, 5, 9, 11);
                    return Pick(hash, 0, 2, 5, 7, 9);
            }
        }

        private static int Pick(int hash, params int[] choices)
        {
            return choices[(hash / 11) % choices.Length];
        }

        private bool IsLuminousBiomeEvent(int index)
        {
            if (profile.Id == "ember-vaults") return index == 2 || index == 3 || index == 4 || index >= 6;
            if (profile.Id == "drowned-crypt") return index == 3 || index == 5 || index == 7 || index == 9;
            if (profile.Id == "charnel-gardens") return index == 0 || index == 3 || index == 4 || index == 6 || index == 9;
            if (profile.Id == "obsidian-sanctum") return index != 1 && index != 7;
            return index == 1 || index == 3 || index == 8 || index == 10;
        }

        private bool IsHostileBiomeEvent(int index)
        {
            if (profile.Id == "ember-vaults") return index == 3 || index == 4 || index == 9 || index == 11;
            if (profile.Id == "drowned-crypt") return index == 4 || index == 8;
            if (profile.Id == "charnel-gardens") return index == 1 || index == 4 || index == 8 || index == 11;
            if (profile.Id == "obsidian-sanctum") return index == 3 || index == 4 || index == 7 || index == 9 || index == 10;
            return index == 3 || index == 11;
        }

        private static DungeonHazardKind HazardKindForBiome(string biome)
        {
            switch (biome)
            {
                case "ember-vaults": return DungeonHazardKind.Lava;
                case "drowned-crypt": return DungeonHazardKind.Brine;
                case "charnel-gardens": return DungeonHazardKind.Bile;
                case "obsidian-sanctum": return DungeonHazardKind.VoidRift;
                default: return DungeonHazardKind.EmberSeep;
            }
        }

        private int ThemePrimary(DungeonRoomTheme theme, int hash)
        {
            if (profile.Id == "ashen-catacombs")
            {
                switch (theme)
                {
                    case DungeonRoomTheme.Shrine: return (hash & 1) == 0 ? 1 : 8;
                    case DungeonRoomTheme.Reliquary: return (hash & 1) == 0 ? 8 : 10;
                    case DungeonRoomTheme.Ossuary: return 5;
                    case DungeonRoomTheme.Armory: return (hash & 1) == 0 ? 4 : 7;
                    case DungeonRoomTheme.Ritual: return (hash & 1) == 0 ? 11 : 8;
                    default: return profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
                }
            }
            switch (theme)
            {
                case DungeonRoomTheme.Shrine: return 7;
                case DungeonRoomTheme.Reliquary: return (hash & 1) == 0 ? 2 : 5;
                case DungeonRoomTheme.Ossuary: return (hash & 1) == 0 ? 1 : 6;
                case DungeonRoomTheme.Armory: return (hash & 1) == 0 ? 4 : 5;
                case DungeonRoomTheme.Ritual: return (hash & 1) == 0 ? 8 : 10;
                case DungeonRoomTheme.Cistern: return (hash & 1) == 0 ? 1 : 8;
                case DungeonRoomTheme.Forge: return (hash & 1) == 0 ? 0 : 6;
                case DungeonRoomTheme.Garden: return (hash & 1) == 0 ? 1 : 7;
                case DungeonRoomTheme.Observatory: return (hash & 1) == 0 ? 8 : 10;
                default: return profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
            }
        }

        private struct ThemeBay
        {
            public Vector2 Anchor;
            public Vector2 Tangent;
            public Vector2 Inward;

            public ThemeBay(Vector2 anchor, Vector2 tangent, Vector2 inward)
            {
                Anchor = anchor;
                Tangent = tangent;
                Inward = inward;
            }
        }

        private void BuildWallTheme(DungeonData data, RectInt bounds, int hash, int primary,
            DungeonRoomTheme theme)
        {
            if (!TryChooseWallBay(data, bounds, hash, out var bay)) return;
            if (!CreateProp(data, primary, bay.Anchor, .82f + hash % 3 * .06f,
                    "Wall Theme · Primary", true, structuralDecor)) return;

            // A readable triptych: two companions follow the wall and an offering sits in front.
            // Details are subordinate to the primary and are never spawned if it failed.
            PlaceThemeDetail(data, bounds, hash, bay.Anchor - bay.Tangent * 1.35f, 0, theme);
            PlaceThemeDetail(data, bounds, hash, bay.Anchor + bay.Tangent * 1.35f, 1, theme);
            if ((hash / 13) % 3 != 0)
                PlaceThemeDetail(data, bounds, hash, bay.Anchor + bay.Inward * 1.05f, 2, theme);
        }

        private void BuildCornerTheme(DungeonData data, RectInt bounds, int hash, int primary,
            DungeonRoomTheme theme)
        {
            var corners = new[]
            {
                new ThemeBay(new Vector2(bounds.xMin + 1.35f, bounds.yMin + 1.35f), Vector2.right, Vector2.up),
                new ThemeBay(new Vector2(bounds.xMax - 1.35f, bounds.yMin + 1.35f), Vector2.left, Vector2.up),
                new ThemeBay(new Vector2(bounds.xMax - 1.35f, bounds.yMax - 1.35f), Vector2.left, Vector2.down),
                new ThemeBay(new Vector2(bounds.xMin + 1.35f, bounds.yMax - 1.35f), Vector2.right, Vector2.down)
            };
            ThemeBay chosen = default;
            var bestScore = float.MinValue;
            for (var offset = 0; offset < corners.Length; offset++)
            {
                var candidate = corners[(hash + offset) % corners.Length];
                var score = ThemeClearance(data, bounds, candidate.Anchor, true);
                if (score <= bestScore) continue;
                bestScore = score;
                chosen = candidate;
            }
            if (bestScore < 1.85f || !CreateProp(data, primary, chosen.Anchor, .78f,
                    "Corner Theme · Primary", true, structuralDecor)) return;
            PlaceThemeDetail(data, bounds, hash, chosen.Anchor + chosen.Tangent * 1.2f, 0, theme);
            PlaceThemeDetail(data, bounds, hash, chosen.Anchor + chosen.Inward * 1.2f, 1, theme);
        }

        private void BuildCentralTheme(DungeonData data, RectInt bounds, int hash, int primary,
            DungeonRoomTheme theme)
        {
            var center = (Vector2)bounds.center;
            if (ThemeClearance(data, bounds, center, false) < 2.5f ||
                !CreateProp(data, primary, center, .9f, "Central Theme · Primary", true, structuralDecor))
                return;

            // Deliberate axial arrangement, leaving broad diagonal combat lanes around it.
            var axis = (hash & 1) == 0 ? Vector2.right : Vector2.up;
            PlaceThemeDetail(data, bounds, hash, center - axis * 1.65f, 0, theme);
            PlaceThemeDetail(data, bounds, hash, center + axis * 1.65f, 1, theme);
            if (bounds.width >= 13 && bounds.height >= 12)
            {
                var cross = new Vector2(-axis.y, axis.x);
                PlaceThemeDetail(data, bounds, hash, center - cross * 1.65f, 2, theme);
                PlaceThemeDetail(data, bounds, hash, center + cross * 1.65f, 3, theme);
            }
        }

        private bool TryChooseWallBay(DungeonData data, RectInt bounds, int hash, out ThemeBay chosen)
        {
            var candidates = new[]
            {
                new ThemeBay(new Vector2(bounds.center.x, bounds.yMax - 1.25f), Vector2.right, Vector2.down),
                new ThemeBay(new Vector2(bounds.xMax - 1.25f, bounds.center.y), Vector2.up, Vector2.left),
                new ThemeBay(new Vector2(bounds.center.x, bounds.yMin + 1.25f), Vector2.right, Vector2.up),
                new ThemeBay(new Vector2(bounds.xMin + 1.25f, bounds.center.y), Vector2.up, Vector2.right)
            };
            chosen = default;
            var bestScore = float.MinValue;
            for (var offset = 0; offset < candidates.Length; offset++)
            {
                var candidate = candidates[(hash + offset) % candidates.Length];
                var score = ThemeClearance(data, bounds, candidate.Anchor, true);
                score = Mathf.Min(score, ThemeClearance(data, bounds,
                    candidate.Anchor - candidate.Tangent * 1.35f, true));
                score = Mathf.Min(score, ThemeClearance(data, bounds,
                    candidate.Anchor + candidate.Tangent * 1.35f, true));
                if (score <= bestScore) continue;
                bestScore = score;
                chosen = candidate;
            }
            return bestScore >= 1.85f;
        }

        private float ThemeClearance(DungeonData data, RectInt bounds, Vector2 position, bool keepCenterClear)
        {
            if (position.x < bounds.xMin + .8f || position.x > bounds.xMax - .8f ||
                position.y < bounds.yMin + .8f || position.y > bounds.yMax - .8f ||
                !data.IsFloor(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))) return -1f;
            if (keepCenterClear && Vector2.Distance(position, bounds.center) < 2.15f) return -1f;
            var clearance = Mathf.Min(Vector2.Distance(position, data.CellCenter(data.StartCell)),
                Vector2.Distance(position, data.CellCenter(data.ExitCell)));
            foreach (var feature in data.Architecture)
                clearance = Mathf.Min(clearance, Vector2.Distance(position, feature.Position));
            return clearance;
        }

        private void PlaceThemeDetail(DungeonData data, RectInt bounds, int hash, Vector2 position, int member,
            DungeonRoomTheme theme)
        {
            if (ThemeClearance(data, bounds, position, false) < 1.65f) return;
            var pool = ThemeDetailPool(theme);
            var propIndex = pool[(hash / (member + 3) + member * 5) % pool.Length];
            CreateProp(data, propIndex, position, .48f + member % 2 * .08f,
                "Theme Detail · " + theme, false, clutterDecor);
        }

        private int[] ThemeDetailPool(DungeonRoomTheme theme)
        {
            if (profile.Id == "ashen-catacombs")
            {
                switch (theme)
                {
                    case DungeonRoomTheme.Ossuary: return new[] { 5, 6, 7 };
                    case DungeonRoomTheme.Armory: return new[] { 3, 4, 7 };
                    case DungeonRoomTheme.Ritual: return new[] { 2, 5, 9 };
                    case DungeonRoomTheme.Shrine: return new[] { 2, 5, 6 };
                    default: return profile.ClutterProps;
                }
            }
            switch (theme)
            {
                case DungeonRoomTheme.Shrine: return new[] { 0, 2, 9 };
                case DungeonRoomTheme.Reliquary: return new[] { 2, 3, 9 };
                case DungeonRoomTheme.Ossuary: return new[] { 1, 6, 11 };
                case DungeonRoomTheme.Armory: return new[] { 4, 5, 11 };
                case DungeonRoomTheme.Ritual: return new[] { 0, 3, 9, 10 };
                case DungeonRoomTheme.Cistern: return new[] { 0, 2, 6, 9 };
                case DungeonRoomTheme.Forge: return new[] { 0, 4, 6, 11 };
                case DungeonRoomTheme.Garden: return new[] { 0, 3, 9, 11 };
                case DungeonRoomTheme.Observatory: return new[] { 3, 8, 9, 10 };
                default: return profile.ClutterProps;
            }
        }

        private void BuildArrivalDecor(DungeonData data, RectInt bounds, int hash)
        {
            // The arrival room is a deliberately quiet miniset: recognizable biome dressing,
            // enough light to read the only door, and no blocking altar or random clutter in the
            // player's first steps. Enemy spawning already excludes room zero.
            var center = (Vector2)bounds.center;
            var doorPosition = center + Vector2.down;
            foreach (var feature in data.Architecture)
                if (feature.Kind == DungeonArchitectureKind.ClosedDoor &&
                    feature.Position.x >= bounds.xMin - .1f && feature.Position.x <= bounds.xMax + .1f &&
                    feature.Position.y >= bounds.yMin - .1f && feature.Position.y <= bounds.yMax + .1f)
                {
                    doorPosition = feature.Position;
                    break;
                }
            var doorDelta = doorPosition - center;
            var shrinePosition = center;
            if (Mathf.Abs(doorDelta.x) >= Mathf.Abs(doorDelta.y))
                shrinePosition.x = doorDelta.x >= 0f ? bounds.xMin + 1.15f : bounds.xMax - 1.15f;
            else
                shrinePosition.y = doorDelta.y >= 0f ? bounds.yMin + 1.15f : bounds.yMax - 1.15f;
            var shrineIndex = profile.StructuralProps[(hash / 7) % profile.StructuralProps.Length];
            CreateProp(data, shrineIndex, shrinePosition, .72f,
                "Arrival Shrine · " + profile.Id, false, structuralDecor);

            var lightProp = profile.Id == "ashen-catacombs" ? 2 : ((hash & 1) == 0 ? 0 : 8);
            CreateProp(data, lightProp, new Vector2(bounds.xMin + 1.05f, bounds.yMin + 1.05f),
                profile.Id == "ashen-catacombs" ? .76f : .62f, "Arrival Vigil", false, lightDecor);

            var offeringIndex = profile.ClutterProps[(hash / 11) % profile.ClutterProps.Length];
            CreateProp(data, offeringIndex, new Vector2(bounds.xMax - 1.05f, bounds.yMin + 1.05f), .5f,
                "Arrival Offering", false, clutterDecor);
        }

        private bool CreateProp(DungeonData data, int index, Vector2 position, float scale, string objectName, bool blocks,
            Transform group)
        {
            var cell = Vector2Int.FloorToInt(position);
            if (!data.IsFloor(cell.x, cell.y)) return false;
            if (data.IsHazardCell(cell.x, cell.y)) return false;
            var authoredSetPiece = objectName.StartsWith("Wall Theme", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Corner Theme", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Central Theme", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Theme Detail", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Biome Event", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Event Satellite", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Set Piece", System.StringComparison.Ordinal) ||
                                   objectName.StartsWith("Mini Set", System.StringComparison.Ordinal);
            if (data.HasSemantic(cell, DungeonCellSemantic.EventReserved) && !authoredSetPiece) return false;
            foreach (var feature in data.Architecture)
                if (Vector2.Distance(position, feature.Position) < 1.6f) return false;
            if (!authoredSetPiece &&
                (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 1.25f ||
                 Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 1.25f)) return false;
            if (blocks && (Vector2.Distance(position, data.CellCenter(data.StartCell)) < 2f ||
                           Vector2.Distance(position, data.CellCenter(data.ExitCell)) < 2f))
                blocks = false;
            var footprintRadius = DecorRadius(objectName, scale, blocks);
            if (!CanPlaceDecor(position, footprintRadius)) return false;
            if (blocks && !data.TryAddObstaclePreservingRoutes(position)) return false;
            var prop = new GameObject(objectName + " " + index);
            prop.transform.SetParent(group, false);
            prop.transform.position = position;
            if (profile.Id != "ashen-catacombs" && (index == 0 || index == 8)) scale = .72f;
            var visual = new GameObject("Projected Prop");
            visual.transform.SetParent(prop.transform, false);
            visual.transform.localScale = Vector3.one * scale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = EnvironmentSpriteAtlas.Prop(profile.Id, index);
            renderer.color = Color.white;
            DarkfallRenderMaterials.MakeLit(renderer);
            visual.AddComponent<IsoVisual>().Initialize(prop.transform, 0f, 1000);
            if (blocks)
            {
                var caster = visual.AddComponent<ShadowCaster2D>();
                caster.castsShadows = true;
                caster.selfShadows = false;
                caster.alphaCutoff = .22f;
            }
            var customBiomeDecor = profile.Id != "ashen-catacombs";
            if ((!customBiomeDecor && index == 2) || (customBiomeDecor && index == 0))
            {
                AddFlame(visual.transform, new Vector2(0, .24f), .56f, 9);
                data.AddLightSource(position + new Vector2(0, .22f), profile.FireTint, 5.8f, .16f);
            }
            else if (customBiomeDecor && index == 3)
            {
                data.AddLightSource(position + new Vector2(0, .28f), profile.FireTint, 4.6f, .13f);
            }
            else if (customBiomeDecor && index == 8)
            {
                data.AddLightSource(position + new Vector2(0, .18f), profile.FireTint, 4.8f, .14f);
            }
            else if (index == 8)
            {
                AddFlame(visual.transform, new Vector2(-.37f, .31f), .105f, 9);
                AddFlame(visual.transform, new Vector2(.19f, .29f), .10f, 9);
                AddFlame(visual.transform, new Vector2(.37f, .13f), .09f, 9);
                data.AddLightSource(position + new Vector2(0, .2f), profile.FireTint * new Color(1, 1, 1, .62f), 3.6f, .1f);
            }
            RegisterDecor(position, footprintRadius);
            return true;
        }

        private static float DecorRadius(string objectName, float scale, bool blocks)
        {
            if (objectName.StartsWith("Central Theme", System.StringComparison.Ordinal))
                return Mathf.Max(.72f, scale * .78f);
            if (objectName.StartsWith("Wall Theme", System.StringComparison.Ordinal) ||
                objectName.StartsWith("Corner Theme", System.StringComparison.Ordinal))
                return Mathf.Max(.62f, scale * .7f);
            if (objectName.StartsWith("Set Piece", System.StringComparison.Ordinal))
                return blocks ? Mathf.Max(.55f, scale * .68f) : Mathf.Max(.28f, scale * .48f);
            if (objectName.StartsWith("Theme Detail", System.StringComparison.Ordinal) ||
                objectName.StartsWith("Event Satellite", System.StringComparison.Ordinal))
                return Mathf.Max(.27f, scale * .48f);
            if (objectName.Contains("Light", System.StringComparison.Ordinal) ||
                objectName.Contains("Vigil", System.StringComparison.Ordinal))
                return Mathf.Max(.4f, scale * .52f);
            return blocks ? Mathf.Max(.54f, scale * .66f) : Mathf.Max(.3f, scale * .48f);
        }

        private bool CanPlaceDecor(Vector2 position, float radius)
        {
            foreach (var footprint in decorFootprints)
                if (Vector2.Distance(position, footprint.Position) < radius + footprint.Radius)
                    return false;
            return true;
        }

        private void RegisterDecor(Vector2 position, float radius)
        {
            decorFootprints.Add(new DecorFootprint(position, radius));
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void AddFlame(Transform parent, Vector2 localPosition, float scale, int sortingOrder)
        {
            var flame = new GameObject("Animated Flame");
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = localPosition;
            flame.transform.localScale = Vector3.one * scale;
            flame.AddComponent<DungeonFlameAnimator>().Initialize(sortingOrder);
        }

        private void BuildContourShadows(DungeonContour contour)
        {
            var root = new GameObject("Smoothed Isometric Shadows");
            root.transform.SetParent(transform, false);
            root.AddComponent<CompositeShadowCaster2D>();
            foreach (var segment in contour.Segments)
                CreateShadowEdge(root.transform, segment.From, segment.To);
        }

        private static void CreateShadowEdge(Transform parent, Vector2 from, Vector2 to)
        {
            // A 2D caster has no real Z dimension. Raising it halfway up the facade detached the
            // cast shadow from the plinth. Keep it almost on the floor so the shadow begins at the
            // contact line and reads as weight, not as an air gap.
            var a = IsoWorld.Project(from) + Vector2.up * .012f;
            var b = IsoWorld.Project(to) + Vector2.up * .012f;
            var delta = b - a;
            var shadow = new GameObject("Wall Shadow Edge");
            shadow.transform.SetParent(parent, false);
            shadow.transform.position = (a + b) * .5f;
            shadow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // A thin occlusion edge preserves Nox-like local-light blocking without projecting the
            // broad detached black band that made authored wall plinths appear to float.
            shadow.transform.localScale = new Vector3(delta.magnitude, .035f, 1);
            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeAssets.Square;
            renderer.color = Color.clear;
            renderer.sortingOrder = -100;
            DarkfallRenderMaterials.MakeEmissive(renderer);
            var caster = shadow.AddComponent<ShadowCaster2D>();
            caster.castsShadows = true;
            caster.selfShadows = false;
            caster.alphaCutoff = .01f;
        }

        private Mesh MakeMesh(string name, List<Vector3> vertices, List<int> triangles, List<Color> colors, List<Vector2> uvs)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        private GameObject CreateLayer(string name, Mesh mesh, Material material, int sortingOrder)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(transform, false);
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return layer;
        }

        private static float RandomTint(int x, int y)
        {
            var hash = (x * 73856093) ^ (y * 19349663);
            return .90f + Mathf.Abs(hash % 13) / 100f;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
            }
            return (value & 0x7fffffff) / (float)int.MaxValue;
        }

    }
}
