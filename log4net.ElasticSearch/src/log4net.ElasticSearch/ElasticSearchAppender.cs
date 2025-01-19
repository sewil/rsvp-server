using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using log4net.Appender;
using log4net.Core;
using log4net.ElasticSearch.Infrastructure;
using log4net.ElasticSearch.Models;
using HttpClient = log4net.ElasticSearch.Infrastructure.HttpClient;
using Uri = log4net.ElasticSearch.Models.Uri;

namespace log4net.ElasticSearch
{
    public class ElasticSearchAppender : BufferingAppenderSkeleton
    {
        static readonly string AppenderType = nameof(ElasticSearchAppender);

        private Infrastructure.HttpClient _httpClient;

        public ElasticSearchAppender() : base(false)
        {
        }

        public string ConnectionString { get; set; }

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            ServicePointManager.Expect100Continue = false;

            _httpClient = new HttpClient();

            _httpClient.StartThread(Uri.For(ConnectionString));
        }

        protected override void SendBuffer(LoggingEvent[] events)
        {
            _httpClient.AddEntries(logEvent.CreateMany(events));
        }

        protected override void OnClose()
        {
            Console.WriteLine("Stopping ESA");
            base.OnClose();

            _httpClient.StopThread();
        }
    }
}