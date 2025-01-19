using System;
using System.Collections.Generic;
using log4net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Common
{
    public class Pinger
    {
        public static int CurrentLoggingConnections => _connections.Count;

        private static ILog _log = LogManager.GetLogger("Pinger");
        private static readonly List<AbstractConnection> _connections = new List<AbstractConnection>();
        public const int PingCheckTimeSeconds = 15;
        public const int PingCheckTime = PingCheckTimeSeconds * 1000;
        public const int MaxLostPings = 3;

        private static long _lastPingTime = 0;

        private static readonly object lockobj = 1;

        public static void Add(AbstractConnection conn)
        {
            _log.Debug("Adding connection " + conn.IP + ":" + conn.Port);
            lock (lockobj)
            {
                _connections.Add(conn);
            }
        }

        public static void Remove(AbstractConnection conn)
        {
            _log.Debug("Removing connection " + conn.IP + ":" + conn.Port);
            lock (lockobj)
            {
                _connections.Remove(conn);
            }
        }

        public static void Init(Action<string> pingcallback = null, Action<string> dcCallback = null)
        {
            new MasterThread.RepeatingAction(
                "Pinger",
                time =>
                {
                    if (_lastPingTime != 0 &&
                        (time - _lastPingTime) < PingCheckTime)
                    {
                        _log.Debug($"Ignoring ping (too much!): {(time - _lastPingTime)}");
                        return;
                    }
                    _lastPingTime = time;
                    AbstractConnection[] d;

                    lock (lockobj)
                    {
                        d = _connections.ToArray();
                    }

                    foreach (var session in d)
                    {
                        if (session.gotPong)
                        {
                            session.gotPong = false;
                            session.pings = 0;
                        }

                        if (session.pings >= MaxLostPings)
                        {
                            dcCallback?.Invoke("Pinger Disconnected! Too many retries, killing connection. " + session.IP + ":" + session.Port + " " + MasterThread.CurrentDate);
                            
                            if (session.Disconnect())
                            {
                                // Killed
                                dcCallback?.Invoke("Session is now disconnected. " + session.IP + ":" +
                                                   session.Port + " " + MasterThread.CurrentDate);
                            }
                            else
                            {
                                dcCallback?.Invoke("Connection was already dead?! Getting rid of it. " +
                                                   session.IP + ":" + session.Port + " " +
                                                   MasterThread.CurrentDate);

                                Remove(session);
                            }

                            continue;
                        }

                        session.SendPing();
                        session.pings++;
                    }
                }, PingCheckTime, PingCheckTime).Start();
        }
    }
}
