using Object = UnityEngine.Object;

namespace GameDemo.DataConfig
{
    /// <summary>
    /// Bridge adapter to the existing AssetModule.
    /// </summary>
    public sealed class AssetManagerLoader : IAssetLoader
    {
        public T Load<T>(string path) where T : Object
        {
            return AssetManager.Instance.Load<T>(path);
        }
    }
}
