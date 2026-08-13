using System.Collections.Generic;
using Darkfall.Core;
using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    /// <summary>
    /// Stateful threshold object. The frame remains architecture, while the closed leaf owns the
    /// collision and lock. Opening removes the blocker only after the leaf has visibly moved.
    /// </summary>
    public sealed class DungeonDoor : MonoBehaviour
    {
        private const float InteractionDistance = 1.55f;
        private static readonly List<DungeonDoor> Active = new List<DungeonDoor>();
        private DungeonData dungeon;
        private DungeonDoorLockKind lockKind;
        private SpriteRenderer closedRenderer;
        private int obstacleId;
        private int killsAtSpawn;
        private int killsRequired;
        private float opening;
        private bool openingStarted;
        private bool open;
        private string keyId;

        public bool IsOpen => open;
        public DungeonDoorLockKind LockKind => lockKind;

        public static void Spawn(DungeonData data, DungeonArchitectureFeature feature, string biome, Transform parent)
        {
            var owner = new GameObject($"Door · {feature.DoorLock}");
            owner.transform.SetParent(parent, false);
            owner.transform.position = feature.Position;
            var door = owner.AddComponent<DungeonDoor>();
            door.dungeon = data;
            door.lockKind = feature.DoorLock;
            door.killsAtSpawn = GameManager.Instance != null ? GameManager.Instance.SessionKills : 0;
            door.killsRequired = 2 + Mathf.Clamp((GameManager.Instance?.Depth ?? 1) / 12, 0, 2);
            door.keyId = $"dungeon_key_{GameManager.Instance?.Depth ?? 1}";

            door.closedRenderer = door.CreateVisual("Animated Door", feature.Vertical, .985f);
            door.ApplyLockedTint();

            // The contour owns both wall cheeks and the authored door owns its complete frame.
            // Extra jamb wings duplicated the cheeks and, once the leaf opened, read as two false
            // passages beside the real threshold.

            var width = Mathf.Max(1.5f, feature.Width);
            var blocker = feature.Vertical
                ? new Rect(feature.Position.x - .14f, feature.Position.y - width * .5f, .28f, width)
                : new Rect(feature.Position.x - width * .5f, feature.Position.y - .14f, width, .28f);
            door.obstacleId = data.AddDynamicObstacle(blocker);
            Active.Add(door);
        }

        private SpriteRenderer CreateVisual(string objectName, bool flipX, float scale)
        {
            var visual = new GameObject(objectName);
            visual.transform.SetParent(transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = DungeonDoorSpriteLibrary.Closed;
            visual.transform.localScale = Vector3.one * scale;
            renderer.flipX = flipX;
            renderer.color = Color.white;
            DarkfallRenderMaterials.MakeLit(renderer);
            visual.AddComponent<IsoVisual>().Initialize(transform, 0f, 1004, false);
            return renderer;
        }

        private void ApplyLockedTint()
        {
            if (closedRenderer == null) return;
            closedRenderer.color = lockKind switch
            {
                DungeonDoorLockKind.Key => new Color(.88f, .80f, .62f),
                DungeonDoorLockKind.EnemySeal => new Color(.9f, .52f, .44f),
                _ => Color.white
            };
        }

        private void Update()
        {
            if (open || !openingStarted)
            {
                if (!open && lockKind == DungeonDoorLockKind.EnemySeal && RemainingKills <= 0)
                    closedRenderer.color = Color.Lerp(Color.white, new Color(1f, .58f, .42f),
                        .08f + Mathf.Sin(Time.time * 3.1f) * .035f);
                return;
            }

            opening = Mathf.MoveTowards(opening, 1f, Time.deltaTime / .48f);
            if (closedRenderer != null)
            {
                if (opening < .34f) closedRenderer.sprite = DungeonDoorSpriteLibrary.Opening(0);
                else if (opening < .72f) closedRenderer.sprite = DungeonDoorSpriteLibrary.Opening(1);
                else closedRenderer.sprite = DungeonDoorSpriteLibrary.Open;
            }
            if (opening >= .72f && obstacleId != 0)
            {
                dungeon.RemoveDynamicObstacle(obstacleId);
                obstacleId = 0;
            }
            if (opening < 1f) return;
            open = true;
            openingStarted = false;
        }

        private int RemainingKills => Mathf.Max(0, killsRequired -
            ((GameManager.Instance?.SessionKills ?? killsAtSpawn) - killsAtSpawn));

        private bool TryOpen()
        {
            if (open || openingStarted) return true;
            if (lockKind == DungeonDoorLockKind.Key &&
                !(GameManager.Instance?.Inventory.TryConsume(keyId) ?? false))
            {
                GameManager.Instance?.ShowMessage("Нужен ключ от этой двери");
                return true;
            }
            if (lockKind == DungeonDoorLockKind.EnemySeal && RemainingKills > 0)
            {
                GameManager.Instance?.ShowMessage($"Печать требует ещё убийств: {RemainingKills}");
                return true;
            }
            openingStarted = true;
            return true;
        }

        public string InteractionHint()
        {
            if (open || openingStarted) return "";
            if (lockKind == DungeonDoorLockKind.Key && (GameManager.Instance?.Inventory.Count(keyId) ?? 0) <= 0)
                return "НУЖЕН КЛЮЧ ОТ ДВЕРИ";
            if (lockKind == DungeonDoorLockKind.EnemySeal && RemainingKills > 0)
                return $"ПЕЧАТЬ ДВЕРИ  ·  УБИТЬ ЕЩЁ {RemainingKills}";
            return lockKind == DungeonDoorLockKind.Key ? "[E] ОТПЕРЕТЬ ДВЕРЬ" :
                lockKind == DungeonDoorLockKind.EnemySeal ? "[E] СНЯТЬ ПЕЧАТЬ" : "[E] ОТКРЫТЬ ДВЕРЬ";
        }

        public static bool InteractNearest(PlayerController target)
        {
            var nearest = Nearest(target, out var distance);
            return nearest != null && distance <= InteractionDistance && nearest.TryOpen();
        }

        public static float DistanceToNearest(PlayerController target)
        {
            Nearest(target, out var distance);
            return distance;
        }

        public static string HintNearest(PlayerController target)
        {
            var nearest = Nearest(target, out var distance);
            return nearest != null && distance <= InteractionDistance ? nearest.InteractionHint() : "";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool ForceOpenNearestForVisualAudit(PlayerController target)
        {
            var nearest = Nearest(target, out _);
            if (nearest == null) return false;
            nearest.opening = 1f;
            nearest.openingStarted = false;
            nearest.open = true;
            if (nearest.closedRenderer != null)
            {
                nearest.closedRenderer.sprite = DungeonDoorSpriteLibrary.Open;
                nearest.closedRenderer.color = Color.white;
            }
            if (nearest.obstacleId != 0)
            {
                nearest.dungeon.RemoveDynamicObstacle(nearest.obstacleId);
                nearest.obstacleId = 0;
            }
            return true;
        }

        public static bool ForceOpeningPhaseForVisualAudit(PlayerController target, float normalized)
        {
            // The preceding audit deliberately opens every production door. For the frame-by-frame
            // sequence we must still be able to address that same door and drive it back to Closed.
            // Gameplay queries continue to use Nearest(), which correctly ignores open doors.
            var nearest = NearestIncludingOpen(target, out _);
            if (nearest == null) return false;
            normalized = Mathf.Clamp01(normalized);
            nearest.opening = normalized;
            nearest.openingStarted = false;
            nearest.open = normalized >= 1f;
            if (nearest.closedRenderer != null)
            {
                nearest.closedRenderer.color = Color.white;
                nearest.closedRenderer.sprite = normalized < .01f ? DungeonDoorSpriteLibrary.Closed :
                    normalized < .5f ? DungeonDoorSpriteLibrary.Opening(0) :
                    normalized < .9f ? DungeonDoorSpriteLibrary.Opening(1) :
                    DungeonDoorSpriteLibrary.Open;
            }
            return true;
        }
#endif

        private static DungeonDoor Nearest(PlayerController target, out float distance)
        {
            distance = float.MaxValue;
            DungeonDoor nearest = null;
            if (target == null) return null;
            foreach (var door in Active)
            {
                if (door == null || door.open) continue;
                var current = Vector2.Distance(door.transform.position, target.transform.position);
                if (current >= distance) continue;
                distance = current;
                nearest = door;
            }
            return nearest;
        }

        private static DungeonDoor NearestIncludingOpen(PlayerController target, out float distance)
        {
            distance = float.MaxValue;
            DungeonDoor nearest = null;
            if (target == null) return null;
            foreach (var door in Active)
            {
                if (door == null) continue;
                var current = Vector2.Distance(door.transform.position, target.transform.position);
                if (current >= distance) continue;
                distance = current;
                nearest = door;
            }
            return nearest;
        }

        public static void SpawnRequiredKeys(PlayerController player, DungeonData data)
        {
            foreach (var door in Active)
            {
                if (door == null || door.lockKind != DungeonDoorLockKind.Key) continue;
                var start = data.CellCenter(data.StartCell);
                var position = start + Vector2.right * .75f;
                if (!data.CanOccupy(position, .18f)) position = start + Vector2.up * .75f;
                Pickup.SpawnItem(position, player, new ItemInstance
                {
                    id = door.keyId,
                    baseId = door.keyId,
                    name = "Ключ от внутренних врат",
                    description = "Открывает редкую запертую дверь на этом этаже",
                    kind = ItemKind.Scroll,
                    rarity = ItemRarity.Rare,
                    quantity = 1
                });
            }
        }

        public static void ResetRegistry()
        {
            Active.Clear();
        }

        private void OnDestroy()
        {
            if (obstacleId != 0 && dungeon != null) dungeon.RemoveDynamicObstacle(obstacleId);
            Active.Remove(this);
        }
    }
}
