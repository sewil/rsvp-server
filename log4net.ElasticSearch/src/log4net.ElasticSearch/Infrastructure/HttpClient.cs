using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using log4net.ElasticSearch.Models;
using Uri = System.Uri;

namespace log4net.ElasticSearch.Infrastructure
{
    public class HttpClient
    {
        const string ContentType = "application/json";
        const string Method = "POST";

        public static HttpWebRequest RequestFor(Uri uri)
        {
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(uri);

            httpWebRequest.ContentType = ContentType;
            httpWebRequest.Method = Method;

            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                httpWebRequest.Headers.Remove(HttpRequestHeader.Authorization);
                httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(uri.UserInfo)));
            }

            return httpWebRequest;
        }

        private Thread _thread;
        private ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private AutoResetEvent _hasDataEvent = new AutoResetEvent(false);
        private bool _stop = false;

        public void StartThread(Uri uri)
        {
            StopThread();

            _stop = false;
            _thread = new Thread(() => RunThread(uri));
            _thread.Name = "ElasticSearchAppender flusher";
            _thread.Start();
        }

        public void StopThread()
        {
            while (_thread?.IsAlive ?? false)
            {
                _hasDataEvent.Set();
                _stop = true;
            }
        }

        public void AddEntries(IEnumerable<logEvent> items)
        {
            items.Select(x => x.ToJson()).Do(_queue.Enqueue);
            _hasDataEvent.Set();
        }

        private void RunThread(Uri uri)
        {
            var failedEntries = new List<string>();

            while (!_stop)
            {
                failedEntries.Do(_queue.Enqueue);
                failedEntries.Clear();

                if (_queue.IsEmpty) _hasDataEvent.WaitOne();
                if (_queue.IsEmpty) continue;

                try
                {
                    FlushAgain:

                    // TODO: Figure out a way to implement new System.Net.Http.HttpClient
                    // Reason is open connections...

                    var httpWebRequest = RequestFor(uri);
                    using (var requestStream = httpWebRequest.GetRequestStream())
                    using (var streamWriter = new StreamWriter(requestStream))
                    {
                        // For each logEvent, we build a bulk API request which consists of one line for
                        // the action, one line for the document. In this case "index" (idempotent) and then the doc
                        // Since we're appending _bulk to the end of the Uri, ES will default to using the
                        // index and type already specified in the Uri segments
                        // We are limiting request size because it would otherwise throw "Stream was too long" exceptions...
                        // Cannot use requestStream.Length because is not 'supported'
                        var requestStreamLength = 0;
                        while (requestStreamLength < 0x20000 && _queue.TryDequeue(out var line))
                        {
                            streamWriter.Write("{\"index\":{}}\n");
                            streamWriter.Write(line + "\n");

                            failedEntries.Add(line);
                            requestStreamLength += line.Length;
                        }

                        streamWriter.Flush();
                    }

                    try
                    {
                        using var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                        using var stream = new StreamReader(httpResponse.GetResponseStream());
                        var text = stream.ReadToEnd();

                        if (httpResponse.StatusCode != HttpStatusCode.Created &&
                            httpResponse.StatusCode != HttpStatusCode.OK)
                        {
                            Console.WriteLine($"Failed to post to {uri}.");
                        }
                        else if (text.Contains("error\":"))
                        {
                            Console.WriteLine("Error from ElasticSearch:");
                            Console.WriteLine("{0}", text);
                        }
                        else
                        {
                            failedEntries.Clear();

                            // Keep flushing when everything is alright
                            if (!_queue.IsEmpty) goto FlushAgain;
                        }
                    }
                    catch (WebException ex)
                    {
                        var errorResponse = (HttpWebResponse)ex.Response;
                        if (errorResponse == null)
                        {
                            // Like a Host is unknown error
                            Console.WriteLine($"Unable to send data: {ex}");
                        }
                        else
                        {
                            using var stream = new StreamReader(errorResponse.GetResponseStream());
                            var text = stream.ReadToEnd();

                            if (errorResponse.StatusCode != HttpStatusCode.Created &&
                                errorResponse.StatusCode != HttpStatusCode.OK)
                            {
                                Console.WriteLine($"Failed to post to {uri}.");
                            }
                            else if (text.Contains("error\":"))
                            {
                                Console.WriteLine("Error from ElasticSearch:");
                                Console.WriteLine("{0}", text);
                            }
                            else
                            {
                                Console.WriteLine($"Failed to post to {uri}. {ex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to post to {uri}. {ex}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to post to {uri}. {ex}");
                }
            }
        }
    }
}