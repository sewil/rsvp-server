namespace WvsBeta.Common
{
    public interface IMainForm
    {
        void LogAppend(string pFormat);
        void LogAppend(string pFormat, params object[] pParams);
        void LogDebug(string pFormat);
        void LogDebug(string pFormat, params object[] pParams);
        void LogToFile(string what);
        void ChangeLoad(bool up);
        void Shutdown();
    }
}
