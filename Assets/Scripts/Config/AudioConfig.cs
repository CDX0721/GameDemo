using System;
using GameDemo.DataConfig;

namespace GameDemo.Config
{
    [Serializable]
    public class AudioConfig : IConfigRecord
    {
        public string id;
        public string clipPath;
        public string channel;
        public float volume = 1f;
        public bool loop;

        public string Id => id;
    }
}
