using System;
using System.Collections.Generic;
using System.Globalization;
using GameDemo.DataConfig.Planning;

namespace GameDemo.Battle
{
    public static class BattleActionParser
    {
        public static void ParseSkill(
            SkillConfig config,
            Skill skill,
            IReadOnlyDictionary<string, BattleEffect> effects,
            List<string> warnings)
        {
            if (config == null || skill == null)
            {
                return;
            }

            ParseSkillConditions(config, skill, warnings);
            ParseSkillActions(config, skill, effects, warnings);
        }

        public static void ParseEffect(
            BattleEffectConfig config,
            BattleEffect effect,
            List<string> warnings)
        {
            if (config == null || effect == null)
            {
                return;
            }

            ParseEffectActions(config, effect, warnings);
        }

        static void ParseSkillConditions(SkillConfig config, Skill skill, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(config.canCastConditions))
            {
                return;
            }

            foreach (string raw in SplitTokens(config.canCastConditions))
            {
                string token = raw.Trim();
                if (TryReadSingleArgument(token, "HasMana", out string manaText))
                {
                    if (!TryParseFloat(manaText, out float mana))
                    {
                        warnings.Add($"Skill {config.id} HasMana invalid value: {manaText}");
                        continue;
                    }

                    skill.CanCastConditions.Add(_ =>
                    {
                        Skill current = BattleSkillContext.Current;
                        if (current == null || current.Caster == null)
                            throw new InvalidOperationException("Skill.Caster is null in HasMana.");
                        return current.Caster.CurrentMana >= mana;
                    });
                    continue;
                }

                if (string.Equals(token, "NotUsedThisBattle", StringComparison.OrdinalIgnoreCase))
                {
                    skill.CanCastConditions.Add(_ =>
                    {
                        Skill current = BattleSkillContext.Current;
                        if (current == null)
                            throw new InvalidOperationException("Skill is null in NotUsedThisBattle.");
                        return !current.UsedThisBattle;
                    });
                    continue;
                }

                if (TryReadSingleArgument(token, "HpAbove", out string hpText))
                {
                    if (!TryParseFloat(hpText, out float hp))
                    {
                        warnings.Add($"Skill {config.id} HpAbove invalid value: {hpText}");
                        continue;
                    }

                    skill.CanCastConditions.Add(_ =>
                    {
                        Skill current = BattleSkillContext.Current;
                        if (current == null || current.Caster == null)
                            throw new InvalidOperationException("Skill.Caster is null in HpAbove.");
                        return current.Caster.CurrentHP > hp;
                    });
                    continue;
                }

                if (TryReadSingleArgument(token, "SelfShieldAbove", out string shieldText))
                {
                    if (!TryParseFloat(shieldText, out float shield))
                    {
                        warnings.Add($"Skill {config.id} SelfShieldAbove invalid value: {shieldText}");
                        continue;
                    }

                    skill.CanCastConditions.Add(_ =>
                    {
                        Skill current = BattleSkillContext.Current;
                        if (current == null || current.Caster == null)
                            throw new InvalidOperationException("Skill.Caster is null in SelfShieldAbove.");
                        return current.Caster.Shield > shield;
                    });
                    continue;
                }

                warnings.Add($"Skill {config.id} unsupported condition: {token}");
            }
        }

        static void ParseSkillActions(
            SkillConfig config,
            Skill skill,
            IReadOnlyDictionary<string, BattleEffect> effects,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(config.applyActions))
            {
                return;
            }

