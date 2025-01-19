using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;

namespace WvsBeta.Game
{
    public class MysticDoor
    {
        public readonly int OwnerId;
        public int OwnerPartyId { get; set; }
        public readonly short X;
        public readonly short Y;
        public readonly int TownID;
        public readonly int FieldID;
        public long EndTime { get; set; }

        public MysticDoor(int ownerId, int ownPtId, short x, short y, int fieldId, int townID, long tEnd)
        {
            OwnerId = ownerId;
            OwnerPartyId = ownPtId;
            X = x;
            Y = y;
            FieldID = fieldId;
            TownID = townID;
            EndTime = tEnd;
        }

        public bool CanEnterDoor(Character chr)
        {
            if (chr.PartyID > 0 && OwnerPartyId == chr.PartyID) return true;

            return chr.ID == OwnerId;
        }

        public void Encode(Packet packet)
        {
            packet.WriteInt(TownID);
            packet.WriteInt(FieldID);
            packet.WriteShort(X);
            packet.WriteShort(Y);
        }
        

        public static readonly MysticDoor DefaultNoDoor = new MysticDoor(0, 0, 0, 0, Constants.InvalidMap, Constants.InvalidMap, long.MaxValue);

        public override string ToString()
        {
            return $"Door from {OwnerId}, party {OwnerPartyId}, town {TownID}, field {FieldID}, at {X}, {Y}";
        }
    }

    public class DoorManager
    {
        private static ILog _log = LogManager.GetLogger(typeof(DoorManager));

        public readonly Dictionary<int, MysticDoor> DoorsLeadingHere;
        public readonly Dictionary<int, MysticDoor> Doors;
        private readonly Map Field;

        public DoorManager(Map field)
        {
            Doors = new Dictionary<int, MysticDoor>();
            DoorsLeadingHere = new Dictionary<int, MysticDoor>();
            Field = field;
        }

        public static void TryRemoveDoor(Character chr)
        {
            if (chr.DoorMapId == Constants.InvalidMap) return;
            MapProvider.Maps[chr.DoorMapId].DoorPool.TryRemoveDoor(chr.ID);

            chr.DoorMapId = Constants.InvalidMap;
        }

        public static bool TryGetDoor(Character chr, out MysticDoor door)
        {
            if (chr.DoorMapId != Constants.InvalidMap)
            {
                return MapProvider.Maps[chr.DoorMapId].DoorPool.Doors.TryGetValue(chr.ID, out door);
            }

            door = null;
            return false;
        }

        public static void EncodeDoorInfo(Character chr, Packet pw)
        {
            if (!TryGetDoor(chr, out var door))
                door = MysticDoor.DefaultNoDoor;
            
            door.Encode(pw);
        }

        public void TryRemoveDoor(int ownerCharId)
        {
            if (!Doors.TryGetValue(ownerCharId, out var door)) return;
            
            _log.Info($"Removing door {door}");

            var owner = Server.Instance.GetCharacter(ownerCharId);
            if (owner == null)
            {
                _log.Error("Trying to remove door of a disconnected owner! This shouldnt happen!");
            }

            if (owner != null)
                owner.DoorMapId = Constants.InvalidMap;

            Doors.Remove(ownerCharId);

            Field.SendPacket(MapPacket.RemoveDoor(door, 0));

            MapProvider.Maps[Field.ReturnMap].DoorPool.DoorsLeadingHere.Remove(ownerCharId);
            

            if (owner != null)
            {
                MapPacket.SetTownPortalDataOwner(owner, null);
            }

            if (door.OwnerPartyId != 0)
            {
                Server.Instance.CenterConnection.PartyDoorChanged(ownerCharId, MysticDoor.DefaultNoDoor);
            }
        }

        public void CreateDoor(Character chr, short x, short y, long endTime)
        {
            chr.DoorMapId = Field.ID;
            var townID = Field.ReturnMap;

            var door = new MysticDoor(chr.ID, chr.PartyID, x, y, Field.ID, townID, endTime);
            
            _log.Info($"Created door {door}");

            Doors.Add(chr.ID, door);
            Field.SendPacket(MapPacket.ShowDoor(door, 0));

            MapProvider.Maps[townID].DoorPool.DoorsLeadingHere.Add(chr.ID, door);

            //Owner is never in town when spawning door out of town, so no need to send portal spawn packet til he enters town

            if (chr != null)
            {
                MapPacket.SetTownPortalDataOwner(chr, door);
            }

            if (chr.PartyID != 0)
            {
                Server.Instance.CenterConnection.PartyDoorChanged(chr.ID, door);
            }
        }

        public void ShowAllDoorsTo(Character fucker)
        {
            foreach (var d in Doors.Values)
            {
                fucker.SendPacket(MapPacket.ShowDoor(d, 1));
            }

            if (DoorsLeadingHere.TryGetValue(fucker.ID, out var ownDoor))
            {
                MapPacket.SetTownPortalDataOwner(fucker, ownDoor);
            }
        }

        public void Update(long pNow)
        {
            foreach (var door in Doors.Values.Where(x => x.EndTime < pNow).ToList())
            { 
                TryRemoveDoor(door.OwnerId);
            }
        }
        
    }
}
