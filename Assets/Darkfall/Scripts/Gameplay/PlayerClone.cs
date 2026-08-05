using Darkfall.Core;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class PlayerClone : MonoBehaviour
    {
        private PlayerController owner;
        private float expiresAt;
        private float nextAttack;

        public static void Spawn(PlayerController player, float duration)
        {
            var cloneObject = new GameObject("Arcane Clone");
            cloneObject.transform.position = player.transform.position + new Vector3(.8f, .3f);
            var clone = cloneObject.AddComponent<PlayerClone>();
            clone.owner = player;
            clone.expiresAt = Time.time + duration;
            var visual = new GameObject("Clone Visual");
            visual.transform.SetParent(cloneObject.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = DirectionalSpriteAtlas.HeroPortrait(player.Hero.heroClass);
            renderer.color = new Color(.4f, .8f, 1f, .65f);
            renderer.sortingOrder = 19;
            DarkfallRenderMaterials.MakeLit(renderer);
            visual.transform.localScale = Vector3.one * 1.2f;
            visual.AddComponent<IsoVisual>().Initialize(cloneObject.transform, 0f, 1000);
        }

        private void Update()
        {
            if (owner == null || Time.time >= expiresAt) { Destroy(gameObject); return; }
            if (GameManager.Instance == null || GameManager.Instance.IsPaused || Time.time < nextAttack) return;
            var target = EnemyController.FindNearest(transform.position, owner.AttackRange);
            if (target == null) return;
            nextAttack = Time.time + owner.Hero.attackCooldown;
            Projectile.Spawn(transform.position,
                ((Vector2)(target.transform.position - transform.position)).normalized,
                owner.Damage * .5f, new Color(.35f, .8f, 1f));
        }
    }
}
