namespace GameDemo.Battle
{
    /// <summary>
    /// 玩家可操作战斗单位实例。
    /// </summary>
    public class PlayableUnitInstance : BattleUnitInstance
    {
        public PlayableUnitInstance(BattleUnit template, float initialCost)
            : base(template, initialCost) { }
    }
}
