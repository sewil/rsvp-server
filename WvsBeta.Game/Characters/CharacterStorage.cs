using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using MySqlConnector;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider;

namespace WvsBeta.Game
{
    public class CharacterStorage
    {
        private static ILog _log = LogManager.GetLogger(typeof(CharacterStorage));

        public Character Character { get; set; }
        private BaseItem[][] _items { get; set; }

        public byte MaxSlots { get; set; }
        public byte TotalSlotsUsed => (byte)(_items != null ? _items.SelectMany(x => x.Where(y => y != null)).Count() : 0);
        public int Mesos { get; set; }


        public CharacterStorage(Character chr)
        {
            Character = chr;
        }

        public void Load()
        {
            var initStorage = false;
            using (var data = (MySqlDataReader)Server.Instance.CharacterDatabase.RunQuery(
                    "SELECT slots, mesos FROM storage WHERE userid = @userid AND world_id = @worldid",
                    "@userid", Character.UserID,
                    "@worldid", Server.Instance.WorldID
            ))
            {

                if (data.Read() == false)
                {
                    MaxSlots = 4;
                    Mesos = 0;
                    initStorage = true;
                }
                else
                {
                    MaxSlots = (byte)data.GetInt16(0);
                    Mesos = data.GetInt32(1);
                }
            }

            if (initStorage)
            {
                Server.Instance.CharacterDatabase.RunQuery(
                    "INSERT INTO storage (userid, world_id) VALUES (@userid, @worldid)",
                    "@userid", Character.UserID,
                    "@worldid", Server.Instance.WorldID
                );
            }

            _items = new BaseItem[5][];
            SetSlots(MaxSlots);

            SplitDBInventory.Load(
                Server.Instance.CharacterDatabase,
                "storage",
                $"userid = {Character.UserID} AND world_id = {Server.Instance.WorldID}",
                (type, inventory, slot, item) =>
                {
                    if (!AddItem(item))
                    {
                        _log.Error(new UnableToLoadError {
                            item = item,
                            message = "Unable to load data into user info!!!"
                        });
                    }
                }
           );
        }

        public class UnableToLoadError
        {
            public string message { get; set; }
            public BaseItem item { get; set; }

            public override string ToString() => message;
        }

        public void Save()
        {
            var userId = Character.UserID;
            var worldId = Server.Instance.WorldID;

            Server.Instance.CharacterDatabase.RunQuery(
                "UPDATE storage SET slots = @slots, mesos = @mesos " +
                "WHERE userid = @userId AND world_id = @worldId",
                "@userId", userId,
                "@worldId", worldId,
                "@mesos", Mesos,
                "@slots", MaxSlots);

            short slot = 0;
            for (var i = 0; i < _items.Length; i++)
            {
                for (var j = 0; j < MaxSlots; j++)
                {
                    var x = _items[i][j];
                    if (x != null) x.InventorySlot = slot++;
                }
            }

            SplitDBInventory.Save(
                Server.Instance.CharacterDatabase,
                "storage",
                $"{userId}, {worldId},",
                $"userid = {userId} AND world_id = {worldId}",
                (type, inventory) =>
                {
                    if (inventory == 5) return new List<BaseItem>();
                    return GetInventoryItems(inventory);
                },
                Program.MainForm.LogAppend
            );

        }

        public bool AddItem(BaseItem item)
        {
            var inv = Constants.getInventory(item.ItemID);
            var items = _items[inv - 1];
            // Find first empty slot
            for (var i = 0; i < MaxSlots; i++)
            {
                if (items[i] != null) continue;
                items[i] = item;
                return true;
            }

            // No slot found
            return false;
        }

        public IEnumerable<BaseItem> GetInventoryItems(byte inv)
        {
            return _items[inv - 1].Where(x => x != null && Constants.getInventory(x.ItemID) == inv);
        }

        public void TakeItemOut(byte inv, byte slot)
        {
            var items = _items[inv - 1];
            var tmp = new BaseItem[MaxSlots];
            var tmpOffset = 0;

            for (var i = 0; i < MaxSlots; i++)
            {
                if (i == slot)
                {
                    continue;
                }

                tmp[tmpOffset++] = items[i];
            }

            _items[inv - 1] = tmp;
        }

        public BaseItem GetItem(byte inv, byte slot)
        {
            if (slot >= MaxSlots) return null;
            return _items[inv - 1][slot];
        }

        public bool SetSlots(byte amount)
        {
            if (amount < 4) amount = 4;
            else if (amount > 100) amount = 100;

            MaxSlots = amount;

            for (var i = 0; i < 5; i++)
            {
                if (_items[i] == null)
                    _items[i] = new BaseItem[MaxSlots];
                else
                    Array.Resize(ref _items[i], MaxSlots);
            }

            return true;
        }

        public bool SlotsAvailable()
        {
            return MaxSlots - TotalSlotsUsed > 0;
        }


        public void ChangeMesos(int value)
        {
            var newMesos = 0;
            if (value < 0)
            { //if value is less than zero 
                if ((Mesos - value) < 0) newMesos = 0;
                else newMesos = Mesos - value; // neg - neg = pos
            }
            else
            {
                if ((long)(Mesos + value) > int.MaxValue) newMesos = int.MaxValue;
                else newMesos = Mesos - value; //this was the little fucker that fucked everything up
            }
            Mesos = newMesos;

            StoragePacket.SendChangedMesos(Character);
        }

    }
}
