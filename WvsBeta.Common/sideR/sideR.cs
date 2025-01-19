using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using log4net;

namespace WvsBeta.Common.sideR
{
    /// <summary>
    /// Quick and Dirty Redis client
    /// - Supports < 2.6.0 Redis version (so no millisecond precision)
    /// </summary>
    public class sideR
    {
        public static ILog _log = LogManager.GetLogger(typeof(sideR));

        public string Hostname { get; private set; }
        public int Port { get; private set; }
        private string Password { get; }

        public bool Trace { get; set; }

        private const string RedisNewline = "\r\n";

        private Socket _socket;
        private NetworkStream _ns;
        private StreamReader _sr;
        private StreamWriter _sw;

        public sideR(string hostname, int port, string password)
        {
            Hostname = hostname;
            Port = port;
            Password = password;

            MasterThread.RepeatingAction.Start("Redis Pinger", PING, 0, 60 * 1000);
        }

        private bool connecting = false;

        private void Reconnect()
        {
            connecting = true;
            if (_socket != null)
            {
                try { _socket.Shutdown(SocketShutdown.Both); } catch { }
                try { _socket.Disconnect(false); } catch { }
                try { _socket.Close(); } catch { }
            }

            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socket.ReceiveTimeout = 100;
            _socket.SendTimeout = 100;
            _socket.NoDelay = true;

            _log.Info($"Connecting to redis @ {Hostname}:{Port}...");

            _socket.Connect(Hostname, Port);

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 5000);

            // Setup

            _ns = new NetworkStream(_socket);

            _sw = new StreamWriter(_ns, Encoding.ASCII);
            _sw.NewLine = "\r\n";
            _sw.AutoFlush = true;

            _sr = new StreamReader(_ns, Encoding.ASCII, false);

            // Try to connect

            var authResponse = Write("AUTH", Password);
            if (!authResponse.Contains("no password is set") && !authResponse.Contains("OK"))
            {
                _log.Error($"Unable to auth: {authResponse}");
                throw new Exception($"Unable to authenticate to Redis, error {authResponse}");
            }

            if (!SET("TEST_KEY", "test1234"))
            {
                throw new Exception("Unable to set test key!");
            }
            else
            {
                var tk = GET("TEST_KEY");
                if (tk != "test1234")
                    throw new Exception($"Unable to validate test key! {tk}");
            }
            
            _log.Info("Connected!");
            connecting = false;
        }

        private bool HasDataLeft() => _ns.DataAvailable;

        private string ReadLine()
        {
            var line = _sr.ReadLine();
            if (Trace)
            {
                _log.Debug("IN: " + line);
            }
            
            return line;
        }

        private void EnsureConnection()
        {
            while (_socket == null || !_socket.Connected || connecting)
            {
                _log.Warn("Trying to connect to Redis server...");
                try
                {
                    Reconnect();
                }
                catch (Exception se)
                {
                    _log.Error("Unable to connect", se);
                }
            }
        }
        private void EnforceReconnect()
        {
            while (true)
            {
                _log.Warn("Trying to connect to Redis server...");
                try
                {
                    Reconnect();
                    break;
                }
                catch (Exception se)
                {
                    _log.Error("Unable to connect", se);
                }
            }
        }

        private string Write(params string[] elements)
        {
            ReSend:
            if (!connecting) EnsureConnection();

            try
            {
                // Flush data
                while (HasDataLeft()) _sr.Read();

                if (Trace)
                {
                    if (elements[0] == "AUTH")
                    {
                        _log.Debug("OUT: AUTH (redacted)");
                    }
                    else
                    {
                        _log.Debug("OUT: " + string.Join(" ", elements));
                    }
                }

                _sw.Write("*" + elements.Length + RedisNewline);
                foreach (var line in elements)
                {
                    _sw.Write("$" + line.Length + RedisNewline + line + RedisNewline);
                }

                return ReadLine();
            }
            catch (Exception ex)
            {
                _log.Error("Error while sending to redis.", ex);
                if (connecting) return "FAIL";
                _log.Warn("Enforcing reconnection...");
                EnforceReconnect();
                goto ReSend;
            }
        }

        private string ReadBulkString(string response)
        {
            var bulkStringLength = int.Parse(response.Substring(1));
            // No Data
            if (bulkStringLength < 0)
                return null;


            var buf = new Span<char>(new char[bulkStringLength]);
           
            if (_sr.Read(buf) != buf.Length)
            {
                throw new Exception($"Unable to read {bulkStringLength} bytes");
            }

            if (_sr.Read() != '\r') throw new Exception("Did not receive \\r after bulkstring");
            if (_sr.Read() != '\n') throw new Exception("Did not receive \\n after bulkstring");

            return new string(buf);
        }


