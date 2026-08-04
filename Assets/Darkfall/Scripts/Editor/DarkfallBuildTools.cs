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
            failures += Require(Resources.Load<AudioClip>("Audio/Main") != null, "Main music is missing");
            failures += Require(Resources.Load<Font>("Fonts/PTSans-Regular") != null, "Body UI font is missing");
            failures += Require(Resources.Load<Font>("Fonts/CormorantGaramond") != null, "Heading UI font is missing");
            failures += ValidateHeroFrames("mage");
            failures += ValidateHeroFrames("warrior");
            failures += ValidateHeroFrames("rogue");
            failures += Require(Resources.Load<Texture2D>("Sprites/Directional/enemy-melee-v2") != null, "Melee enemy sheet is missing");
            failures += Require(Resources.Load<Texture2D>("Sprites/Directional/enemy-ranged-v2") != null, "Ranged enemy sheet is missing");
            failures += Require(Resources.Load<Texture2D>("Sprites/Directional/enemy-caster-v2") != null, "Caster enemy sheet is missing");
            failures += Require(Resources.Load<Texture2D>("Textures/dungeon-floor-v2") != null, "Dungeon floor v2 texture is missing");
            failures += Require(Resources.Load<Texture2D>("Textures/dungeon-wall-v2") != null, "Dungeon wall v2 texture is missing");
            failures += Require(Resources.Load<Texture2D>("Sprites/Environment/dungeon-props-v2") != null, "Dungeon prop atlas is missing");
            for (var prop = 0; prop < 12; prop++)
                failures += Require(Resources.Load<Texture2D>($"Sprites/Environment/Props/prop-{prop}") != null,
                    $"Individual environment prop is missing: {prop}");
            for (var frame = 0; frame < 4; frame++)
                failures += Require(Resources.Load<Texture2D>($"Sprites/Environment/Flames/flame-{frame}") != null,
                    $"Individual flame frame is missing: {frame}");
            failures += Require(DungeonVisualProfile.ForDepth(1).Id != DungeonVisualProfile.ForDepth(11).Id,
                "Biome profile must change after depth 10");
            failures += Require(LegacyCatalog.Data.characters?.Length == 3, "Legacy parity: expected 3 heroes");
            failures += Require(LegacyCatalog.Data.enemies?.Length == 12, "Legacy parity: expected 12 enemy types");
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

        private static int Require(bool condition, string message)
        {
            if (condition) return 0;
            Debug.LogError(message);
            return 1;
        }

        private static int ValidateHeroFrames(string hero)
        {
            var failures = 0;
            var directions = new[] { "down", "up", "side" };
            var frames = new[] { "idle", "walk_1", "walk_2", "attack", "hurt" };
            foreach (var direction in directions)
            foreach (var frame in frames)
            {
                var path = $"Sprites/Characters/{hero}/{direction}/{frame}";
                failures += Require(Resources.Load<Texture2D>(path) != null,
                    $"Hero frame is missing: {hero}/{direction}/{frame}");
            }
            return failures;
        }
    }
}
#endif
