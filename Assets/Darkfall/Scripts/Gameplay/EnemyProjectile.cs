using System;
using Darkfall.Core;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private Vector2 direction;
        private float damage;
        private float speed;
        private float expiresAt;
        private Action onHit;
        private Color color;
        private ProjectileVisualStyle visualStyle;

        public static void Spawn(Vector2 position, Vector2 direction, float damage, float speed, Color color, Action onHit)
        {
            var projectileObject = new GameObject("Enemy Projectile");
            projectileObject.transform.position = position;
            var projectile = projectileObject.AddComponent<EnemyProjectile>();
            projectile.direction = direction.normalized;
            projectile.damage = damage;
            projectile.speed = speed;
            projectile.expiresAt = Time.time + 4f;
            projectile.onHit = onHit;
            projectile.color = color;
            projectile.visualStyle = CombatVfx.ConfigureProjectile(projectileObject, direction, color, true, 29);
        }

        private void Update()
        {
            var game = GameManager.Instance;
            if (game == null || game.IsPaused || game.Dungeon == null) return;
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            var player = game.Player;
            if (player != null && Vector2.Distance(transform.position, player.transform.position) < .45f)
            {
                player.TakeDamage(damage);
                onHit?.Invoke();
                Finish(true);
                return;
            }
            if (!game.Dungeon.CanOccupy(transform.position, .05f)) Finish(true);
            else if (Time.time >= expiresAt) Finish(false);
        }

        private void Finish(bool impact)
        {
            if (impact)
                CombatVfx.SpawnImpact(transform.position, visualStyle, color, .72f);
            Destroy(gameObject);
        }
    }
}
