using System;
using System.Linq;
using System.Runtime.InteropServices;
using log4net;
using WvsBeta.Common.Bullshit;

namespace WvsBeta.Common
{
    public abstract class MainFormConsole : IMainForm
    {
        protected static ILog _log = LogManager.GetLogger("MainFormConsole");

        private string text;
        public string Text
        {
            get => text;
            set
            {
                text = value;
                // Console.Title = text;
            }
        }
        
        protected MainFormConsole()
        {
            AddShutdownHook();
            DisableConsoleQuickEdit.Go();
        }

        public void LogAppend(string what)
        {
            what = what.Trim('\r', '\n', '\t', ' ');
            var keys = log4net.ThreadContext.Properties.GetKeys() ?? new string[0] { };
            var copy = keys.Select(x => new Tuple<string, object>(x, log4net.ThreadContext.Properties[x])).ToArray();
            MasterThread.Instance.AddCallback((date) =>
            {
                copy.ForEach(x => log4net.ThreadContext.Properties[x.Item1] = x.Item2);
                Console.WriteLine(what);
                LogToFile(what);
                copy.ForEach(x => log4net.ThreadContext.Properties.Remove(x.Item1));
            }, "LogAppend");
        }

        public void LogAppend(string pFormat, params object[] pParams)
        {
            LogAppend(string.Format(pFormat, pParams));
        }

        public void LogDebug(string pFormat)
        {
#if DEBUG
            LogAppend(pFormat);
#endif
        }

        public void LogDebug(string pFormat, params object[] pParams)
        {
#if DEBUG
            LogAppend(string.Format(pFormat, pParams));
#endif
        }

        public abstract void LogToFile(string what);

        public void AddShutdownHook()
        {

            Console.CancelKeyPress += (sender, args) =>
            {
                Shutdown(args);
            };
        }

        public abstract void ChangeLoad(bool up);

        public abstract void Shutdown();

        public abstract void InitializeServer();

        public abstract void Shutdown(ConsoleCancelEventArgs args);

        public abstract void HandleCommand(string name, string[] args);

        public void ReadInput()
        {
            
            while (true)
            {
                var line = Console.ReadLine();

                // Might use many CPU cycles, but the server could be shutting down. (ctrl+c sets null)
                if (line == null) continue;

                var str = line.Split(' ');

                switch (str[0])
                {
                    case "shutdown": Shutdown(null); break;
                    default: 
                        MasterThread.Instance.AddCallback(_ =>
                        {
                            HandleCommand(str[0], str.Skip(1).ToArray());
                        }, $"Handle command {line}");
                        break;
                }
            }
        }
    }
}
