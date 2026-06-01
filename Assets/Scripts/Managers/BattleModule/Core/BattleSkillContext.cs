using System;
using System.Collections.Generic;

namespace GameDemo.Battle
{
    /// <summary>
    /// 技能执行期间的动画注册结构。
    /// </summary>
    public struct SkillAnimEntry
    {
        public BattleUnitInstance Target;
        public float Delay;
    }

    public static class BattleSkillContext
    {
        [ThreadStatic]
        static Skill _current;

        public static Skill Current
        {
            get => _current;
            internal set => _current = value;
        }

        /// <summary>ApplyActions 内部注册的目标动画列表。表现层读取后清空。</summary>
        public static List<SkillAnimEntry>? PendingSkillAnimations;

        /// <summary>注册一个需要播放技能特效动画的目标（delay 为相对 Apply 完成的秒数）。</summary>
        public static void RegisterAnimTarget(BattleUnitInstance target, float delay = 0f)
        {
            PendingSkillAnimations ??= new List<SkillAnimEntry>();
            PendingSkillAnimations.Add(new SkillAnimEntry { Target = target, Delay = delay });
        }
    }
}
