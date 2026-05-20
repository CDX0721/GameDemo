using System.Collections.Generic;

namespace GameDemo.Battle
{
    public sealed class BattlePlanningCatalog
    {
        public IReadOnlyDictionary<string, BattleUnit> Units { get; }
        public IReadOnlyDictionary<string, Skill> Skills { get; }
        public IReadOnlyDictionary<string, BattleEffect> Effects { get; }
        public IReadOnlyList<string> Warnings { get; }

        public BattlePlanningCatalog(
            Dictionary<string, BattleUnit> units,
            Dictionary<string, Skill> skills,
            Dictionary<string, BattleEffect> effects,
            List<string> warnings)
        {
            Units = units ?? new Dictionary<string, BattleUnit>();
            Skills = skills ?? new Dictionary<string, Skill>();
            Effects = effects ?? new Dictionary<string, BattleEffect>();
            Warnings = warnings ?? new List<string>();
        }
    }
}
