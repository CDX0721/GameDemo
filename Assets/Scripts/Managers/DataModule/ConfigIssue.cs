namespace GameDemo.DataConfig
{
    public enum ConfigIssueLevel
    {
        Warning = 0,
        Error = 1
    }

    public readonly struct ConfigIssue
    {
        public ConfigIssueLevel Level { get; }
        public string Code { get; }
        public string Message { get; }

        public ConfigIssue(ConfigIssueLevel level, string code, string message)
        {
            Level = level;
            Code = code;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{Level}] {Code}: {Message}";
        }
    }
}
