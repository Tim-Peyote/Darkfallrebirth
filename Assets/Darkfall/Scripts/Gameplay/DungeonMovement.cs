using Darkfall.World;
using UnityEngine;

namespace Darkfall.Gameplay
{
    /// <summary>Shared collision resolution for actors moving on the logical dungeon grid.</summary>
    public static class DungeonMovement
    {
        public static Vector2 ResolveStep(DungeonData dungeon, Vector2 current, Vector2 step,
            float radius, bool preferX)
        {
            if (dungeon == null || step.sqrMagnitude <= .0000001f) return current;
            var full = current + step;
            if (dungeon.CanTraverse(current, full, radius)) return full;

            // Slide only when the intended diagonal is blocked. Alternating the first axis for
            // equal isometric components removes the permanent X bias that made A/D movement
            // twitch along narrow walls and door cheeks.
            if (preferX)
            {
                current = ResolveAxis(dungeon, current, new Vector2(step.x, 0f), radius);
                return ResolveAxis(dungeon, current, new Vector2(0f, step.y), radius);
            }
            current = ResolveAxis(dungeon, current, new Vector2(0f, step.y), radius);
            return ResolveAxis(dungeon, current, new Vector2(step.x, 0f), radius);
        }

        private static Vector2 ResolveAxis(DungeonData dungeon, Vector2 current, Vector2 step, float radius)
        {
            if (step.sqrMagnitude <= .0000001f) return current;
            var target = current + step;
            return dungeon.CanTraverse(current, target, radius) ? target : current;
        }
    }
}
