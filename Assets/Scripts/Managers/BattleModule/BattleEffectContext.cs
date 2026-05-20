using System;

namespace GameDemo.Battle
{
    public static class BattleEffectContext
    {
        [ThreadStatic]
        static BattleEffectInstance _current;

        public static BattleEffectInstance Current
        {
            get => _current;
            internal set => _current = value;
        }
    }
}
