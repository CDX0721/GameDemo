using System;
using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public sealed class ConfigService
    {
        readonly IConfigTextProvider _textProvider;
        readonly IConfigSerializer _serializer;
        readonly ConfigRepository _repository;

        public ConfigService(IConfigTextProvider textProvider, IConfigSerializer serializer, ConfigRepository repository)
        {
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public ConfigLoadReport LoadTable<T>(string resourcePath, IConfigValidator<T> validator = null)
            where T : class, IConfigRecord
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return ConfigLoadReport.Fail(resourcePath, "config.path.empty", "Config resource path is null or empty.");
            }

            if (!_textProvider.TryGetText(resourcePath, out string json, out string textError))
            {
                return ConfigLoadReport.Fail(resourcePath, "config.text.load.failed", textError);
            }

            if (!_serializer.TryDeserializeList<T>(json, out List<T> records, out string parseError))
            {
                return ConfigLoadReport.Fail(resourcePath, "config.deserialize.failed", parseError);
            }

            IConfigValidator<T> effectiveValidator = validator ?? new DefaultConfigValidator<T>();
            ConfigValidationReport validation = effectiveValidator.Validate(records);
            if (validation.HasErrors)
            {
                return new ConfigLoadReport(false, resourcePath, 0, validation.Issues);
            }

            _repository.SetTable(records);
            return new ConfigLoadReport(true, resourcePath, records.Count, validation.Issues);
        }

        public bool TryGet<T>(string id, out T record) where T : class, IConfigRecord
        {
            return _repository.TryGet(id, out record);
        }

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfigRecord
        {
            return _repository.GetAll<T>();
        }

        public void Clear()
        {
            _repository.Clear();
        }
    }
}
