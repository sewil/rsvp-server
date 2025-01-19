using System;

using System.IO;
using System.Xml;
using log4net;

namespace WvsBeta.Common
{
    public class Logfile
    {
        public string Filename { get; private set; }
        private StreamWriter _writer = null;

        const string EXTENSION = "txt";

        private ILog log;

        public Logfile(string pLogname, bool pAddDate = true, string pFolder = "Logs")
        {
            log = LogManager.GetLogger(pFolder + "-" + pLogname);

            if (pFolder == "Logs")
            {
                pFolder += Path.DirectorySeparatorChar + pLogname;
            }
            Directory.CreateDirectory(pFolder);

            if (pAddDate)
            {
                Filename = string.Format("{0} - {1:yyyy-MM-dd HHmmssfff}.{2}", pLogname, DateTime.Now, EXTENSION);
            }
            else
            {
                Filename = string.Format("{0}.{1}", pLogname, EXTENSION);
            }
            Filename = pFolder + Path.DirectorySeparatorChar + Filename;
            _writer = new StreamWriter(File.Open(Filename, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }

        private bool lastWasNewline = true;

        public void Write(string pFormat, params object[] pParams)
        {
            string txt = "";
            if (lastWasNewline)
            {
                txt = string.Format("[{0:yyyy-MM-dd HH:mm:ss:fff}] {1}", DateTime.Now, string.Format(pFormat, pParams));
            }
            else
            {
                txt = string.Format(pFormat, pParams);
            }
            log.Debug(txt);

            try
            {
                _writer.Write(txt);

            }
            catch { }
            //File.AppendAllText(Filename, txt);
            lastWasNewline = false;
        }

        public void WriteLine(string pFormat = null, params object[] pParams)
        {
            if (pFormat == null) // Creates a newline
            {
                WriteLine(Environment.NewLine);
                return;
            }
            
            WriteLine(string.Format(pFormat, pParams));
        }
        
        public void WriteLine(string formatted)
        {
            string txt;
            if (lastWasNewline)
            {
                txt = string.Format("[{0:yyyy-MM-dd HH:mm:ss:fff}] {1}", DateTime.Now, formatted);
            }
            else
            {
                txt = formatted;
            }
            txt += Environment.NewLine;
            log.Debug(formatted.Trim('\r', '\n', '\t', ' '));
            try
            {
                _writer.Write(txt);
            }
            catch { }
            //File.AppendAllText(Filename, txt + Environment.NewLine);
            lastWasNewline = true;
        }
    }
}
