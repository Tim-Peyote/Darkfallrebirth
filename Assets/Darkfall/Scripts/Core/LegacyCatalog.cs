using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    [Serializable]
    public sealed class LegacyCatalogData
    {
        public LegacyCharacter[] characters;
        public LegacyEnemy[] enemies;
        public LegacyEnemy[] bosses;
        public LegacyItem[] items;
        public LegacyAffix[] affixes;
        public LegacyRarity[] rarities;
        public LegacyShopUpgrade[] shop;
    }

    [Serializable]
    public sealed class LegacyCharacter
    {
        public string id, name, heroClass, description, type, color;
        public float hp, maxHp, damage, moveSpeed, attackSpeed, attackRadius, defense, crit, projectileSpeed;
    }

    [Serializable]
    public sealed class LegacyEnemy
    {
        public string type, color;
        public float hp, damage, speed, attackRange, reward, projectileSpeed;
        public int levelRequirement, levelTier;
        public bool hasBow, canFreeze, canPoison, canStun, canTeleport, canReflect;
        public float freezeChance, freezeDuration, poisonChance, poisonDamage, poisonDuration;
        public float stunChance, stunDuration, teleportChance, reflectChance;
        public string[] abilities;
    }

    [Serializable]
    public sealed class LegacyItem
    {
        public string baseId, name, requiredClass, icon, color, type, slot, description;
        public float minRadius, maxRadius;
    }

    [Serializable]
    public sealed class LegacyAffix
    {
        public string key, name;
        public float min, max;
    }

    [Serializable]
    public sealed class LegacyRarity
    {
        public string key, name, color;
        public float chance;
    }

    [Serializable]
    public sealed class LegacyShopUpgrade
    {
        public string id, name, description, icon, stat;
        public float basePrice, value;
        public int maxPurchases;
    }

    public static class LegacyCatalog
    {
        private static LegacyCatalogData data;
        private static Dictionary<string, LegacyItem> itemById;
        public static LegacyCatalogData Data => data ??= Load();
        public static IReadOnlyList<LegacyItem> Items => Data.items;
        public static IReadOnlyList<LegacyEnemy> Enemies => Data.enemies;
        public static IReadOnlyList<LegacyEnemy> Bosses => Data.bosses;

        public static LegacyItem Item(string baseId)
        {
            EnsureIndex();
            return itemById.TryGetValue(baseId, out var item) ? item : null;
        }

        public static LegacyRarity RollRarity()
        {
            var roll = UnityEngine.Random.value;
            var cumulative = 0f;
            foreach (var rarity in Data.rarities)
            {
                cumulative += rarity.chance;
                if (roll <= cumulative) return rarity;
            }
            return Data.rarities[0];
        }

        private static LegacyCatalogData Load()
        {
            var asset = Resources.Load<TextAsset>("Data/legacy-catalog");
            if (asset == null) throw new InvalidOperationException("Legacy catalog is missing.");
            var parsed = JsonUtility.FromJson<LegacyCatalogData>(asset.text);
            if (parsed?.items == null || parsed.items.Length != 46)
                throw new InvalidOperationException("Legacy catalog failed parity validation: expected 46 base items.");
            return parsed;
        }

        private static void EnsureIndex()
        {
            if (itemById != null) return;
            itemById = new Dictionary<string, LegacyItem>(StringComparer.Ordinal);
            foreach (var item in Data.items) itemById[item.baseId] = item;
        }
    }
}
