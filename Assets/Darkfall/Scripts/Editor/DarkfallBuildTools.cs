#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Darkfall.Core;
using Darkfall.Gameplay;
using Darkfall.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
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
            failures += Require(Mathf.Approximately(IsoWorld.HalfWidth / IsoWorld.HalfHeight, 2f),
                "Isometric art contract: IsoWorld must remain a 2:1 dimetric projection");
            failures += Require(Mathf.Abs(Mathf.Atan2(IsoWorld.HalfHeight, IsoWorld.HalfWidth) *
                                           Mathf.Rad2Deg - 26.565052f) < .001f,
                "Isometric art contract: projected ground-edge angle changed without updating art");
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
                         "Inventory_open", "Inventory_close"
                     })
                failures += Require(Resources.Load<AudioClip>($"Audio/Fx/{effect}") != null,
                    $"Gameplay audio effect is missing: {effect}");
            failures += Require(Resources.Load<Font>("Fonts/PTSans-Regular") != null, "Body UI font is missing");
            failures += Require(Resources.Load<Font>("Fonts/CormorantGaramond") != null, "Heading UI font is missing");
            failures += ValidateHeroFrames("mage");
            failures += ValidateHeroFrames("warrior");
            failures += ValidateHeroFrames("rogue");
            failures += ValidateDirectionalRuntimeContract();
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
            {
                var flame = Resources.Load<Texture2D>($"Sprites/Environment/Flames/flame-{frame}");
                failures += Require(flame != null, $"Individual flame frame is missing: {frame}");
                failures += Require(flame != null && flame.width == 256 && flame.height == 341,
                    $"Flame frame canvas must remain 256x341: {frame}");
            }
            var brazierBody = Resources.Load<Texture2D>(
                "Sprites/Environment/MiniSets/ashen-catacombs/campfire-unlit");
            failures += Require(brazierBody != null, "Canonical Ashen brazier body is missing");
            failures += Require(brazierBody != null && brazierBody.width == 512 && brazierBody.height == 512,
                "Canonical Ashen brazier body must keep its 512x512 canvas");
            foreach (var fixture in new[] { "wall-sconce-01", "floor-campfire-01" })
            {
                var texture = Resources.Load<Texture2D>(
                    $"Sprites/Environment/FireFixtures/ashen-catacombs/{fixture}");
                failures += Require(texture != null, $"Ashen fire fixture is missing: {fixture}");
                failures += Require(texture != null && texture.width == 362 && texture.height == 362,
                    $"Ashen fire fixture must keep its 362x362 canvas: {fixture}");
            }
            foreach (var state in new[] { "closed", "opening-01", "opening-02", "open" })
            {
                var door = Resources.Load<Texture2D>($"Sprites/Interactables/DungeonDoor/{state}");
                failures += Require(door != null, $"Dungeon door state is missing: {state}");
                failures += Require(door != null && door.width == 512 && door.height == 512,
                    $"Dungeon door state must keep the shared 512x512 canvas: {state}");
            }
            failures += Require(DungeonVisualProfile.ForDepth(1).Id != DungeonVisualProfile.ForDepth(11).Id,
                "Biome profile must change after depth 10");
            failures += Require(DungeonVisualProfile.ForDepth(11).Id != DungeonVisualProfile.ForDepth(21).Id &&
                                DungeonVisualProfile.ForDepth(21).Id != DungeonVisualProfile.ForDepth(31).Id &&
                                DungeonVisualProfile.ForDepth(31).Id != DungeonVisualProfile.ForDepth(41).Id,
                "Each post-boss chapter through depth 50 must use a distinct biome");
            foreach (var depth in new[] { 1, 11, 21, 31, 41 })
            {
                var atmosphere = DungeonVisualProfile.ForDepth(depth);
                failures += Require(atmosphere.AtmosphereDensity >= .5f && atmosphere.AtmosphereDensity <= 1.8f,
                    $"Biome atmosphere density is invalid: {atmosphere.Id}");
                failures += Require(atmosphere.AtmosphereTint.maxColorComponent > .15f &&
                                    atmosphere.AtmosphereDrift.sqrMagnitude > .000001f,
                    $"Biome atmosphere recipe is missing: {atmosphere.Id}");
            }
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
                for (var eventIndex = 0; eventIndex < 12; eventIndex++)
                    failures += Require(Resources.Load<Texture2D>(
                            $"Sprites/Environment/Events/{biome}/event-{eventIndex:00}") != null,
                        $"Biome event module is missing: {biome}/{eventIndex:00}");
                foreach (var module in architectureModules)
                {
                    failures += Require(Resources.Load<Texture2D>(
                            $"Sprites/Environment/Architecture/{biome}/{module}-01") != null,
                        $"Architecture module is missing: {biome}/{module}");
                    failures += Require(ArchitectureSpriteLibrary.ValidateSocketContract(biome, module,
                            out var socketError),
                        $"Architecture socket contract is invalid: {socketError}");
                }
            }
            var biomeEnemySheets = new[]
            {
                "enemy-ash-warden-v1", "enemy-ember-revenant-v1", "enemy-drowned-sentinel-v1",
                "enemy-spore-stalker-v1", "enemy-obsidian-acolyte-v1"
            };
            foreach (var sheet in biomeEnemySheets)
                failures += ValidateDirectionalSheet(sheet, "Biome enemy");
            failures += Require(LegacyCatalog.Data.bosses?.Length == 3, "Legacy parity: expected 3 bosses");
            failures += Require(LegacyCatalog.Data.items?.Length == 48, "Equipment expansion: expected 48 base items");
            foreach (var item in LegacyCatalog.Items)
            {
                failures += Require(ItemSpriteAtlas.HasMapping(item.baseId),
                    $"Item art mapping is missing: {item.baseId}");
                failures += Require(ItemSpriteAtlas.Get(item.baseId) != null,
                    $"Runtime item sprite is missing: {item.baseId}");
            }
            failures += Require(Resources.Load<Texture2D>("Sprites/Items/Individual/gold_pouch") != null,
                "Individual gold pouch sprite is missing");
            failures += Require(LegacyCatalog.Data.affixes?.Length == 9, "Legacy parity: expected 9 affixes");
            failures += Require(LegacyCatalog.Data.shop?.Length == 8, "Legacy parity: expected 8 shop upgrades");
            var inventory = new InventorySystem();
            failures += Require(inventory.Slots.Length == 42, "Legacy parity: backpack must contain 42 slots");
            failures += Require(inventory.Equipment.Length == 10, "Equipment model: expected 10 semantic slots");
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
            failures += Require(interactionInventory.AssignQuickSlot(1, 1) &&
                                interactionInventory.QuickSlots[0] == null &&
                                interactionInventory.QuickSlots[1] == "test_potion",
                "Inventory interaction: duplicate quick-slot binding was not moved to the new slot");
            failures += Require(!interactionInventory.MoveBackpackToEquipment(1, 2, null) &&
                                interactionInventory.Slots[1]?.id == "test_potion" &&
                                interactionInventory.Equipment[2] == null,
                "Inventory interaction: invalid consumable drop changed inventory state");
            var handInventory = new InventorySystem();
            handInventory.Slots[0] = new ItemInstance
                { id = "test_staff", baseId = "staff", name = "Test staff", kind = ItemKind.Weapon, weaponGrip = WeaponGrip.TwoHanded };
            handInventory.Slots[1] = new ItemInstance
                { id = "test_focus", baseId = "grimoire", name = "Test focus", kind = ItemKind.Focus };
            failures += Require(handInventory.MoveBackpackToEquipment(0, (int)EquipmentSlot.MainHand, null) && handInventory.IsOffHandBlocked,
                "Equipment hands: two-handed weapon did not block off-hand");
            failures += Require(!handInventory.MoveBackpackToEquipment(1, (int)EquipmentSlot.OffHand, null) &&
                                handInventory.Slots[1]?.id == "test_focus",
                "Equipment hands: off-hand accepted an item while blocked by two-handed weapon");
            var jewelryInventory = new InventorySystem();
            jewelryInventory.Slots[0] = new ItemInstance { id = "test_ring", baseId = "ring", name = "Test ring", kind = ItemKind.Ring };
            jewelryInventory.Slots[1] = new ItemInstance { id = "test_amulet", baseId = "amulet", name = "Test amulet", kind = ItemKind.Amulet };
            failures += Require(jewelryInventory.MoveBackpackToEquipment(0, (int)EquipmentSlot.RingRight, null) &&
                                jewelryInventory.MoveBackpackToEquipment(1, (int)EquipmentSlot.Amulet, null),
                "Equipment jewelry: ring and amulet semantic slots failed");
            interactionInventory.SwapBackpack(1, 3);
            failures += Require(interactionInventory.Slots[3]?.id == "test_potion",
                "Inventory interaction: backpack drag swap failed");

            var stackingInventory = new InventorySystem();
            failures += Require(stackingInventory.Add(new ItemInstance
                { id = "potion_roll_a", baseId = "health_potion", kind = ItemKind.Potion, quantity = 1 }),
                "Inventory lifecycle: first consumable stack could not be added");
            failures += Require(stackingInventory.Add(new ItemInstance
                { id = "potion_roll_b", baseId = "health_potion", kind = ItemKind.Potion, quantity = 2 }),
                "Inventory lifecycle: second consumable stack could not be added");
            failures += Require(stackingInventory.Count("health_potion") == 3 &&
                                stackingInventory.Slots[1] == null,
                "Inventory lifecycle: generated consumables with unique instance ids did not stack by base type");
            failures += Require(stackingInventory.TryConsume("health_potion", 2) &&
                                stackingInventory.Count("health_potion") == 1,
                "Inventory lifecycle: stacked consumable could not be consumed correctly");
            failures += Require(stackingInventory.AssignQuickSlot(0, 0) &&
                                stackingInventory.TryConsume("health_potion") &&
                                stackingInventory.Count("health_potion") == 0 &&
                                stackingInventory.QuickSlots[0] == null,
                "Inventory lifecycle: empty consumable left a stale quick-slot binding");

            failures += ValidateLootLifecycle();
            failures += ValidateShopCatalog();
            failures += ValidateEnemyCatalog();

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
                failures += Require(dungeon.GenerationInfo != null &&
                                    !string.IsNullOrEmpty(dungeon.GenerationInfo.strategy) &&
                                    dungeon.GenerationInfo.depth == generatedDepth,
                    $"Seed {seed}: generation diagnostics are missing");
                if (generatedDepth < 10)
                {
                    var shrineCount = 0;
                    for (var roomIndex = 1; roomIndex < dungeon.Rooms.Count - 1; roomIndex++)
                        if (dungeon.Rooms[roomIndex].theme == DungeonRoomTheme.Shrine) shrineCount++;
                    failures += Require(shrineCount > 0,
                        $"Seed {seed}: Ashen Catacombs have no authored chapel room");
                    failures += Require(dungeon.TryGetSetPiece(DungeonSetPieceKind.Entrance, out _) &&
                                        dungeon.TryGetSetPiece(DungeonSetPieceKind.Portal, out _) &&
                                        dungeon.TryGetSetPiece(DungeonSetPieceKind.Shrine, out _),
                        $"Seed {seed}: required Ashen set pieces are missing");
                    var treasureVaults = 0;
                    var hasEliteArena = false;
                    var hasEventRoom = false;
                    foreach (var setPiece in dungeon.SetPieces)
                    {
                        if (setPiece.Kind == DungeonSetPieceKind.TreasureVault) treasureVaults++;
                        else if (setPiece.Kind == DungeonSetPieceKind.EliteArena) hasEliteArena = true;
                        else if (setPiece.Kind == DungeonSetPieceKind.EventRoom) hasEventRoom = true;
                        var anchorCell = Vector2Int.FloorToInt(setPiece.Anchor);
                        failures += Require(dungeon.IsFloor(anchorCell.x, anchorCell.y),
                            $"Seed {seed}: {setPiece.Kind} anchor is not on walkable floor");
                        if (setPiece.Kind == DungeonSetPieceKind.EliteArena)
                            failures += Require(dungeon.Rooms[setPiece.RoomIndex].theme == DungeonRoomTheme.Ritual,
                                $"Seed {seed}: elite arena is not assigned to a Ritual room");
                        if (setPiece.Kind == DungeonSetPieceKind.EventRoom)
                            failures += Require(dungeon.Rooms[setPiece.RoomIndex].theme == DungeonRoomTheme.Ossuary,
                                $"Seed {seed}: event encounter is not assigned to an Ossuary room");
                    }
                    failures += Require(treasureVaults > 0,
                        $"Seed {seed}: Ashen Catacombs have no gameplay treasure vault");
                    failures += Require(hasEliteArena,
                        $"Seed {seed}: Ashen Catacombs have no elite arena encounter");
                    failures += Require(hasEventRoom,
                        $"Seed {seed}: Ashen Catacombs have no ossuary event encounter");
                    for (var first = 0; first < dungeon.SetPieces.Count; first++)
                    for (var second = first + 1; second < dungeon.SetPieces.Count; second++)
                        failures += Require(!dungeon.SetPieces[first].Mask.Overlaps(dungeon.SetPieces[second].Mask),
                            $"Seed {seed}: set-piece masks overlap");
                    foreach (var hazard in dungeon.Hazards)
                    {
                        if (!dungeon.HasSemantic(hazard.Cell, DungeonCellSemantic.EventReserved)) continue;
                        var authoredCrossing = false;
                        foreach (var miniSet in dungeon.MiniSets)
                            if (miniSet.Kind == DungeonMiniSetKind.HazardBridge &&
                                miniSet.Mask.Contains(hazard.Cell))
                            {
                                authoredCrossing = true;
                                break;
                            }
                        failures += Require(authoredCrossing,
                            $"Seed {seed}: hazard overlaps a reserved feature outside its authored bridge crossing");
                    }
                    failures += Require(dungeon.MiniSets.Count > 0,
                        $"Seed {seed}: Ashen Catacombs have no matched mini-sets");
                    for (var first = 0; first < dungeon.MiniSets.Count; first++)
                    {
                        failures += Require(dungeon.MiniSets[first].Mask.width == 3 ||
                                            dungeon.MiniSets[first].Mask.width == 5,
                            $"Seed {seed}: mini-set mask is not 3x3/5x5");
                        for (var second = first + 1; second < dungeon.MiniSets.Count; second++)
                            failures += Require(!dungeon.MiniSets[first].Mask.Overlaps(dungeon.MiniSets[second].Mask),
                                $"Seed {seed}: mini-set masks overlap");
                    }
                }
                failures += Require(dungeon.HasCompletedStage(DungeonGenerationStage.Layout) &&
                                    dungeon.HasCompletedStage(DungeonGenerationStage.Repair) &&
                                    dungeon.HasCompletedStage(DungeonGenerationStage.SetPieces) &&
                                    dungeon.NextGenerationStage == DungeonGenerationStage.TileResolution,
                    $"Seed {seed}: logical generation stages are incomplete or out of order");
                if (generatedDepth % 10 != 0)
                    failures += Require(DungeonLayoutRepairPass.CountUnresolvedNotches(
                                            SnapshotFloor(dungeon), dungeon.Rooms) == 0,
                        $"Seed {seed}: unresolved 3x3 corridor notch remains after repair");
                DungeonFloorTileResolver.Resolve(dungeon, seed);
                failures += Require(dungeon.ResolvedFloorTiles.Count == CountFloorCells(dungeon),
                    $"Seed {seed}: context tile resolver did not cover every floor cell");
                foreach (var tile in dungeon.ResolvedFloorTiles)
                {
                    failures += Require(DungeonFloorTileResolver.Classify(tile.Neighbours) == tile.Kind,
                        $"Seed {seed}: context tile {tile.Cell} has an invalid classification");
                    failures += Require(tile.Variant < DungeonFloorTileResolver.VariantCount,
                        $"Seed {seed}: context tile {tile.Cell} has an invalid variant");
                    if (dungeon.TryGetResolvedFloorTile(tile.Cell.x - 1, tile.Cell.y, out var west))
                        failures += Require(west.Variant != tile.Variant,
                            $"Seed {seed}: adjacent floor modules repeat at {west.Cell}/{tile.Cell}");
                    if (dungeon.TryGetResolvedFloorTile(tile.Cell.x, tile.Cell.y - 1, out var south))
                        failures += Require(south.Variant != tile.Variant,
                            $"Seed {seed}: adjacent floor modules repeat at {south.Cell}/{tile.Cell}");
                }
                DungeonWallTileResolver.Resolve(dungeon, seed);
                failures += Require(dungeon.ResolvedWallModules.Count == CountBoundaryUnits(dungeon),
                    $"Seed {seed}: wall resolver did not cover every boundary unit");
                for (var wallIndex = 0; wallIndex < dungeon.ResolvedWallModules.Count; wallIndex++)
                {
                    var wall = dungeon.ResolvedWallModules[wallIndex];
                    failures += Require(IsBoundaryWall(dungeon, wall),
                        $"Seed {seed}: resolved wall at {wall.Anchor} is not on the floor boundary");
                    for (var previousIndex = 0; previousIndex < wallIndex; previousIndex++)
                    {
                        var previousWall = dungeon.ResolvedWallModules[previousIndex];
                        if (previousWall.Vertical != wall.Vertical) continue;
                        var sameRun = wall.Vertical
                            ? Mathf.Abs(previousWall.Anchor.x - wall.Anchor.x) < .01f &&
                              Mathf.Abs(previousWall.Anchor.y - wall.Anchor.y) < 1.01f
                            : Mathf.Abs(previousWall.Anchor.y - wall.Anchor.y) < .01f &&
                              Mathf.Abs(previousWall.Anchor.x - wall.Anchor.x) < 1.01f;
                        if (sameRun)
                            failures += Require(previousWall.Variant != wall.Variant,
                                $"Seed {seed}: adjacent wall modules repeat at {previousWall.Anchor}/{wall.Anchor}");
                    }
                }
                failures += Require(dungeon.HasSemantic(dungeon.StartCell,
                        DungeonCellSemantic.Floor | DungeonCellSemantic.Room |
                        DungeonCellSemantic.Arrival | DungeonCellSemantic.NoDecor),
                    $"Seed {seed}: arrival semantics or reservation are incomplete");
                failures += Require(dungeon.HasSemantic(dungeon.ExitCell,
                        DungeonCellSemantic.Floor | DungeonCellSemantic.Room | DungeonCellSemantic.Exit |
                        DungeonCellSemantic.Portal | DungeonCellSemantic.NoDecor),
                    $"Seed {seed}: exit/portal semantics or reservation are incomplete");
                foreach (var hazard in dungeon.Hazards)
                    failures += Require(dungeon.HasSemantic(hazard.Cell,
                            DungeonCellSemantic.Hazard | DungeonCellSemantic.NoDecor),
                        $"Seed {seed}: hazard cell is not reserved from decor");
                failures += Require(dungeon.IsFloor(dungeon.StartCell.x, dungeon.StartCell.y), $"Seed {seed}: invalid start");
                failures += Require(dungeon.IsFloor(dungeon.ExitCell.x, dungeon.ExitCell.y), $"Seed {seed}: invalid exit");
                var lightingOrigins = new List<Vector2> { dungeon.CellCenter(dungeon.StartCell) };
                for (var roomIndex = 1; roomIndex < Mathf.Min(dungeon.Rooms.Count, 5); roomIndex++)
                    lightingOrigins.Add(dungeon.Rooms[roomIndex].Center + Vector2.one * .5f);
                foreach (var lightingOrigin in lightingOrigins)
                    for (var rayIndex = 0; rayIndex < 32; rayIndex++)
                    {
                        var angle = rayIndex * Mathf.PI * 2f / 32f;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        var maximum = DungeonLighting.PlayerVisionRadius(Vector2.right, direction);
                        var distance = DungeonLighting.TraceDistance(dungeon, lightingOrigin, direction,
                            maximum, out var blocked);
                        failures += Require(!float.IsNaN(distance) && !float.IsInfinity(distance) &&
                                            distance >= .06f && distance <= maximum + .07f,
                            $"Seed {seed}: invalid lighting ray distance at {lightingOrigin}");
                        failures += Require(!blocked || distance < maximum,
                            $"Seed {seed}: blocked lighting ray was not clipped");
                    }
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
                        var previousHeight = dungeon.SurfaceHeight(lowSide);
                        for (var sampleIndex = 1; sampleIndex <= 12; sampleIndex++)
                        {
                            var samplePoint = Vector2.Lerp(lowSide, highSide, sampleIndex / 12f);
                            var sampleHeight = dungeon.SurfaceHeight(samplePoint);
                            failures += Require(sampleHeight + .001f >= previousHeight,
                                $"Seed {seed}: stair surface is not monotonic at sample {sampleIndex}");
                            failures += Require(sampleHeight - previousHeight <=
                                                DungeonData.ElevationStepHeight * .24f,
                                $"Seed {seed}: stair surface contains a vertical snap at sample {sampleIndex}");
                            previousHeight = sampleHeight;
                        }
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
                var hazardByCell = new Dictionary<Vector2Int, DungeonHazardCell>();
                foreach (var hazard in dungeon.Hazards) hazardByCell[hazard.Cell] = hazard;
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
                var unvisitedHazards = new HashSet<Vector2Int>(hazardCells);
                while (unvisitedHazards.Count > 0)
                {
                    var origin = default(Vector2Int);
                    foreach (var candidate in unvisitedHazards) { origin = candidate; break; }
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(origin);
                    unvisitedHazards.Remove(origin);
                    var sources = 0;
                    var sinks = 0;
                    var componentKind = hazardByCell[origin].Kind;
                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        var currentHazard = hazardByCell[cell];
                        if (currentHazard.Terminal == DungeonHazardTerminal.Source) sources++;
                        if (currentHazard.Terminal == DungeonHazardTerminal.Sink) sinks++;
                        failures += Require(currentHazard.Kind == componentKind,
                            $"Seed {seed}: two hazard materials were joined into one river");
                        foreach (var direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up })
                        {
                            var next = cell + direction;
                            if (!unvisitedHazards.Remove(next)) continue;
                            queue.Enqueue(next);
                        }
                    }
                    failures += Require(sources == 1 && sinks == 1,
                        $"Seed {seed}: hazard flow must have exactly one source and one sink");
                }
                var start = dungeon.CellCenter(dungeon.StartCell);
                failures += Require(dungeon.CanOccupy(start + Vector2.left * .3f, .22f), $"Seed {seed}: start blocks left movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.right * .3f, .22f), $"Seed {seed}: start blocks right movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.up * .3f, .22f), $"Seed {seed}: start blocks upward movement");
                failures += Require(dungeon.CanOccupy(start + Vector2.down * .3f, .22f), $"Seed {seed}: start blocks downward movement");
                foreach (var screenDirection in new[]
                         { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    var logicalStep = IsoWorld.UnprojectDirection(screenDirection).normalized * .15f;
                    var resolved = DungeonMovement.ResolveStep(dungeon, start, logicalStep, .22f, true);
                    var logicalDelta = resolved - start;
                    var projectedDelta = IsoWorld.ProjectDirection(logicalDelta).normalized;
                    failures += Require(logicalDelta.sqrMagnitude > .01f &&
                                        Vector2.Dot(projectedDelta, screenDirection) > .98f,
                        $"Seed {seed}: screen movement {screenDirection} does not preserve its intended direction");
                }
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

        [MenuItem("Darkfall/Run Release Smoke")]
        public static void RunReleaseSmoke()
        {
            // The runtime harness owns shutdown and the exit code. Keeping this as a real Play
            // Mode pass catches renderer lifetime, input and coroutine defects that edit-time
            // validation cannot observe. Batch callers must also pass -darkfall-smoke.
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static int CountFloorCells(DungeonData dungeon)
        {
            var count = 0;
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
                if (dungeon.IsFloor(x, y)) count++;
            return count;
        }

        private static bool[,] SnapshotFloor(DungeonData dungeon)
        {
            var floor = new bool[dungeon.Width, dungeon.Height];
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
                floor[x, y] = dungeon.IsFloor(x, y);
            return floor;
        }

        private static int CountBoundaryUnits(DungeonData dungeon)
        {
            var count = 0;
            for (var x = 0; x < dungeon.Width; x++)
            for (var y = 0; y < dungeon.Height; y++)
            {
                if (!dungeon.IsFloor(x, y)) continue;
                if (!dungeon.IsFloor(x - 1, y)) count++;
                if (!dungeon.IsFloor(x + 1, y)) count++;
                if (!dungeon.IsFloor(x, y - 1)) count++;
                if (!dungeon.IsFloor(x, y + 1)) count++;
            }
            return count;
        }

        private static bool IsBoundaryWall(DungeonData dungeon, DungeonResolvedWallModule wall)
        {
            if (wall.Vertical)
            {
                var x = Mathf.RoundToInt(wall.Anchor.x);
                var y = Mathf.FloorToInt(wall.Anchor.y);
                return dungeon.IsFloor(x - 1, y) != dungeon.IsFloor(x, y);
            }
            else
            {
                var x = Mathf.FloorToInt(wall.Anchor.x);
                var y = Mathf.RoundToInt(wall.Anchor.y);
                return dungeon.IsFloor(x, y - 1) != dungeon.IsFloor(x, y);
            }
        }


        [MenuItem("Darkfall/Capture Biome Visual Audit")]
        public static void CaptureBiomeVisualAudit()
        {
            var output = Path.GetFullPath("work/visual-audit");
            Directory.CreateDirectory(output);
            foreach (var depth in new[] { 1, 11, 21, 31, 41 })
                CaptureBiomeDepth(output, depth);
            Debug.Log("Darkfall visual audit captured: " + output);
        }

        [MenuItem("Darkfall/Capture Ashen Catacombs Audit")]
        public static void CaptureAshenCatacombsAudit()
        {
            var output = Path.GetFullPath("work/visual-audit");
            Directory.CreateDirectory(output);
            CaptureBiomeDepth(output, 1);
            CaptureAshenMiniSetVariants(output);
            Debug.Log("Darkfall Ashen Catacombs audit captured: " + output);
        }

        private static void CaptureAshenMiniSetVariants(string output)
        {
            var wanted = new HashSet<DungeonMiniSetKind>
            {
                DungeonMiniSetKind.StatueNiche, DungeonMiniSetKind.Altar,
                DungeonMiniSetKind.Colonnade,
                DungeonMiniSetKind.SideChapel, DungeonMiniSetKind.HazardBridge
            };
            var balance = GameBalance.RuntimeDefault();
            try
            {
                for (var seed = 73001; seed < 75001 && wanted.Count > 0; seed++)
                {
                    // Ritual rooms are intentionally uncommon. Search the complete first-biome
                    // depth band instead of increasing campfire density just to satisfy an audit.
                    var depth = 1 + (seed - 73001) % 9;
                    var dungeon = DungeonGenerator.Generate(balance, depth, seed);
                    foreach (var miniSet in dungeon.MiniSets)
                    {
                        if (!wanted.Remove(miniSet.Kind)) continue;
                        var root = new GameObject("Mini Set Variant Audit Root");
                        root.AddComponent<DungeonView>().Build(dungeon, depth);
                        var ambient = new GameObject("Mini Set Variant Audit Ambient").AddComponent<Light2D>();
                        ambient.lightType = Light2D.LightType.Global;
                        ambient.color = new Color(.40f, .39f, .36f);
                        ambient.intensity = .84f;
                        ambient.shadowsEnabled = false;
                        CaptureAuditFrame(output, depth,
                            $"miniset-target-{miniSet.Kind.ToString().ToLowerInvariant()}-seed-{seed}",
                            miniSet.Anchor, miniSet.Kind == DungeonMiniSetKind.SideChapel ? 4.2f : 3.2f);
                        UnityEngine.Object.DestroyImmediate(ambient.gameObject);
                        UnityEngine.Object.DestroyImmediate(root);
                        break;
                    }
                }
                if (wanted.Count > 0)
                    throw new InvalidOperationException("Missing mini-set audit variants: " +
                                                        string.Join(", ", wanted));
                CaptureAshenFireFixtureVariants(output, balance);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(balance);
            }
        }

        private static void CaptureAshenFireFixtureVariants(string output, GameBalance balance)
        {
            const int depth = 1;
            var dungeon = DungeonGenerator.Generate(balance, depth, 73191);
            var root = new GameObject("Fire Fixture Audit Root");
            root.AddComponent<DungeonView>().Build(dungeon, depth);
            var ambient = new GameObject("Fire Fixture Audit Ambient").AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.color = new Color(.40f, .39f, .36f);
            ambient.intensity = .84f;
            ambient.shadowsEnabled = false;
            try
            {
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!candidate.name.Contains("Wall Torch", StringComparison.Ordinal)) continue;
                    CaptureAnimatedFireFrames(output, depth, "fire-wall-sconce", candidate, candidate.position);
                    break;
                }

                // Campfires are deliberately rare and ritual-only. Audit their authored body
                // directly instead of requiring the procedural map to spawn extra fire clutter.
                var room = dungeon.Rooms[Mathf.Min(1, dungeon.Rooms.Count - 1)];
                var roomCenter = new Vector2(room.Center.x, room.Center.y);
                var fixtureRoot = new GameObject("Audit Floor Campfire");
                fixtureRoot.transform.SetParent(root.transform, false);
                fixtureRoot.transform.position = new Vector3(roomCenter.x, roomCenter.y, 0f);
                var visual = new GameObject("Projected Audit Floor Campfire");
                visual.transform.SetParent(fixtureRoot.transform, false);
                visual.transform.localScale = Vector3.one * .66f;
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = FireFixtureSpriteLibrary.FloorCampfire;
                DarkfallRenderMaterials.MakeLit(renderer);
                visual.AddComponent<IsoVisual>().Initialize(fixtureRoot.transform, 0f, 1004);
                var flame = new GameObject("Animated Flame");
                flame.transform.SetParent(visual.transform, false);
                flame.transform.localPosition = new Vector2(0f, .55f);
                flame.transform.localScale = Vector3.one * .48f;
                flame.AddComponent<DungeonFlameAnimator>().Initialize(9);
                CaptureAnimatedFireFrames(output, depth, "fire-floor-campfire", fixtureRoot.transform, roomCenter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ambient.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CaptureAnimatedFireFrames(string output, int depth, string subject,
            Transform fixtureRoot, Vector2 focus)
        {
            var animator = fixtureRoot.GetComponentInChildren<DungeonFlameAnimator>(true);
            if (animator == null) throw new InvalidOperationException(subject + " has no flame animator");
            var fixturePosition = fixtureRoot.position;
            var fixtureScale = fixtureRoot.localScale;
            var sprites = new HashSet<Sprite>();
            for (var frame = 0; frame < 4; frame++)
            {
                animator.AdvanceFrameForAudit();
                sprites.Add(animator.CurrentSpriteForAudit);
                CaptureAuditFrame(output, depth, $"{subject}-frame-{frame + 1}", focus, 2.25f);
                if (fixtureRoot.position != fixturePosition || fixtureRoot.localScale != fixtureScale)
                    throw new InvalidOperationException(subject + " fixture moved or changed scale during animation");
            }
            if (sprites.Count != 4)
                throw new InvalidOperationException(subject + " did not render four distinct flame frames");
        }

        [MenuItem("Darkfall/Capture Elevation Variants Audit")]
        public static void CaptureElevationVariantsAudit()
        {
            var output = Path.GetFullPath("work/visual-audit/elevation");
            Directory.CreateDirectory(output);
            var balance = GameBalance.RuntimeDefault();
            try
            {
                CaptureElevationVariant(output, balance, -1, 2, "descent-narrow");
                CaptureElevationVariant(output, balance, -1, 3, "descent-wide");
                CaptureElevationVariant(output, balance, 1, 2, "ascent-narrow");
                CaptureElevationVariant(output, balance, 1, 3, "ascent-wide");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(balance);
            }
            Debug.Log("Darkfall elevation variants audit captured: " + output);
        }

        private static void CaptureElevationVariant(string output, GameBalance balance, int requestedPlatformLevel,
            int requestedWidth, string subject)
        {
            const int auditDepth = 9;
            for (var seed = 73001; seed < 79001; seed++)
            {
                var dungeon = DungeonGenerator.Generate(balance, auditDepth, seed);
                foreach (var feature in dungeon.Architecture)
                {
                    if (feature.Kind != DungeonArchitectureKind.ElevationStairs || feature.Width != requestedWidth)
                        continue;
                    var normal = feature.Vertical ? Vector2.right : Vector2.up;
                    var negative = dungeon.ElevationLevel(
                        Mathf.FloorToInt(feature.Position.x - normal.x * .25f),
                        Mathf.FloorToInt(feature.Position.y - normal.y * .25f));
                    var positive = dungeon.ElevationLevel(
                        Mathf.FloorToInt(feature.Position.x + normal.x * .25f),
                        Mathf.FloorToInt(feature.Position.y + normal.y * .25f));
                    var platformLevel = negative != 0 ? negative : positive;
                    if (platformLevel != requestedPlatformLevel) continue;

                    var root = new GameObject("Elevation Variant Audit Root");
                    root.AddComponent<DungeonView>().Build(dungeon, auditDepth);
                    var ambient = new GameObject("Elevation Variant Audit Ambient").AddComponent<Light2D>();
                    ambient.lightType = Light2D.LightType.Global;
                    ambient.color = new Color(.40f, .39f, .36f);
                    ambient.intensity = .84f;
                    ambient.shadowsEnabled = false;

                    var lowerDirection = negative < positive ? -normal : normal;
                    CaptureAuditFrame(output, auditDepth, subject + $"-raw-seed-{seed}", feature.Position, 3.2f);
                    var observer = new GameObject("Elevation Variant Audit Observer");
                    observer.transform.position = feature.Position - lowerDirection * .72f;
                    var veil = new GameObject("Elevation Variant Audit Veil").AddComponent<ElevationDepthVeil>();
                    veil.Initialize(dungeon, observer.transform);
                    CaptureAuditFrame(output, auditDepth, subject + $"-seed-{seed}", feature.Position, 3.2f);
                    UnityEngine.Object.DestroyImmediate(veil.gameObject);
                    UnityEngine.Object.DestroyImmediate(observer);
                    UnityEngine.Object.DestroyImmediate(ambient.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    return;
                }
            }
            throw new InvalidOperationException($"No elevation variant found: {subject}");
        }

        private static void CaptureBiomeDepth(string output, int depth)
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

                Vector2 elevationFocus = dungeon.Rooms[Mathf.Min(1, dungeon.Rooms.Count - 1)].Center;
                var foundElevation = false;
                foreach (var room in dungeon.Rooms)
                {
                    var cell = Vector2Int.FloorToInt(room.Center);
                    if (dungeon.IsFloor(cell.x, cell.y) && dungeon.ElevationLevel(cell.x, cell.y) != 0)
                    {
                        elevationFocus = room.Center;
                        foundElevation = true;
                        break;
                    }
                }
                if (!foundElevation)
                    for (var x = 0; x < dungeon.Width && !foundElevation; x++)
                    for (var y = 0; y < dungeon.Height; y++)
                        if (dungeon.IsFloor(x, y) && dungeon.ElevationLevel(x, y) != 0)
                        {
                            elevationFocus = new Vector2(x + .5f, y + .5f);
                            foundElevation = true;
                            break;
                        }
                var observerPosition = elevationFocus;
                var highestLevel = int.MinValue;
                for (var x = 0; x < dungeon.Width; x++)
                for (var y = 0; y < dungeon.Height; y++)
                    if (dungeon.IsFloor(x, y) && dungeon.ElevationLevel(x, y) > highestLevel)
                    {
                        highestLevel = dungeon.ElevationLevel(x, y);
                        observerPosition = new Vector2(x + .5f, y + .5f);
                    }
                var elevationObserver = new GameObject("Elevation Audit Observer");
                elevationObserver.transform.position = observerPosition;
                var elevationVeil = new GameObject("Elevation Audit Depth Veil").AddComponent<ElevationDepthVeil>();
                elevationVeil.Initialize(dungeon, elevationObserver.transform);
                CaptureAuditFrame(output, depth, "elevation", elevationFocus);
                CaptureAuditFrame(output, depth, "arrival-threshold", dungeon.CellCenter(dungeon.StartCell));
                CaptureAuditFrame(output, depth, "exit-threshold", dungeon.CellCenter(dungeon.ExitCell));
                var capturedInnerCorner = new bool[4];
                var capturedOuterCorner = new bool[4];
                foreach (var corner in dungeon.ResolvedWallCorners)
                {
                    var orientation = corner.Kind == DungeonWallCornerKind.Outer
                        ? corner.FloorQuadrants : (byte)(15 ^ corner.FloorQuadrants);
                    var orientationIndex = orientation == 1 ? 0 : orientation == 2 ? 1 :
                        orientation == 4 ? 2 : 3;
                    if (corner.Kind == DungeonWallCornerKind.Inner && !capturedInnerCorner[orientationIndex])
                    {
                        CaptureAuditFrame(output, depth, $"wall-corner-inner-{orientation}", corner.Anchor, 1.35f);
                        capturedInnerCorner[orientationIndex] = true;
                    }
                    else if (corner.Kind == DungeonWallCornerKind.Outer && !capturedOuterCorner[orientationIndex])
                    {
                        CaptureAuditFrame(output, depth, $"wall-corner-outer-{orientation}", corner.Anchor, 1.35f);
                        capturedOuterCorner[orientationIndex] = true;
                    }
                    if (System.Array.TrueForAll(capturedInnerCorner, value => value) &&
                        System.Array.TrueForAll(capturedOuterCorner, value => value)) break;
                }
                foreach (var feature in dungeon.Architecture)
                    if (feature.Kind == DungeonArchitectureKind.ElevationStairs)
                    {
                        CaptureAuditFrame(output, depth, "elevation-transition", feature.Position);
                        break;
                    }

                if (dungeon.Hazards.Count > 0)
                    CaptureAuditFrame(output, depth, "hazard", dungeon.Hazards[0].Cell + Vector2.one * .5f);

                var miniSetCounters = new Dictionary<DungeonMiniSetKind, int>();
                foreach (var miniSet in dungeon.MiniSets)
                {
                    if (miniSet.Kind != DungeonMiniSetKind.StatueNiche &&
                        miniSet.Kind != DungeonMiniSetKind.Altar &&
                        miniSet.Kind != DungeonMiniSetKind.Campfire &&
                        miniSet.Kind != DungeonMiniSetKind.Colonnade &&
                        miniSet.Kind != DungeonMiniSetKind.SideChapel &&
                        miniSet.Kind != DungeonMiniSetKind.HazardBridge) continue;
                    miniSetCounters.TryGetValue(miniSet.Kind, out var miniSetIndex);
                    miniSetCounters[miniSet.Kind] = miniSetIndex + 1;
                    CaptureAuditFrame(output, depth,
                        $"miniset-{miniSet.Kind.ToString().ToLowerInvariant()}-{miniSetIndex + 1}",
                        miniSet.Anchor, miniSet.Kind == DungeonMiniSetKind.SideChapel ? 4.2f : 3.2f);
                }

                Transform firstEvent = null;
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!candidate.name.StartsWith("Biome Event ·")) continue;
                    if (firstEvent == null) firstEvent = candidate;
                    var pieces = candidate.name.Split('·');
                    if (pieces.Length < 3 || !int.TryParse(pieces[2].Trim(), out var eventIndex) || eventIndex < 6)
                        continue;
                    firstEvent = candidate;
                    break;
                }
                if (firstEvent != null) CaptureAuditFrame(output, depth, "event", firstEvent.position);
                var setPieceCounters = new Dictionary<DungeonSetPieceKind, int>();
                foreach (var setPiece in dungeon.SetPieces)
                {
                    if (setPiece.Kind != DungeonSetPieceKind.TreasureVault &&
                        setPiece.Kind != DungeonSetPieceKind.EliteArena &&
                        setPiece.Kind != DungeonSetPieceKind.EventRoom &&
                        setPiece.Kind != DungeonSetPieceKind.MimicLair) continue;
                    setPieceCounters.TryGetValue(setPiece.Kind, out var setPieceIndex);
                    setPieceCounters[setPiece.Kind] = setPieceIndex + 1;
                    CaptureAuditFrame(output, depth,
                        $"setpiece-{setPiece.Kind.ToString().ToLowerInvariant()}-{setPieceIndex + 1}",
                        setPiece.Anchor, 4.8f);
                }
                UnityEngine.Object.DestroyImmediate(elevationVeil.gameObject);
                UnityEngine.Object.DestroyImmediate(elevationObserver);
                UnityEngine.Object.DestroyImmediate(ambient.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CaptureAuditFrame(string output, int depth, string subject, Vector2 logicalFocus,
            float orthographicSize = -1f)
        {
            var projected = IsoWorld.Project(logicalFocus);
            var cameraObject = new GameObject("Visual Audit Camera · " + subject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize > 0f ? orthographicSize : subject == "event" ? 4.8f : 6.4f;
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
            failures += Require(DirectionalSpriteAtlas.HasCompleteHeroLayout(hero),
                $"Hero frame grounding metadata is incomplete: {hero}");
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
                var texture = Resources.Load<Texture2D>(path);
                failures += Require(texture != null,
                    $"Hero frame is missing: {hero}/{direction}/{frame}");
                failures += Require(texture != null && texture.width == 256 && texture.height == 256,
                    $"Hero frame canvas must remain 256x256: {hero}/{direction}/{frame}");
            }
            // Left-facing heroes use their authored asymmetric weapon/armour frames. The imported
            // source currently provides a neutral idle plus complete walk, attack and hurt actions.
            var leftFrames = new[]
            {
                "idle", "walk_1", "walk_2", "walk_3", "walk_4",
                "attack_1", "attack_2", "attack_3", "hurt_1", "hurt_2"
            };
            foreach (var frame in leftFrames)
            {
                var path = $"Sprites/Characters/{hero}/left/{frame}";
                var texture = Resources.Load<Texture2D>(path);
                failures += Require(texture != null, $"Hero frame is missing: {hero}/left/{frame}");
                failures += Require(texture != null && texture.width == 256 && texture.height == 256,
                    $"Hero frame canvas must remain 256x256: {hero}/left/{frame}");
            }
            return failures;
        }

        private static int ValidateDirectionalSheet(string resourceName, string displayName)
        {
            var sheet = Resources.Load<Texture2D>($"Sprites/Directional/{resourceName}");
            var failures = Require(sheet != null, $"{displayName} directional sheet is missing");
            failures += Require(sheet != null && sheet.width == 1774 && sheet.height == 887,
                $"{displayName} directional sheet must match the 8x4 enemy grid dimensions");
            failures += Require(DirectionalSpriteAtlas.HasEnemyDirectionConvention(resourceName),
                $"{displayName} has no reviewed left/right row convention: {resourceName}");
            return failures;
        }

        private static int ValidateDirectionalRuntimeContract()
        {
            var failures = 0;
            var directions = new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
            var directionNames = new[] { "down", "up", "left", "right" };
            var motions = new[]
            {
                CharacterMotion.Idle, CharacterMotion.Walk, CharacterMotion.Attack, CharacterMotion.Hit
            };
            foreach (var sheet in new[] { "mage-v2", "warrior-v2", "rogue-v2" })
            for (var direction = 0; direction < directions.Length; direction++)
            foreach (var motion in motions)
            {
                var sprite = DirectionalSpriteAtlas.Get(sheet, directions[direction], motion, .12f, out var flipX);
                failures += Require(sprite != null,
                    $"Directional runtime returned no sprite: {sheet}/{directionNames[direction]}/{motion}");
                failures += Require(sprite != null && sprite.name.Contains("-" + directionNames[direction] + "-"),
                    $"Directional runtime selected the wrong authored side: {sheet}/{directionNames[direction]}/{motion}");
                failures += Require(!flipX,
                    $"Authored hero direction must not be mirrored: {sheet}/{directionNames[direction]}/{motion}");
            }

            var enemyRight = DirectionalSpriteAtlas.Get("enemy-melee-v2", Vector2.right,
                CharacterMotion.Walk, .2f, out var enemyRightFlip);
            var enemyLeft = DirectionalSpriteAtlas.Get("enemy-melee-v2", Vector2.left,
                CharacterMotion.Walk, .2f, out var enemyLeftFlip);
            failures += Require(enemyRight != null && enemyLeft != null && !enemyRightFlip && enemyLeftFlip,
                "Canonical enemy side must face right normally and left through one stable mirror");
            failures += Require(DirectionalSpriteAtlas.StabilizeFourWay(new Vector2(.72f, .69f), Vector2.right) == Vector2.right,
                "Four-way facing hysteresis must keep the current axis near a diagonal");
            return failures;
        }

        private static int ValidateLootLifecycle()
        {
            var failures = 0;
            var baseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in LegacyCatalog.Items) baseIds.Add(definition.baseId);
            var kinds = new HashSet<ItemKind>();
            var rarities = new HashSet<ItemRarity>();
            var previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(913733);
            foreach (var depth in new[] { 1, 10, 11, 21, 31, 41, 50 })
            for (var roll = 0; roll < 160; roll++)
            {
                var item = InventorySystem.GenerateLoot(depth);
                failures += Require(item != null, $"Loot depth {depth}: generator returned null");
                if (item == null) continue;
                kinds.Add(item.kind);
                rarities.Add(item.rarity);
                failures += Require(baseIds.Contains(item.baseId),
                    $"Loot depth {depth}: unknown base item {item.baseId}");
                failures += Require(item.quantity > 0, $"Loot depth {depth}: {item.baseId} has invalid quantity");
                failures += Require(item.itemLevel > 0 && item.power > 0 && float.IsFinite(item.power),
                    $"Loot depth {depth}: {item.baseId} has invalid progression values");
                failures += Require(ItemSpriteAtlas.Get(item.baseId) != null,
                    $"Loot depth {depth}: generated item art is missing for {item.baseId}");
                var bag = new InventorySystem();
                failures += Require(bag.Add(item) && bag.Count(item.baseId) == item.quantity,
                    $"Loot depth {depth}: generated {item.baseId} cannot enter inventory");
            }
            UnityEngine.Random.state = previousState;
            failures += Require(kinds.Contains(ItemKind.Potion) && kinds.Contains(ItemKind.Scroll) &&
                                kinds.Count >= 6,
                "Loot distribution: generated sample does not cover consumables, scrolls and equipment types");
            failures += Require(rarities.Contains(ItemRarity.Common) && rarities.Contains(ItemRarity.Rare) &&
                                rarities.Contains(ItemRarity.Epic) && rarities.Contains(ItemRarity.Legendary),
                "Loot distribution: generated sample does not cover all rarity tiers");
            return failures;
        }

        private static int ValidateShopCatalog()
        {
            var failures = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var supported = new HashSet<string>(StringComparer.Ordinal)
                { "max_hp", "damage", "defense", "speed", "crit", "attack_speed", "heal_full", "attack_radius" };
            foreach (var upgrade in LegacyCatalog.Data.shop)
            {
                failures += Require(upgrade != null && !string.IsNullOrEmpty(upgrade.id),
                    "Shop catalog: upgrade without id");
                if (upgrade == null || string.IsNullOrEmpty(upgrade.id)) continue;
                failures += Require(ids.Add(upgrade.id), $"Shop catalog: duplicate upgrade id {upgrade.id}");
                failures += Require(supported.Contains(upgrade.id),
                    $"Shop catalog: {upgrade.id} has no PlayerController application rule");
                failures += Require(upgrade.basePrice > 0 && float.IsFinite(upgrade.basePrice),
                    $"Shop catalog: {upgrade.id} has invalid price");
                failures += Require(upgrade.maxPurchases > 0,
                    $"Shop catalog: {upgrade.id} cannot be purchased");
                failures += Require(upgrade.id == "heal_full" || upgrade.value > 0,
                    $"Shop catalog: {upgrade.id} has no upgrade value");
            }
            failures += Require(ids.Count >= 5, "Shop catalog: not enough offers to fill the sanctuary shop");
            return failures;
        }

        private static int ValidateEnemyCatalog()
        {
            var failures = 0;
            var types = new HashSet<string>(StringComparer.Ordinal);
            foreach (var enemy in LegacyCatalog.Data.enemies)
                failures += ValidateEnemyDefinition(enemy, false, types);
            foreach (var boss in LegacyCatalog.Data.bosses)
                failures += ValidateEnemyDefinition(boss, true, types);

            foreach (var depth in new[] { 1, 4, 11, 21, 31, 41, 50 })
            {
                var biome = DungeonVisualProfile.ForDepth(depth).Id;
                var eligible = 0;
                var local = 0;
                foreach (var enemy in LegacyCatalog.Data.enemies)
                    if (enemy.levelRequirement <= depth &&
                        (string.IsNullOrEmpty(enemy.biome) || enemy.biome == biome))
                    {
                        eligible++;
                        if (enemy.biome == biome) local++;
                    }
                failures += Require(eligible > 0, $"Enemy pool depth {depth}: no eligible enemies");
                if (depth >= 4)
                    failures += Require(local > 0, $"Enemy pool depth {depth}: biome {biome} has no signature inhabitant");
            }
            failures += Require(Resources.Load<Texture2D>("Sprites/Directional/enemy-mimic-v1") != null,
                "Mimic lifecycle: directional sprite sheet is unavailable in Resources");
            return failures;
        }

        private static int ValidateEnemyDefinition(LegacyEnemy enemy, bool boss, HashSet<string> types)
        {
            var failures = 0;
            failures += Require(enemy != null && !string.IsNullOrEmpty(enemy.type),
                $"{(boss ? "Boss" : "Enemy")} catalog: definition without type");
            if (enemy == null || string.IsNullOrEmpty(enemy.type)) return failures;
            failures += Require(types.Add(enemy.type), $"Enemy catalog: duplicate type {enemy.type}");
            failures += Require(enemy.hp > 0 && enemy.damage > 0 && enemy.speed > 0 && enemy.attackRange > 0,
                $"Enemy catalog: {enemy.type} has invalid combat values");
            failures += Require(enemy.reward >= 0 && enemy.levelRequirement >= 0,
                $"Enemy catalog: {enemy.type} has invalid progression values");
            failures += Require(!enemy.hasBow || enemy.projectileSpeed > 0,
                $"Enemy catalog: bow user {enemy.type} has no projectile speed");
            if (!string.IsNullOrEmpty(enemy.sheet))
                failures += Require(Resources.Load<Texture2D>("Sprites/Directional/" + enemy.sheet) != null,
                    $"Enemy catalog: authored sheet is missing for {enemy.type}");
            if (boss)
                failures += Require(enemy.abilities != null && enemy.abilities.Length > 0,
                    $"Boss catalog: {enemy.type} has no active abilities");
            return failures;
        }
    }
}
#endif
