using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDemo.DataConfig
{
    /// <summary>
    /// Unity JsonUtility serializer for { "items": [ ... ] } shaped config files.
    /// </summary>
    public sealed class UnityJsonConfigSerializer : IConfigSerializer
    {
        [Serializable]
        sealed class ConfigListWrapper<T>
        {
            public List<T> items;
            public List<T> Items;
        }

        public bool TryDeserializeList<T>(string json, out List<T> records, out string errorMessage) where T : class, IConfigRecord
        {
            records = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "Input json is null or empty.";
                return false;
            }

            try
            {
                ConfigListWrapper<T> wrapper = JsonUtility.FromJson<ConfigListWrapper<T>>(json);
                if (wrapper == null)
                {
                    errorMessage = "JsonUtility returned null wrapper.";
                    return false;
                }

                records = wrapper.items ?? wrapper.Items;
                if (records == null)
                {
                    errorMessage = "Missing \"items\" array in config json.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
