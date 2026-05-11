using Object = UnityEngine.Object;

namespace GameDemo.DataConfig
{
    public interface IAssetLoader
    {
        T Load<T>(string path) where T : Object;
    }
}
