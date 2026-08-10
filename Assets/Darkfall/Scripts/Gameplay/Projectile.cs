using Darkfall.Core;
using System;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class Projectile : MonoBehaviour
    {
        private Vector2 direction;
        private float damage;
        private float expiresAt;
        private Action<EnemyController> onHit;
        private Color color;
        private ProjectileVisualStyle visualStyle;

        public static void Spawn(Vector2 position, Vector2 direction, float damage, Color color, Action<EnemyController> onHit = null)
        {
            var gameObject = new GameObject("Fireball");
            gameObject.transform.position = position;
            var projectile = gameObject.AddComponent<Projectile>();
            projectile.direction = direction.normalized;
            projectile.damage = damage;
            projectile.expiresAt = Time.time + 2.5f;
            projectile.onHit = onHit;
            projectile.color = color;
            projectile.visualStyle = CombatVfx.ConfigureProjectile(gameObject, direction, color, false, 30);
        }

        private void Update()
        {
            var game = GameManager.Instance;
            if (game == null || game.IsPaused || game.Dungeon == null) return;
            transform.position += (Vector3)(direction * (9f * Time.deltaTime));
            var enemy = EnemyController.FindNearest(transform.position, 0.48f);
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                onHit?.Invoke(enemy);
                Finish(true);
                return;
            }
            if (!game.Dungeon.CanOccupy(transform.position, 0.05f)) Finish(true);
            else if (Time.time >= expiresAt) Finish(false);
        }

        private void Finish(bool impact)
        {
            if (impact)
                CombatVfx.SpawnImpact(transform.position, visualStyle, color, .82f);
            Destroy(gameObject);
        }
    }
}
