using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.Game.GameObjects
{
    public static class EventDateMan
    {
        private static ILog _log = LogManager.GetLogger(typeof(EventDateMan));
        private static Dictionary<string, (int startDate, int endDate)> _events = new Dictionary<string, (int startDate, int endDate)>();
        
        // Format: YYYYMMDDHH
        public static int CurrentYYYYMMDDHH { get; private set; }
        public static int CurrentYYYY { get; private set; }

        public static void Init()
        {
            StartDateUpdater();
            RegisterIsActiveCheck();
            ReloadEvents();
        }

        private static void StartDateUpdater()
        {
            MasterThread.RepeatingAction.Start("CurrentSpawnDate updater", () =>
            {
                var date = MasterThread.CurrentDate;
                var t = 0;
                t = 0;
                t += date.Year * 1000000;
                CurrentYYYY = t;
                t += date.Month * 10000;
                t += date.Day * 100;
                t += date.Hour;
                CurrentYYYYMMDDHH = t;

            }, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public static IEnumerable<string> GetEventNames() => _events.Keys;

        private static void RegisterIsActiveCheck()
        {
            // Register IsActive check in LimitedObject
            LimitedObject.CheckIsActive = o =>
            {
                if (string.IsNullOrEmpty(o.LimitedName) && o.StartDate == null && o.EndDate == null)
                {
                    // No data set, so active
                    return true;
                }
                // Active by event name
                if (!string.IsNullOrEmpty(o.LimitedName)) return IsEventActive(o.LimitedName);

                // Active by start/end date
                if ((o.StartDate == null || o.StartDate <= CurrentYYYYMMDDHH) && 
                    (o.EndDate == null || o.EndDate > CurrentYYYYMMDDHH)) return true;

                return false;
            };
        }

        public static void ReloadEvents()
        {
            var path = Server.Instance.GetConfigPath("Server", "EventDate.img");

            _events.Clear();
            if (!File.Exists(path))
            {
                _log.Error($"No eventdate.img found: {path}");
                return;
            }
            
            using var events = new FSFile(path);

            foreach (var prop in events)
            {
                var startDate = prop.GetInt32("startDate");
                var endDate = prop.GetInt32("endDate");
                if (startDate == null || endDate == null)
                {
                    _log.Error($"Missing startDate or endDate on event {prop.Name}");
                    continue;
                }

                _events[prop.Name] = (
                    startDate.Value,
                    endDate.Value
                );
            }

            _log.Info($"Loaded {_events.Count} EventDate events");
        }

        public static (int startDate, int endDate)? GetEventData(string eventName)
        {
            if (!_events.TryGetValue(eventName, out var tuple))
            {
                return null;
            }
            if (tuple.startDate < 1000000) tuple.startDate += CurrentYYYY; // TODO: Check if the tuple value is changed in _events
            if (tuple.endDate < 1000000) tuple.endDate += CurrentYYYY;
            return tuple;
        }

        public static bool IsEventActive(string eventName)
        {
            var tuple = GetEventData(eventName);
            if (tuple == null) return false;
            return CurrentYYYYMMDDHH >= tuple.Value.startDate && tuple.Value.endDate > CurrentYYYYMMDDHH;
        }

        public static bool IsEventDone(string eventName)
        {
            var tuple = GetEventData(eventName);
            if (tuple == null) return false;
            return CurrentYYYYMMDDHH >= tuple.Value.endDate;
        }

        public static (DateTime startDate, DateTime endDate)? GetDateTupleForEvent(string eventName)
        {
            var tuple = GetEventData(eventName);
            if (tuple == null) return null;

            return (tuple.Value.startDate.AsYYYYMMDDHHDateTime(), tuple.Value.endDate.AsYYYYMMDDHHDateTime());
        }
    }
}
