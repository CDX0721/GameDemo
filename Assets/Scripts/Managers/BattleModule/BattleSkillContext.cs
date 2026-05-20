using System;

namespace GameDemo.Battle
{
    public static class BattleSkillContext
    {
        [ThreadStatic]
        static Skill _current;

        public static Skill Current
        {
            get => _current;
            internal set => _current = value;
        }
    }
}
