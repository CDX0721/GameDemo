using System;
using GameDemo.DataConfig;

namespace GameDemo.Config
{
    [Serializable]
    public class AudioSettings : IConfigRecord
    {
        public string id;
        public float masterVolume = 1f;
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public bool mute;

        public string Id => id;
    }
}
