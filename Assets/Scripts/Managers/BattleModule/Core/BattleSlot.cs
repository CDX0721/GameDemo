namespace GameDemo.Battle
{
    /// <summary>
    /// 3x3 阵形中的方格坐标。
    /// </summary>
    public struct BattleSlot
    {
        public int Row { get; }
        public int Col { get; }

        public BattleSlot(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool IsValid => Row >= 0 && Row < Formation.Rows && Col >= 0 && Col < Formation.Cols;

        public override string ToString() => $"({Row}, {Col})";
    }
}
