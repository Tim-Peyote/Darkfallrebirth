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

        public void Initialize(float effectRadius, float damagePerSecond,
            Darkfall.World.DungeonHazardKind hazardKind = Darkfall.World.DungeonHazardKind.EmberSeep)
        {
            radius = effectRadius;
            damage = damagePerSecond;
            kind = hazardKind;
        }

        private void Update()
        {
            if (Time.time < nextTick) return;
            player ??= FindFirstObjectByType<PlayerController>();
            if (player == null || Vector2.Distance(transform.position, player.transform.position) > radius) return;
            nextTick = Time.time + .5f;
            player.TakeDamage(damage * .5f);
            player.ApplyEnvironmentalStatus(kind, damage);
        }
    }
}
