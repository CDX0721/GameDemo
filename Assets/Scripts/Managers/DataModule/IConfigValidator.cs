using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public interface IConfigValidator<in T> where T : IConfigRecord
    {
        ConfigValidationReport Validate(IReadOnlyList<T> records);
    }
}
