namespace GameDemo.DataConfig
{
    public interface IConfigTextProvider
    {
        bool TryGetText(string resourcePath, out string text, out string errorMessage);
    }
}
