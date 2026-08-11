using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class TreasureChest : MonoBehaviour
    {
        public const float MimicChance = .02f;
        private static readonly List<TreasureChest> Active = new List<TreasureChest>();
        private PlayerController player;
        private SpriteRenderer spriteRenderer;
        private bool resolved;
        public readonly ItemInstance[] Items = new ItemInstance[12];

        public static TreasureChest Spawn(Vector2 position, PlayerController target)
        {
            var chestObject = new GameObject("Treasure Chest");
            if (GameManager.Instance?.LevelRoot != null) chestObject.transform.SetParent(GameManager.Instance.LevelRoot);
            chestObject.transform.position = position;
            var chest = chestObject.AddComponent<TreasureChest>();
            chest.player = target;
            var visual = new GameObject("Chest Visual");
            visual.transform.SetParent(chestObject.transform, false);
            chest.spriteRenderer = visual.AddComponent<SpriteRenderer>();
            chest.spriteRenderer.sprite = GameSpriteAtlas.Chest(false);
            chest.spriteRenderer.color = Color.white;
            chest.spriteRenderer.sortingOrder = 11;
            DarkfallRenderMaterials.MakeLit(chest.spriteRenderer);
            visual.transform.localScale = Vector3.one * 1.35f;
            visual.AddComponent<IsoVisual>().Initialize(chestObject.transform, 0f, 1000);
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
            Active.Add(chest);
            return chest;
        }

        private void OnDestroy() => Active.Remove(this);

        public static void InteractNearest(PlayerController target)
        {
            TreasureChest nearest = null;
            var distance = 1.35f;
            foreach (var chest in Active)
            {
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
                if (chest != null) best = Mathf.Min(best, Vector2.Distance(chest.transform.position, target.transform.position));
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
                if (allowMimic && Random.value < MimicChance)
                {
                    Active.Remove(this);
                    gameObject.SetActive(false);
                    GameManager.Instance.SpawnMimic(transform.position);
                    Destroy(gameObject);
                    return true;
                }
            }
            spriteRenderer.sprite = GameSpriteAtlas.Chest(true);
            Darkfall.UI.InventoryUI.Instance?.OpenChest(this);
            return true;
        }

        /// <summary>Deterministic entry point for the non-destructive release smoke.</summary>
        internal bool OpenForValidation() => Open(false, true);

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
