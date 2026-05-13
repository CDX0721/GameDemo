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

            var coreReport = _service.LoadTable<CoreFrameworkModuleConfig>(PlanningConfigPaths.CoreFrameworkModules);
            batch.Add(coreReport);

            var characterReport = _service.LoadTable<CharacterConfig>(PlanningConfigPaths.Characters);
            batch.Add(characterReport);
            var characters = _service.GetAll<CharacterConfig>();

            var rewardsReport = _service.LoadTable<BattleRewardConfig>(PlanningConfigPaths.BattleRewards);
            batch.Add(rewardsReport);
            var rewards = _service.GetAll<BattleRewardConfig>();

            var skillsValidator = new CompositeConfigValidator<SkillConfig>()
                .Add(new DefaultConfigValidator<SkillConfig>())
                .Add(PlanningConfigValidators.BuildSkillValidator(characters));
            var skillsReport = _service.LoadTable(PlanningConfigPaths.Skills, skillsValidator);
            batch.Add(skillsReport);

            var enemiesValidator = new CompositeConfigValidator<EnemyConfig>()
                .Add(new DefaultConfigValidator<EnemyConfig>())
                .Add(PlanningConfigValidators.BuildEnemyValidator(rewards));
            var enemiesReport = _service.LoadTable(PlanningConfigPaths.Enemies, enemiesValidator);
            batch.Add(enemiesReport);

            batch.Add(_service.LoadTable<StateConfig>(PlanningConfigPaths.States));
            batch.Add(_service.LoadTable<DesignRuleNoteConfig>(PlanningConfigPaths.DesignRuleNotes));
            batch.Add(_service.LoadTable<BattleFormulaConfig>(PlanningConfigPaths.BattleFormulas));
            batch.Add(_service.LoadTable<ItemEquipmentConfig>(PlanningConfigPaths.ItemsEquipment));

            return batch;
        }
    }
}

