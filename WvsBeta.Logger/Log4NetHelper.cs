using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;

namespace WvsBeta.Logger
{
    public static class Log4NetHelper
    {
        public static void Init(string configFilename)
        {
            var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr", configFilename));

            if (!fi.Exists)
            {
                Console.WriteLine("[Warning] Unable to find logging configuration file at {0}!", fi.FullName);
            }

            if (Environment.GetEnvironmentVariable("LOG4NET_DEBUG") == "1")
            {
                Console.WriteLine("Enabling Log4Net internal debugging");
                log4net.Util.LogLog.InternalDebugging = true;
            }

            XmlConfigurator.ConfigureAndWatch(
                LogManager.GetRepository(Assembly.GetEntryAssembly()),
                fi
            );

            LogManager.GetLogger("Log4NetHelper").Info("Logger initialized from start.");
        }
    }
}
