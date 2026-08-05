#if UNITY_EDITOR
using System;
using System.IO;
using Darkfall.Core;
using Darkfall.Gameplay;
using Darkfall.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
                failures += Require(Resources.Load<Texture2D>($"Sprites/Environment/Biomes/{biome}-decor") != null,
                    $"Biome decor atlas is missing: {biome}");
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
            foreach (var module in architectureModules)
                failures += Require(Resources.Load<Texture2D>(
                        $"Sprites/Environment/Architecture/{biome}/{module}-01") != null,
                    $"Architecture module is missing: {biome}/{module}");
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
            for (var seed = 0; seed < 100; seed++)
            {
                var dungeon = DungeonGenerator.Generate(balance, 1 + seed % 25, seed);
                failures += Require(dungeon.Rooms.Count >= 2, $"Seed {seed}: insufficient rooms");
                failures += Require(dungeon.IsFloor(dungeon.StartCell.x, dungeon.StartCell.y), $"Seed {seed}: invalid start");
                failures += Require(dungeon.IsFloor(dungeon.ExitCell.x, dungeon.ExitCell.y), $"Seed {seed}: invalid exit");
                var start = dungeon.CellCenter(dungeon.StartCell);
                failures += Require(dungeon.CanOccupy(start + Vector2.left * .3f, .22f), $"Seed {seed}: start blocks left movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.right * .3f, .22f), $"Seed {seed}: start blocks right movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.up * .3f, .22f), $"Seed {seed}: start blocks upward movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.down * .3f, .22f), $"Seed {seed}: start blocks downward movement");
                for (var room = 1; room < dungeon.Rooms.Count; room++)
                {
                    var previous = dungeon.CellCenter(dungeon.Rooms[room - 1].Center);
                    var current = dungeon.CellCenter(dungeon.Rooms[room].Center);
                    failures += Require(HasWalkableRoute(dungeon, previous, current),
                        $"Seed {seed}: generated passage {room - 1}->{room} is disconnected");
                }
            }
            var bossArena = DungeonGenerator.Generate(balance, 10, 1010);
            failures += Require(bossArena.Width == 30 && bossArena.Height == 30, "Boss arena must be 30x30");
            failures += Require(bossArena.IsFloor(bossArena.StartCell.x, bossArena.StartCell.y), "Boss arena start is blocked");
            failures += Require(bossArena.IsFloor(bossArena.ExitCell.x, bossArena.ExitCell.y), "Boss arena boss spawn is blocked");
            UnityEngine.Object.DestroyImmediate(balance);

            if (failures > 0) throw new InvalidOperationException($"Darkfall validation failed: {failures} error(s)");
            Debug.Log("Darkfall validation passed: structure, resources and 100 dungeon seeds are valid.");
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
