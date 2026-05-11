using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public interface IConfigSerializer
    {
        bool TryDeserializeList<T>(string json, out List<T> records, out string errorMessage) where T : class, IConfigRecord;
    }
}
