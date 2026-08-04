using Darkfall.Core;
using UnityEngine;

namespace Darkfall.Gameplay
{
    public sealed class Pickup : MonoBehaviour
    {
        private static Sprite goldSprite;
        private PlayerController player;
        private ItemInstance item;
        private int gold;
        private Vector3 restingPosition;
        private float animationPhase;

        public static void SpawnItem(Vector2 position, PlayerController target, ItemInstance loot)
        {
            Spawn(position, target, loot, 0);
        }

        public static void SpawnGold(Vector2 position, PlayerController target, int amount)
        {
            Spawn(position, target, null, Mathf.Max(1, amount));
        }

        private static void Spawn(Vector2 position, PlayerController target, ItemInstance loot, int amount)
        {
            var pickupObject = new GameObject(loot == null ? "Gold Pouch" : loot.name);
            if (GameManager.Instance?.LevelRoot != null) pickupObject.transform.SetParent(GameManager.Instance.LevelRoot);
            pickupObject.transform.position = position;
            var pickup = pickupObject.AddComponent<Pickup>();
            pickup.player = target;
            pickup.item = loot;
            pickup.gold = amount;
            pickup.restingPosition = position;
            pickup.animationPhase = Random.value * Mathf.PI * 2f;
            var renderer = pickupObject.AddComponent<SpriteRenderer>();
            renderer.sprite = loot == null ? GoldSprite() : RuntimeItemIcons.Get(loot);
            renderer.color = Color.white;
            renderer.sortingOrder = 12;
            DarkfallRenderMaterials.MakeLit(renderer);
            pickupObject.transform.localScale = Vector3.one * (loot == null ? .3f : .42f);

        }

        private static Sprite GoldSprite()
        {
            if (goldSprite != null) return goldSprite;
            goldSprite = RuntimeItemIcons.Get(new ItemInstance
            {
                id = "world_gold_pouch",
                baseId = "gold_pouch",
                name = "Мешочек золота",
                kind = ItemKind.Gold,
                rarity = ItemRarity.Common,
                quantity = 1
            });
            return goldSprite;
        }

        private void Update()
        {
            var wave = Time.time * 2.4f + animationPhase;
            transform.position = restingPosition + Vector3.up * (Mathf.Sin(wave) * .075f);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(wave * .55f) * 4f);
            var pulse = 1f + Mathf.Sin(wave * .8f) * .035f;
            var baseScale = item == null ? .3f : .42f;
            transform.localScale = Vector3.one * baseScale * pulse;
            if (player == null || Vector2.Distance(transform.position, player.transform.position) >= .78f) return;
            if (item != null)
            {
                if (!GameManager.Instance.Inventory.Add(item)) return;
            }
            else GameManager.Instance.AddGold(gold);
            GameManager.Instance.Audio.PlayEffect("item_pickup");
            Destroy(gameObject);
        }
    }
}
