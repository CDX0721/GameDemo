using System.Collections.Generic;

namespace GameDemo.Battle
{
    public struct SkillAnimEntry
    {
        public BattleUnitInstance Target;
        public float Delay;
    }

    public struct SkillSFXEntry
    {
        public string SkillId;
        public float Delay;
    }

    public static class BattleSkillContext
    {
        /// <summary>ApplyActions 内部注册的目标动画列表。表现层读取后清空。</summary>
        public static List<SkillAnimEntry>? PendingSkillAnimations;

        /// <summary>ApplyActions 内部注册的音效列表。表现层读取后清空。</summary>
        public static List<SkillSFXEntry>? PendingSFX;

        /// <summary>注册一个需要播放技能特效动画的目标（delay 为相对 Apply 完成的秒数）。</summary>
        public static void RegisterAnimTarget(BattleUnitInstance target, float delay = 0f)
        {
            PendingSkillAnimations ??= new List<SkillAnimEntry>();
            PendingSkillAnimations.Add(new SkillAnimEntry { Target = target, Delay = delay });
        }

        /// <summary>注册技能音效。同 delay 同 skillId 只播放一次，不同 delay 各自播放。</summary>
        public static void RegisterSFX(string skillId, float delay = 0f)
        {
            PendingSFX ??= new List<SkillSFXEntry>();
            PendingSFX.Add(new SkillSFXEntry { SkillId = skillId, Delay = delay });
        }
    }
}
