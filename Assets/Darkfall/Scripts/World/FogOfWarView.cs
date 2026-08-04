using Darkfall.Gameplay;
using UnityEngine;

namespace Darkfall.World
{
    public sealed class FogOfWarView : MonoBehaviour
    {
        private DungeonData dungeon;
        private PlayerController player;
        private float nextUpdate;

        public void Initialize(DungeonData data, PlayerController target)
        {
            dungeon = data;
            player = target;
            RefreshVisibilityData();
        }

        private void Update()
        {
            if (player == null || dungeon == null || Time.time < nextUpdate) return;
            nextUpdate = Time.time + .085f;
            RefreshVisibilityData();
        }

        private void RefreshVisibilityData()
        {
            if (player == null || dungeon == null) return;
            var origin = new Vector2Int(Mathf.FloorToInt(player.transform.position.x), Mathf.FloorToInt(player.transform.position.y));
            dungeon.BeginVisibilityUpdate();
            for (var x = origin.x - 12; x <= origin.x + 12; x++)
            for (var y = origin.y - 12; y <= origin.y + 12; y++)
            {
                if (x < 0 || y < 0 || x >= dungeon.Width || y >= dungeon.Height) continue;
                var cell = new Vector2Int(x, y);
                if (IsInsideVision(origin, cell) && HasLineOfSight(origin, cell)) dungeon.Reveal(x, y);
            }
        }

        private bool IsInsideVision(Vector2Int origin, Vector2Int cell)
        {
            var offset = (Vector2)(cell - origin);
            var distance = offset.magnitude;
            if (offset.sqrMagnitude < .001f) return true;
            var facing = player.FacingDirection.sqrMagnitude > .001f ? player.FacingDirection.normalized : Vector2.right;
            var visibleRadius = DungeonLighting.PlayerVisionRadius(facing, offset.normalized);
            return distance <= visibleRadius;
        }

        private bool HasLineOfSight(Vector2Int from, Vector2Int to)
        {
            var x = from.x;
            var y = from.y;
            var dx = Mathf.Abs(to.x - from.x);
            var dy = Mathf.Abs(to.y - from.y);
            var sx = from.x < to.x ? 1 : -1;
            var sy = from.y < to.y ? 1 : -1;
            var error = dx - dy;
            while (x != to.x || y != to.y)
            {
                var twice = error * 2;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
                if ((x != to.x || y != to.y) && dungeon.BlocksVision(x, y)) return false;
            }
            return true;
        }
    }
}
