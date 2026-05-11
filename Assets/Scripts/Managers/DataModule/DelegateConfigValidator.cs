using System;
using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public sealed class DelegateConfigValidator<T> : IConfigValidator<T> where T : IConfigRecord
    {
        readonly Func<IReadOnlyList<T>, ConfigValidationReport> _validate;

        public DelegateConfigValidator(Func<IReadOnlyList<T>, ConfigValidationReport> validate)
        {
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }

        public ConfigValidationReport Validate(IReadOnlyList<T> records)
        {
            return _validate(records);
        }
    }
}
