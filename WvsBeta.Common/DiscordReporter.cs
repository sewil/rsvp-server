using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using log4net;
using Newtonsoft.Json;

namespace WvsBeta.Common
{
    public class DiscordReporter
    {
        private static ILog _log = LogManager.GetLogger(typeof(DiscordReporter));

        public string WebhookURL { get; private set; }
        public static string Username { get; set; }
        public static bool Disabled { get; set; }
        private readonly ConcurrentQueue<string> _messagesToPost = new ConcurrentQueue<string>();
        private Thread _thread = null;

#warning Building WITHOUT Discord support
        public const string BanLogURL = "";
        public const string ServerTraceURL = "";
        public const string MuteBanURL = "";
        public const string ReportsURL = "";
        public const string PlayerLogURL = "";

        private string ActualUsername
        {
            get
            {
#if DEBUG
                return Username + "-DEBUG";
#else
                return Username;
#endif
            }
        }

        public string Name { get; set; }

        public DiscordReporter(string webhookUrl, string name)
        {
            WebhookURL = webhookUrl;
            Name = name;
            Start();
        }

        public void Enqueue(string message)
        {
            _log.Info(message);
            _messagesToPost.Enqueue(message);
        }

        private struct WebhookMessage
        {
            public string content;
            public string username;
        }

        private void Start()
        {
            if (Disabled) return;
            if (_thread != null) return;

            _thread = new Thread(TryToSend)
            {
                IsBackground = true,
                Name = "DiscordReporter: " + Name,
            };
            _thread.Start();
        }

        private void TryToSend()
        {
            while (true)
            {
                while (_messagesToPost.TryDequeue(out var content))
                {
                    if (WebhookURL == "") continue;
                    var wc = new WebClient();
                    wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                    wc.Proxy = null;
                    try
                    {
                        wc.UploadString(WebhookURL, JsonConvert.SerializeObject(new WebhookMessage
                        {
                            content = content,
                            username = ActualUsername
                        }));
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Unable to send message to Discord. {content}", ex);

                        // Some error occurred, try to squash all the messages
                        var msgCount = 1;
                        var totalStr = content;
                        while (totalStr.Length < 2000 && msgCount < 5 && _messagesToPost.TryDequeue(out content))
                        {
                            if (totalStr.Length + content.Length > 2000)
                            {
                                // Ignore this message, for now
                                _messagesToPost.Enqueue(content);
                                break;
                            }
                            totalStr += "\r\n" + content;
                            msgCount++;
                        }

                        _messagesToPost.Enqueue(totalStr);

                        try
                        {
                            Thread.Sleep(int.Parse(wc.ResponseHeaders["Retry-After"]));
                            continue;
                        }
                        catch (Exception ex2)
                        {
                            _log.Error("Unable to wait for discord Retry-After.", ex2);
                        }

                        // Just wait some more
                        Thread.Sleep(5000);
                        break;
                    }

                    Thread.Sleep(200);
                }

                Thread.Sleep(1000);
            }
        }
    }
}