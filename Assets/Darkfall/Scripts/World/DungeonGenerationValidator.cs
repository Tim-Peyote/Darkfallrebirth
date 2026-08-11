using System;

namespace Darkfall.World
{
    /// <summary>
    /// Final runtime gate for the generated product. Seed audits may add expensive statistical
    /// checks, but every playable floor passes these invariants after population and before input.
    /// </summary>
    public static class DungeonGenerationValidator
    {
        public static void ValidateAndComplete(DungeonData dungeon)
        {
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            if (!dungeon.HasCompletedStage(DungeonGenerationStage.Population))
                throw new InvalidOperationException("Dungeon validation requires completed population.");
            if (dungeon.Rooms == null || dungeon.Rooms.Count < 2)
                throw new InvalidOperationException("Dungeon requires distinct arrival and exit regions.");
            if (!dungeon.IsFloor(dungeon.StartCell.x, dungeon.StartCell.y) ||
                !dungeon.HasSemantic(dungeon.StartCell, DungeonCellSemantic.Arrival | DungeonCellSemantic.NoDecor))
                throw new InvalidOperationException("Dungeon arrival is missing or not reserved.");
            if (!dungeon.IsFloor(dungeon.ExitCell.x, dungeon.ExitCell.y) ||
                !dungeon.HasSemantic(dungeon.ExitCell,
                    DungeonCellSemantic.Exit | DungeonCellSemantic.Portal | DungeonCellSemantic.NoDecor))
                throw new InvalidOperationException("Dungeon exit portal is missing or not reserved.");
            dungeon.CompleteGenerationStage(DungeonGenerationStage.Validation);
        }
    }
}
