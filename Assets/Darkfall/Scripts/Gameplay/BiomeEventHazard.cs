using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class BiomeEventHazard : MonoBehaviour
    {
        private PlayerController player;
        private float nextTick;
        private float radius;
        private float damage;
        private Darkfall.World.DungeonHazardKind kind;
        private float nextTelegraph;

        public void Initialize(float effectRadius, float damagePerSecond,
            Darkfall.World.DungeonHazardKind hazardKind = Darkfall.World.DungeonHazardKind.EmberSeep)
        {
            radius = effectRadius;
            damage = damagePerSecond;
            kind = hazardKind;
            nextTelegraph = Time.time + Random.Range(.15f, .55f);
        }

        private void Update()
        {
            player ??= FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance <= radius * 1.8f && Time.time >= nextTelegraph)
            {
                nextTelegraph = Time.time + 1.15f;
                var color = kind == Darkfall.World.DungeonHazardKind.Lava ||
                            kind == Darkfall.World.DungeonHazardKind.EmberSeep
                    ? new Color(1f, .28f, .045f, .72f)
                    : kind == Darkfall.World.DungeonHazardKind.Brine
                        ? new Color(.12f, .58f, .72f, .58f)
                        : kind == Darkfall.World.DungeonHazardKind.Bile
                            ? new Color(.52f, .68f, .08f, .62f)
                            : new Color(.62f, .18f, .86f, .68f);
                CombatVfx.SpawnPulse(transform.position, color, radius, .78f);
            }
            if (Time.time < nextTick || distance > radius) return;
            nextTick = Time.time + .5f;
            player.TakeDamage(damage * .5f);
            player.ApplyEnvironmentalStatus(kind, damage);
        }
    }
}
