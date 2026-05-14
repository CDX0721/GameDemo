namespace GameDemo.Battle
{
    /// <summary>
    /// 自动行动战斗单位实例，由 AI 驱动。
    /// </summary>
    public class AutoUnitInstance : BattleUnitInstance
    {
        public AutoUnitInstance(BattleUnit template, float initialCost)
            : base(template, initialCost) { }
    }
}
