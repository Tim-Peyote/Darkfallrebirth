using Darkfall.Core;
using Darkfall.World;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public readonly struct PlayerStatusSnapshot
    {
        public readonly string Label;
        public readonly float Remaining;
        public readonly bool Negative;
        public PlayerStatusSnapshot(string label, float remaining, bool negative)
        {
            Label = label; Remaining = remaining; Negative = negative;
        }
    }

    public sealed class PlayerController : MonoBehaviour
    {
        private HeroDefinition hero;
        private DungeonData dungeon;
        private SpriteRenderer spriteRenderer;
        private float nextAttack;
        private float abilityReadyAt;
        private float shieldUntil;
        private float damageInvulnerableUntil;
        private float barrier;
        private readonly List<ActiveDebuff> debuffs = new List<ActiveDebuff>();
        private readonly List<ActiveTimedState> timedStates = new List<ActiveTimedState>();
        private Camera gameplayCamera;
        private Transform visual;
        private Vector2 lastMoveDirection = Vector2.right;
        private Vector2 facingDirection = Vector2.right;
        private Vector2 spriteFacingDirection = Vector2.right;
        private Vector2 actualVelocity;
        private float attackAnimationUntil;
        private float hitAnimationUntil;
        private float nextHazardTick;
        private ActiveDebuff hazardDebuff;
        private float hazardVfxUntil;
        private string directionalSheet;
        private float visualScale = 1f;

        public float Health { get; private set; }
        public float MaxHealth => hero.maxHealth + EquipmentStat(item => item.maxHp);
        public HeroDefinition Hero => hero;
        public float DamageMultiplier { get; private set; } = 1;
        public float SpeedMultiplier { get; private set; } = 1;
        public float DefenseMultiplier { get; private set; } = 1;
        public bool IsInvulnerable { get; private set; }
        public bool IsInvisible { get; private set; }
        public bool Vampirism { get; private set; }
        public bool IsStunned { get; private set; }
        public float Barrier => barrier;
        public float AbilityCooldownRemaining => Mathf.Max(0, abilityReadyAt - Time.time);
        public float Damage => (hero.damage + EquipmentStat(item => item.damage)) * DamageMultiplier;
        public float Defense => (hero.defense + EquipmentStat(item => item.defense)) * DefenseMultiplier;
        public float CriticalChance => Mathf.Clamp01(hero.criticalChance + EquipmentStat(item => item.crit) / 100f);
        public float AttackRange => hero.attackRange + EquipmentStat(item => item.attackRadius) / 32f;
        public float BaseMoveSpeed => hero.speed;
        public float BaseAttackCooldown => hero.attackCooldown;
        public bool DeveloperInvincible { get; private set; }
        public float FireResistance => Mathf.Clamp(EquipmentStat(item => item.fire), 0, 75);
        public float IceResistance => Mathf.Clamp(EquipmentStat(item => item.ice), 0, 75);
        public Vector2 FacingDirection => facingDirection;
        public Vector2 ActualVelocity => actualVelocity;
        public bool IsAttacking => Time.time < attackAnimationUntil;
        public bool IsTakingHit => Time.time < hitAnimationUntil;
        public Sprite CurrentSprite => spriteRenderer != null ? spriteRenderer.sprite : null;

        public void Initialize(HeroDefinition definition, DungeonData data, float? carriedHealth = null)
        {
            hero = definition;
            dungeon = data;
            Health = carriedHealth.HasValue ? Mathf.Clamp(carriedHealth.Value, 0f, MaxHealth) : MaxHealth;
            gameplayCamera = Camera.main;
            visual = new GameObject("Animated Visual").transform;
            visual.SetParent(transform, false);
            spriteRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = DirectionalSpriteAtlas.HeroPortrait(hero.heroClass);
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 20;
            DarkfallRenderMaterials.MakeLit(spriteRenderer);
            // The source sheets have deliberately different transparent gutters and silhouette
            // heights. A single transform scale made the rogue visibly undersized and caused the
            // apparent size to jump when a directional frame changed.
            visualScale = hero.heroClass == HeroClass.Rogue ? 1.38f :
                hero.heroClass == HeroClass.Warrior ? .96f : 1f;
            visual.localScale = Vector3.one * visualScale;
            visual.gameObject.AddComponent<IsoVisual>().Initialize(transform, 0f, 1000);
            directionalSheet = hero.heroClass == HeroClass.Mage ? "mage-v2" :
                hero.heroClass == HeroClass.Warrior ? "warrior-v2" : "rogue-v2";
            gameObject.AddComponent<CircleCollider2D>().radius = 0.45f;
            GameManager.Instance.Inventory.Changed += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance?.Inventory != null) GameManager.Instance.Inventory.Changed -= OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            Health = Mathf.Min(Health, MaxHealth);
            GameManager.Instance.NotifyStatsChanged();
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused) return;
            if (IsStunned) return;
            var input = GameInput.Move;
            UpdateFacing(input);
            Move(input);
            UpdateFloorHazard();
            Animate();
            if (Time.time >= nextAttack) Attack();
            if (GameInput.ConsumeAbility()) UseAbility();
            if (hero.heroClass == HeroClass.Rogue && GameInput.RogueDashPressed) UseAbility();
        }

        private void UpdateFloorHazard()
        {
            if (dungeon == null || Time.time < nextHazardTick) return;
            if (!dungeon.TryGetHazardAt(transform.position, out var hazard) || hazard.DamagePerSecond <= 0f) return;
            const float interval = .5f;
            nextHazardTick = Time.time + interval;
            TakeDamage(hazard.DamagePerSecond * interval);
            ApplyHazardDebuff(hazard.Kind, hazard.DamagePerSecond);
        }

        private void ApplyHazardDebuff(DungeonHazardKind kind, float damagePerSecond)
        {
            const float lingerDuration = 2.5f;
            var key = "hazard:" + kind;
            if (hazardDebuff != null && hazardDebuff.key == key)
            {
                hazardDebuff.expiresAt = Time.time + lingerDuration;
                if (Time.time + .6f >= hazardVfxUntil)
                {
                    CombatVfx.SpawnStatus(transform, HazardStatusStyle(kind), lingerDuration, 1f);
                    hazardVfxUntil = Time.time + lingerDuration;
                }
                return;
            }
            if (hazardDebuff != null) RemoveDebuff(hazardDebuff);

            var label = kind == DungeonHazardKind.Lava || kind == DungeonHazardKind.EmberSeep ? "ГОРЕНИЕ" :
                kind == DungeonHazardKind.Brine ? "ПРОМОКАНИЕ" :
                kind == DungeonHazardKind.Bile ? "КОРРОЗИЯ" : "СКВЕРНА БЕЗДНЫ";
            var style = HazardStatusStyle(kind);
            var speed = kind == DungeonHazardKind.Brine ? .82f : 1f;
            var defense = kind == DungeonHazardKind.Bile ? .88f : 1f;
            var damage = kind == DungeonHazardKind.VoidRift ? .88f : 1f;
            hazardDebuff = new ActiveDebuff
            {
                key = key, label = label, speed = speed, defense = defense, damage = damage,
                expiresAt = Time.time + lingerDuration
            };
            // Environmental danger must remain visible in the four-slot HUD even when the hero
            // is already carrying several combat curses.
            debuffs.Insert(0, hazardDebuff);
            SpeedMultiplier *= speed;
            DefenseMultiplier *= defense;
            DamageMultiplier *= damage;
            hazardDebuff.routine = StartCoroutine(HazardDebuffRoutine(hazardDebuff, kind, damagePerSecond));
            CombatVfx.SpawnStatus(transform, style, lingerDuration, 1f);
            hazardVfxUntil = Time.time + lingerDuration;
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ApplyEnvironmentalStatus(DungeonHazardKind kind, float damagePerSecond)
        {
            ApplyHazardDebuff(kind, damagePerSecond);
        }

        private static StatusVisualStyle HazardStatusStyle(DungeonHazardKind kind)
        {
            if (kind == DungeonHazardKind.Lava || kind == DungeonHazardKind.EmberSeep)
                return StatusVisualStyle.Burning;
            if (kind == DungeonHazardKind.Brine) return StatusVisualStyle.Drenched;
            if (kind == DungeonHazardKind.Bile) return StatusVisualStyle.Corrosion;
            return StatusVisualStyle.Void;
        }

        private System.Collections.IEnumerator HazardDebuffRoutine(ActiveDebuff debuff,
            DungeonHazardKind kind, float damagePerSecond)
        {
            const float interval = .5f;
            while (Time.time < debuff.expiresAt)
            {
                yield return new WaitForSeconds(interval);
                if (!debuffs.Contains(debuff)) yield break;
                if (kind != DungeonHazardKind.Brine)
                    TakeDamage(Mathf.Max(1f, damagePerSecond * .16f));
            }
            RemoveDebuff(debuff);
        }

        private void LateUpdate()
        {
            if (gameplayCamera == null) return;
            var target = IsoWorld.Project((Vector2)transform.position) +
                         Vector2.up * (dungeon != null ? dungeon.SurfaceHeight(transform.position) : 0f);
            var current = gameplayCamera.transform.position;
            gameplayCamera.transform.position = Vector3.Lerp(current, new Vector3(target.x, target.y, -10), 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        private void UpdateFacing(Vector2 movement)
        {
            if (gameplayCamera != null && Input.mousePresent && !Application.isMobilePlatform)
            {
                var mouse = gameplayCamera.ScreenToWorldPoint(Input.mousePosition);
                var logicalMouse = IsoWorld.Unproject(mouse);
                var aim = logicalMouse - (Vector2)transform.position;
                if (aim.sqrMagnitude > .16f)
                {
                    facingDirection = aim.normalized;
                    return;
                }
            }
            movement = IsoWorld.UnprojectDirection(movement);
            if (movement.sqrMagnitude > .01f) facingDirection = movement.normalized;
        }

        private void Move(Vector2 input)
        {
            var start = (Vector2)transform.position;
            actualVelocity = Vector2.zero;
            if (input.sqrMagnitude < 0.001f) return;
            input = IsoWorld.UnprojectDirection(input).normalized;
            lastMoveDirection = input;
            var moveBonus = 1f + EquipmentStat(item => item.moveSpeed) / 100f;
            var distance = hero.speed * moveBonus * SpeedMultiplier * Time.deltaTime;
            var delta = input.normalized * distance;
            var steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .08f));
            var step = delta / steps;
            var current = start;
            for (var i = 0; i < steps; i++)
            {
                var xOnly = new Vector2(current.x + step.x, current.y);
                if (dungeon.CanTraverse(current, xOnly, .22f)) current.x = xOnly.x;
                var yOnly = new Vector2(current.x, current.y + step.y);
                if (dungeon.CanTraverse(current, yOnly, .22f)) current.y = yOnly.y;
            }
            transform.position = current;
            if (Time.deltaTime > .0001f) actualVelocity = (current - start) / Time.deltaTime;
        }

        private void Animate()
        {
            if (visual == null) return;
            var moving = actualVelocity.sqrMagnitude > .01f;
            var motion = IsTakingHit ? CharacterMotion.Hit : IsAttacking ? CharacterMotion.Attack :
                moving ? CharacterMotion.Walk : CharacterMotion.Idle;
            var animationTime = IsTakingHit ? Time.time - (hitAnimationUntil - .22f) :
                IsAttacking ? Time.time - (attackAnimationUntil - .24f) : Time.time;
            var visualFacing = IsoWorld.ProjectDirection(facingDirection).normalized;
            spriteFacingDirection = DirectionalSpriteAtlas.StabilizeFourWay(visualFacing, spriteFacingDirection);
            var directional = DirectionalSpriteAtlas.Get(directionalSheet, spriteFacingDirection, motion, animationTime, out var flipX);
            if (directional != null)
            {
                spriteRenderer.sprite = directional;
                spriteRenderer.flipX = flipX;
                visual.localScale = Vector3.one * visualScale;
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                return;
            }
            var bob = Mathf.Sin(Time.time * (moving ? 11f : 3f)) * (moving ? .055f : .018f);
            visual.localPosition = new Vector3(0, bob, 0);
            visual.localRotation = Quaternion.Euler(0, 0, moving ? Mathf.Sin(Time.time * 11f) * 2.2f : 0);
            spriteRenderer.flipX = facingDirection.x < -.05f;
        }

        private void Attack()
        {
            var target = EnemyController.FindNearest(transform.position, AttackRange);
            if (target == null || !HasLineOfSight(target.transform.position)) return;
            facingDirection = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            attackAnimationUntil = Time.time + .24f;
            var attackSpeedBonus = Mathf.Clamp(EquipmentStat(item => item.attackSpeed) / 100f, 0, .75f);
            nextAttack = Time.time + hero.attackCooldown * (1f - attackSpeedBonus);
            if (hero.heroClass == HeroClass.Mage)
            {
                var direction = ((Vector2)(target.transform.position - transform.position)).normalized;
                Projectile.Spawn(transform.position, direction, Damage, hero.color, ApplyElementalEffects);
                GameManager.Instance.Audio.PlayEffect("Fireball");
                return;
            }

            if (target == null) return;
            var damage = Damage * (Random.value < CriticalChance ? 2f : 1f);
            target.TakeDamage(damage);
            ApplyElementalEffects(target);
            GameManager.Instance.Audio.PlayEffect(hero.heroClass == HeroClass.Warrior ? "sword" : "Dagger");
            StartCoroutine(AttackFlash());
        }

        private void ApplyElementalEffects(EnemyController target)
        {
            if (target == null) return;
            var fire = EquipmentStat(item => item.fire);
            if (fire > 0 && Random.value < .08f) target.ApplyDamageOverTime(Mathf.Max(1, fire * .3f), 6f, 1.2f);
            var ice = EquipmentStat(item => item.ice);
            if (ice > 0 && Random.value < .06f) target.ApplySlow(.8f, 4f);
        }

        private bool HasLineOfSight(Vector2 target)
        {
            return dungeon != null && dungeon.HasLineOfSight(transform.position, target);
        }

        private System.Collections.IEnumerator AttackFlash()
        {
            var original = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            if (spriteRenderer != null) spriteRenderer.color = original;
        }

        private void UseAbility()
        {
            if (Time.time < abilityReadyAt) return;
            attackAnimationUntil = Time.time + .24f;
            switch (hero.heroClass)
            {
                case HeroClass.Rogue:
                    var direction = GameInput.Move.sqrMagnitude > 0.01f ? GameInput.Move.normalized : Vector2.right;
                    facingDirection = direction;
                    var destination = (Vector2)transform.position;
                    var dashOrigin = destination;
                    for (var i = 0; i < 10; i++)
                    {
                        var step = destination + direction * 0.25f;
                        if (!dungeon.CanOccupy(step)) break;
                        destination = step;
                    }
                    transform.position = destination;
                    CombatVfx.SpawnAfterimage(dashOrigin, CurrentSprite, new Color(.28f, .12f, .48f, .8f), facingDirection);
                    CombatVfx.SpawnAfterimage(destination, CurrentSprite, new Color(.72f, .38f, 1f, .72f), facingDirection);
                    CombatVfx.SpawnImpact(dashOrigin, ProjectileVisualStyle.Cursed, new Color(.4f, .16f, .72f), .72f);
                    CombatVfx.SpawnImpact(destination, ProjectileVisualStyle.Cursed, new Color(.72f, .34f, 1f), .9f);
                    CombatVfx.SpawnStatus(transform, StatusVisualStyle.Dash, .34f, .8f);
                    abilityReadyAt = Time.time + 3f;
                    GameManager.Instance.Audio.PlayEffect("Dash");
                    break;
                case HeroClass.Warrior:
                    shieldUntil = Time.time + 4f;
                    CombatVfx.SpawnImpact(transform.position, ProjectileVisualStyle.Arcane, new Color(1f, .58f, .18f), 1.35f);
                    CombatVfx.SpawnStatus(transform, StatusVisualStyle.Ward, 4f, 1.45f);
                    abilityReadyAt = Time.time + 8f;
                    GameManager.Instance.Audio.PlayEffect("Armor");
                    break;
                default:
                    CombatVfx.SpawnImpact(transform.position, ProjectileVisualStyle.Arcane, new Color(.72f, .18f, 1f), 2.7f);
                    CombatVfx.SpawnStatus(transform, StatusVisualStyle.ArcaneCharge, .65f, 1.35f);
                    var enemies = EnemyController.Snapshot();
                    for (var i = 0; i < enemies.Count; i++)
                        if (enemies[i] != null && Vector2.Distance(transform.position, enemies[i].transform.position) <= 4f)
                        {
                            enemies[i].TakeDamage(40f);
                            CombatVfx.SpawnImpact(enemies[i].transform.position, ProjectileVisualStyle.Arcane,
                                new Color(.8f, .2f, 1f), .68f);
                        }
                    abilityReadyAt = Time.time + 12f;
                    GameManager.Instance.Audio.PlayEffect("explosion");
                    break;
            }
        }

        public void TakeDamage(float rawDamage)
        {
            if (DeveloperInvincible) return;
            if (IsInvulnerable || Time.time < damageInvulnerableUntil) return;
            if (barrier > 0)
            {
                var absorbed = Mathf.Min(barrier, rawDamage);
                barrier -= absorbed;
                rawDamage -= absorbed;
                if (rawDamage <= 0) { GameManager.Instance.NotifyStatsChanged(); return; }
            }
            var defense = Defense + (Time.time < shieldUntil ? 15 : 0);
            Health = Mathf.Max(0, Health - Mathf.Max(1, rawDamage - defense));
            damageInvulnerableUntil = Time.time + 1f;
            hitAnimationUntil = Time.time + .22f;
            GameManager.Instance.Audio.PlayEffect("heroes_hit");
            GameManager.Instance.NotifyStatsChanged();
            if (Health <= 0) GameManager.Instance.GameOver();
        }

        public void AddBarrier(float amount)
        {
            barrier = Mathf.Max(barrier, amount);
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ApplyDebuff(float duration, float damage = 1, float speed = 1, float defense = 1, bool stunned = false)
        {
            var frozen = !stunned && speed <= .35f;
            var label = stunned ? "ОГЛУШЕНИЕ" : frozen ? "ЗАМОРОЗКА" : speed < .999f ? "ЗАМЕДЛЕНИЕ" :
                defense < .999f ? "УЯЗВИМОСТЬ" : damage < .999f ? "СЛАБОСТЬ" : "ПРОКЛЯТИЕ";
            var debuff = new ActiveDebuff
                { damage = damage, speed = speed, defense = defense, stunned = stunned, label = label, expiresAt = Time.time + duration };
            debuffs.Add(debuff);
            DamageMultiplier *= damage;
            SpeedMultiplier *= speed;
            DefenseMultiplier *= defense;
            if (stunned) IsStunned = true;
            if (stunned) CombatVfx.SpawnStatus(transform, StatusVisualStyle.Stun, duration, 1.15f);
            else if (speed < .999f) CombatVfx.SpawnStatus(transform, StatusVisualStyle.Freeze, duration, frozen ? 1.2f : .82f);
            debuff.routine = StartCoroutine(RemoveDebuffAfter(debuff, duration));
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ApplyDamageOverTime(float damage, float duration, float interval)
        {
            var debuff = new ActiveDebuff { label = "ПЕРИОДИЧЕСКИЙ УРОН", expiresAt = Time.time + duration };
            debuffs.Add(debuff);
            debuff.routine = StartCoroutine(DamageOverTime(debuff, damage, duration, interval));
            CombatVfx.SpawnStatus(transform, StatusVisualStyle.Poison, duration, .9f);
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ClearNegativeEffects()
        {
            var copy = debuffs.ToArray();
            foreach (var debuff in copy) RemoveDebuff(debuff);
            debuffs.Clear();
            IsStunned = false;
            CombatVfx.ClearNegativeStatuses(transform);
        }

        private System.Collections.IEnumerator RemoveDebuffAfter(ActiveDebuff debuff, float duration)
        {
            yield return new WaitForSeconds(duration);
            RemoveDebuff(debuff);
        }

        private System.Collections.IEnumerator DamageOverTime(ActiveDebuff debuff, float damage, float duration, float interval)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += interval)
            {
                yield return new WaitForSeconds(interval);
                if (!debuffs.Contains(debuff)) yield break;
                TakeDamage(damage);
            }
            RemoveDebuff(debuff);
        }

        private void RemoveDebuff(ActiveDebuff debuff)
        {
            if (!debuffs.Remove(debuff)) return;
            if (debuff.routine != null) StopCoroutine(debuff.routine);
            if (Mathf.Abs(debuff.damage) > .001f) DamageMultiplier /= debuff.damage;
            if (Mathf.Abs(debuff.speed) > .001f) SpeedMultiplier /= debuff.speed;
            if (Mathf.Abs(debuff.defense) > .001f) DefenseMultiplier /= debuff.defense;
            if (debuff.stunned)
            {
                IsStunned = false;
                foreach (var active in debuffs) if (active.stunned) { IsStunned = true; break; }
            }
            if (hazardDebuff == debuff) hazardDebuff = null;
            GameManager.Instance.NotifyStatsChanged();
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(MaxHealth, Health + amount);
            GameManager.Instance.NotifyStatsChanged();
        }

        public void SetDeveloperInvincible(bool value)
        {
            DeveloperInvincible = value;
            if (value) Health = MaxHealth;
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ApplyDeveloperStats(float maxHealth, float damage, float defense, float moveSpeed,
            float criticalPercent, float attackRange)
        {
            hero.maxHealth = Mathf.Max(1, maxHealth);
            hero.damage = Mathf.Max(0, damage);
            hero.defense = Mathf.Max(0, defense);
            hero.speed = Mathf.Max(.1f, moveSpeed);
            hero.criticalChance = Mathf.Clamp01(criticalPercent / 100f);
            hero.attackRange = Mathf.Max(.1f, attackRange);
            Health = Mathf.Min(Health, MaxHealth);
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ApplyShopUpgrade(LegacyShopUpgrade upgrade)
        {
            switch (upgrade.id)
            {
                case "max_hp": hero.maxHealth += upgrade.value; Health += upgrade.value; break;
                case "damage": hero.damage += upgrade.value; break;
                case "defense": hero.defense += upgrade.value; break;
                case "speed": hero.speed += upgrade.value / 32f; break;
                case "crit": hero.criticalChance = Mathf.Clamp01(hero.criticalChance + upgrade.value / 100f); break;
                case "attack_speed": hero.attackCooldown = Mathf.Max(.15f, hero.attackCooldown - upgrade.value); break;
                case "heal_full": Health = MaxHealth; break;
                case "attack_radius": hero.attackRange += upgrade.value / 32f; break;
            }
            GameManager.Instance.NotifyStatsChanged();
        }

        public void ApplyTimedState(float duration, float damage = 1, float speed = 1, float defense = 1,
            bool invisible = false, bool invulnerable = false, bool vampirism = false)
        {
            StartCoroutine(TimedState(duration, damage, speed, defense, invisible, invulnerable, vampirism));
        }

        private System.Collections.IEnumerator TimedState(float duration, float damage, float speed, float defense,
            bool invisible, bool invulnerable, bool vampirism)
        {
            var label = invisible ? "НЕВИДИМОСТЬ" : invulnerable ? "НЕУЯЗВИМОСТЬ" : vampirism ? "ВАМПИРИЗМ" :
                damage > 1.001f ? "УСИЛЕНИЕ" : speed > 1.001f ? "УСКОРЕНИЕ" : defense > 1.001f ? "ЗАЩИТА" : "ЭФФЕКТ";
            var state = new ActiveTimedState { label = label, expiresAt = Time.time + duration };
            timedStates.Add(state);
            DamageMultiplier *= damage; SpeedMultiplier *= speed; DefenseMultiplier *= defense;
            IsInvisible |= invisible; IsInvulnerable |= invulnerable; Vampirism |= vampirism;
            GameManager.Instance.NotifyStatsChanged();
            yield return new WaitForSeconds(duration);
            DamageMultiplier /= damage; SpeedMultiplier /= speed; DefenseMultiplier /= defense;
            if (invisible) IsInvisible = false;
            if (invulnerable) IsInvulnerable = false;
            if (vampirism) Vampirism = false;
            timedStates.Remove(state);
            GameManager.Instance.NotifyStatsChanged();
        }

        public void GetStatusSnapshots(List<PlayerStatusSnapshot> target)
        {
            target.Clear();
            foreach (var debuff in debuffs)
                target.Add(new PlayerStatusSnapshot(debuff.label, Mathf.Max(0, debuff.expiresAt - Time.time), true));
            foreach (var state in timedStates)
                target.Add(new PlayerStatusSnapshot(state.label, Mathf.Max(0, state.expiresAt - Time.time), false));
            if (barrier > 0) target.Add(new PlayerStatusSnapshot($"БАРЬЕР {barrier:0}", 0, false));
            if (Time.time < shieldUntil) target.Add(new PlayerStatusSnapshot("СТРАЖ", shieldUntil - Time.time, false));
        }

        public void TeleportToRandomRoom()
        {
            var rooms = GameManager.Instance.Dungeon.Rooms;
            var room = rooms[Random.Range(0, rooms.Count)];
            transform.position = GameManager.Instance.Dungeon.CellCenter(room.Center);
        }

        private static float EquipmentStat(System.Func<ItemInstance, float> selector)
        {
            var inventory = GameManager.Instance?.Inventory;
            return inventory == null ? 0 : inventory.EquipmentStat(selector);
        }

        private sealed class ActiveDebuff
        {
            public string key;
            public float damage = 1;
            public float speed = 1;
            public float defense = 1;
            public bool stunned;
            public string label;
            public float expiresAt;
            public Coroutine routine;
        }

        private sealed class ActiveTimedState
        {
            public string label;
            public float expiresAt;
        }
    }
}
