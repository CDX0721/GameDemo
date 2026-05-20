using System;
using System.Collections.Generic;
using GameDemo.DataConfig.Planning;

namespace GameDemo.Battle
{
    public static class BattlePlanningCompiler
    {
        public static BattlePlanningCatalog Compile(
            IReadOnlyList<BattleUnitConfig> units,
            IReadOnlyList<SkillConfig> skills,
            IReadOnlyList<BattleEffectConfig> effects)
        {
            var warnings = new List<string>();
            var effectDict = BuildEffects(effects, warnings);
            var skillDict = BuildSkills(skills, effectDict, warnings);
            var unitDict = BuildUnits(units, skillDict, warnings);
            return new BattlePlanningCatalog(unitDict, skillDict, effectDict, warnings);
        }

        static Dictionary<string, BattleEffect> BuildEffects(
            IReadOnlyList<BattleEffectConfig> effects,
            List<string> warnings)
        {
            var dict = new Dictionary<string, BattleEffect>();
            if (effects == null)
            {
                warnings.Add("BattleEffect configs missing.");
                return dict;
            }

            foreach (BattleEffectConfig config in effects)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.id))
                {
                    warnings.Add("BattleEffect config missing id.");
                    continue;
                }

                if (dict.ContainsKey(config.id))
                {
                    warnings.Add($"BattleEffect duplicate id: {config.id}");
                    continue;
                }

                if (!Enum.TryParse(config.effectType, true, out BattleEffectType effectType))
                {
                    warnings.Add($"BattleEffect {config.id} invalid effectType: {config.effectType}");
                    effectType = BattleEffectType.Other;
                }

                if (!Enum.TryParse(config.statusType, true, out BattleEffectStatusType statusType))
                {
                    warnings.Add($"BattleEffect {config.id} invalid statusType: {config.statusType}");
                    statusType = BattleEffectStatusType.Mark;
                }

                var effect = new BattleEffect(
                    config.id,
                    config.displayName,
                    effectType,
                    statusType,
                    Math.Max(0, config.initialTurns),
                    Math.Max(1, config.maxStackCount));

                BattleActionParser.ParseEffect(config, effect, warnings);
                dict[config.id] = effect;
            }

            return dict;
        }

        static Dictionary<string, Skill> BuildSkills(
            IReadOnlyList<SkillConfig> skills,
            IReadOnlyDictionary<string, BattleEffect> effects,
            List<string> warnings)
        {
            var dict = new Dictionary<string, Skill>();
            if (skills == null)
            {
                warnings.Add("Skill configs missing.");
                return dict;
            }

            foreach (SkillConfig config in skills)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.id))
                {
                    warnings.Add("Skill config missing id.");
                    continue;
                }

                if (dict.ContainsKey(config.id))
                {
                    warnings.Add($"Skill duplicate id: {config.id}");
                    continue;
                }

                if (!Enum.TryParse(config.skillType, true, out SkillType skillType))
                {
                    warnings.Add($"Skill {config.id} invalid skillType: {config.skillType}");
                    skillType = SkillType.Support;
                }

                if (!Enum.TryParse(config.targetType, true, out TargetType targetType))
                {
                    warnings.Add($"Skill {config.id} invalid targetType: {config.targetType}");
                    targetType = TargetType.SingleEnemy;
                }

                var skill = new Skill(config.id, config.displayName, skillType, targetType, Math.Max(1, config.level))
                {
                    ManaCost = Math.Max(0, config.manaCost),
                    OncePerBattle = config.oncePerBattle
                };

                BattleActionParser.ParseSkill(config, skill, effects, warnings);
                dict[config.id] = skill;
            }

            return dict;
        }

        static Dictionary<string, BattleUnit> BuildUnits(
            IReadOnlyList<BattleUnitConfig> units,
            IReadOnlyDictionary<string, Skill> skills,
            List<string> warnings)
        {
            var dict = new Dictionary<string, BattleUnit>();
            if (units == null)
            {
                warnings.Add("BattleUnit configs missing.");
                return dict;
            }

            foreach (BattleUnitConfig config in units)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.id))
                {
                    warnings.Add("BattleUnit config missing id.");
                    continue;
                }

                if (dict.ContainsKey(config.id))
                {
                    warnings.Add($"BattleUnit duplicate id: {config.id}");
                    continue;
                }

                var unit = new BattleUnit(
                    config.id,
                    config.displayName,
                    config.attack,
                    config.defense,
                    config.hp,
                    config.speed,
                    config.mana);

                if (config.innateSkillIds != null)
                {
                    for (int i = 0; i < config.innateSkillIds.Length; i++)
                    {
                        string skillId = config.innateSkillIds[i];
                        if (string.IsNullOrWhiteSpace(skillId))
                            continue;
                        if (!skills.TryGetValue(skillId, out Skill skill))
                        {
                            warnings.Add($"BattleUnit {config.id} missing skill: {skillId}");
                            continue;
                        }
                        unit.InnateSkills.Add(skill);
                    }
                }

                dict[config.id] = unit;
            }

            return dict;
        }
    }
}
