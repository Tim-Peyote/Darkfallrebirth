using Darkfall.Core;
using UnityEngine;

namespace Darkfall.World
{
    /// <summary>
    /// Keeps simulation on the original square dungeon grid while presenting it as a 2.5D world.
    /// All screen-facing systems must go through this class so aiming, camera tracking and depth
    /// sorting cannot drift apart.
    /// </summary>
    public static class IsoWorld
    {
        public const float HalfWidth = .72f;
        public const float HalfHeight = .36f;
        public const float WallHeight = .82f;
        public const int DepthPrecision = 32;

        public static Vector2 Project(Vector2 logical) => new Vector2(
            (logical.x - logical.y) * HalfWidth,
            -(logical.x + logical.y) * HalfHeight);

        public static Vector3 Project(Vector3 logical, float elevation = 0f)
        {
            var point = Project((Vector2)logical);
            return new Vector3(point.x, point.y + elevation, logical.z);
        }

        public static Vector2 Unproject(Vector2 projected)
        {
            var x = projected.x / HalfWidth;
            var y = -projected.y / HalfHeight;
            return new Vector2((x + y) * .5f, (y - x) * .5f);
        }

        public static Vector2 ProjectDirection(Vector2 logicalDirection)
        {
            if (logicalDirection.sqrMagnitude < .000001f) return Vector2.zero;
            return Project(logicalDirection) - Project(Vector2.zero);
        }

        public static Vector2 UnprojectDirection(Vector2 screenDirection)
        {
            if (screenDirection.sqrMagnitude < .000001f) return Vector2.zero;
            return Unproject(screenDirection) - Unproject(Vector2.zero);
        }

        public static int SortingOrder(Vector2 logical, int offset = 0) =>
            offset + Mathf.RoundToInt((logical.x + logical.y) * DepthPrecision);
    }

    /// <summary>Projects a visual child while its owner remains in simulation space.</summary>
    public sealed class IsoVisual : MonoBehaviour
    {
        [SerializeField] private Transform logicalOwner;
        [SerializeField] private float elevation;
        [SerializeField] private int sortingOffset = 1000;
        [SerializeField] private bool followDungeonSurface = true;
        private Renderer[] renderers;
        private int[] rendererOffsets;
        public Vector2 LogicalPosition => logicalOwner != null ? (Vector2)logicalOwner.position : Vector2.zero;

        public void Initialize(Transform owner, float visualElevation = 0f, int orderOffset = 1000,
            bool followSurface = true)
        {
            logicalOwner = owner;
            elevation = visualElevation;
            sortingOffset = orderOffset;
            followDungeonSurface = followSurface;
            CacheRenderers();
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (logicalOwner == null) return;
            var dungeon = GameManager.Instance?.Dungeon;
            var surface = followDungeonSurface && dungeon != null
                ? dungeon.SurfaceHeight(logicalOwner.position)
                : 0f;
            transform.position = IsoWorld.Project(logicalOwner.position, elevation + surface);
            if (renderers == null || renderers.Length == 0) CacheRenderers();
            // The stair sprite must remain part of the architectural join, above its platform
            // fascia. While an actor occupies the narrow traversable flight, lift only the actor
            // in depth so the single-piece stair art cannot swallow their feet or torso.
            var stairActorBoost = followDungeonSurface && dungeon != null &&
                                  dungeon.IsOnElevationStair(logicalOwner.position) ? 72 : 0;
            var order = IsoWorld.SortingOrder(logicalOwner.position, sortingOffset + stairActorBoost);
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sortingOrder = order + rendererOffsets[i];
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            rendererOffsets = new int[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) rendererOffsets[i] = renderers[i].sortingOrder;
        }
    }
}
