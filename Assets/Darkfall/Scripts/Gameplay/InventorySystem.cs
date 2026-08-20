using System;
using System.Collections.Generic;
using Darkfall.Core;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public enum ItemKind { Weapon, Shield, Armor, Head, Gloves, Belt, Boots, Accessory, Potion, Scroll, Gold, Ring, Amulet, Focus }
    public enum WeaponGrip { None, OneHanded, TwoHanded }
    public enum EquipmentSlot
    {
        Head = 0, MainHand = 1, Armor = 2, OffHand = 3, Amulet = 4,
        Gloves = 5, RingLeft = 6, Belt = 7, Boots = 8, RingRight = 9
    }
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
        public WeaponGrip weaponGrip;
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
        public const int EquipmentCapacity = 10;
        public readonly ItemInstance[] Equipment = new ItemInstance[EquipmentCapacity];
        // The original game stores the consumable base type, not a concrete stack.
        public readonly string[] QuickSlots = new string[3];
        public ItemInstance Weapon => Equipment[1];
        public ItemInstance Armor => Equipment[2];
        public ItemInstance Amulet => Equipment[4];
        public bool IsOffHandBlocked => IsTwoHanded(Equipment[(int)EquipmentSlot.MainHand]);
        public event Action Changed;

        public bool Add(ItemInstance item)
        {
            if (item == null) return false;
            if (item.kind == ItemKind.Potion || item.kind == ItemKind.Scroll)
            {
                for (var i = 0; i < Slots.Length; i++)
                    if (Slots[i] != null && Slots[i].baseId == item.baseId)
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
            QuickSlots[quickIndex] = null;
            Changed?.Invoke();
        }

        public bool AssignQuickSlot(int inventoryIndex, int quickIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= Slots.Length || quickIndex < 0 || quickIndex >= QuickSlots.Length) return false;
            var item = Slots[inventoryIndex];
            if (item == null || (item.kind != ItemKind.Potion && item.kind != ItemKind.Scroll)) return false;
            for (var i = 0; i < QuickSlots.Length; i++)
                if (i != quickIndex && QuickSlots[i] == item.baseId) QuickSlots[i] = null;
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

        public bool TryConsume(string baseId, int amount = 1)
        {
            if (string.IsNullOrEmpty(baseId) || amount <= 0 || Count(baseId) < amount) return false;
            for (var i = 0; i < Slots.Length && amount > 0; i++)
            {
                var item = Slots[i];
                if (item == null || item.baseId != baseId) continue;
                var taken = Mathf.Min(item.quantity, amount);
                item.quantity -= taken;
                amount -= taken;
                if (item.quantity <= 0) Slots[i] = null;
            }
            ClearQuickBindingsWithoutItems(baseId);
            Changed?.Invoke();
            return true;
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
            var failure = GetEquipFailure(item, equipmentIndex, player);
            if (!string.IsNullOrEmpty(failure)) { ShowEquipFailure(failure); return false; }

            var displaced = new List<ItemInstance>();
            if (Equipment[equipmentIndex] != null) displaced.Add(Equipment[equipmentIndex]);
            if (equipmentIndex == (int)EquipmentSlot.MainHand && IsTwoHanded(item) &&
                Equipment[(int)EquipmentSlot.OffHand] != null)
                displaced.Add(Equipment[(int)EquipmentSlot.OffHand]);

            var destinations = FreeBackpackDestinations(backpackIndex, displaced.Count);
            if (destinations.Count < displaced.Count)
            {
                ShowEquipFailure("Освободите место в рюкзаке: двуручное оружие снимает предмет из второй руки.");
                return false;
            }

            Slots[backpackIndex] = null;
            Equipment[equipmentIndex] = item;
            if (equipmentIndex == (int)EquipmentSlot.MainHand && IsTwoHanded(item))
                Equipment[(int)EquipmentSlot.OffHand] = null;
            for (var i = 0; i < displaced.Count; i++) Slots[destinations[i]] = displaced[i];
            Changed?.Invoke();
            return true;
        }

        public bool MoveEquipmentToBackpack(int equipmentIndex, int backpackIndex)
        {
            if (equipmentIndex < 0 || equipmentIndex >= Equipment.Length || backpackIndex < 0 || backpackIndex >= Slots.Length) return false;
            var equipped = Equipment[equipmentIndex];
            if (equipped == null) return false;
            var backpackItem = Slots[backpackIndex];
            if (backpackItem != null && !string.IsNullOrEmpty(GetEquipFailure(backpackItem, equipmentIndex, null))) return false;
            if (backpackItem != null && equipmentIndex == (int)EquipmentSlot.MainHand && IsTwoHanded(backpackItem) &&
                Equipment[(int)EquipmentSlot.OffHand] != null)
            {
                ShowEquipFailure("Сначала освободите вторую руку для двуручного оружия.");
                return false;
            }
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
            if ((second == (int)EquipmentSlot.OffHand && IsTwoHanded(Equipment[(int)EquipmentSlot.MainHand])) ||
                (first == (int)EquipmentSlot.OffHand && IsTwoHanded(Equipment[second]))) return false;
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
            var baseId = Slots[index].baseId;
            Slots[index] = null;
            ClearQuickBindingsWithoutItems(baseId);
            Changed?.Invoke();
        }

        private void Consume(int index)
        {
            var baseId = Slots[index].baseId;
            if (--Slots[index].quantity <= 0) Slots[index] = null;
            ClearQuickBindingsWithoutItems(baseId);
        }

        private void ClearQuickBindingsWithoutItems(string baseId)
        {
            if (string.IsNullOrEmpty(baseId) || Count(baseId) > 0) return;
            for (var i = 0; i < QuickSlots.Length; i++)
                if (QuickSlots[i] == baseId) QuickSlots[i] = null;
        }

        private void Equip(int index, PlayerController player)
        {
            var item = Slots[index];
            var equipmentIndex = FindEquipmentSlot(item);
            if (equipmentIndex < 0) return;
            MoveBackpackToEquipment(index, equipmentIndex, player);
        }

        public static ItemInstance GenerateLoot(int depth)
        {
            var definitions = LegacyCatalog.Data.items;
            var playerClass = GameManager.Instance?.SelectedHero ?? HeroClass.Mage;
            var categoryRoll = UnityEngine.Random.value;
            var weights = LootCategoryWeights(playerClass);
            var equipmentWeight = weights.x;
            var potionWeight = weights.y;
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
                weaponGrip = GripFor(definition.baseId),
                rarity = rarity,
                itemLevel = Mathf.Max(1, depth + UnityEngine.Random.Range(-1, 2)),
                power = (3 + depth * 0.6f) * multiplier
            };
            ApplyBaseStats(item);
            ApplyAffixes(item, (int)rarity);
            return item;
        }

        /// <summary>
        /// Equipment, potion and scroll weights after the global 35% scroll-drop reduction.
        /// Removed scroll weight is redistributed proportionally between the other two categories,
        /// so every class keeps its original identity and the total remains exactly one.
        /// </summary>
        public static Vector3 LootCategoryWeights(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => new Vector3(.54375f, .32625f, .13f),
                HeroClass.Rogue => new Vector3(.4466667f, .3908333f, .1625f),
                _ => new Vector3(.27f, .405f, .325f)
            };
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
                weaponGrip = GripFor(definition.baseId),
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
                case ItemKind.Weapon:
                    item.damage = (IsTwoHanded(item) ? 11 : 7) + level * (IsTwoHanded(item) ? 2.5f : 1.65f);
                    item.attackRadius = level * (IsTwoHanded(item) ? .45f : .28f);
                    break;
                case ItemKind.Shield: item.defense = 6 + level * 1.5f; item.maxHp = 15 + level * 2; break;
                case ItemKind.Armor: item.defense = 4 + level; item.maxHp = 10 + level * 2; break;
                case ItemKind.Head: item.defense = 2 + level * .8f; item.maxHp = 8 + level * 1.5f; break;
                case ItemKind.Gloves: item.attackSpeed = 3 + level; item.crit = 1 + level * .35f; break;
                case ItemKind.Belt: item.maxHp = 8 + level * 2; item.moveSpeed = 2 + level * .7f; break;
                case ItemKind.Boots: item.moveSpeed = 4 + level; item.defense = 1 + level * .5f; break;
                case ItemKind.Accessory:
                case ItemKind.Amulet: item.maxHp = 5 + level * 1.2f; item.moveSpeed = 1 + level * .35f; break;
                case ItemKind.Ring: item.crit = 1.5f + level * .4f; item.attackSpeed = 1 + level * .35f; break;
                case ItemKind.Focus: item.damage = 3 + level * .8f; item.crit = 1 + level * .3f; break;
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
                "boots" => ItemKind.Boots, "focus" => ItemKind.Focus,
                "accessory" => item.baseId == "ring" ? ItemKind.Ring : item.baseId == "amulet" ? ItemKind.Amulet : ItemKind.Accessory,
                _ => ItemKind.Gold
            };
        }

        private int FindEquipmentSlot(ItemInstance item)
        {
            int[] candidates = item.kind switch
            {
                ItemKind.Head => new[] { 0 },
                ItemKind.Weapon => IsTwoHanded(item) ? new[] { 1 } : new[] { 1, 3 },
                ItemKind.Shield => new[] { 3 }, ItemKind.Focus => new[] { 3 },
                ItemKind.Armor => new[] { 2 }, ItemKind.Amulet => new[] { 4 },
                ItemKind.Accessory => item.baseId == "ring" ? new[] { 6, 9 } : new[] { 4 },
                ItemKind.Ring => new[] { 6, 9 }, ItemKind.Gloves => new[] { 5 },
                ItemKind.Belt => new[] { 7 }, ItemKind.Boots => new[] { 8 }, _ => Array.Empty<int>()
            };
            foreach (var candidate in candidates)
                if (Equipment[candidate] == null && string.IsNullOrEmpty(GetEquipFailure(item, candidate, null))) return candidate;
            return candidates.Length > 0 ? candidates[0] : -1;
        }

        public static bool CanEquipInSlot(ItemInstance item, int equipmentIndex)
        {
            if (item == null) return true;
            return item.kind switch
            {
                ItemKind.Head => equipmentIndex == 0,
                ItemKind.Weapon => equipmentIndex == 1 || (!IsTwoHanded(item) && equipmentIndex == 3),
                ItemKind.Shield => equipmentIndex == 3,
                ItemKind.Focus => equipmentIndex == 3,
                ItemKind.Armor => equipmentIndex == 2,
                ItemKind.Amulet => equipmentIndex == 4,
                ItemKind.Ring => equipmentIndex == 6 || equipmentIndex == 9,
                ItemKind.Accessory => item.baseId == "ring" ? equipmentIndex == 6 || equipmentIndex == 9 : equipmentIndex == 4,
                ItemKind.Gloves => equipmentIndex == 5,
                ItemKind.Belt => equipmentIndex == 7,
                ItemKind.Boots => equipmentIndex == 8,
                _ => false
            };
        }

        public string GetEquipFailure(ItemInstance item, int equipmentIndex, PlayerController player)
        {
            if (item == null) return "В этом слоте нет предмета.";
            NormalizeLegacyItem(item);
            if (!CanEquipInSlot(item, equipmentIndex))
                return IsTwoHanded(item) && equipmentIndex == (int)EquipmentSlot.OffHand
                    ? "Двуручное оружие можно взять только в основную руку."
                    : "Тип предмета не подходит для этого слота.";
            if (!IsClassCompatible(item, player))
                return $"Этот предмет предназначен для класса: {LocalizedClass(item.requiredClass)}.";
            if (equipmentIndex == (int)EquipmentSlot.OffHand && IsOffHandBlocked)
                return $"Вторая рука занята двуручным оружием «{Equipment[(int)EquipmentSlot.MainHand].name}».";
            return null;
        }

        public static bool IsClassCompatible(ItemInstance item, PlayerController player)
        {
            return item == null || player == null || string.IsNullOrEmpty(item.requiredClass) ||
                   string.Equals(item.requiredClass, player.Hero.heroClass.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTwoHanded(ItemInstance item)
        {
            if (item == null || item.kind != ItemKind.Weapon) return false;
            return item.weaponGrip == WeaponGrip.TwoHanded ||
                   (item.weaponGrip == WeaponGrip.None && GripFor(item.baseId) == WeaponGrip.TwoHanded);
        }

        public static string GripName(ItemInstance item) => item?.kind == ItemKind.Weapon
            ? IsTwoHanded(item) ? "Двуручное" : "Одноручное"
            : null;

        private static WeaponGrip GripFor(string baseId) => baseId switch
        {
            "staff" => WeaponGrip.TwoHanded,
            "crossbow" => WeaponGrip.TwoHanded,
            "axe" => WeaponGrip.TwoHanded,
            "sword" => WeaponGrip.OneHanded,
            "wand" => WeaponGrip.OneHanded,
            "dagger" => WeaponGrip.OneHanded,
            _ => WeaponGrip.None
        };

        private static void NormalizeLegacyItem(ItemInstance item)
        {
            if (item.kind == ItemKind.Accessory)
                item.kind = item.baseId == "ring" ? ItemKind.Ring : item.baseId == "amulet" ? ItemKind.Amulet : item.kind;
            if (item.kind == ItemKind.Weapon && item.weaponGrip == WeaponGrip.None)
                item.weaponGrip = GripFor(item.baseId);
        }

        private List<int> FreeBackpackDestinations(int sourceIndex, int needed)
        {
            var result = new List<int>(needed);
            if (needed <= 0) return result;
            result.Add(sourceIndex);
            for (var i = 0; i < Slots.Length && result.Count < needed; i++)
                if (i != sourceIndex && Slots[i] == null) result.Add(i);
            return result;
        }

        private static void ShowEquipFailure(string message)
        {
            if (GameManager.Instance != null) GameManager.Instance.ShowMessage(message);
        }

        private static string LocalizedClass(string value) => value?.ToLowerInvariant() switch
        {
            "mage" => "Маг", "warrior" => "Воин", "rogue" => "Разбойник", _ => value
        };

        public float EquipmentStat(Func<ItemInstance, float> selector)
        {
            var total = 0f;
            foreach (var item in Equipment) if (item != null) total += selector(item);
            return total;
        }
    }
}
