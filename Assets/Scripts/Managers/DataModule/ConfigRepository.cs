using System;
using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public sealed class ConfigRepository
    {
        readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();

        public void SetTable<T>(IReadOnlyList<T> records) where T : class, IConfigRecord
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            var byId = new Dictionary<string, T>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                T record = records[i];
                if (record == null || string.IsNullOrWhiteSpace(record.Id))
                {
                    continue;
                }

                byId[record.Id] = record;
            }

            _tables[typeof(T)] = byId;
        }

        public bool TryGet<T>(string id, out T record) where T : class, IConfigRecord
        {
            record = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (!_tables.TryGetValue(typeof(T), out object tableObj))
            {
                return false;
            }

            var table = tableObj as Dictionary<string, T>;
            if (table == null)
            {
                return false;
            }

            return table.TryGetValue(id, out record);
        }

        public IReadOnlyList<T> GetAll<T>() where T : class, IConfigRecord
        {
            if (!_tables.TryGetValue(typeof(T), out object tableObj))
            {
                return Array.Empty<T>();
            }

            var table = tableObj as Dictionary<string, T>;
            if (table == null)
            {
                return Array.Empty<T>();
            }

            var list = new List<T>(table.Count);
            foreach (var pair in table)
            {
                list.Add(pair.Value);
            }

            return list;
        }

        public bool HasTable<T>() where T : class, IConfigRecord
        {
            return _tables.ContainsKey(typeof(T));
        }

        public void Clear()
        {
            _tables.Clear();
        }
    }
}
