using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class BiomeEventHazard : MonoBehaviour
    {
        private PlayerController player;
        private float nextTick;
        private float radius;
        private float damage;

        public void Initialize(float effectRadius, float damagePerSecond)
        {
            radius = effectRadius;
            damage = damagePerSecond;
        }

        private void Update()
        {
            if (Time.time < nextTick) return;
            player ??= FindFirstObjectByType<PlayerController>();
            if (player == null || Vector2.Distance(transform.position, player.transform.position) > radius) return;
            nextTick = Time.time + .5f;
            player.TakeDamage(damage * .5f);
        }
    }
}
