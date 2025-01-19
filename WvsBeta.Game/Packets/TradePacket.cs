using System.Linq;
using System.Threading.Channels;
using log4net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public static class MiniRoomPacket
    {
        private static ILog miniroomLog = LogManager.GetLogger("MiniroomLog");
        private static ILog miniroomChatLog = LogManager.GetLogger("MiniroomChatLog");


        public static void HandlePacket(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            var opcode = (GameObjects.MiniRooms.MiniRoomBase.Opcodes)packet.ReadByte();

            miniroomLog.Debug($"MiniRoomLog IN {opcode} : {packet}");

            switch (opcode)
            {
                case GameObjects.MiniRooms.MiniRoomBase.Opcodes.MRP_Create:
                    var type = (GameObjects.MiniRooms.MiniRoomBase.E_MINI_ROOM_TYPE)packet.ReadByte();
                    if (!GameObjects.MiniRooms.MiniRoomBase.IsValidType(type))
                    {
                        miniroomLog.Error($"User tried to create miniroom that we do not support {type}");
                        return;
                    }
                    if (type == GameObjects.MiniRooms.MiniRoomBase.E_MINI_ROOM_TYPE.MR_EntrustedShop &&
                        true /*chr.HasOpenedEntrustedShop*/) return;

                    GameObjects.MiniRooms.MiniRoomBase.Create(chr, type, packet, false, 0);
                    break;
                
                case GameObjects.MiniRooms.MiniRoomBase.Opcodes.MRP_InviteResult:
                    var serial = packet.ReadInt();
                    var result = (GameObjects.MiniRooms.MiniRoomBase.InviteResults) packet.ReadByte();
                    GameObjects.MiniRooms.MiniRoomBase.InviteResult(chr, serial, result);
                    break;

                case GameObjects.MiniRooms.MiniRoomBase.Opcodes.MRP_Enter:
                    GameObjects.MiniRooms.MiniRoomBase.Enter(chr, packet.ReadInt(), packet, false);
                    break;
                
                default:
                    chr.RoomV2?.OnPacketBase(opcode, chr, packet);
                    break;
            }
        }
    }
}