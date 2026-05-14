namespace GameDemo.Battle
{
    /// <summary>
    /// 附加效果运行时实例，持有模板引用和当前状态。
    /// 以 (Template.Id, Source) 作为去重关键字。
    /// </summary>
    public class BattleEffectInstance
    {
        public BattleEffect Template { get; }
        public int RemainingTurns { get; set; }
        public BattleUnitInstance? Source { get; }
        public int CurrentStackCount { get; set; }

        public bool IsExpired => RemainingTurns <= 0;

        public BattleEffectInstance(BattleEffect template, BattleUnitInstance? source)
        {
            Template = template;
            Source = source;
            RemainingTurns = template.InitialTurns;
            CurrentStackCount = 1;
        }

        public void ApplyTo(BattleUnitInstance unit)
        {
            Template.Apply(unit, CurrentStackCount);
        }
    }
}
