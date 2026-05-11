namespace GameDemo.DataConfig
{
    /// <summary>
    /// Base contract for all config records with a stable primary key.
    /// </summary>
    public interface IConfigRecord
    {
        string Id { get; }
    }
}
