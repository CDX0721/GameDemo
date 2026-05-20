using System.Collections.Generic;

namespace GameDemo.DataConfig.Planning
{
    public static class PlanningConfigValidators
    {
        public static IConfigValidator<SkillConfig> BuildSkillValidator(IReadOnlyList<BattleUnitConfig> units)
        {
            var unitIds = new HashSet<string>();
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] != null && !string.IsNullOrWhiteSpace(units[i].id))
                    {
                        unitIds.Add(units[i].id);
                    }
                }
            }

            return new DelegateConfigValidator<SkillConfig>(records =>
            {
                var issues = new List<ConfigIssue>();
                if (records == null)
                {
                    issues.Add(new ConfigIssue(ConfigIssueLevel.Error, "planning.skills.null", "Skill records are null."));
                    return new ConfigValidationReport(issues);
                }

                for (int i = 0; i < records.Count; i++)
                {
                    var s = records[i];
                    if (s == null)
                    {
                        continue;
                    }

                    if (s.manaCost < 0)
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.skills.cost.negative",
                            $"Skill id={s.id} has negative mana cost: {s.manaCost}."));
                    }

                    if (s.level <= 0)
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Warning,
                            "planning.skills.level.invalid",
                            $"Skill id={s.id} has non-positive level: {s.level}."));
                    }

                    if (!string.IsNullOrWhiteSpace(s.ownerUnitId) && !unitIds.Contains(s.ownerUnitId))
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.skills.owner.not_found",
                            $"Skill id={s.id} ownerUnitId={s.ownerUnitId} was not found in battle units."));
                    }
                }

                return new ConfigValidationReport(issues);
            });
        }

        public static IConfigValidator<BattleEffectConfig> BuildBattleEffectValidator()
        {
            return new DelegateConfigValidator<BattleEffectConfig>(records =>
            {
                var issues = new List<ConfigIssue>();
                if (records == null)
                {
                    issues.Add(new ConfigIssue(ConfigIssueLevel.Error, "planning.effects.null", "BattleEffect records are null."));
                    return new ConfigValidationReport(issues);
                }

                for (int i = 0; i < records.Count; i++)
                {
                    var effect = records[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    if (effect.maxStackCount < 0)
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.effects.max_stack.negative",
                            $"BattleEffect id={effect.id} has negative maxStackCount: {effect.maxStackCount}."));
                    }

                    if (effect.initialTurns < 0)
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.effects.initial_turns.negative",
                            $"BattleEffect id={effect.id} has negative initialTurns: {effect.initialTurns}."));
                    }
                }

                return new ConfigValidationReport(issues);
            });
        }
    }
}

