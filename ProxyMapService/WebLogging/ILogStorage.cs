namespace ProxyMapService.WebLogging
{
    public interface ILogStorage
    {
        void AddLog(LogMessageEntry log);
        IEnumerable<LogMessageEntry> GetRecentLogs();
        void Clear();
    }
}
