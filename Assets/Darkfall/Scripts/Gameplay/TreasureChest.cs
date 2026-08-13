using System.Collections.Generic;
using System.Collections;
using Darkfall.Core;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class TreasureChest : MonoBehaviour
    {
        public const float MimicChance = .02f;
        // Interaction is measured in the logical dungeon plane. The rendered child is projected
        // into isometric screen space, so neither its transform nor a Unity physics collider may
        // be used for this distance.
        private const float InteractionReach = 1.35f;
        private static readonly List<TreasureChest> Active = new List<TreasureChest>();
        private PlayerController player;
        private SpriteRenderer spriteRenderer;
        private bool resolved;
        private bool guaranteedMimic;
        private Coroutine animationRoutine;
        private ChestState state = ChestState.Closed;
        public readonly ItemInstance[] Items = new ItemInstance[12];

        private enum ChestState
        {
            Closed,
            Opening,
            Open,
            Closing
        }

        public static TreasureChest Spawn(Vector2 position, PlayerController target,
            bool guaranteedReward = false, bool guaranteedMimic = false)
        {
            var chestObject = new GameObject("Treasure Chest");
            if (GameManager.Instance?.LevelRoot != null) chestObject.transform.SetParent(GameManager.Instance.LevelRoot);
            chestObject.transform.position = position;
            var chest = chestObject.AddComponent<TreasureChest>();
            chest.player = target;
            chest.guaranteedMimic = guaranteedMimic;
            var visual = new GameObject("Chest Visual");
            visual.transform.SetParent(chestObject.transform, false);
            chest.spriteRenderer = visual.AddComponent<SpriteRenderer>();
            chest.spriteRenderer.sprite = TreasureChestSpriteLibrary.Closed ?? GameSpriteAtlas.Chest(false);
            chest.spriteRenderer.color = Color.white;
            chest.spriteRenderer.sortingOrder = 11;
            DarkfallRenderMaterials.MakeLit(chest.spriteRenderer);
            // Authored to one floor-tile footprint. Keep the character readable behind it and
            // never compensate a bad source crop by scaling the complete chest at runtime.
            visual.transform.localScale = Vector3.one * 1.1f;
            visual.AddComponent<IsoVisual>().Initialize(chestObject.transform, 0f, 1000);
            // Chests deliberately do not become navigation obstacles. Enemies currently steer
            // directly rather than pathfinding around temporary props; registering the chest as
            // an obstacle made whole packs freeze behind it. Interaction still uses this owner's
            // logical position while the child is projected only for rendering.
            var roll = Random.value;
            var maxItems = roll < .10f ? 0 : roll < .40f ? 1 : roll < .65f ? 2 :
                roll < .80f ? 3 : roll < .90f ? 4 : roll < .95f ? 5 : Random.Range(6, 10);
            var itemCount = maxItems == 0 ? 0 : Random.Range(1, maxItems + 1);
            for (var i = 0; i < itemCount; i++) chest.Items[i] = InventorySystem.GenerateLoot(GameManager.Instance.Depth);
            if (Random.value < .25f)
                for (var i = 0; i < chest.Items.Length; i++)
                    if (chest.Items[i] == null)
                    {
                        chest.Items[i] = new ItemInstance
                        {
                            id = "gold_pouch_" + Random.Range(1000, 9999),
                            baseId = "gold_pouch",
                            name = "Мешочек золота",
                            description = "Монеты из глубин",
                            kind = ItemKind.Gold,
                            quantity = Random.Range(3, 13) * GameManager.Instance.Depth
                        };
                        break;
                    }
            if (guaranteedReward)
            {
                chest.Items[0] ??= InventorySystem.GenerateLoot(GameManager.Instance.Depth);
                chest.Items[1] = new ItemInstance
                {
                    id = "vault_gold_" + Random.Range(1000, 9999),
                    baseId = "gold_pouch",
                    name = "Клад катакомб",
                    description = "Награда из запечатанной сокровищницы",
                    kind = ItemKind.Gold,
                    quantity = Mathf.Max(12, 8 + GameManager.Instance.Depth * 7)
                };
            }
            Active.Add(chest);
            return chest;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        public static void InteractNearest(PlayerController target)
        {
            TreasureChest nearest = null;
            var distance = InteractionReach;
            foreach (var chest in Active)
            {
                if (chest == null) continue;
                var current = Vector2.Distance(chest.transform.position, target.transform.position);
                if (current < distance) { distance = current; nearest = chest; }
            }
            if (nearest != null) nearest.Open(true, false);
        }

        public static float DistanceToNearest(PlayerController target)
        {
            if (target == null) return float.MaxValue;
            var best = float.MaxValue;
            foreach (var chest in Active)
                if (chest != null)
                    best = Mathf.Min(best,
                        Vector2.Distance(chest.transform.position, target.transform.position));
            return best;
        }

        private bool Open(bool allowMimic, bool ignoreCombat)
        {
            if (!ignoreCombat && EnemyController.FindNearest(transform.position, 150f / 32f) != null)
            {
                GameManager.Instance.ShowMessage("Сундук нельзя открыть в бою");
                return false;
            }
            if (!resolved)
            {
                resolved = true;
                if (allowMimic && (guaranteedMimic || Random.value < MimicChance))
                {
                    Active.Remove(this);
                    gameObject.SetActive(false);
                    GameManager.Instance.SpawnMimic(transform.position);
                    Destroy(gameObject);
                    return true;
                }
            }
            if (state != ChestState.Closed) return false;
            state = ChestState.Opening;
            animationRoutine = StartCoroutine(PlayOpeningThenShowInventory());
            return true;
        }

        private IEnumerator PlayOpeningThenShowInventory()
        {
            // Do not pause the game or cover the chest until the complete opening motion has
            // been shown. Realtime waits also keep this deterministic if another modal changed
            // time scale during the transition.
            var frame = TreasureChestSpriteLibrary.Opening(0);
            if (frame != null) spriteRenderer.sprite = frame;
            yield return new WaitForSecondsRealtime(.11f);
            frame = TreasureChestSpriteLibrary.Opening(1);
            if (frame != null) spriteRenderer.sprite = frame;
            yield return new WaitForSecondsRealtime(.12f);
            spriteRenderer.sprite = TreasureChestSpriteLibrary.Open ?? GameSpriteAtlas.Chest(true);
            yield return new WaitForSecondsRealtime(.08f);

            state = ChestState.Open;
            animationRoutine = null;
            Darkfall.UI.InventoryUI.Instance?.OpenChest(this);
        }

        public void OnInventoryClosed()
        {
            if (state != ChestState.Open) return;
            state = ChestState.Closing;
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = StartCoroutine(PlayClosing());
        }

        private IEnumerator PlayClosing()
        {
            // Closing starts as soon as the chest panel disappears and mirrors the authored
            // opening frames instead of snapping directly to the closed sprite.
            var frame = TreasureChestSpriteLibrary.Opening(1);
            if (frame != null) spriteRenderer.sprite = frame;
            yield return new WaitForSecondsRealtime(.09f);
            frame = TreasureChestSpriteLibrary.Opening(0);
            if (frame != null) spriteRenderer.sprite = frame;
            yield return new WaitForSecondsRealtime(.10f);
            spriteRenderer.sprite = TreasureChestSpriteLibrary.Closed ?? GameSpriteAtlas.Chest(false);
            state = ChestState.Closed;
            animationRoutine = null;
        }

        /// <summary>Deterministic entry point for the non-destructive release smoke.</summary>
        internal bool OpenForValidation() => Open(false, true);
        internal bool HasInvalidWorldCollider => GetComponent<Collider2D>() != null;
        internal bool HasVisibleSprite => spriteRenderer != null && spriteRenderer.sprite != null &&
                                          spriteRenderer.enabled && spriteRenderer.color.a > .01f;
        internal bool IsClosedAfterAnimation => state == ChestState.Closed &&
                                                spriteRenderer != null &&
                                                spriteRenderer.sprite == (TreasureChestSpriteLibrary.Closed ??
                                                                          GameSpriteAtlas.Chest(false));

        public void Take(int index)
        {
            if (index < 0 || index >= Items.Length || Items[index] == null) return;
            if (Items[index].kind == ItemKind.Gold)
            {
                GameManager.Instance.AddGold(Items[index].quantity);
                Items[index] = null;
                Darkfall.UI.InventoryUI.Instance?.Refresh();
                return;
            }
            if (!GameManager.Instance.Inventory.Add(Items[index])) return;
            Items[index] = null;
            Darkfall.UI.InventoryUI.Instance?.Refresh();
        }

        public bool TakeTo(int index, int backpackIndex)
        {
            if (index < 0 || index >= Items.Length || Items[index] == null) return false;
            var item = Items[index];
            if (item.kind == ItemKind.Gold)
            {
                GameManager.Instance.AddGold(item.quantity);
                Items[index] = null;
                return true;
            }
            // Transaction order matters: the chest retains ownership until the destination has
            // accepted the exact item. InventoryUI performs one refresh after this returns.
            if (!GameManager.Instance.Inventory.AddAt(item, backpackIndex)) return false;
            Items[index] = null;
            return true;
        }
    }
}
