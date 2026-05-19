namespace GameDemo.DataConfig.Planning
{
    public sealed class PlanningConfigLoader
    {
        readonly ConfigService _service;

        public PlanningConfigLoader(ConfigService service)
        {
            _service = service;
        }

        public PlanningConfigBatchReport LoadAll()
        {
            var batch = new PlanningConfigBatchReport();

            batch.Add(_service.LoadTable<DesignGuidelineConfig>(PlanningConfigPaths.DesignGuidelines));

            var unitsReport = _service.LoadTable<BattleUnitConfig>(PlanningConfigPaths.BattleUnits);
            batch.Add(unitsReport);
            var units = _service.GetAll<BattleUnitConfig>();

            var skillsValidator = new CompositeConfigValidator<SkillConfig>()
                .Add(new DefaultConfigValidator<SkillConfig>())
                .Add(PlanningConfigValidators.BuildSkillValidator(units));
            var skillsReport = _service.LoadTable(PlanningConfigPaths.Skills, skillsValidator);
            batch.Add(skillsReport);

            var effectsValidator = new CompositeConfigValidator<BattleEffectConfig>()
                .Add(new DefaultConfigValidator<BattleEffectConfig>())
                .Add(PlanningConfigValidators.BuildBattleEffectValidator());
            batch.Add(_service.LoadTable(PlanningConfigPaths.BattleEffects, effectsValidator));

            batch.Add(_service.LoadTable<BattleRewardConfig>(PlanningConfigPaths.BattleRewards));

            return batch;
        }
    }
}

