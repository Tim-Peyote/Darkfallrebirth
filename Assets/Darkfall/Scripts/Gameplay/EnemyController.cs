using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class EnemyController : MonoBehaviour
    {
        private static readonly List<EnemyController> Active = new List<EnemyController>();
        private DungeonData dungeon;
        private PlayerController player;
        private float health;
        private float damage;
        private float speed;
        private float nextAttack;
        private bool boss;
        private SpriteRenderer spriteRenderer;
        private LegacyEnemy definition;
        private float attackRange;
        private float stunnedUntil;
        private float afraidUntil;
        private float chaoticUntil;
        private float maxHealth;
        private float reward;
        private float projectileSpeed;
        private bool ranged;
        private float baseSpeed;
        private float baseDamage;
        private float abilityReadyAt;
        private int bossPhase = 1;
        private int abilityIndex;
        private Transform visual;
        private float nextTeleportCheck;
        private Vector2 previousPosition;
        private Vector2 facingDirection = Vector2.down;
        private Vector2 spriteFacingDirection = Vector2.down;
        private float attackAnimationUntil;
        private float hitAnimationUntil;
        private string directionalSheet;

        public static int Count => Active.Count;
        public bool IsBoss => boss;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public string DisplayName => definition?.type ?? name;

        public void Initialize(DungeonData data, PlayerController target, int depth, bool isBoss, LegacyEnemy enemyDefinition)
        {
            if (data == null || target == null || enemyDefinition == null)
            {
                Debug.LogError("Enemy initialization failed: dungeon, player or enemy definition is missing.", this);
                enabled = false;
                Destroy(gameObject);
                return;
            }
            dungeon = data;
            player = target;
            boss = isBoss;
            definition = enemyDefinition;
            var balance = GameManager.Instance?.Balance;
            var healthPerLevel = balance != null ? balance.enemyHealthPerLevel : .12f;
            var damagePerLevel = balance != null ? balance.enemyDamagePerLevel : .12f;
            var healthProgression = 1f + Mathf.Max(0, depth - 1) * healthPerLevel + Mathf.Max(0, depth - 10) * .03f;
            var damageProgression = 1f + Mathf.Max(0, depth - 1) * damagePerLevel + Mathf.Max(0, depth - 10) * .025f;
            var bossScale = 1f + Mathf.Max(0, depth - 10) * .08f;
            var healthScale = boss ? bossScale : healthProgression;
            var damageScale = boss ? bossScale : damageProgression;
            health = definition.hp * healthScale;
            maxHealth = health;
            damage = definition.damage * damageScale;
            speed = Mathf.Max(.75f, definition.speed / 32f);
            if (!boss && depth >= 5) speed *= 1 + Mathf.Min(.3f, (depth - 5) * .02f);
            baseSpeed = speed;
            baseDamage = damage;
            attackRange = Mathf.Max(0.9f, definition.attackRange / 32f);
            reward = definition.reward;
            projectileSpeed = Mathf.Max(4f, definition.projectileSpeed / 32f);
            ranged = definition.projectileSpeed > 0 || definition.hasBow;
            gameObject.name = definition.type;
            var lowerType = definition.type.ToLowerInvariant();
            directionalSheet = lowerType.Contains("mimic") ? "enemy-mimic-v1" :
                lowerType.Contains("archer") || lowerType.Contains("spitter") || lowerType.Contains("assassin")
                ? "enemy-ranged-v2"
                : lowerType.Contains("mage") || lowerType.Contains("wraith") || lowerType.Contains("demon") ||
                  lowerType.Contains("lich") || lowerType.Contains("dragon") ? "enemy-caster-v2" : "enemy-melee-v2";

            visual = new GameObject("Animated Visual").transform;
            visual.SetParent(transform, false);
            spriteRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = DirectionalSpriteAtlas.Get(directionalSheet, Vector2.down, CharacterMotion.Idle, 0f)
                                    ?? GameSpriteAtlas.Enemy(definition.type);
            spriteRenderer.color = boss ? new Color(1f, 0.42f, 0.42f) : ParseColor(definition.color);
            spriteRenderer.sortingOrder = 15;
            DarkfallRenderMaterials.MakeLit(spriteRenderer);
            visual.localScale = Vector3.one * (boss ? 2.1f : 1.25f);
            gameObject.AddComponent<CircleCollider2D>().radius = 0.46f;
            Active.Add(this);
            abilityReadyAt = Time.time + 4.5f;
            previousPosition = transform.position;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            var manager = GameManager.Instance;
            if (player == null || dungeon == null || definition == null || manager == null || manager.IsPaused || player.IsInvisible) return;
            if (boss) UpdateBossPhaseAndAbilities();
            if (Time.time < stunnedUntil) return;
            var toPlayer = (Vector2)(player.transform.position - transform.position);
            if (toPlayer.sqrMagnitude > .001f) facingDirection = toPlayer.normalized;
            Animate((Vector2)transform.position - previousPosition);
            previousPosition = transform.position;
            var distance = toPlayer.magnitude;
            if (definition.canTeleport && Time.time >= nextTeleportCheck)
            {
                nextTeleportCheck = Time.time + 1.2f;
                if (Random.value < definition.teleportChance)
                {
                    var destination = (Vector2)player.transform.position + Random.insideUnitCircle.normalized * Random.Range(2f, 4f);
                    if (dungeon.CanOccupy(destination)) transform.position = destination;
                }
            }
            if (Time.time < chaoticUntil)
            {
                var other = FindNearestOther(this, attackRange + 4f);
                if (other != null)
                {
                    MoveOrAttackEnemy(other);
                    return;
                }
            }
            if (Time.time < afraidUntil)
            {
                var nextAway = (Vector2)transform.position - toPlayer.normalized * speed * Time.deltaTime;
                if (dungeon.CanOccupy(nextAway)) transform.position = nextAway;
                return;
            }
            if (distance > attackRange)
            {
                var next = (Vector2)transform.position + toPlayer.normalized * speed * Time.deltaTime;
                if (dungeon.CanOccupy(next)) transform.position = next;
            }
            else if (Time.time >= nextAttack)
            {
                nextAttack = Time.time + (boss ? 0.85f : 1.15f);
                attackAnimationUntil = Time.time + .28f;
                if (ranged)
                    EnemyProjectile.Spawn(transform.position, toPlayer.normalized, damage, projectileSpeed, ParseColor(definition.color), ApplyOnHitEffect);
                else
                {
                    player.TakeDamage(damage);
                    ApplyOnHitEffect();
                }
            }
        }

        private void Animate(Vector2 movement)
        {
            if (visual == null) return;
            var motion = Time.time < hitAnimationUntil ? CharacterMotion.Hit :
                Time.time < attackAnimationUntil ? CharacterMotion.Attack :
                movement.sqrMagnitude > .00001f ? CharacterMotion.Walk : CharacterMotion.Idle;
            var animationTime = motion == CharacterMotion.Hit ? Time.time - (hitAnimationUntil - .2f) :
                motion == CharacterMotion.Attack ? Time.time - (attackAnimationUntil - .28f) : Time.time;
            spriteFacingDirection = DirectionalSpriteAtlas.StabilizeFourWay(facingDirection, spriteFacingDirection);
            var directional = DirectionalSpriteAtlas.Get(directionalSheet, spriteFacingDirection, motion,
                animationTime, out var flipX);
            if (directional != null)
            {
                spriteRenderer.sprite = directional;
                spriteRenderer.flipX = flipX;
                spriteRenderer.color = boss ? new Color(1f, .72f, .72f) : Color.white;
                visual.localScale = Vector3.one * (boss ? 1.55f : 1f);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                return;
            }
            var frequency = boss ? 3.5f : 7f;
            visual.localPosition = new Vector3(0, Mathf.Sin(Time.time * frequency + GetInstanceID()) * .035f, 0);
            visual.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * frequency) * (boss ? 1.2f : 2f));
            if (spriteRenderer != null) spriteRenderer.flipX = facingDirection.x < 0;
        }

        public void TakeDamage(float amount)
        {
            if (definition != null && definition.canReflect && Random.value < definition.reflectChance)
            {
                if (player != null) player.TakeDamage(amount * 0.5f);
                return;
            }
            health -= amount;
            hitAnimationUntil = Time.time + .2f;
            if (GameManager.Instance.Player != null && GameManager.Instance.Player.Vampirism)
                GameManager.Instance.Player.Heal(amount * 0.5f);
            if (health > 0)
            {
                StartCoroutine(HitFlash());
                GameManager.Instance.Audio.PlayEffect("enemy_hit");
                return;
            }
            GameManager.Instance.EnemyDefeated(transform.position, boss, reward);
            Destroy(gameObject);
        }

        private System.Collections.IEnumerator HitFlash()
        {
            var color = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.06f);
            if (spriteRenderer != null) spriteRenderer.color = color;
        }

        public static EnemyController FindNearest(Vector2 point, float range)
        {
            EnemyController result = null;
            var best = range * range;
            for (var i = 0; i < Active.Count; i++)
            {
                if (Active[i] == null) continue;
                var distance = ((Vector2)Active[i].transform.position - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                result = Active[i];
            }
            return result;
        }

        public static List<EnemyController> Snapshot() => new List<EnemyController>(Active);
        public static void ClearRegistry() => Active.Clear();

        public void ApplySlow(float factor, float duration) => StartCoroutine(SlowRoutine(factor, duration));
        public void ApplyStun(float duration) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
        public void ApplyFear(float duration) => afraidUntil = Mathf.Max(afraidUntil, Time.time + duration);
        public void ApplyChaos(float duration) => chaoticUntil = Mathf.Max(chaoticUntil, Time.time + duration);
        public void ApplyDamageOverTime(float amount, float duration, float interval) => StartCoroutine(DamageOverTime(amount, duration, interval));
        private System.Collections.IEnumerator SlowRoutine(float factor, float duration)
        {
            speed *= factor;
            yield return new WaitForSeconds(duration);
            speed /= factor;
        }

        private System.Collections.IEnumerator DamageOverTime(float amount, float duration, float interval)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += interval)
            {
                yield return new WaitForSeconds(interval);
                if (this == null) yield break;
                TakeDamage(amount);
            }
        }

        private void MoveOrAttackEnemy(EnemyController other)
        {
            var direction = (Vector2)(other.transform.position - transform.position);
            if (direction.magnitude > attackRange)
            {
                var next = (Vector2)transform.position + direction.normalized * speed * Time.deltaTime;
                if (dungeon.CanOccupy(next)) transform.position = next;
            }
            else if (Time.time >= nextAttack)
            {
                nextAttack = Time.time + 1.15f;
                attackAnimationUntil = Time.time + .28f;
                other.TakeDamage(damage);
            }
        }

        private static EnemyController FindNearestOther(EnemyController source, float range)
        {
            EnemyController result = null;
            var best = range * range;
            foreach (var candidate in Active)
            {
                if (candidate == null || candidate == source) continue;
                var distance = ((Vector2)(candidate.transform.position - source.transform.position)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                result = candidate;
            }
            return result;
        }

        private void ApplyOnHitEffect()
        {
            if (definition.canFreeze && Random.value < definition.freezeChance)
                player.ApplyDebuff(definition.freezeDuration, speed: 0.3f);
            if (definition.canPoison && Random.value < definition.poisonChance)
                player.ApplyDamageOverTime(definition.poisonDamage, definition.poisonDuration, 1f);
            if (definition.canStun && Random.value < definition.stunChance)
                player.ApplyDebuff(definition.stunDuration, stunned: true);
        }

        private void UpdateBossPhaseAndAbilities()
        {
            var ratio = maxHealth <= 0 ? 0 : health / maxHealth;
            var phase = ratio <= .25f ? 3 : ratio <= .5f ? 2 : 1;
            if (phase != bossPhase)
            {
                bossPhase = phase;
                speed = baseSpeed * (phase == 3 ? 1.6f : 1.3f);
                damage = baseDamage * (phase == 3 ? 1.4f : 1.2f);
                GameManager.Instance.ShowMessage($"{DisplayName}: фаза {phase}");
                StartCoroutine(HitFlash());
            }
            if (Time.time < abilityReadyAt || definition?.abilities == null || definition.abilities.Length == 0) return;
            abilityReadyAt = Time.time + (bossPhase == 3 ? 2f : bossPhase == 2 ? 3.5f : 5f);
            var ability = definition.abilities[abilityIndex++ % definition.abilities.Length];
            UseBossAbility(ability);
        }

        private void UseBossAbility(string ability)
        {
            attackAnimationUntil = Time.time + .28f;
            var toPlayer = (Vector2)(player.transform.position - transform.position);
            switch (ability)
            {
                case "charge":
                    var destination = (Vector2)transform.position;
                    var chargeOrigin = destination;
                    for (var i = 0; i < 15; i++)
                    {
                        var next = destination + toPlayer.normalized * .25f;
                        if (!dungeon.CanOccupy(next)) break;
                        destination = next;
                    }
                    transform.position = destination;
                    CombatVfx.SpawnAfterimage(chargeOrigin, spriteRenderer.sprite,
                        new Color(.75f, .12f, .08f, .7f), facingDirection);
                    CombatVfx.SpawnPulse(destination, new Color(.85f, .16f, .08f), 1.5f, .3f);
                    if (Vector2.Distance(transform.position, player.transform.position) < 1.2f) player.TakeDamage(60);
                    break;
                case "summon":
                    CombatVfx.SpawnPulse(transform.position, new Color(.55f, .2f, .78f), 2.2f, .5f);
                    var count = definition.type == "Skeleton King" ? 3 : 2;
                    for (var i = 0; i < count; i++)
                    {
                        var angle = i * Mathf.PI * 2 / count;
                        GameManager.Instance.SpawnSummonedSkeleton((Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.3f);
                    }
                    break;
                case "firebreath":
                    var shots = bossPhase == 3 ? 7 : 5;
                    var baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x);
                    for (var i = 0; i < shots; i++)
                    {
                        var offset = (i - (shots - 1) * .5f) * .16f;
                        var direction = new Vector2(Mathf.Cos(baseAngle + offset), Mathf.Sin(baseAngle + offset));
                        EnemyProjectile.Spawn(transform.position, direction, 35, 9.4f, new Color(1f, .28f, .04f), null);
                    }
                    break;
                case "stomp":
                    CombatVfx.SpawnPulse(transform.position, new Color(.8f, .42f, .12f), 100f / 32f, .55f);
                    if (toPlayer.magnitude <= 100f / 32f) player.TakeDamage(45);
                    break;
                case "teleport":
                    var teleportOrigin = (Vector2)transform.position;
                    for (var attempt = 0; attempt < 12; attempt++)
                    {
                        var offset = Random.insideUnitCircle * Random.Range(2f, 6f);
                        var point = (Vector2)player.transform.position + offset;
                        if (!dungeon.CanOccupy(point)) continue;
                        transform.position = point;
                        break;
                    }
                    CombatVfx.SpawnAfterimage(teleportOrigin, spriteRenderer.sprite,
                        new Color(.35f, .18f, .78f, .7f), facingDirection);
                    CombatVfx.SpawnPulse(transform.position, new Color(.55f, .25f, .9f), 1.4f, .34f);
                    break;
                case "curse":
                    CombatVfx.SpawnAura(player.transform, new Color(.7f, .08f, .55f), 5f, .92f);
                    player.ApplyDebuff(5, damage: .8f, speed: .7f, defense: .8f);
                    break;
            }
        }

        private static Color ParseColor(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
        }
    }
}
