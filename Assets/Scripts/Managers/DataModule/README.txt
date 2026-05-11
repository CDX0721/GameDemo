Data/Config Foundation (GameDemo.DataConfig)

Key modules
- IConfigRecord: record id contract
- UnityJsonConfigSerializer: parses { "items": [ ... ] }
- DefaultConfigValidator / CompositeConfigValidator: validation chain
- ConfigRepository: in-memory typed table storage
- ConfigService: load-validate-store query flow
- AssetManagerLoader + AssetManagerTextProvider: integration bridge to AssetModule
- ConfigModule: top-level facade singleton

Usage
1) ConfigModule.Instance.Initialize();
2) ConfigModule.Instance.LoadTable<AudioConfig>("TestConfigs/audio_config_list");
3) ConfigModule.Instance.TryGet("bgm_main", out AudioConfig config);
