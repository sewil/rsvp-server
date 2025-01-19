namespace WvsBeta.Center
{
    public interface IFormMain
    {
        void UpdateServerList();
        void LogAppend(string what, params object[] args);
        void LogAppend(string what);
        void LogDebug(string what);
    }
}
