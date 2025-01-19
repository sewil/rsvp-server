using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using log4net;

namespace WvsBeta.Common.Sessions
{
    public abstract class AbstractConnection : Session
    {
        private static ILog _cfgHackLog = LogManager.GetLogger("CfgHackLog");

        public struct MemoryEdit
        {
            public string address { get; set; }
            public string aob { get; set; }
        }

        public struct OtherHacks
        {
            public bool speedhack { get; set; }
        }

        private static ILog log = LogManager.GetLogger("AbstractConnection");

        public bool gotPong = true;
        public int pings { get; set; }


        public long pingSentDateTime { get; private set; }
        public int PingMS { get; private set; }

        public const bool MEMORY_CRC_ENABLED = false;

        public bool UseMemoryCRC { get; protected set; }
        private bool _ignoredWrongCRC = false;

        private bool IsConnectedAsClient { get; }

        protected AbstractConnection(System.Net.Sockets.Socket pSocket)
            : base(pSocket, "")
        {
            pings = 0;
            IsConnectedAsClient = false;
        }

        protected AbstractConnection(string pIP, ushort pPort)
            : base(pIP, pPort, "")
        {
            pings = 0;
            IsConnectedAsClient = true;
        }

        public virtual void StartLogging()
        {
            log4net.ThreadContext.Properties["RemoteEndpoint"] = IP + ":" + Port;
        }

        public virtual void EndLogging()
        {
            log4net.ThreadContext.Properties.Remove("RemoteEndpoint");
        }

        private int _memoryOffset = 0;

        public static ConcurrentDictionary<Tuple<uint, uint, uint>, uint> IvCrcMapping = new ConcurrentDictionary<Tuple<uint, uint, uint>, uint>();

        private bool ValidateCRC(uint input)
        {
            var calculatedCRC = CRC32.CalcCrc32(previousDecryptIV, 2);

            if (UseMemoryCRC)
            {
                foreach (var region in MemoryRegions.Instance.Regions)
                {
                    calculatedCRC = CRC32.CalcCrc32(
                        region.Data,
                        region.Length,
                        calculatedCRC ^ (region.Address + (uint)_memoryOffset),
                        _memoryOffset
                    );
                }
            }

            var isValid = calculatedCRC == input;

            if (!isValid)
            {
                if (_ignoredWrongCRC)
                {
                    log.Error($"Disconnecting client because CRC didnt match _again_");
                }
                else
                {
                    log.Warn($"Accepting wrong CRC for _once_...");
                    _ignoredWrongCRC = true;
                    isValid = true;
                }
            }

            return isValid;
        }

        private static Random rnd = new Random();
        protected void SendMemoryRegions()
        {
            if (IsConnectedAsClient || !MEMORY_CRC_ENABLED) return;

            var packet = new Packet(ServerMessages.SECURITY_SOMETHING);
            packet.WriteByte(0);

            var regions = MemoryRegions.Instance.Regions;

            _memoryOffset = rnd.Next(0, MemoryRegions.Instance.MaxRandomMemoryOffset);

            packet.WriteShort((short)regions.Count);
            foreach (var region in regions)
            {
                packet.WriteUInt(region.Address + (uint)_memoryOffset);
                packet.WriteInt(region.Length - _memoryOffset);
            }

            SendPacket(packet);


            UseMemoryCRC = true;
        }


        public RedisBackend.HackKind? HackDetected { get; protected set; } = null;

        public virtual void OnHackDetected(List<MemoryEdit> memEdits) { }

        protected void TryRegisterHackDetection(int userId)
        {
            if (!HackDetected.HasValue) return;

            // Okay, register.
            RedisBackend.Instance.RegisterNonGameHackDetection(userId, HackDetected.Value);
            log.Warn($"Registered hack by userid {userId}: {HackDetected}. Waiting for him to go to a channel.");
        }


        private bool dcEnqueued = false;
        public void ScheduleDisconnect()
        {
            if (dcEnqueued) return;

            dcEnqueued = true;
            MasterThread.RepeatingAction.Start("DC " + IP + ":" + Port, Disconnect, 5000, 0);
        }

