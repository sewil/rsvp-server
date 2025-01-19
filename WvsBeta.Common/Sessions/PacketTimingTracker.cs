using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace WvsBeta.Common.Sessions
{
    public class PacketTimingTracker<T>
    {
        private static ILog _log = LogManager.GetLogger("PacketTimingTracker<" + typeof(T).Name + ">");

        private readonly Dictionary<byte, PacketTimingInfo> _times = new Dictionary<byte, PacketTimingInfo>();

        private double _measurementStartTime;


        class PacketTimingInfo
        {
            public byte packetOpcode;
            public string packetName;
            public double totalTime;
            public double timeAvg;
            public double timeMin;
            public double timeMax;
            public long samples;

            public void PrepareForLog()
            {
                timeAvg = totalTime / (double)samples;

                // Round the numbers so they are easier to read
                timeMin = Math.Round(timeMin, 3);
                timeMax = Math.Round(timeMax, 3);
                timeAvg = Math.Round(timeAvg, 3);
                totalTime = Math.Round(totalTime, 3);
            }

            public PacketTimingInfo(Type enumType, byte opcode)
            {
                packetName = (enumType.Name + ".") + (Enum.GetName(enumType, opcode) ?? "UNKNOWN");
                packetOpcode = opcode;
                Reset();
            }

            public void Reset()
            {
                timeMin = double.MaxValue;
                timeMax = double.MinValue;
                samples = 0;
                totalTime = 0;
            }

            public void Add(double time)
            {
                timeMax = Math.Max(timeMax, time);
                timeMin = Math.Min(timeMin, time);
                totalTime += time;
                samples++;
            }
        }
        
        public void Flush()
        {
            foreach (var pti in _times.Values.Where(x => x.samples > 0))
            {
                pti.PrepareForLog();
                // Not using pti here directly, as it would then get modified (we need a copy). This is a tuple
                _log.Info(new
                {
                    pti.packetName,
                    pti.packetOpcode,
                    pti.samples,
                    pti.timeMin,
                    pti.timeMax,
                    pti.timeAvg,
                    pti.totalTime,
                });
                pti.Reset();
            }
        }

        public void StartMeasurement() => _measurementStartTime = MasterThread.CurrentTimeMicrosecond;

        public void EndMeasurement(byte opcode) => Register(opcode, (MasterThread.CurrentTimeMicrosecond - _measurementStartTime) / 1.0e3);

        private void Register(byte opcode, double deltaMillisecond)
        {
            PacketTimingInfo pti;
            if (!_times.TryGetValue(opcode, out pti))
            {
                pti = _times[opcode] = new PacketTimingInfo(typeof(T), opcode);
            }

            pti.Add(deltaMillisecond);
        }
    }
}