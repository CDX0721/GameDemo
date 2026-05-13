using System;

namespace GameDemo.DataConfig.Planning
{
    [Serializable]
    public sealed class CoreFrameworkModuleConfig : IConfigRecord
    {
        public string id;
        public string moduleName;
        public string stage;
        public string featureDescription;
        public string note;
        public string Id => id;
    }

    [Serializable]
    public sealed class CharacterConfig : IConfigRecord
    {
        public string id;
        public string roleName;
        public int initialHp;
        public int initialMpSp;
        public string Id => id;
    }

    [Serializable]
    public sealed class SkillConfig : IConfigRecord
    {
        public string id;
        public string ownerRoleId;
        public int quality;
        public bool singleUsePerBattle;
        public string skillName;
        public string skillType;
        public int costValue;
        public string target;
        public string effects;
        public string description;
        public string Id => id;
    }

    [Serializable]
    public sealed class EnemyConfig : IConfigRecord
    {
        public string id;
        public bool isElite;
        public string battlePattern;
        public string rewardId;
        public string Id => id;
    }

    [Serializable]
    public sealed class BattleRewardConfig : IConfigRecord
    {
        public string id;
        public string rewardName;
        public string[] options;
        public string Id => id;
    }

    [Serializable]
    public sealed class StateConfig : IConfigRecord
    {
        public string id;
        public string stateName;
        public string stateType;
        public string duration;
        public string affectedAttribute;
        public string valueDescription;
        public string description;
        public string Id => id;
    }

    [Serializable]
    public sealed class DesignRuleNoteConfig : IConfigRecord
    {
        public string id;
        public string content;
        public string Id => id;
    }

    [Serializable]
    public sealed class BattleFormulaConfig : IConfigRecord
    {
        public string id;
        public string formulaType;
        public string formulaContent;
        public string variables;
        public string effectTriggerChance;
        public string note;
        public string Id => id;
    }

    [Serializable]
    public sealed class ItemEquipmentConfig : IConfigRecord
    {
        public string id;
        public string name;
        public string itemType;
        public string propertyBonus1;
        public string propertyBonus2;
        public string source;
        public string restriction;
        public string Id => id;
    }
}