        public override void OnPacketInbound(Packet pPacket)
        {
            if (pPacket.Length == 0)
                return;
            StartLogging();
            try
            {
                byte header = pPacket.ReadByte();

                if (IsConnectedAsClient)
                {
                    if (header == (byte)ServerMessages.PING)
                    {
                        SendPong();
                    }
                }
                else
                {
                    if (header == (byte)ClientMessages.PONG)
                    {
                        gotPong = true;
                        PingMS = (int)(pPacket.PacketCreationTime - pingSentDateTime);
                    }
                    else if (header == (byte)ClientMessages.__CUSTOM_DC_ME__)
                    {
                        ScheduleDisconnect();
                        return;
                    }
                    else if (header == (byte)ClientMessages.CFG)
                    {
                        if (pPacket.ReadByte<CfgClientMessages>() == CfgClientMessages.CFG_MEMORY_EDIT_DETECTED)
                        {
                            var speedHack = pPacket.ReadBool();
                            if (speedHack)
                            {
                                _cfgHackLog.Info(new OtherHacks
                                {
                                    speedhack = true
                                });
                            }

                            var amount = pPacket.ReadShort();
                            var memEdits = new List<MemoryEdit>();
                            for (var i = 0; i < amount; i++)
                            {
                                var addr = pPacket.ReadInt();
                                var byteLen = pPacket.ReadByte();
                                var bytes = string.Join(" ", pPacket.ReadBytes(byteLen).Select(x => x.ToString("X2")));

                                memEdits.Add(new MemoryEdit
                                {
                                    address = addr.ToString("X08"),
                                    aob = bytes
                                });
                            }

                            memEdits.ForEach(x => _cfgHackLog.Info(x));

                            var hk = HackDetected ?? 0;
                            if (speedHack) hk |= RedisBackend.HackKind.Speedhack;
                            if (amount > 0) hk |= RedisBackend.HackKind.MemoryEdits;
                            HackDetected = hk;

                            OnHackDetected(memEdits);
                        }
                    }
                    else if (MEMORY_CRC_ENABLED)
                    {
                        // Check for expected CRC packet
                        if ((BitConverter.ToUInt16(previousDecryptIV, 0) % 31) == 0)
                        {
                            bool disconnect = true;
                            if (header == (byte)ClientMessages.CLIENT_HASH)
                            {
                                var mode = pPacket.ReadByte();
                                if (mode == 1)
                                {
                                    var clientCRC = pPacket.ReadUInt();
                                    if (ValidateCRC(clientCRC))
                                    {
                                        disconnect = false;
                                    }
                                    else
                                    {
                                        log.Error($"Disconnecting client because CRC didnt match {clientCRC}");
                                    }
                                }
                                else
                                {
                                    log.Error($"Disconnecting client because unexpected mode: {mode}");
                                }
                            }
                            else
                            {
                                log.Error(
                                    $"Disconnecting client because expected CLIENT_HASH packet, but got {header} instead");
                            }

                            if (disconnect)
                            {
                                Disconnect();
                                return;
                            }
                        }
                    }
                }

                pPacket.Reset(0);

                AC_OnPacketInbound(pPacket);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                EndLogging();
            }
        }

        public abstract void AC_OnPacketInbound(Packet pPacket);

        public override void SendPacket(Packet pPacket)
        {
            while (IsConnectedAsClient && MEMORY_CRC_ENABLED && (BitConverter.ToUInt16(_encryptIV, 0) % 31) == 0)
            {
                var p = new Packet((byte)ClientMessages.CLIENT_HASH);
                p.WriteByte(1);
                p.WriteUInt(CRC32.CalcCrc32(_encryptIV, 2)); // TODO: Get CRC for memory
                base.SendPacket(p);
            }

            base.SendPacket(pPacket);
        }


        private static Packet _pingPacket = new Packet(ServerMessages.PING);
        private static Packet _pongPacket = new Packet((byte)ClientMessages.PONG);

        public void SendPing()
        {
            pingSentDateTime = MasterThread.CurrentTime;
            SendPacket(_pingPacket);
        }

        public void SendPong()
        {
            SendPacket(_pongPacket);
        }
    }
}