using System;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    public enum HeroClass { Mage, Warrior, Rogue }

    [Serializable]
    public sealed class HeroDefinition
    {
        public HeroClass heroClass;
        public string displayName;
        [TextArea] public string description;
        public float maxHealth;
        public float damage;
        public float speed;
        public float attackCooldown;
        public float attackRange;
        public float defense;
        public float criticalChance;
        public Color color;

        public static HeroDefinition Create(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Warrior:
                    return new HeroDefinition
                    {
                        heroClass = heroClass, displayName = "Andre", description = "Стойкий воин ближнего боя",
                        maxHealth = 140, damage = 18, speed = 75f / 32f, attackCooldown = 1f,
                        attackRange = 64f / 32f, defense = 8, criticalChance = 0, color = new Color(0.9f, 0.2f, 0.17f)
                    };
                case HeroClass.Rogue:
                    return new HeroDefinition
                    {
                        heroClass = heroClass, displayName = "Tim", description = "Быстрый разбойник с рывком",
                        maxHealth = 85, damage = 25, speed = 110f / 32f, attackCooldown = 0.7f,
                        attackRange = 56f / 32f, defense = 2, criticalChance = 0.15f, color = new Color(0.15f, 0.75f, 0.3f)
                    };
                default:
                    return new HeroDefinition
                    {
                        heroClass = heroClass, displayName = "Dimon", description = "Маг дальнего боя с огненными сферами",
                        maxHealth = 80, damage = 15, speed = 70f / 32f, attackCooldown = 1.2f,
                        attackRange = 200f / 32f, defense = 2, criticalChance = 0.05f, color = new Color(0.55f, 0.25f, 0.8f)
                    };
            }
        }
    }

    [CreateAssetMenu(menuName = "Darkfall/Game Balance", fileName = "GameBalance")]
    public sealed class GameBalance : ScriptableObject
    {
        [Header("Dungeon")]
        [Min(28)] public int mapSize = 60;
        [Min(4)] public int roomAttempts = 15;
        [Min(4)] public int minimumRoomSize = 8;
        [Min(7)] public int maximumRoomSize = 16;

        [Header("Progression")]
        [Min(1)] public int baseEnemyCount = 12;
        [Min(1)] public int bossEveryLevels = 10;
        [Min(0.01f)] public float enemyHealthPerLevel = 0.12f;
        [Min(0.01f)] public float enemyDamagePerLevel = 0.12f;

        public static GameBalance RuntimeDefault()
        {
            return CreateInstance<GameBalance>();
        }
    }

    [Serializable]
    public sealed class RunRecord
    {
        public int depth;
        public int kills;
        public float seconds;
        public string hero;
        public string date;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int bestDepth;
        public int totalKills;
        public float masterVolume = 0.8f;
        public float musicVolume = 0.65f;
        public float sfxVolume = 0.8f;
        public bool audioEnabled = true;
        public bool showHelp = true;
        public List<RunRecord> topRecords = new List<RunRecord>();
    }
}
