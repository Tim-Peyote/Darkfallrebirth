using Darkfall.Core;
using Darkfall.World;
using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Gameplay
{
    /// <summary>
    /// Authored Ritual-room hazard. It stays dormant until the player enters the arena, clearly
    /// telegraphs every strike and never damages actors standing on another dungeon elevation.
    /// </summary>
    public sealed class RitualArenaTrap : MonoBehaviour
    {
        private const float TriggerRadius = 1.28f;
        private const float StrikeRadius = 1.05f;
        private const float TelegraphSeconds = .42f;
        private const float CooldownSeconds = 4.4f;

        private static readonly Color WarningColor = new Color(1f, .31f, .055f, 1f);
        private DungeonData dungeon;
        private PlayerController player;
        private float damage;
        private float armedAt = -1f;
        private float readyAt;
        private readonly List<EnemyController> guardians = new List<EnemyController>(4);
        private bool rewardReleased;
        private RitualSealVisual floorMark;
        internal float TriggerDistance => TriggerRadius;
        internal float StrikeDistance => StrikeRadius;

        public void Initialize(DungeonData source, PlayerController target, int depth)
        {
            dungeon = source;
            player = target;
            damage = 12f + Mathf.Max(0, depth - 1) * .7f;
            readyAt = Time.time + 1.25f;
            BuildFloorMark();
        }

        public void RegisterGuardian(EnemyController guardian)
        {
            if (guardian != null) guardians.Add(guardian);
        }

        private void Update()
        {
            if (dungeon == null || player == null) return;
            if (!rewardReleased && guardians.Count > 0)
            {
                var alive = false;
                for (var i = 0; i < guardians.Count; i++) alive |= guardians[i] != null;
                if (!alive)
                {
                    rewardReleased = true;
                    TreasureChest.Spawn(transform.position, player);
                    CombatVfx.SpawnPulse(transform.position, new Color(1f, .68f, .18f), 1.45f, .55f);
                    floorMark?.Resolve();
                    enabled = false;
                    return;
                }
            }
            var anchor = (Vector2)transform.position;
            var playerPosition = (Vector2)player.transform.position;
            if (!dungeon.SharesCombatElevation(anchor, playerPosition))
            {
                armedAt = -1f;
                floorMark?.SetArmed(false);
                return;
            }

            if (armedAt >= 0f)
            {
                if (Time.time - armedAt < TelegraphSeconds) return;
                Strike(anchor, playerPosition);
                armedAt = -1f;
                readyAt = Time.time + CooldownSeconds;
                floorMark?.SetArmed(false);
                return;
            }

            if (Time.time < readyAt || Vector2.Distance(anchor, playerPosition) > TriggerRadius) return;
            armedAt = Time.time;
            floorMark?.SetArmed(true);
            CombatVfx.SpawnPulse(anchor, WarningColor, StrikeRadius, TelegraphSeconds);
        }

        private void Strike(Vector2 anchor, Vector2 playerPosition)
        {
            CombatVfx.SpawnImpact(anchor, ProjectileVisualStyle.Cursed, WarningColor, 1.15f);
            CombatVfx.SpawnPulse(anchor, new Color(1f, .13f, .025f), StrikeRadius, .28f);
            if (Vector2.Distance(anchor, playerPosition) > StrikeRadius) return;
            player.TakeDamage(damage);
            player.ApplyEnvironmentalStatus(DungeonHazardKind.EmberSeep, damage * .35f);
        }

        private void BuildFloorMark()
        {
            var visual = new GameObject("Ritual Trap · Animated Seal");
            if (GameManager.Instance != null && GameManager.Instance.LevelRoot != null)
                visual.transform.SetParent(GameManager.Instance.LevelRoot, false);
            floorMark = visual.AddComponent<RitualSealVisual>();
            floorMark.Initialize(transform.position);
        }

        private void OnDestroy()
        {
            if (floorMark != null) Destroy(floorMark.gameObject);
        }
    }

    internal sealed class RitualSealVisual : MonoBehaviour
    {
        private SpriteRenderer renderer;
        private Sprite dormant;
        private Sprite armed;
        private Sprite resolved;
        private bool isArmed;
        private bool isResolved;
        private float phase;
        private Vector2 logicalAnchor;

        public void Initialize(Vector2 logicalPosition)
        {
            logicalAnchor = logicalPosition;
            dormant = Load("dormant");
            armed = Load("armed");
            resolved = Load("resolved");
            transform.position = IsoWorld.Project(logicalPosition, .025f);
            renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = dormant;
            renderer.sortingOrder = IsoWorld.SortingOrder(logicalPosition, 1008);
            DarkfallRenderMaterials.MakeLit(renderer);
            // The source is already authored in the project's 2:1 isometric floor plane.
            // Uniform scaling preserves that projection; never squash it again in code.
            transform.localScale = Vector3.one * .7f;
            renderer.color = new Color(.34f, .31f, .29f, .24f);
        }

        public void SetArmed(bool value)
        {
            if (isResolved) return;
            isArmed = value;
            if (renderer != null) renderer.sprite = value ? armed : dormant;
        }

        public void Resolve()
        {
            isResolved = true;
            isArmed = false;
            if (renderer != null)
            {
                renderer.sprite = resolved;
                renderer.color = new Color(.72f, .68f, .62f, .92f);
            }
            transform.localScale = Vector3.one * .7f;
        }

        private void Update()
        {
            if (renderer == null || isResolved) return;
            phase += Time.deltaTime * (isArmed ? 12f : .8f);
            transform.localScale = Vector3.one * .7f;
            if (isArmed)
            {
                var brightness = .9f + Mathf.Sin(phase) * .1f;
                renderer.color = new Color(1f, brightness * .48f, brightness * .18f, .96f);
            }
            else renderer.color = new Color(.34f, .31f, .29f, .24f);
        }

        private static Sprite Load(string state)
        {
            var texture = Resources.Load<Texture2D>("Sprites/Scenarios/RitualSeal/" + state);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 180f, 0, SpriteMeshType.Tight);
        }
    }
}