            foreach (string raw in SplitTokens(config.applyActions))
            {
                string token = raw.Trim();
                if (TryReadArguments(token, "DealDamage", out var args))
                {
                    AddDealDamageAction(config, skill, args, warnings);
                    continue;
                }

                if (TryReadArguments(token, "AddShield", out args))
                {
                    AddShieldAction(config, skill, args, warnings);
                    continue;
                }

                if (TryReadArguments(token, "ApplyEffect", out args))
                {
                    AddApplyEffectAction(config, skill, args, effects, warnings);
                    continue;
                }

                if (TryReadArguments(token, "HealPercent", out args))
                {
                    AddHealPercentAction(config, skill, args, warnings);
                    continue;
                }

                if (TryReadArguments(token, "LoseHP", out args))
                {
                    AddLoseHpAction(config, skill, args, warnings);
                    continue;
                }

                warnings.Add($"Skill {config.id} unsupported action: {token}");
            }
        }

        static void ParseEffectActions(BattleEffectConfig config, BattleEffect effect, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(config.applyActions))
            {
                return;
            }

            foreach (string raw in SplitTokens(config.applyActions))
            {
                string token = raw.Trim();
                if (token.Contains("If ", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("Else", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("PreAction", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("EndAction", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("OnAttack", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Effect {config.id} complex action not supported: {token}");
                    continue;
                }

                if (string.Equals(token, "CanAct=false", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "CanAct = false", StringComparison.OrdinalIgnoreCase))
                {
                    effect.ApplyActions.Add(units =>
                    {
                        foreach (BattleUnitInstance unit in units)
                            unit.CanAct = false;
                    });
                    continue;
                }

                if (TryReadAssignment(token, "SpeedFlat", out string speedExpr))
                {
                    if (!IsEffectExpressionSupported(speedExpr, out string error))
                    {
                        warnings.Add($"Effect {config.id} SpeedFlat invalid expression: {speedExpr} ({error})");
                        continue;
                    }

                    effect.ApplyActions.Add(units =>
                    {
                        float value = EvaluateEffectExpression(speedExpr);
                        foreach (BattleUnitInstance unit in units)
                            unit.BonusSpeed += value;
                    });
                    continue;
                }

                if (TryReadAssignment(token, "AttackFlat", out string attackExpr))
                {
                    if (!IsEffectExpressionSupported(attackExpr, out string error))
                    {
                        warnings.Add($"Effect {config.id} AttackFlat invalid expression: {attackExpr} ({error})");
                        continue;
                    }

                    effect.ApplyActions.Add(units =>
                    {
                        float value = EvaluateEffectExpression(attackExpr);
                        foreach (BattleUnitInstance unit in units)
                            unit.BonusAttack += value;
                    });
                    continue;
                }

                if (TryReadMultiplier(token, "OutgoingDamageMultiplier", out float outgoing))
                {
                    effect.ApplyActions.Add(units =>
                    {
                        foreach (BattleUnitInstance unit in units)
                        {
                            float current = 1f + unit.DamageBonus;
                            float next = current * outgoing;
                            unit.DamageBonus = next - 1f;
                        }
                    });
                    continue;
                }

                if (TryReadMultiplier(token, "IncomingActiveAttackDamageMultiplier", out float incoming))
                {
                    effect.ApplyActions.Add(units =>
                    {
                        foreach (BattleUnitInstance unit in units)
                        {
                            float current = 1f - unit.DamageReduction;
                            float next = current * incoming;
                            unit.DamageReduction = 1f - next;
                        }
                    });
                    continue;
                }

                warnings.Add($"Effect {config.id} unsupported action: {token}");
            }
        }

        static void AddDealDamageAction(SkillConfig config, Skill skill, List<string> args, List<string> warnings)
        {
            string targetSpec;
            string expr;
            if (args.Count == 1)
            {
                targetSpec = "Target";
                expr = args[0];
            }
            else
            {
                targetSpec = args[0];
                expr = args.Count > 1 ? args[1] : string.Empty;
            }

            if (!IsSkillExpressionSupported(expr, out string error))
            {
                warnings.Add($"Skill {config.id} DealDamage invalid expr: {expr} ({error})");
                return;
            }

            skill.ApplyActions.Add(targets =>
            {
                BattleUnitInstance caster = RequireCaster();
                float baseValue = EvaluateSkillExpression(expr, caster);
                foreach (BattleUnitInstance target in ResolveTargets(targetSpec, caster, targets))
                {
                    if (target == null || !target.IsAlive) continue;
                    target.TakeDamage(baseValue);
                }
            });
        }

        static void AddShieldAction(SkillConfig config, Skill skill, List<string> args, List<string> warnings)
        {
            if (args.Count == 0)
            {
                warnings.Add($"Skill {config.id} AddShield missing args");
                return;
            }

            string targetSpec = args.Count > 1 ? args[0] : "Target";
            string expr = args.Count > 1 ? args[1] : args[0];

            if (!IsSkillExpressionSupported(expr, out string error))
            {
                warnings.Add($"Skill {config.id} AddShield invalid expr: {expr} ({error})");
                return;
            }

            skill.ApplyActions.Add(targets =>
            {
                BattleUnitInstance caster = RequireCaster();
                float value = EvaluateSkillExpression(expr, caster);
                foreach (BattleUnitInstance target in ResolveTargets(targetSpec, caster, targets))
                {
                    if (target == null || !target.IsAlive) continue;
                    target.Shield += value;
                }
            });
        }

        static void AddApplyEffectAction(
            SkillConfig config,
            Skill skill,
            List<string> args,
            IReadOnlyDictionary<string, BattleEffect> effects,
            List<string> warnings)
        {
            if (args.Count < 2)
            {
                warnings.Add($"Skill {config.id} ApplyEffect missing args");
                return;
            }

            string targetSpec;
            string effectId;
            string stacksText;

            if (args.Count == 2)
            {
                targetSpec = "Target";
                effectId = args[0];
                stacksText = args[1];
            }
            else
            {
                targetSpec = args[0];
                effectId = args[1];
                stacksText = args[2];
            }

            if (!TryParseInt(stacksText, out int stacks))
            {
                warnings.Add($"Skill {config.id} ApplyEffect invalid stacks: {stacksText}");
                return;
            }

            if (!effects.TryGetValue(effectId, out BattleEffect effect))
            {
                warnings.Add($"Skill {config.id} ApplyEffect unknown effect: {effectId}");
                return;
            }

            skill.ApplyActions.Add(targets =>
            {
                BattleUnitInstance caster = RequireCaster();
                foreach (BattleUnitInstance target in ResolveTargets(targetSpec, caster, targets))
                {
                    if (target == null || !target.IsAlive) continue;
                    for (int i = 0; i < stacks; i++)
                        target.AddEffect(effect, caster);
                }
            });
        }

        static void AddHealPercentAction(SkillConfig config, Skill skill, List<string> args, List<string> warnings)
        {
            if (args.Count < 2)
            {
                warnings.Add($"Skill {config.id} HealPercent missing args");
                return;
            }

            string targetSpec = args[0];
            string percentText = args[1];
            if (!TryParseFloat(percentText, out float percent))
            {
                warnings.Add($"Skill {config.id} HealPercent invalid value: {percentText}");
                return;
            }

            skill.ApplyActions.Add(targets =>
            {
                BattleUnitInstance caster = RequireCaster();
                foreach (BattleUnitInstance target in ResolveTargets(targetSpec, caster, targets))
                {
                    if (target == null || !target.IsAlive) continue;
                    float amount = target.MaxHP * percent / 100f;
                    target.CurrentHP = MathF.Min(target.MaxHP, target.CurrentHP + amount);
                }
            });
        }

        static void AddLoseHpAction(SkillConfig config, Skill skill, List<string> args, List<string> warnings)
        {
            if (args.Count < 2)
            {
                warnings.Add($"Skill {config.id} LoseHP missing args");
                return;
            }

            string targetSpec = args[0];
            string amountText = args[1];
            bool ignoreDefense = false;
            if (args.Count > 2)
            {
                for (int i = 2; i < args.Count; i++)
                {
                    if (args[i].Equals("IgnoreDefense=true", StringComparison.OrdinalIgnoreCase))
                        ignoreDefense = true;
                }
            }

            if (!TryParseFloat(amountText, out float amount))
            {
                warnings.Add($"Skill {config.id} LoseHP invalid value: {amountText}");
                return;
            }

            skill.ApplyActions.Add(targets =>
            {
                BattleUnitInstance caster = RequireCaster();
                foreach (BattleUnitInstance target in ResolveTargets(targetSpec, caster, targets))
                {
                    if (target == null || !target.IsAlive) continue;
                    if (ignoreDefense)
                    {
                        target.CurrentHP = MathF.Max(0f, target.CurrentHP - amount);
                    }
                    else
                    {
                        target.TakeDamage(amount);
                    }
                }
            });
        }

        static IEnumerable<BattleUnitInstance> ResolveTargets(
            string targetSpec,
            BattleUnitInstance caster,
            List<BattleUnitInstance> targets)
        {
            if (string.Equals(targetSpec, "Self", StringComparison.OrdinalIgnoreCase))
                return new[] { caster };
            if (string.Equals(targetSpec, "Target", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetSpec, "Enemy", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetSpec, "Ally", StringComparison.OrdinalIgnoreCase))
                return targets;
            return targets;
        }

        static BattleUnitInstance RequireCaster()
        {
            Skill current = BattleSkillContext.Current;
            if (current == null || current.Caster == null)
                throw new InvalidOperationException("Skill.Caster is null.");
            return current.Caster;
        }

        static IEnumerable<string> SplitTokens(string raw)
        {
            return raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        static bool TryReadSingleArgument(string token, string name, out string argument)
        {
            argument = null;
            if (!TryReadArguments(token, name, out var args))
            {
                return false;
            }

            if (args.Count != 1)
            {
                return false;
            }

            argument = args[0];
            return true;
        }

        static bool TryReadArguments(string token, string name, out List<string> args)
        {
            args = null;
            if (!token.StartsWith(name + "(", StringComparison.OrdinalIgnoreCase) || !token.EndsWith(")"))
                return false;

            string inner = token.Substring(name.Length + 1, token.Length - name.Length - 2);
            args = new List<string>();
            foreach (string part in inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                args.Add(part.Trim());
            }
            return true;
        }

        static bool TryReadAssignment(string token, string left, out string expr)
        {
            expr = null;
            if (!token.StartsWith(left, StringComparison.OrdinalIgnoreCase))
                return false;
            int opIndex = token.IndexOf("+=", left.Length, StringComparison.OrdinalIgnoreCase);
            if (opIndex < 0)
                return false;
            expr = token.Substring(opIndex + 2).Trim();
            return true;
        }

        static bool TryReadMultiplier(string token, string left, out float value)
        {
            value = 0f;
            if (!token.StartsWith(left, StringComparison.OrdinalIgnoreCase))
                return false;
            int opIndex = token.IndexOf("*=", left.Length, StringComparison.OrdinalIgnoreCase);
            if (opIndex < 0)
                return false;
            string expr = token.Substring(opIndex + 2).Trim();
            return TryParseFloat(expr, out value);
        }

        static bool IsSkillExpressionSupported(string expr, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(expr))
            {
                error = "empty";
                return false;
            }

            string normalized = expr.Replace(" ", string.Empty);
            string[] parts = normalized.Split('*');
            foreach (string part in parts)
            {
                if (string.Equals(part, "Attack", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(part, "CurrentShield", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(part, "CurrentMana", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (TryParseFloat(part, out float _))
                    continue;

                error = $"unknown token {part}";
                return false;
            }

            return true;
        }

        static float EvaluateSkillExpression(string expr, BattleUnitInstance caster)
        {
            float result = 1f;
            string normalized = expr.Replace(" ", string.Empty);
            string[] parts = normalized.Split('*');
            foreach (string part in parts)
            {
                if (string.Equals(part, "Attack", StringComparison.OrdinalIgnoreCase))
                {
                    result *= caster.CurrentAttack;
                    continue;
                }

                if (string.Equals(part, "CurrentShield", StringComparison.OrdinalIgnoreCase))
                {
                    result *= caster.Shield;
                    continue;
                }

                if (string.Equals(part, "CurrentMana", StringComparison.OrdinalIgnoreCase))
                {
                    result *= caster.CurrentMana;
                    continue;
                }

                if (TryParseFloat(part, out float number))
                {
                    result *= number;
                    continue;
                }

                throw new InvalidOperationException($"Unsupported token in skill expression: {part}");
            }

            return result;
        }

        static bool IsEffectExpressionSupported(string expr, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(expr))
            {
                error = "empty";
                return false;
            }

            string normalized = expr.Replace(" ", string.Empty);
            string[] parts = normalized.Split('*');
            foreach (string part in parts)
            {
                if (string.Equals(part, "StackCount", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (TryParseFloat(part, out float _))
                    continue;

                error = $"unknown token {part}";
                return false;
            }

            return true;
        }

        static float EvaluateEffectExpression(string expr)
        {
            int stack = BattleEffectContext.Current != null ? BattleEffectContext.Current.CurrentStackCount : 1;

            float result = 1f;
            string normalized = expr.Replace(" ", string.Empty);
            string[] parts = normalized.Split('*');
            foreach (string part in parts)
            {
                if (string.Equals(part, "StackCount", StringComparison.OrdinalIgnoreCase))
                {
                    result *= stack;
                    continue;
                }

                if (TryParseFloat(part, out float number))
                {
                    result *= number;
                    continue;
                }

                throw new InvalidOperationException($"Unsupported token in effect expression: {part}");
            }

            return result;
        }

        static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
