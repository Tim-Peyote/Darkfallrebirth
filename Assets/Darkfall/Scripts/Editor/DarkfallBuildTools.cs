#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Darkfall.Core;
using Darkfall.Gameplay;
using Darkfall.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Darkfall.Editor
{
    public static class DarkfallBuildTools
    {
        private const string MainScene = "Assets/Scenes/Main.unity";

        [MenuItem("Darkfall/Validate Project")]
        public static void ValidateProject()
        {
            var failures = 0;
            failures += Require(File.Exists(MainScene), "Main scene is missing");
            failures += Require(Resources.Load<Texture2D>("Art/Main") != null, "Main menu art is missing");
            failures += Require(Resources.Load<Texture2D>("Art/shop-sanctuary") != null, "Unique shop sanctuary art is missing");
            failures += Require(Resources.Load<AudioClip>("Audio/Main") != null, "Main music is missing");
            for (var biomeTrack = 1; biomeTrack <= 5; biomeTrack++)
                failures += Require(Resources.Load<AudioClip>($"Audio/{biomeTrack}") != null,
                    $"Biome music is missing: {biomeTrack}");
            failures += Require(Resources.Load<AudioClip>("Audio/boss") != null, "Boss music is missing");
            foreach (var tavernTrack in new[] { "tavern", "tavern2", "tavern3" })
                failures += Require(Resources.Load<AudioClip>($"Audio/{tavernTrack}") != null,
                    $"Tavern music is missing: {tavernTrack}");
            foreach (var effect in new[]
                     {
                         "sword", "Dagger", "Fireball", "enemy_hit", "enemy_die", "heroes_hit",
                         "Heroes_die", "explosion", "Dash", "Armor", "health_potion", "item_pickup",
                         "Inventory_open"
                     })
                failures += Require(Resources.Load<AudioClip>($"Audio/Fx/{effect}") != null,
                    $"Gameplay audio effect is missing: {effect}");
            failures += Require(Resources.Load<Font>("Fonts/PTSans-Regular") != null, "Body UI font is missing");
            failures += Require(Resources.Load<Font>("Fonts/CormorantGaramond") != null, "Heading UI font is missing");
            failures += ValidateHeroFrames("mage");
            failures += ValidateHeroFrames("warrior");
            failures += ValidateHeroFrames("rogue");
            failures += ValidateDirectionalSheet("enemy-melee-v2", "Melee enemy");
            failures += ValidateDirectionalSheet("enemy-ranged-v2", "Ranged enemy");
            failures += ValidateDirectionalSheet("enemy-caster-v2", "Caster enemy");
            failures += ValidateDirectionalSheet("enemy-mimic-v1", "Mimic");
            failures += Require(TreasureChest.MimicChance > 0f && TreasureChest.MimicChance <= .03f,
                "Mimic encounter chance must remain rare");
            failures += Require(Resources.Load<Texture2D>("Textures/dungeon-floor-v2") != null, "Dungeon floor v2 texture is missing");
            failures += Require(Resources.Load<Texture2D>("Textures/dungeon-wall-v2") != null, "Dungeon wall v2 texture is missing");
            var biomeTextures = new[] { "ember", "drowned", "charnel", "obsidian" };
            foreach (var biome in biomeTextures)
            {
                failures += Require(Resources.Load<Texture2D>($"Textures/Biomes/{biome}-floor") != null,
                    $"Biome floor texture is missing: {biome}");
                failures += Require(Resources.Load<Texture2D>($"Textures/Biomes/{biome}-wall") != null,
                    $"Biome wall texture is missing: {biome}");
            }
            failures += Require(Resources.Load<Texture2D>("Sprites/Environment/dungeon-props-v2") != null, "Dungeon prop atlas is missing");
            for (var prop = 0; prop < 12; prop++)
                failures += Require(Resources.Load<Texture2D>($"Sprites/Environment/Props/prop-{prop}") != null,
                    $"Individual environment prop is missing: {prop}");
            for (var frame = 0; frame < 4; frame++)
                failures += Require(Resources.Load<Texture2D>($"Sprites/Environment/Flames/flame-{frame}") != null,
                    $"Individual flame frame is missing: {frame}");
            failures += Require(DungeonVisualProfile.ForDepth(1).Id != DungeonVisualProfile.ForDepth(11).Id,
                "Biome profile must change after depth 10");
            failures += Require(DungeonVisualProfile.ForDepth(11).Id != DungeonVisualProfile.ForDepth(21).Id &&
                                DungeonVisualProfile.ForDepth(21).Id != DungeonVisualProfile.ForDepth(31).Id &&
                                DungeonVisualProfile.ForDepth(31).Id != DungeonVisualProfile.ForDepth(41).Id,
                "Each post-boss chapter through depth 50 must use a distinct biome");
            failures += Require(LegacyCatalog.Data.characters?.Length == 3, "Legacy parity: expected 3 heroes");
            failures += Require(LegacyCatalog.Data.enemies?.Length == 17, "Expected 12 shared and 5 biome enemy types");
            var biomeAssets = new[] { "ember", "drowned", "charnel", "obsidian" };
            foreach (var biome in biomeAssets)
            {
                failures += Require(Resources.Load<Texture2D>($"Sprites/Environment/Biomes/{biome}-decor") != null,
                    $"Biome decor atlas is missing: {biome}");
                for (var prop = 0; prop < 12; prop++)
                    failures += Require(Resources.Load<Texture2D>(
                            $"Sprites/Environment/Biomes/{biome}/decor-{prop:00}") != null,
                        $"Replaceable biome decor module is missing: {biome}/{prop:00}");
            }
            var architectureBiomes = new[]
            {
                "ashen-catacombs", "ember-vaults", "drowned-crypt", "charnel-gardens", "obsidian-sanctum"
            };
            var architectureModules = new[]
            {
                "wall-left", "wall-right", "corner-outer", "corner-inner", "arch-open", "door-closed",
                "wall-broken", "wall-niche", "column", "arcade", "stairs", "landmark"
            };
            foreach (var biome in architectureBiomes)
            {
                failures += Require(ArchitectureSpriteLibrary.HasBiome(biome),
                    $"Architecture pipeline cannot load biome: {biome}");
                foreach (var hazardModule in new[]
                         { "straight", "corner", "end", "tee", "isolated", "bridge", "body-4way" })
                    failures += Require(Resources.Load<Texture2D>(
                            $"Sprites/Environment/Hazards/{biome}/{hazardModule}-01") != null,
                        $"Authored hazard module is missing: {biome}/{hazardModule}");
                for (var eventIndex = 0; eventIndex < 6; eventIndex++)
                    failures += Require(Resources.Load<Texture2D>(
                            $"Sprites/Environment/Events/{biome}/event-{eventIndex:00}") != null,
                        $"Biome event module is missing: {biome}/{eventIndex:00}");
            foreach (var module in architectureModules)
                failures += Require(Resources.Load<Texture2D>(
                        $"Sprites/Environment/Architecture/{biome}/{module}-01") != null,
                    $"Architecture module is missing: {biome}/{module}");
            }
            var biomeEnemySheets = new[]
            {
                "enemy-ash-warden-v1", "enemy-ember-revenant-v1", "enemy-drowned-sentinel-v1",
                "enemy-spore-stalker-v1", "enemy-obsidian-acolyte-v1"
            };
            foreach (var sheet in biomeEnemySheets)
                failures += ValidateDirectionalSheet(sheet, "Biome enemy");
            failures += Require(LegacyCatalog.Data.bosses?.Length == 3, "Legacy parity: expected 3 bosses");
            failures += Require(LegacyCatalog.Data.items?.Length == 46, "Legacy parity: expected 46 base items");
            foreach (var item in LegacyCatalog.Items)
            {
                failures += Require(ItemSpriteAtlas.HasMapping(item.baseId),
                    $"Item art mapping is missing: {item.baseId}");
                failures += Require(Resources.Load<Texture2D>("Sprites/Items/Individual/" + item.baseId) != null,
                    $"Individual item sprite is missing: {item.baseId}");
            }
            failures += Require(Resources.Load<Texture2D>("Sprites/Items/Individual/gold_pouch") != null,
                "Individual gold pouch sprite is missing");
            failures += Require(LegacyCatalog.Data.affixes?.Length == 9, "Legacy parity: expected 9 affixes");
            failures += Require(LegacyCatalog.Data.shop?.Length == 8, "Legacy parity: expected 8 shop upgrades");
            var inventory = new InventorySystem();
            failures += Require(inventory.Slots.Length == 42, "Legacy parity: backpack must contain 42 slots");
            failures += Require(inventory.Equipment.Length == 9, "Legacy parity: equipment must contain 9 slots");
            failures += Require(inventory.QuickSlots.Length == 3, "Legacy parity: expected 3 quick slots");
            var interactionInventory = new InventorySystem();
            interactionInventory.Slots[0] = new ItemInstance
                { id = "test_weapon", baseId = "test_weapon", name = "Test weapon", kind = ItemKind.Weapon };
            interactionInventory.Slots[1] = new ItemInstance
                { id = "test_potion", baseId = "test_potion", name = "Test potion", kind = ItemKind.Potion, quantity = 2 };
            failures += Require(interactionInventory.MoveBackpackToEquipment(0, 1, null),
                "Inventory interaction: backpack to equipment failed");
            failures += Require(interactionInventory.Equipment[1]?.id == "test_weapon" && interactionInventory.Slots[0] == null,
                "Inventory interaction: equipment target state is invalid");
            failures += Require(interactionInventory.MoveEquipmentToBackpack(1, 2),
                "Inventory interaction: equipment to backpack failed");
            failures += Require(interactionInventory.Slots[2]?.id == "test_weapon" && interactionInventory.Equipment[1] == null,
                "Inventory interaction: backpack target state is invalid");
            failures += Require(interactionInventory.AssignQuickSlot(1, 0) && interactionInventory.QuickSlots[0] == "test_potion",
                "Inventory interaction: quick slot assignment failed");
            interactionInventory.SwapBackpack(1, 3);
            failures += Require(interactionInventory.Slots[3]?.id == "test_potion",
                "Inventory interaction: backpack drag swap failed");

            var balance = GameBalance.RuntimeDefault();
            failures += Require(balance.baseEnemyCount == 12, "Progression: default starting enemy budget must be 12");
            var earlyDungeon = DungeonGenerator.Generate(balance, 1, 4242);
            var middleDungeon = DungeonGenerator.Generate(balance, 5, 4242);
            var lateDungeon = DungeonGenerator.Generate(balance, 9, 4242);
            failures += Require(earlyDungeon.Width < middleDungeon.Width && middleDungeon.Width < lateDungeon.Width,
                "Progression: regular dungeon dimensions must grow with depth");
            failures += Require(GameManager.EnemyBudgetForDepth(balance, 1) < GameManager.EnemyBudgetForDepth(balance, 5) &&
                                GameManager.EnemyBudgetForDepth(balance, 5) < GameManager.EnemyBudgetForDepth(balance, 9),
                "Progression: regular enemy budget must grow with depth");
            var generatedOptionalDoorCount = 0;
            var generatedStairFloors = 0;
            for (var seed = 0; seed < 100; seed++)
            {
                var generatedDepth = 1 + seed % 25;
                var dungeon = DungeonGenerator.Generate(balance, generatedDepth, seed);
                failures += Require(dungeon.Rooms.Count >= 2, $"Seed {seed}: insufficient rooms");
                failures += Require(dungeon.IsFloor(dungeon.StartCell.x, dungeon.StartCell.y), $"Seed {seed}: invalid start");
                failures += Require(dungeon.IsFloor(dungeon.ExitCell.x, dungeon.ExitCell.y), $"Seed {seed}: invalid exit");
                var internalStairs = 0;
                var doors = 0;
                var startDoors = 0;
                foreach (var feature in dungeon.Architecture)
                {
                    if (feature.Kind == DungeonArchitectureKind.ElevationStairs) internalStairs++;
                    if (feature.Kind == DungeonArchitectureKind.ClosedDoor)
                    {
                        doors++;
                        var startBounds = dungeon.Rooms[0].bounds;
                        if (feature.Position.x >= startBounds.xMin - .1f && feature.Position.x <= startBounds.xMax + .1f &&
                            feature.Position.y >= startBounds.yMin - .1f && feature.Position.y <= startBounds.yMax + .1f)
                            startDoors++;
                    }
                    var passageX = Mathf.FloorToInt(feature.Position.x);
                    var passageY = Mathf.FloorToInt(feature.Position.y);
                    var validThreshold = feature.Vertical
                        ? dungeon.IsFloor(passageX - 1, passageY) && dungeon.IsFloor(passageX, passageY)
                        : dungeon.IsFloor(passageX, passageY - 1) && dungeon.IsFloor(passageX, passageY);
                    failures += Require(validThreshold,
                        $"Seed {seed}: {feature.Kind} is not attached to a valid room passage");
                    var firstElevation = feature.Vertical
                        ? dungeon.ElevationLevel(passageX - 1, passageY)
                        : dungeon.ElevationLevel(passageX, passageY - 1);
                    var secondElevation = dungeon.ElevationLevel(passageX, passageY);
                    if (feature.Kind == DungeonArchitectureKind.ElevationStairs)
                    {
                        failures += Require(firstElevation != secondElevation,
                            $"Seed {seed}: internal stairs do not connect two elevations");
                        failures += Require(dungeon.CanOccupy(feature.Position, .18f),
                            $"Seed {seed}: internal stair center lane is not traversable");
                        var tangent = feature.Vertical ? Vector2.up : Vector2.right;
                        var cheek = feature.Position + tangent * (feature.Width * .5f - .12f);
                        failures += Require(!dungeon.CanOccupy(cheek, .18f),
                            $"Seed {seed}: actors can pass through a stair side wall");
                        var normal = feature.Vertical ? Vector2.right : Vector2.up;
                        var lowSide = feature.Position - normal * .55f;
                        var highSide = feature.Position + normal * .55f;
                        if (dungeon.SurfaceHeight(lowSide) > dungeon.SurfaceHeight(highSide))
                            (lowSide, highSide) = (highSide, lowSide);
                        failures += Require(dungeon.SurfaceHeight(highSide) - dungeon.SurfaceHeight(lowSide) > .35f,
                            $"Seed {seed}: raised platform is not visually distinct");
                        failures += Require(dungeon.CanTraverse(lowSide, highSide, .18f),
                            $"Seed {seed}: stair ramp does not connect its platform");
                        var outsideLane = lowSide + tangent * .75f;
                        var outsideHigh = highSide + tangent * .75f;
                        failures += Require(!dungeon.CanTraverse(outsideLane, outsideHigh, .18f),
                            $"Seed {seed}: platform edge is climbable outside the stair miniset");
                    }
                    else
                        failures += Require(firstElevation == secondElevation,
                            $"Seed {seed}: open gate incorrectly bridges an elevation change");
                }
                if (generatedDepth % 10 != 0 && internalStairs > 0) generatedStairFloors++;
                var expectedStartDoors = generatedDepth % 10 == 0 ? 0 : 1;
                failures += Require(startDoors == expectedStartDoors,
                    $"Seed {seed}: arrival room must have {expectedStartDoors} safety door(s) " +
                    $"(found {startDoors}, total {doors})");
                if (generatedDepth % 10 != 0)
                {
                    var startBounds = dungeon.Rooms[0].bounds;
                    var perimeterOpenings = 0;
                    for (var y = startBounds.yMin; y < startBounds.yMax; y++)
                    {
                        if (dungeon.IsFloor(startBounds.xMin - 1, y)) perimeterOpenings++;
                        if (dungeon.IsFloor(startBounds.xMax, y)) perimeterOpenings++;
                    }
                    for (var x = startBounds.xMin; x < startBounds.xMax; x++)
                    {
                        if (dungeon.IsFloor(x, startBounds.yMin - 1)) perimeterOpenings++;
                        if (dungeon.IsFloor(x, startBounds.yMax)) perimeterOpenings++;
                    }
                    failures += Require(perimeterOpenings == 2,
                        $"Seed {seed}: safety room perimeter has {perimeterOpenings} open cells instead of one two-cell door");
                }
                failures += Require(doors <= 2, $"Seed {seed}: doors must remain a rare threshold event");
                generatedOptionalDoorCount += doors - startDoors;
                failures += Require(dungeon.ElevationLevel(dungeon.ExitCell.x, dungeon.ExitCell.y) == 0,
                    $"Seed {seed}: level exit must not overlap a raised platform");
                var hazardCells = new HashSet<Vector2Int>();
                foreach (var hazard in dungeon.Hazards) hazardCells.Add(hazard.Cell);
                foreach (var hazard in dungeon.Hazards)
                {
                    failures += Require(dungeon.IsFloor(hazard.Cell.x, hazard.Cell.y),
                        $"Seed {seed}: hazard is outside carved floor");
                    failures += Require(hazard.Cell != dungeon.StartCell && hazard.Cell != dungeon.ExitCell,
                        $"Seed {seed}: hazard overlaps a protected transition");
                    var expected = DungeonHazardConnections.None;
                    if (hazardCells.Contains(hazard.Cell + Vector2Int.left)) expected |= DungeonHazardConnections.West;
                    if (hazardCells.Contains(hazard.Cell + Vector2Int.right)) expected |= DungeonHazardConnections.East;
                    if (hazardCells.Contains(hazard.Cell + Vector2Int.down)) expected |= DungeonHazardConnections.South;
                    if (hazardCells.Contains(hazard.Cell + Vector2Int.up)) expected |= DungeonHazardConnections.North;
                    failures += Require(hazard.Connections == expected,
                        $"Seed {seed}: hazard neighbour mask is inconsistent");
                }
                var start = dungeon.CellCenter(dungeon.StartCell);
                failures += Require(dungeon.CanOccupy(start + Vector2.left * .3f, .22f), $"Seed {seed}: start blocks left movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.right * .3f, .22f), $"Seed {seed}: start blocks right movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.up * .3f, .22f), $"Seed {seed}: start blocks upward movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.down * .3f, .22f), $"Seed {seed}: start blocks downward movement");
                // Exercise the same blocking placements used by structural decor. Rejected props
                // are intentionally absent; accepted props must preserve every room route below.
                foreach (var generatedRoom in dungeon.Rooms)
                {
                    var bounds = generatedRoom.bounds;
                    dungeon.TryAddObstaclePreservingRoutes(new Vector2(bounds.xMin + 1.2f, bounds.yMin + 1.1f));
                }
                for (var room = 1; room < dungeon.Rooms.Count; room++)
                {
                    var previous = dungeon.CellCenter(dungeon.Rooms[room - 1].Center);
                    var current = dungeon.CellCenter(dungeon.Rooms[room].Center);
                    failures += Require(HasWalkableRoute(dungeon, previous, current),
                        $"Seed {seed}: generated passage {room - 1}->{room} is disconnected");
                }
            }
            // A stair is optional architectural punctuation, never a quota that may override
            // topology or asset direction. It should still appear on nearly every regular floor.
            failures += Require(generatedStairFloors >= 85,
                "Architecture: internal stairs are absent from too many regular floors");
            failures += Require(generatedOptionalDoorCount >= 3 && generatedOptionalDoorCount <= 30,
                $"Door grammar: expected a rare but observable optional sample, got {generatedOptionalDoorCount}/100 floors");
            var bossArena = DungeonGenerator.Generate(balance, 10, 1010);
            failures += Require(bossArena.Width == 30 && bossArena.Height == 30, "Boss arena must be 30x30");
            failures += Require(bossArena.IsFloor(bossArena.StartCell.x, bossArena.StartCell.y), "Boss arena start is blocked");
            failures += Require(bossArena.IsFloor(bossArena.ExitCell.x, bossArena.ExitCell.y), "Boss arena boss spawn is blocked");
            UnityEngine.Object.DestroyImmediate(balance);

            if (failures > 0) throw new InvalidOperationException($"Darkfall validation failed: {failures} error(s)");
            Debug.Log("Darkfall validation passed: structure, resources and 100 dungeon seeds are valid.");
        }

        [MenuItem("Darkfall/Capture Biome Visual Audit")]
        public static void CaptureBiomeVisualAudit()
        {
            var output = Path.GetFullPath("work/visual-audit");
            Directory.CreateDirectory(output);
            foreach (var depth in new[] { 1, 11, 21, 31, 41 })
            {
                var dungeon = DungeonGenerator.Generate(GameBalance.RuntimeDefault(), depth, 73000 + depth);
                var root = new GameObject("Visual Audit Root");
                root.AddComponent<DungeonView>().Build(dungeon, depth);
                var ambient = new GameObject("Visual Audit Ambient").AddComponent<Light2D>();
                ambient.lightType = Light2D.LightType.Global;
                ambient.color = Color.Lerp(new Color(.40f, .41f, .44f),
                    DungeonVisualProfile.ForDepth(depth).WallTint, .22f);
                // Audit frames are deliberately brighter than gameplay: their job is to expose
                // seams, wrong axes and grounding defects, not to approve them by hiding them.
                ambient.intensity = Mathf.Max(.82f, DungeonVisualProfile.ForDepth(depth).AmbientIntensity);
                ambient.shadowsEnabled = false;

                var elevationFocus = dungeon.Rooms[Mathf.Min(1, dungeon.Rooms.Count - 1)].Center;
                foreach (var room in dungeon.Rooms)
                    if (dungeon.ElevationLevel(Mathf.FloorToInt(room.Center.x), Mathf.FloorToInt(room.Center.y)) > 0)
                    {
                        elevationFocus = room.Center;
                        break;
                    }
                CaptureAuditFrame(output, depth, "elevation", elevationFocus);

                if (dungeon.Hazards.Count > 0)
                    CaptureAuditFrame(output, depth, "hazard", dungeon.Hazards[0].Cell + Vector2.one * .5f);

                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!candidate.name.StartsWith("Biome Event ·")) continue;
                    CaptureAuditFrame(output, depth, "event", candidate.position);
                    break;
                }
                UnityEngine.Object.DestroyImmediate(ambient.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("Darkfall visual audit captured: " + output);
        }

        private static void CaptureAuditFrame(string output, int depth, string subject, Vector2 logicalFocus)
        {
            var projected = IsoWorld.Project(logicalFocus);
            var cameraObject = new GameObject("Visual Audit Camera · " + subject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = subject == "event" ? 4.8f : 6.4f;
            camera.aspect = 16f / 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.006f, .005f, .012f, 1f);
            camera.transform.position = new Vector3(projected.x, projected.y, -10f);
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var capture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            capture.Apply();
            File.WriteAllBytes(Path.Combine(output,
                $"depth-{depth:00}-{DungeonVisualProfile.ForDepth(depth).Id}-{subject}.png"), capture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        [MenuItem("Darkfall/Configure Build")]
        public static void ConfigureBuild()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            PlayerSettings.companyName = "Darkfall Studio";
            PlayerSettings.productName = "Darkfall Depths";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
            Debug.Log("Darkfall build settings configured.");
        }

        public static void BuildMac()
        {
            ValidateProject();
            ConfigureBuild();
            Directory.CreateDirectory("Builds/macOS");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = "Builds/macOS/Darkfall Depths.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.CleanBuildCache
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build failed: {report.summary.result}");
        }

        public static void BuildWindows()
        {
            ValidateProject();
            ConfigureBuild();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = "Builds/Windows/Darkfall Depths.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build failed: {report.summary.result}");
        }

        private static int Require(bool condition, string message)
        {
            if (condition) return 0;
            Debug.LogError(message);
            return 1;
        }

        private static bool HasWalkableRoute(DungeonData dungeon, Vector2 from, Vector2 to)
        {
            var start = new Vector2Int(Mathf.FloorToInt(from.x), Mathf.FloorToInt(from.y));
            var goal = new Vector2Int(Mathf.FloorToInt(to.x), Mathf.FloorToInt(to.y));
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var visited = new bool[dungeon.Width, dungeon.Height];
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == goal) return true;
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= dungeon.Width || next.y >= dungeon.Height ||
                        visited[next.x, next.y] || !dungeon.CanOccupy(dungeon.CellCenter(next), .22f)) continue;
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private static int ValidateHeroFrames(string hero)
        {
            var failures = 0;
            // Horizontal animation has one canonical authored side. Runtime mirrors it with
            // SpriteRenderer.flipX so the gait and action phases cannot drift between left/right.
            var directions = new[] { "down", "up", "right" };
            var frames = new[]
            {
                "idle_1", "idle_2", "idle_3", "idle_4", "walk_1", "walk_2", "walk_3", "walk_4",
                "attack_1", "attack_2", "attack_3", "hurt_1", "hurt_2"
            };
            foreach (var direction in directions)
            foreach (var frame in frames)
            {
                var path = $"Sprites/Characters/{hero}/{direction}/{frame}";
                failures += Require(Resources.Load<Texture2D>(path) != null,
                    $"Hero frame is missing: {hero}/{direction}/{frame}");
            }
            return failures;
        }

        private static int ValidateDirectionalSheet(string resourceName, string displayName)
        {
            var sheet = Resources.Load<Texture2D>($"Sprites/Directional/{resourceName}");
            var failures = Require(sheet != null, $"{displayName} directional sheet is missing");
            failures += Require(sheet != null && sheet.width == 1774 && sheet.height == 887,
                $"{displayName} directional sheet must match the 8x4 enemy grid dimensions");
            return failures;
        }
    }
}
#endif
