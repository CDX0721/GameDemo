using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 3x3 阵形，管理 BattleUnitInstance 在网格中的位置。
    /// </summary>
    public class Formation
    {
        public const int Rows = 3;
        public const int Cols = 3;

        private readonly BattleUnitInstance?[,] _grid = new BattleUnitInstance?[Rows, Cols];

        public IReadOnlyList<BattleUnitInstance> Units
        {
            get
            {
                var list = new List<BattleUnitInstance>();
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++)
                        if (_grid[r, c] is BattleUnitInstance unit)
                            list.Add(unit);
                return list;
            }
        }

        public int UnitCount
        {
            get
            {
                int count = 0;
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++)
                        if (_grid[r, c] != null)
                            count++;
                return count;
            }
        }

        public BattleUnitInstance? PlaceUnit(BattleUnitInstance unit, BattleSlot slot)
        {
            BattleUnitInstance? old = _grid[slot.Row, slot.Col];
            _grid[slot.Row, slot.Col] = unit;
            unit.Row = slot.Row;
            unit.Col = slot.Col;
            return old;
        }

        public BattleUnitInstance? RemoveUnit(BattleSlot slot)
        {
            BattleUnitInstance? unit = _grid[slot.Row, slot.Col];
            if (unit != null)
            {
                unit.Row = -1;
                unit.Col = -1;
            }
            _grid[slot.Row, slot.Col] = null;
            return unit;
        }

        public BattleUnitInstance? GetUnit(BattleSlot slot) => _grid[slot.Row, slot.Col];

        public BattleSlot FindUnit(BattleUnitInstance unit)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (_grid[r, c] == unit)
                        return new BattleSlot(r, c);
            return new BattleSlot(-1, -1);
        }

        public bool IsOccupied(BattleSlot slot) => _grid[slot.Row, slot.Col] != null;
    }
}
