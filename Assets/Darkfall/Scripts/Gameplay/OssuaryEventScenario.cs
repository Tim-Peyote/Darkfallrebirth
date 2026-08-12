using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    /// <summary>
    /// Ossuary room encounter: the altar periodically telegraphs a curse while its keepers live,
    /// then becomes inert and releases a guaranteed scroll reward when the room is cleared.
    /// </summary>
    public sealed class OssuaryEventScenario : MonoBehaviour
    {
        private const float ActivationRadius = 3.1f;
        private const float CurseRadius = 2.45f;
        private const float TelegraphSeconds = 1.35f;
        private const float CooldownSeconds = 5.2f;

        private static readonly Color CurseColor = new Color(.58f, .16f, .82f, 1f);
        private readonly List<EnemyController> keepers = new List<EnemyController>(3);
        private DungeonData dungeon;
        private PlayerController player;
        private float armedAt = -1f;
        private float readyAt;
        private bool rewardReleased;
        private OssuaryAltarVisual altarVisual;

        public void Initialize(DungeonData source, PlayerController target)
        {
            dungeon = source;
            player = target;
            readyAt = Time.time + 1.5f;
            BuildAltarVisual();
        }

        public void RegisterKeeper(EnemyController keeper)
        {
            if (keeper != null) keepers.Add(keeper);
        }

        private void Update()
        {
            if (dungeon == null || player == null) return;
            if (!rewardReleased && keepers.Count > 0 && !HasLivingKeepers())
            {
                rewardReleased = true;
                var scroll = LegacyCatalog.Item("mystery_scroll");
                if (scroll != null)
                    Pickup.SpawnItem(transform.position, player,
                        InventorySystem.CreateFromDefinition(scroll, GameManager.Instance.Depth));
                CombatVfx.SpawnPulse(transform.position, new Color(.82f, .66f, 1f), 1.55f, .65f);
                altarVisual?.Resolve();
                enabled = false;
                return;
            }

            var anchor = (Vector2)transform.position;
            var playerPosition = (Vector2)player.transform.position;
            if (!dungeon.SharesCombatElevation(anchor, playerPosition))
            {
                armedAt = -1f;
                altarVisual?.SetArmed(false);
                return;
            }
            if (armedAt >= 0f)
            {
                if (Time.time - armedAt < TelegraphSeconds) return;
                CombatVfx.SpawnImpact(anchor, ProjectileVisualStyle.Cursed, CurseColor, 1.05f);
                if (Vector2.Distance(anchor, playerPosition) <= CurseRadius)
                    player.ApplyDebuff(2.4f, speed: .72f);
                armedAt = -1f;
                readyAt = Time.time + CooldownSeconds;
                altarVisual?.SetArmed(false);
                return;
            }
            if (Time.time < readyAt || !HasLivingKeepers() ||
                Vector2.Distance(anchor, playerPosition) > ActivationRadius) return;
            armedAt = Time.time;
            altarVisual?.SetArmed(true);
            CombatVfx.SpawnPulse(anchor, CurseColor, CurseRadius, TelegraphSeconds);
        }

        private void BuildAltarVisual()
        {
            var visual = new GameObject("Ossuary Event · Animated Altar");
            if (GameManager.Instance != null && GameManager.Instance.LevelRoot != null)
                visual.transform.SetParent(GameManager.Instance.LevelRoot, false);
            altarVisual = visual.AddComponent<OssuaryAltarVisual>();
            altarVisual.Initialize(transform.position);
        }

        private void OnDestroy()
        {
            if (altarVisual != null) Destroy(altarVisual.gameObject);
        }

        private bool HasLivingKeepers()
        {
            for (var i = 0; i < keepers.Count; i++)
                if (keepers[i] != null) return true;
            return false;
        }
    }

    internal sealed class OssuaryAltarVisual : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite dormant;
        private Sprite armed;
        private Sprite resolved;
        private bool isArmed;
        private bool isResolved;
        private float phase;

        public void Initialize(Vector2 logicalPosition)
        {
            dormant = Load("dormant");
            armed = Load("armed");
            resolved = Load("resolved");
            transform.position = IsoWorld.Project(logicalPosition, .06f);
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = dormant;
            spriteRenderer.sortingOrder = IsoWorld.SortingOrder(logicalPosition, 1014);
            DarkfallRenderMaterials.MakeLit(spriteRenderer);
            transform.localScale = Vector3.one * .38f;
        }

        public void SetArmed(bool value)
        {
            if (isResolved) return;
            isArmed = value;
            if (spriteRenderer != null) spriteRenderer.sprite = value ? armed : dormant;
        }

        public void Resolve()
        {
            isResolved = true;
            isArmed = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = resolved;
                spriteRenderer.color = new Color(.72f, .72f, .68f, .96f);
            }
            transform.localScale = Vector3.one * .38f;
        }

        private void Update()
        {
            if (spriteRenderer == null || isResolved) return;
            phase += Time.deltaTime * (isArmed ? 8.4f : 1.35f);
            var wave = Mathf.Sin(phase);
            transform.localScale = Vector3.one * (.38f + wave * (isArmed ? .012f : .003f));
            var value = isArmed ? .93f + wave * .07f : .8f + wave * .025f;
            spriteRenderer.color = new Color(value, value, value, isArmed ? 1f : .94f);
        }

        private static Sprite Load(string state)
        {
            var texture = Resources.Load<Texture2D>("Sprites/Scenarios/OssuaryAltar/" + state);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .18f), 180f, 0, SpriteMeshType.FullRect);
        }
    }
}
