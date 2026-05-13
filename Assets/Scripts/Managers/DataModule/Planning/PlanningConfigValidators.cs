using System.Collections.Generic;

namespace GameDemo.DataConfig.Planning
{
    public static class PlanningConfigValidators
    {
        public static IConfigValidator<SkillConfig> BuildSkillValidator(IReadOnlyList<CharacterConfig> characters)
        {
            var characterIds = new HashSet<string>();
            if (characters != null)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    if (characters[i] != null && !string.IsNullOrWhiteSpace(characters[i].id))
                    {
                        characterIds.Add(characters[i].id);
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

                    if (s.costValue < 0)
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.skills.cost.negative",
                            $"Skill id={s.id} has negative cost: {s.costValue}."));
                    }

                    if (string.IsNullOrWhiteSpace(s.target))
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Warning,
                            "planning.skills.target.empty",
                            $"Skill id={s.id} has empty target."));
                    }

                    if (!string.IsNullOrWhiteSpace(s.ownerRoleId) &&
                        s.ownerRoleId != "通用" &&
                        !characterIds.Contains(s.ownerRoleId))
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.skills.owner.not_found",
                            $"Skill id={s.id} ownerRoleId={s.ownerRoleId} was not found in characters."));
                    }
                }

                return new ConfigValidationReport(issues);
            });
        }

        public static IConfigValidator<EnemyConfig> BuildEnemyValidator(IReadOnlyList<BattleRewardConfig> rewards)
        {
            var rewardIds = new HashSet<string>();
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    if (rewards[i] != null && !string.IsNullOrWhiteSpace(rewards[i].id))
                    {
                        rewardIds.Add(rewards[i].id);
                    }
                }
            }

            return new DelegateConfigValidator<EnemyConfig>(records =>
            {
                var issues = new List<ConfigIssue>();
                if (records == null)
                {
                    issues.Add(new ConfigIssue(ConfigIssueLevel.Error, "planning.enemies.null", "Enemy records are null."));
                    return new ConfigValidationReport(issues);
                }

                for (int i = 0; i < records.Count; i++)
                {
                    var e = records[i];
                    if (e == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(e.rewardId))
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Warning,
                            "planning.enemies.reward.empty",
                            $"Enemy id={e.id} has empty rewardId."));
                        continue;
                    }

                    if (!rewardIds.Contains(e.rewardId))
                    {
                        issues.Add(new ConfigIssue(
                            ConfigIssueLevel.Error,
                            "planning.enemies.reward.not_found",
                            $"Enemy id={e.id} rewardId={e.rewardId} was not found."));
                    }
                }

                return new ConfigValidationReport(issues);
            });
        }
    }
}

