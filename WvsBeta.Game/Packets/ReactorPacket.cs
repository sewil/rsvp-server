using System;
using System.Diagnostics;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public static class ReactorPacket
    {
        public static void SpawnReactor(Reactor reactor, Character chr = null, int? animationTime = null)
        {
            Trace.WriteLineIf(Server.Instance.Initialized, $"Spawning reactor {reactor} at {reactor.X} {reactor.Y}, animationTime {animationTime}");
            var packet = new Packet(ServerMessages.REACTOR_ENTER_FIELD);
            packet.WriteShort(reactor.ID);
            packet.WriteByte(reactor.State);
            packet.WriteShort(reactor.X);
            packet.WriteShort(reactor.Y);
            if (animationTime.HasValue)
            {
                packet.WriteBool(true);
                var timeLeft = (int)Math.Ceiling(animationTime.Value / 100.0);
                packet.WriteByte((byte)timeLeft);
            }
            else
            {
                packet.WriteBool(false);
            }

            if (chr != null)
                chr.SendPacket(packet);
            else
                reactor.Field.SendPacket(packet);
        }

        public static void ReactorChangedState(Reactor reactor, short delay, byte properEventIdx, int animationTime)
        {
            var packet = new Packet(ServerMessages.REACTOR_CHANGE_STATE);
            packet.WriteShort(reactor.ID);
            packet.WriteByte(reactor.State);
            packet.WriteShort(reactor.X);
            packet.WriteShort(reactor.Y);
            packet.WriteShort(delay);
            packet.WriteByte(properEventIdx);

            var timeLeft = (int)Math.Ceiling(animationTime / 100.0);

            Trace.WriteLineIf(Server.Instance.Initialized, $"ChangedState reactor {reactor} to state {reactor.State} with {timeLeft} timeLeft (from {animationTime})");

            packet.WriteByte((byte)timeLeft);
            reactor.Field.SendPacket(packet);
        }

        public static void RemoveReactor(Reactor reactor)
        {
            Trace.WriteLineIf(Server.Instance.Initialized, $"Removing reactor {reactor}");

            var packet = new Packet(ServerMessages.REACTOR_LEAVE_FIELD);
            packet.WriteShort(reactor.ID);
            reactor.Field.SendPacket(packet);
        }

        public static void HandleReactorHit(Character chr, Packet packet)
        {
            var rid = packet.ReadByte();
            var option = packet.ReadUInt();
            var delay = packet.ReadShort();

            chr.Field.PlayerHitReactor(chr, rid, delay, option);
        }
    }
}
