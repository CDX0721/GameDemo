using System;
using System.Threading;
using GameDemo.DataConfig.Planning;

namespace GameDemo.DataConfig
{
    /// <summary>
    /// Facade entry point for Data/Config loading and access.
    /// </summary>
    public sealed class ConfigModule
    {
        static readonly Lazy<ConfigModule> InstanceBuilder =
            new Lazy<ConfigModule>(() => new ConfigModule(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static ConfigModule Instance => InstanceBuilder.Value;

        public bool IsInitialized { get; private set; }
        public ConfigService Service { get; private set; }
        public PlanningConfigLoader Planning { get; private set; }

        ConfigModule() { }

        public void Initialize(
            IConfigTextProvider textProvider = null,
            IConfigSerializer serializer = null,
            ConfigRepository repository = null)
        {
            Service = new ConfigService(
                textProvider ?? new AssetManagerTextProvider(),
                serializer ?? new UnityJsonConfigSerializer(),
                repository ?? new ConfigRepository());
            Planning = new PlanningConfigLoader(Service);
            IsInitialized = true;
        }

        public ConfigLoadReport LoadTable<T>(string resourcePath, IConfigValidator<T> validator = null)
            where T : class, IConfigRecord
        {
            EnsureInitialized();
            return Service.LoadTable(resourcePath, validator);
        }

        public bool TryGet<T>(string id, out T record) where T : class, IConfigRecord
        {
            EnsureInitialized();
            return Service.TryGet(id, out record);
        }

        public void Clear()
        {
            if (!IsInitialized || Service == null)
            {
                return;
            }

            Service.Clear();
        }

        public PlanningConfigBatchReport LoadPlanningConfigs()
        {
            EnsureInitialized();
            return Planning.LoadAll();
        }

        void EnsureInitialized()
        {
            if (!IsInitialized || Service == null)
            {
                throw new InvalidOperationException("ConfigModule is not initialized. Call Initialize() first.");
            }
        }
    }
}
