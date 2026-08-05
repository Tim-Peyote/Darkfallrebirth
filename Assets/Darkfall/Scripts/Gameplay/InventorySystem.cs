using System;
using System.Collections.Generic;
using Darkfall.Core;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public enum ItemKind { Weapon, Shield, Armor, Head, Gloves, Belt, Boots, Accessory, Potion, Scroll, Gold }
    public enum ItemRarity { Common, Rare, Epic, Legendary }

    [Serializable]
    public sealed class ItemInstance
    {
        public string id;
        public string baseId;
        public string name;
        public string slot;
        public string description;
        public string icon;
        public string requiredClass;
        public ItemKind kind;
        public ItemRarity rarity;
        public float power;
        public int itemLevel = 1;
        public int quantity = 1;
        public string[] affixes = Array.Empty<string>();
        public float damage, defense, maxHp, moveSpeed, attackSpeed, crit, attackRadius, fire, ice;

        public Color Color => rarity switch
        {
            ItemRarity.Rare => new Color(0.2f, 0.55f, 1f),
            ItemRarity.Epic => new Color(0.9f, 0.42f, 0.1f),
            ItemRarity.Legendary => new Color(0.9f, 0.12f, 0.08f),
            _ => new Color(0.72f, 0.75f, 0.8f)
        };
    }

    public sealed class InventorySystem
    {
        private static int failedRareRolls;
        public const int Capacity = 42;
        public readonly ItemInstance[] Slots = new ItemInstance[Capacity];
        public readonly ItemInstance[] Equipment = new ItemInstance[9];
        // The original game stores the consumable base type, not a concrete stack.
        public readonly string[] QuickSlots = new string[3];
        public ItemInstance Weapon => Equipment[1];
        public ItemInstance Armor => Equipment[2];
        public ItemInstance Amulet => Equipment[4];
        public event Action Changed;

        public bool Add(ItemInstance item)
        {
            if (item == null) return false;
            if (item.kind == ItemKind.Potion || item.kind == ItemKind.Scroll)
            {
                for (var i = 0; i < Slots.Length; i++)
                    if (Slots[i] != null && Slots[i].id == item.id)
                    {
                        Slots[i].quantity += item.quantity;
                        Changed?.Invoke();
                        return true;
                    }
            }
            for (var i = 0; i < Slots.Length; i++)
                if (Slots[i] == null)
                {
                    Slots[i] = item;
                    Changed?.Invoke();
                    return true;
                }
            return false;
        }

        public void UseOrEquip(int index, PlayerController player)
        {
            if (index < 0 || index >= Slots.Length || Slots[index] == null) return;
            var item = Slots[index];
            switch (item.kind)
            {
                case ItemKind.Potion:
                case ItemKind.Scroll:
                    if (!ConsumableEffectSystem.Apply(item, player)) return;
                    Consume(index);
                    break;
                default:
                    Equip(index, player);
                    break;
            }
            Changed?.Invoke();
        }

        public void UseQuickSlot(int quickIndex, PlayerController player)
        {
            if (quickIndex < 0 || quickIndex >= QuickSlots.Length || string.IsNullOrEmpty(QuickSlots[quickIndex])) return;
            for (var i = 0; i < Slots.Length; i++)
                if (Slots[i] != null && Slots[i].baseId == QuickSlots[quickIndex])
                {
                    UseOrEquip(i, player);
                    return;
                }
        }

        public bool AssignQuickSlot(int inventoryIndex, int quickIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= Slots.Length || quickIndex < 0 || quickIndex >= QuickSlots.Length) return false;
            var item = Slots[inventoryIndex];
            if (item == null || (item.kind != ItemKind.Potion && item.kind != ItemKind.Scroll)) return false;
            QuickSlots[quickIndex] = item.baseId;
            Changed?.Invoke();
            return true;
        }

        public void ClearQuickSlot(int quickIndex)
        {
            if (quickIndex < 0 || quickIndex >= QuickSlots.Length) return;
            QuickSlots[quickIndex] = null;
            Changed?.Invoke();
        }

        public void SwapQuickSlots(int first, int second)
        {
            if (first < 0 || first >= QuickSlots.Length || second < 0 || second >= QuickSlots.Length || first == second) return;
            (QuickSlots[first], QuickSlots[second]) = (QuickSlots[second], QuickSlots[first]);
            Changed?.Invoke();
        }

        public int Count(string baseId)
        {
            var total = 0;
            foreach (var item in Slots) if (item != null && item.baseId == baseId) total += item.quantity;
            return total;
        }

        public void Unequip(int equipmentIndex)
        {
            if (equipmentIndex < 0 || equipmentIndex >= Equipment.Length || Equipment[equipmentIndex] == null) return;
            for (var i = 0; i < Slots.Length; i++)
                if (Slots[i] == null)
                {
                    Slots[i] = Equipment[equipmentIndex];
                    Equipment[equipmentIndex] = null;
                    Changed?.Invoke();
                    return;
                }
        }

        public void SwapBackpack(int first, int second)
        {
            if (first < 0 || first >= Slots.Length || second < 0 || second >= Slots.Length || first == second) return;
            (Slots[first], Slots[second]) = (Slots[second], Slots[first]);
            Changed?.Invoke();
        }

        public bool MoveBackpackToEquipment(int backpackIndex, int equipmentIndex, PlayerController player)
        {
            if (backpackIndex < 0 || backpackIndex >= Slots.Length || equipmentIndex < 0 || equipmentIndex >= Equipment.Length) return false;
            var item = Slots[backpackIndex];
            if (item == null || !CanEquipInSlot(item, equipmentIndex)) return false;
            if (!string.IsNullOrEmpty(item.requiredClass) &&
                !string.Equals(item.requiredClass, player.Hero.heroClass.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                GameManager.Instance.ShowMessage($"Предмет предназначен для класса: {item.requiredClass}");
                return false;
            }
            var previous = Equipment[equipmentIndex];
            Equipment[equipmentIndex] = item;
            Slots[backpackIndex] = previous;
            Changed?.Invoke();
            return true;
        }

        public bool MoveEquipmentToBackpack(int equipmentIndex, int backpackIndex)
        {
            if (equipmentIndex < 0 || equipmentIndex >= Equipment.Length || backpackIndex < 0 || backpackIndex >= Slots.Length) return false;
            var equipped = Equipment[equipmentIndex];
            if (equipped == null) return false;
            var backpackItem = Slots[backpackIndex];
            if (backpackItem != null && !CanEquipInSlot(backpackItem, equipmentIndex)) return false;
            Equipment[equipmentIndex] = backpackItem;
            Slots[backpackIndex] = equipped;
            Changed?.Invoke();
            return true;
        }

        public bool SwapEquipment(int first, int second)
        {
            if (first < 0 || first >= Equipment.Length || second < 0 || second >= Equipment.Length || first == second) return false;
            if (Equipment[first] != null && !CanEquipInSlot(Equipment[first], second)) return false;
            if (Equipment[second] != null && !CanEquipInSlot(Equipment[second], first)) return false;
            (Equipment[first], Equipment[second]) = (Equipment[second], Equipment[first]);
            Changed?.Invoke();
            return true;
        }

        public bool AddAt(ItemInstance item, int backpackIndex)
        {
            if (item == null || backpackIndex < 0 || backpackIndex >= Slots.Length) return false;
            if (Slots[backpackIndex] == null)
            {
                Slots[backpackIndex] = item;
                Changed?.Invoke();
                return true;
            }
            var existing = Slots[backpackIndex];
            if ((item.kind == ItemKind.Potion || item.kind == ItemKind.Scroll) && existing.baseId == item.baseId)
            {
                existing.quantity += item.quantity;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void SortBackpack()
        {
            Array.Sort(Slots, (a, b) =>
            {
                if (a == null) return b == null ? 0 : 1;
                if (b == null) return -1;
                var kind = a.kind.CompareTo(b.kind);
                if (kind != 0) return kind;
                var rarity = b.rarity.CompareTo(a.rarity);
                return rarity != 0 ? rarity : string.Compare(a.name, b.name, StringComparison.CurrentCulture);
            });
            Changed?.Invoke();
        }

        public void DeleteBackpack(int index)
        {
            if (index < 0 || index >= Slots.Length || Slots[index] == null) return;
            Slots[index] = null;
            Changed?.Invoke();
        }

        private void Consume(int index)
        {
            if (--Slots[index].quantity <= 0) Slots[index] = null;
        }

        private void Equip(int index, PlayerController player)
        {
            var item = Slots[index];
            if (!string.IsNullOrEmpty(item.requiredClass) &&
                !string.Equals(item.requiredClass, player.Hero.heroClass.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                GameManager.Instance.ShowMessage($"Предмет предназначен для класса: {item.requiredClass}");
                return;
            }
            var equipmentIndex = FindEquipmentSlot(item);
            if (equipmentIndex < 0) return;
            var previous = Equipment[equipmentIndex];
            Equipment[equipmentIndex] = item;
            Slots[index] = previous;
        }

        public static ItemInstance GenerateLoot(int depth)
        {
            var definitions = LegacyCatalog.Data.items;
            var playerClass = GameManager.Instance?.SelectedHero ?? HeroClass.Mage;
            var categoryRoll = UnityEngine.Random.value;
            float equipmentWeight, potionWeight;
            switch (playerClass)
            {
                case HeroClass.Warrior: equipmentWeight = .50f; potionWeight = .30f; break;
                case HeroClass.Rogue: equipmentWeight = .40f; potionWeight = .35f; break;
                default: equipmentWeight = .20f; potionWeight = .30f; break;
            }
            var wanted = categoryRoll < equipmentWeight ? 0 : categoryRoll < equipmentWeight + potionWeight ? 1 : 2;
            var pool = new List<LegacyItem>();
            foreach (var candidate in definitions)
            {
                var category = candidate.baseId.StartsWith("scroll_") || candidate.baseId == "mystery_scroll" ? 2 :
                    candidate.type == "consumable" ? 1 : 0;
                if (category != wanted) continue;
                if (category == 0 && UnityEngine.Random.value < .9f && !string.IsNullOrEmpty(candidate.requiredClass) &&
                    !string.Equals(candidate.requiredClass, playerClass.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                pool.Add(candidate);
            }
            if (pool.Count == 0) pool.AddRange(definitions);
            var definition = pool[UnityEngine.Random.Range(0, pool.Count)];
            var rarityDefinition = RollRarityForDepth(depth);
            Enum.TryParse(rarityDefinition.key, true, out ItemRarity rarity);
            var multiplier = 1 + (int)rarity * 0.55f;
            var item = new ItemInstance
            {
                id = definition.baseId + "_" + rarity + "_" + UnityEngine.Random.Range(1000, 9999),
                baseId = definition.baseId,
                name = $"{rarityDefinition.name} {definition.name}",
                kind = ParseKind(definition),
                slot = definition.slot,
                description = definition.description,
                icon = definition.icon,
                requiredClass = definition.requiredClass,
                rarity = rarity,
                itemLevel = Mathf.Max(1, depth + UnityEngine.Random.Range(-1, 2)),
                power = (3 + depth * 0.6f) * multiplier
            };
            ApplyBaseStats(item);
            ApplyAffixes(item, (int)rarity);
            return item;
        }

        public static ItemInstance CreateFromDefinition(LegacyItem definition, int depth, int quantity = 1)
        {
            if (definition == null) return null;
            var item = new ItemInstance
            {
                id = definition.baseId + "_debug_" + UnityEngine.Random.Range(100000, 999999),
                baseId = definition.baseId,
                name = definition.name,
                kind = ParseKind(definition),
                slot = definition.slot,
                description = definition.description,
                icon = definition.icon,
                requiredClass = definition.requiredClass,
                rarity = ItemRarity.Common,
                itemLevel = Mathf.Max(1, depth),
                quantity = Mathf.Max(1, quantity),
                power = 3 + Mathf.Max(1, depth) * .6f
            };
            ApplyBaseStats(item);
            return item;
        }

        private static LegacyRarity RollRarityForDepth(int depth)
        {
            var roll = UnityEngine.Random.value;
            float rare, epic, legendary;
            if (depth <= 4) { rare = .20f; epic = .08f; legendary = .02f; }
            else if (depth <= 10) { rare = .25f; epic = .10f; legendary = .05f; }
            else { rare = .30f; epic = .15f; legendary = .05f; }
            var pity = Mathf.Min(.5f, failedRareRolls * .02f);
            legendary += pity * .35f;
            epic += pity * .65f;
            var key = roll < legendary ? "legendary" : roll < legendary + epic ? "epic" :
                roll < legendary + epic + rare ? "rare" : "common";
            if (key == "epic" || key == "legendary") failedRareRolls = 0;
            else failedRareRolls++;
            foreach (var rarity in LegacyCatalog.Data.rarities) if (rarity.key == key) return rarity;
            return LegacyCatalog.Data.rarities[0];
        }

        private static void ApplyBaseStats(ItemInstance item)
        {
            var level = item.itemLevel;
            switch (item.kind)
            {
                case ItemKind.Weapon: item.damage = 8 + level * 2; item.attackRadius = level * .35f; break;
                case ItemKind.Shield: item.defense = 6 + level * 1.5f; item.maxHp = 15 + level * 2; break;
                case ItemKind.Armor: item.defense = 4 + level; item.maxHp = 10 + level * 2; break;
                case ItemKind.Head: item.defense = 2 + level * .8f; item.maxHp = 8 + level * 1.5f; break;
                case ItemKind.Gloves: item.attackSpeed = 3 + level; item.crit = 1 + level * .35f; break;
                case ItemKind.Belt: item.maxHp = 8 + level * 2; item.moveSpeed = 2 + level * .7f; break;
                case ItemKind.Boots: item.moveSpeed = 4 + level; item.defense = 1 + level * .5f; break;
                case ItemKind.Accessory: item.moveSpeed = 2 + level * .6f; item.crit = 2 + level * .5f; break;
            }
        }

        private static void ApplyAffixes(ItemInstance item, int count)
        {
            if (count <= 0 || item.kind == ItemKind.Potion || item.kind == ItemKind.Scroll) return;
            var available = new List<LegacyAffix>(LegacyCatalog.Data.affixes);
            var labels = new List<string>();
            for (var i = 0; i < count && available.Count > 0; i++)
            {
                var index = UnityEngine.Random.Range(0, available.Count);
                var affix = available[index];
                available.RemoveAt(index);
                var levelScale = .7f + .3f * (item.itemLevel / 20f);
                var value = UnityEngine.Random.Range(affix.min, affix.max) * levelScale;
                if (item.rarity == ItemRarity.Legendary) value *= 1.2f;
                labels.Add($"+{value:0.#} {affix.name}");
                switch (affix.key)
                {
                    case "damage": item.damage += value; break;
                    case "crit": item.crit += value; break;
                    case "defense": item.defense += value; break;
                    case "maxHp": item.maxHp += value; break;
                    case "moveSpeed": item.moveSpeed += value; break;
                    case "attackSpeed": item.attackSpeed += value; break;
                    case "attackRadius": item.attackRadius += value; break;
                    case "fire": item.fire += value; break;
                    case "ice": item.ice += value; break;
                }
            }
            item.affixes = labels.ToArray();
        }

        private static ItemKind ParseKind(LegacyItem item)
        {
            if (item.baseId.StartsWith("scroll_") || item.baseId == "mystery_scroll") return ItemKind.Scroll;
            if (item.type == "consumable") return ItemKind.Potion;
            return item.type switch
            {
                "weapon" => ItemKind.Weapon, "shield" => ItemKind.Shield, "armor" => ItemKind.Armor,
                "head" => ItemKind.Head, "gloves" => ItemKind.Gloves, "belt" => ItemKind.Belt,
                "boots" => ItemKind.Boots, "accessory" => ItemKind.Accessory, _ => ItemKind.Gold
            };
        }

        private int FindEquipmentSlot(ItemInstance item)
        {
            int[] candidates = item.kind switch
            {
                ItemKind.Head => new[] { 0 }, ItemKind.Weapon => new[] { 1, 3 }, ItemKind.Shield => new[] { 3 },
                ItemKind.Armor => new[] { 2 }, ItemKind.Accessory => new[] { 4, 6 }, ItemKind.Gloves => new[] { 5 },
                ItemKind.Belt => new[] { 7 }, ItemKind.Boots => new[] { 8 }, _ => Array.Empty<int>()
            };
            foreach (var candidate in candidates) if (Equipment[candidate] == null) return candidate;
            return candidates.Length > 0 ? candidates[0] : -1;
        }

        private static bool CanEquipInSlot(ItemInstance item, int equipmentIndex)
        {
            if (item == null) return true;
            return item.kind switch
            {
                ItemKind.Head => equipmentIndex == 0,
                ItemKind.Weapon => equipmentIndex == 1 || equipmentIndex == 3,
                ItemKind.Shield => equipmentIndex == 3,
                ItemKind.Armor => equipmentIndex == 2,
                ItemKind.Accessory => equipmentIndex == 4 || equipmentIndex == 6,
                ItemKind.Gloves => equipmentIndex == 5,
                ItemKind.Belt => equipmentIndex == 7,
                ItemKind.Boots => equipmentIndex == 8,
                _ => false
            };
        }

        public float EquipmentStat(Func<ItemInstance, float> selector)
        {
            var total = 0f;
            foreach (var item in Equipment) if (item != null) total += selector(item);
            return total;
        }
    }
}
