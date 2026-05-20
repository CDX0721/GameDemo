Data/Config Foundation (GameDemo.DataConfig)

Key modules
- IConfigRecord: record id contract
- UnityJsonConfigSerializer: parses { "items": [ ... ] }
- DefaultConfigValidator / CompositeConfigValidator: validation chain
- ConfigRepository: in-memory typed table storage
- ConfigService: load-validate-store query flow
- AssetManagerLoader + AssetManagerTextProvider: integration bridge to AssetModule
- ConfigModule: top-level facade singleton
- Planning configs: typed runtime tables exported from 策划案.xlsx
  - DesignGuidelineConfig
  - BattleUnitConfig
  - SkillConfig
  - BattleEffectConfig
  - BattleRewardConfig
  - PlanningConfigLoader + cross-table validators

Usage
1) ConfigModule.Instance.Initialize();
2) ConfigModule.Instance.LoadTable<AudioConfig>("TestConfigs/audio_config_list");
3) ConfigModule.Instance.TryGet("bgm_main", out AudioConfig config);
4) ConfigModule.Instance.LoadPlanningConfigs();

Export pipeline
- Source xlsx: Assets/ConfigSource/策划案.xlsx
- Export script: Tools/ConfigExport/export_planning_xlsx.py
- Validation runner: Tools/ConfigExport/run_export_validation.ps1