        public string GET(string key)
        {
            var retried = false;

            Retry:

            var firstLine = Write("GET", key);
            if (firstLine[0] != '$')
            {
                if (!retried && !connecting)
                {
                    _log.Error($"Unexpected response for GET {key} request: {firstLine}. Reconnecting and retrying...");
                    EnforceReconnect();
                    retried = true;
                    goto Retry;
                }
            
                _log.Error($"Unexpected response for GET {key} request: {firstLine}. Erroring out...");
                throw new Exception($"Unexpected response for GET {key} request: {firstLine}");

            }

            try
            {
                var result = ReadBulkString(firstLine);

                if (retried) _log.Info($"Recovered from error in GET {key}.");

                return result;
            }
            catch (Exception ex)
            {
                if (!retried && !connecting)
                {
                    _log.Error("Exception while getting BULK response. Reconnecting and retrying...", ex);
                    EnforceReconnect();
                    retried = true;
                    goto Retry;
                }
                
                _log.Error("Exception while getting BULK response. Passing to caller...", ex);
                throw;
            }
        }

        public bool SET(
            string key,
            string value,
            TimeSpan? expireTime = null)
        {
            var retried = false;

            Retry:
            string firstLine;
            if (expireTime != null)
                firstLine = Write("SETEX", key, ((long) expireTime.Value.TotalSeconds).ToString(), value);
            else
                firstLine = Write("SET", key, value);

            if (firstLine == "+OK")
            {
                if (retried) _log.Info($"Recovered from error in SET {key}.");

                return true;
            }

            if (!retried && !connecting)
            {
                _log.Error($"Unable to set Redis key {key} with value {value}: {firstLine}. Reconnecting and retrying...");
                EnforceReconnect();
                retried = true;
                goto Retry;
            }
            
            _log.Error($"Unable to set Redis key {key} with value {value}: {firstLine}. Erroring out...");
            

            return false;
        }

        public bool EXISTS(string key, out bool lookupFailed)
        {
            lookupFailed = false;
            var retried = false;

            Retry:
            var firstLine = Write("EXISTS", key);

            if (firstLine == ":1")
            {
                if (retried) _log.Info($"Recovered from error in EXISTS {key}.");
                return true;
            }

            if (firstLine == ":0")
            {
                if (retried) _log.Info($"Recovered from error in EXISTS {key}.");
                return false;
            }

            
            if (!retried && !connecting)
            {
                _log.Error($"Unable to check if key {key} exists: {firstLine}. Reconnecting and retrying...");
                EnforceReconnect();
                retried = true;
                goto Retry;
            }
            
            _log.Error($"Unable to check if key {key} exists: {firstLine}. Erroring out...");
            lookupFailed = true;

            return false;
        }

        public bool DEL(string key)
        {
            var retried = false;
            Retry:

            var firstLine = Write("DEL", key);

            if (firstLine == ":1")
            {
                if (retried) _log.Info($"Recovered from error in DEL {key}.");
                return true;
            }

            if (firstLine == ":0")
            {
                if (retried) _log.Info($"Recovered from error in DEL {key}.");
                return false;
            }

            
            if (!retried && !connecting)
            {
                _log.Error($"Unable to delete key {key}: {firstLine}. Reconnecting and retrying...");
                EnforceReconnect();
                retried = true;
                goto Retry;
            }
            
            _log.Error($"Unable to delete key {key}: {firstLine}. Erroring out...");

            return false;
        }

        public TimeSpan? TTL(string key)
        {
            var line = Write("TTL", key);

            if (line == ":-2") return null;
            if (line == ":-1") return TimeSpan.Zero;
            // This might throw an exception.
            return TimeSpan.FromSeconds(int.Parse(line.Substring(1)));
        }

        public bool PING()
        {
            var retried = false;
            Retry:
            const string pong = "+PONG";
            var response = Write("PING");
            if (response != pong)
            {
                if (!retried && !connecting)
                {
                    _log.Error($"PING failed with {response}. Reconnecting and retrying...");
                    EnforceReconnect();
                    retried = true;
                    goto Retry;
                }

                
                _log.Error($"PING failed with {response}. Returning false...");
                return false;
            }

            if (retried) _log.Info("Recovered from error in PING.");

            return true;
        }
    }
}