using System;

namespace GameDemo.DataConfig.Planning
{
    [Serializable]
    public sealed class DesignGuidelineConfig : IConfigRecord
    {
        public string id;
        public string topic;
        public string content;
        public string Id => id;
    }

    [Serializable]
    public sealed class BattleUnitConfig : IConfigRecord
    {
        public string id;
        public string displayName;
        public string faction;
        public string role;
        public float hp;
        public float attack;
        public float defense;
        public float speed;
        public float mana;
        public string[] innateSkillIds;
        public string designNotes;
        public string Id => id;
    }

    [Serializable]
    public sealed class SkillConfig : IConfigRecord
    {
        public string id;
        public string displayName;
        public string ownerUnitId;
        public string skillType;
        public string targetType;
        public int level;
        public int manaCost;
        public bool oncePerBattle;
        public int quality;
        public string canCastConditions;
        public string applyActions;
        public string animationCue;
        public string sfxCue;
        public string designNotes;
        public string Id => id;
    }

    [Serializable]
    public sealed class BattleEffectConfig : IConfigRecord
    {
        public string id;
        public string displayName;
        public string effectType;
        public string statusType;
        public int initialTurns;
        public int maxStackCount;
        public string stackRule;
        public string triggerTiming;
        public string applyActions;
        public string visualCue;
        public string sfxCue;
        public string designNotes;
        public string Id => id;
    }

    [Serializable]
    public sealed class BattleRewardConfig : IConfigRecord
    {
        public string id;
        public string displayName;
        public string rewardRarity;
        public string applyActions;
        public string designNotes;
        public string Id => id;
    }
}
