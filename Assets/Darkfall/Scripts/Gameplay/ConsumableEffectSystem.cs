using System.Collections.Generic;
using Darkfall.Core;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public static class ConsumableEffectSystem
    {
        public static bool Apply(ItemInstance item, PlayerController player)
        {
            if (item == null || player == null) return false;
            var applied = item.kind == ItemKind.Scroll ? ApplyScroll(item.baseId, player) : ApplyPotion(item, player);
            if (applied) GameManager.Instance.Audio.PlayEffect(item.kind == ItemKind.Scroll ? "Inventory_open" : "health_potion");
            return applied;
        }

        private static bool ApplyPotion(ItemInstance item, PlayerController player)
        {
            var level = Mathf.Max(1, item.itemLevel);
            switch (item.baseId)
            {
                case "potion":
                    if (player.Health >= player.MaxHealth) return false;
                    player.Heal(40 + level * 2.5f); break;
                case "speed_potion": player.ApplyTimedState(15, speed: 1 + (20 + level * 1.5f) / 100f); break;
                case "strength_potion": player.ApplyTimedState(20, damage: 1 + (15 + level * 2f) / 100f); break;
                case "defense_potion": player.ApplyTimedState(18, defense: 1 + (10 + level * 1.5f) / 100f); break;
                case "regen_potion": player.StartCoroutine(Regenerate(player, 60 + level * 3, 8, 2)); break;
                case "combo_potion":
                    player.Heal(25 + level * 1.5f);
                    player.ApplyTimedState(12, 1.25f + level * .01f, 1.18f + level * .008f);
                    break;
                case "purification_potion":
                    player.ClearNegativeEffects();
                    player.Heal(20 + level * 1.5f);
                    break;
                case "mystery_potion":
                    var effectCount = Random.Range(1, 4);
                    for (var i = 0; i < effectCount; i++) ApplyMysteryEffect(player, Random.value < .6f);
                    break;
                default: return false;
            }
            var color = item.baseId.Contains("speed") || item.baseId.Contains("purification")
                ? new Color(.2f, .72f, 1f) : item.baseId.Contains("regen")
                    ? new Color(.2f, .82f, .38f) : new Color(1f, .2f, .28f);
            CombatVfx.SpawnPulse(player.transform.position, color, 1.25f, .34f);
            return true;
        }

        private static bool ApplyScroll(string id, PlayerController player)
        {
            var enemies = EnemyController.Snapshot();
            switch (id)
            {
                case "scroll_werewolf":
                    player.ApplyTimedState(15, 1.3f, 1.5f);
                    player.ApplyDebuff(15, damage: .8f);
                    break;
                case "scroll_stone": player.ApplyTimedState(12, speed: 0.4f, defense: 2f); break;
                case "scroll_fire_explosion":
                    foreach (var enemy in enemies) if (Near(enemy, player, 3.75f))
                    {
                        enemy.TakeDamage(40);
                        enemy.ApplyDamageOverTime(5, 8, 1.2f);
                    }
                    break;
                case "scroll_ice_storm": foreach (var enemy in enemies) if (Near(enemy, player, 4.7f)) enemy.ApplySlow(.5f, 5); break;
                case "scroll_lightning":
                    enemies.Sort((a, b) => Distance(a, player).CompareTo(Distance(b, player)));
                    for (var i = 0; i < Mathf.Min(5, enemies.Count); i++) if (SameElevation(enemies[i], player))
                    {
                        CombatVfx.SpawnLightning(player.transform.position, enemies[i].transform.position,
                            new Color(1f, .86f, .28f));
                        enemies[i].TakeDamage(25);
                    }
                    break;
                case "scroll_earthquake":
                    foreach (var enemy in enemies) if (Near(enemy, player, 6.25f)) { enemy.TakeDamage(15); enemy.ApplySlow(.7f, 8); }
                    break;
                case "scroll_clone": PlayerClone.Spawn(player, 20); break;
                case "scroll_teleport": player.TeleportToRandomRoom(); break;
                case "scroll_invisibility": player.ApplyTimedState(8, invisible: true); break;
                case "scroll_time": foreach (var enemy in enemies) if (SameElevation(enemy, player)) enemy.ApplySlow(.6f, 10); break;
                case "scroll_curse":
                    foreach (var enemy in enemies) if (Near(enemy, player, 6.25f))
                    {
                        switch (Random.Range(0, 4))
                        {
                            case 0: enemy.ApplyDamageOverTime(5, 8, 1.5f); break;
                            case 1: enemy.ApplyDamageOverTime(5, 8, 1.2f); break;
                            case 2: enemy.ApplySlow(.5f, 8); break;
                            default: enemy.ApplyStun(2.5f); break;
                        }
                    }
                    break;
                case "scroll_chaos": foreach (var enemy in enemies) if (SameElevation(enemy, player)) enemy.ApplyChaos(15); break;
                case "scroll_fear": foreach (var enemy in enemies) if (SameElevation(enemy, player)) enemy.ApplyFear(12); break;
                case "scroll_smoke": player.ApplyTimedState(10, invisible: true); break;
                case "scroll_meteor":
                    enemies.RemoveAll(enemy => !SameElevation(enemy, player));
                    if (enemies.Count > 0)
                        player.StartCoroutine(Meteor(enemies[Random.Range(0, enemies.Count)].transform.position));
                    break;
                case "scroll_barrier": player.AddBarrier(100); break;
                case "scroll_rage": player.ApplyTimedState(12, damage: 2f); break;
                case "scroll_invulnerability": player.ApplyTimedState(5, invulnerable: true); break;
                case "scroll_vampirism": player.ApplyTimedState(15, vampirism: true); break;
                case "mystery_scroll":
                    var scrolls = new List<LegacyItem>();
                    foreach (var definition in LegacyCatalog.Data.items)
                        if (definition.baseId.StartsWith("scroll_")) scrolls.Add(definition);
                    return ApplyScroll(scrolls[Random.Range(0, scrolls.Count)].baseId, player);
                default: return false;
            }
            CombatVfx.PlayScrollCast(player.transform.position, id);
            return true;
        }

        private static bool Near(EnemyController enemy, PlayerController player, float radius) =>
            SameElevation(enemy, player) &&
            Vector2.Distance(enemy.transform.position, player.transform.position) <= radius;

        private static bool SameElevation(EnemyController enemy, PlayerController player)
        {
            var dungeon = GameManager.Instance?.Dungeon;
            return enemy != null && player != null &&
                   (dungeon == null || dungeon.SharesCombatElevation(enemy.transform.position,
                       player.transform.position));
        }

        private static float Distance(EnemyController enemy, PlayerController player) =>
            !SameElevation(enemy, player) ? float.MaxValue :
                Vector2.Distance(enemy.transform.position, player.transform.position);

        private static void DamageInRadius(System.Collections.Generic.IEnumerable<EnemyController> enemies, Vector2 point, float radius, float damage)
        {
            foreach (var enemy in enemies)
                if (enemy != null && Vector2.Distance(enemy.transform.position, point) <= radius) enemy.TakeDamage(damage);
        }

        private static System.Collections.IEnumerator Regenerate(PlayerController player, float total, float duration, float interval)
        {
            var ticks = Mathf.CeilToInt(duration / interval);
            for (var i = 0; i < ticks; i++) { player.Heal(total / ticks); yield return new WaitForSeconds(interval); }
        }

        private static System.Collections.IEnumerator Meteor(Vector2 point)
        {
            CombatVfx.SpawnPulse(point, new Color(1f, .22f, .04f), 2.5f, 3f);
            yield return new WaitForSeconds(3);
            CombatVfx.SpawnImpact(point, ProjectileVisualStyle.Arcane, new Color(1f, .2f, .03f), 2.1f);
            DamageInRadius(EnemyController.Snapshot(), point, 2.5f, 100);
        }

        private static void ApplyMysteryEffect(PlayerController player, bool positive)
        {
            if (positive)
            {
                switch (Random.Range(0, 9))
                {
                    case 0: player.Heal(35); break;
                    case 1: player.ApplyTimedState(12, damage: 1.35f); break;
                    case 2: player.ApplyTimedState(12, defense: 1.5f); break;
                    case 3: player.ApplyTimedState(12, speed: 1.35f); break;
                    case 4: player.ApplyTimedState(10, damage: 1.5f); break;
                    case 5: player.ApplyTimedState(10, speed: 1.25f); break;
                    case 6: player.Heal(50); break;
                    case 7: player.ApplyTimedState(12, damage: 1.25f); break;
                    default: player.AddBarrier(40); break;
                }
            }
            else
            {
                switch (Random.Range(0, 9))
                {
                    case 0: player.ApplyDamageOverTime(4, 8, 1); break;
                    case 1: player.ApplyDamageOverTime(5, 6.4f, .8f); break;
                    case 2: player.ApplyDebuff(5, speed: .3f); break;
                    case 3: player.ApplyDebuff(8, speed: .7f); break;
                    case 4: player.ApplyDebuff(8, damage: .8f); break;
                    case 5: player.ApplyDebuff(8, defense: .75f); break;
                    case 6: player.ApplyDebuff(8, damage: .7f); break;
                    case 7: player.ApplyDebuff(8, defense: .6f); break;
                    default: player.ApplyDebuff(8, speed: .55f); break;
                }
            }
        }
    }
}
