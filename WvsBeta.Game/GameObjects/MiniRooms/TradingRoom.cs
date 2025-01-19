using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;

namespace WvsBeta.Game.GameObjects.MiniRooms
{
    public class TradingRoom : MiniRoomBase
    {
        public class TradeItem
        {
            public BaseItem OriginalItem { get; set; }
        }

        public bool[] Locked;
        private TradeItem[][] ItemList;

        private int[] Mesos;


        public TradingRoom() : base(2)
        {
            ItemList = new TradeItem[2][];
            ItemList[0] = new TradeItem[10];
            ItemList[1] = new TradeItem[10];
            Locked = new bool[2] {false, false};
            Mesos = new int[2] {0, 0};

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    ItemList[i][j] = null;
                }
            }
        }

        public override E_MINI_ROOM_TYPE GetTypeNumber() => E_MINI_ROOM_TYPE.MR_TradingRoom;

        public override void SetBalloon(bool open)
        {
            // No balloon
        }

        public override void OnPacket(Opcodes type, Character chr, Packet packet)
        {
            switch (type)
            {
                case Opcodes.TRP_PutItem:
                    OnPutItem(chr, packet);
                    break;
                case Opcodes.TRP_PutMoney:
                    OnPutMoney(chr, packet);
                    break;
                case Opcodes.TRP_Trade:
                    OnTrade(chr, packet);
                    break;
            }
        }

        public override ErrorMessage IsAdmitted(Character chr, Packet packet, bool onCreate)
        {
            var ret = base.IsAdmitted(chr, packet, onCreate);
            if (ret != 0) return ret;

            // if (chr.IsGM) return ErrorMessage.UnableToDoIt;

            if (onCreate) return 0;

            var owner = FindUserSlot(0);

            if (owner.MapID != chr.MapID)
            {
                DoCloseRequest(null, LeaveReason.TRLeave_FieldError, LeaveReason.MRLeave_UserRequest);

                // BMS would call OnPacketBase to invoke this...
                ProcessLeaveRequest();

                return ErrorMessage.Trade2OnSameMap;
            }


            return 0;
        }

        public void OnPutItem(Character chr, Packet packet)
        {
            // Put Item
            if (CurUsers != 2)
            {
                // You can't put items while the second char isn't there yet
                return;
            }

            var charslot = FindUserSlot(chr);
            if (charslot < 0) return;


            var inventory = packet.ReadByte();
            var slot = packet.ReadShort();
            var amount = packet.ReadShort();
            packet.ReadByte(); // never trust client data, this used to be toslot

            var toslot = (byte) 255;
            var il = ItemList[charslot];
            // NOTE: Skip first element!
            for (var i = 1; i < il.Length; i++)
            {
                if (il[i] == null)
                {
                    toslot = (byte) i;
                    break;
                }
            }

            if (toslot == 255)
            {
                _log.Error("User tried to add item to trade, but no slots left.");
                return;
            }

            var demItem = chr.Inventory.GetItem(inventory, slot);

            if (demItem == null)
            {
                _log.Error($"User tried to put item in trade that doesn't exist... Inventory {inventory} slot {slot}");
                return;
            }

            if (Constants.isRechargeable(demItem.ItemID))
            {
                if (chr.AssertForHack(amount != 1, $"Expected 1 rechargable, got {amount}"))
                {
                    return;
                }

                amount = demItem.Amount;
            }

            var tehItem = chr.Inventory.TakeItemAmountFromSlot(inventory, slot, amount);
            if (tehItem == null)
            {
                _log.Error("Unable to retrieve item stack from inventory");
                return;
            }

            il[toslot] ??= new TradeItem
            {
                OriginalItem = tehItem
            };

            var pTradeItem = il[toslot].OriginalItem;

            ItemTransfer.PlayerTradePutUp(chr.ID, demItem.ItemID, slot, tehItem.Amount, TransactionID, demItem);

            ForEachCharacter((idx, chr) =>
            {
                var pw = new Packet(ServerMessages.MINI_ROOM_BASE);
                pw.WriteByte((byte) Opcodes.TRP_PutItem);
                pw.WriteBool(idx != charslot);
                pw.WriteByte(toslot);
                pw.WriteByte((byte) Constants.getInventory(pTradeItem.ItemID));
                PacketHelper.AddItemData(pw, pTradeItem, 0, false);
                chr.SendPacket(pw);
            });
        }

        public void OnPutMoney(Character chr, Packet packet)
        {
            if (CurUsers != 2)
            {
                // You can't put money while the second char isn't there yet
                return;
            }

            var charslot = FindUserSlot(chr);
            if (charslot < 0) return;

            var amount = packet.ReadInt();

            if (amount < 0 || chr.Inventory.Mesos < amount)
            {
                // HAX
                _log.Error("Player tried putting an incorrect meso amount in trade. Amount: " + amount);
                return;
            }

            chr.AddMesos(-amount);
            MesosTransfer.PlayerTradePutUp(chr.ID, amount, TransactionID);
            Mesos[charslot] += amount;

            var newMesoAmount = Mesos[charslot];

            ForEachCharacter((idx, chr) =>
            {
                var pw = new Packet(ServerMessages.MINI_ROOM_BASE);
                pw.WriteByte((byte) Opcodes.TRP_PutMoney);
                pw.WriteBool(idx != charslot);
                pw.WriteInt(newMesoAmount);
                chr.SendPacket(pw);
            });
        }

        public void OnTrade(Character chr, Packet packet)
        {
            if (CurUsers != 2)
            {
                // You can't put money while the second char isn't there yet
                return;
            }

            var charslot = FindUserSlot(chr);
            if (charslot < 0) return;
            Locked[charslot] = true;

            // Check if trade is accepted

            var otherChar = FindUserSlot(1 - charslot);

            var pw = new Packet(ServerMessages.MINI_ROOM_BASE);
            pw.WriteByte((byte) Opcodes.TRP_Trade);
            otherChar.SendPacket(pw);

            if (!Locked[0] || !Locked[1]) return;

            var chr1 = FindUserSlot(0);
            var chr2 = FindUserSlot(1);

            var result = LeaveReason.TRLeave_TradeFail;

            var (ex1, ex2) = PrepareExchanges(chr1, chr2);

            if (ex1.Check() && ex2.Check())
            {
                chr1.WrappedLogging(() =>
                {
                    if (!ex1.Perform())
                    {
                        _log.Error("Unable to perform ex1 exchange!");
                    }
                });
                chr2.WrappedLogging(() =>
                {
                    if (!ex2.Perform())
                    {
                        _log.Error("Unable to perform ex2 exchange!");
                    }
                });


                chr1.WrappedLogging(() =>
                {
                    chr1.Save();
                });

                chr2.WrappedLogging(() =>
                {
                    chr2.Save();
                });
                result = LeaveReason.TRLeave_TradeDone;
            }

            DoCloseRequest(null, result, 0);
        }

        private void RevertItems()
        {
            for (var i = 0; i < 2; i++)
            {
                var chr = FindUserSlot(i);

                if (chr == null)
                {
                    continue;
                }

                var ii = i;
                // Make sure we log for the right player
                chr.WrappedLogging(() =>
                {
                    ItemList[ii].Where(x => x?.OriginalItem != null).ForEach(ti =>
                    {
                        var amountNotGiven = chr.Inventory.DistributeItemInInventory(ti.OriginalItem);
                        var failed = false;
                        if (amountNotGiven != 0)
                        {
                            Server.Instance.ServerTraceDiscordReporter.Enqueue($"Unable to give back item in trade!!! Check transaction {TransactionID}, unable to give {amountNotGiven} of {ti.OriginalItem.ItemID}");
                            failed = true;
                        }

                        ItemTransfer.PlayerTradeReverted(
                            chr.ID,
                            ti.OriginalItem.ItemID,
                            ti.OriginalItem.Amount,
                            TransactionID,
                            ti.OriginalItem,
                            failed
                        );
                        ti.OriginalItem = null;
                    });
                });
            }
        }

        private (Exchange ex1, Exchange ex2) PrepareExchanges(Character chr1, Character chr2)
        {
            // We don't take items here, because thats already 'done'.

            var ex1 = new Exchange(chr1, chr2, false, TransactionID);
            var ex2 = new Exchange(chr2, chr1, false, TransactionID);

            for (var charslot = 0; charslot < ItemList.Length; charslot++)
            {
                var il = ItemList[charslot];
                var exTo = charslot == 0 ? ex2 : ex1;

                foreach (var item in il)
                {
                    if (item == null) continue;
                    var oi = item.OriginalItem;
                    
                    if (!Constants.isStackable(oi.ItemID))
                        exTo.GiveItem(oi);
                    else
                        exTo.GiveItem(oi.ItemID, oi.Amount);
                }
            }

            ex1.GiveMoney(Mesos[1]);
            ex2.GiveMoney(Mesos[0]);

            return (ex1, ex2);
        }

        public override void OnLeave(Character chr, LeaveReason leaveType)
        {
            if (leaveType != LeaveReason.TRLeave_TradeDone)
            {
                RevertItems();

                var charslot = FindUserSlot(chr);

                var mesos = Mesos[charslot];
                if (mesos != 0)
                {
                    chr.AddMesos(mesos);
                    MesosTransfer.PlayerTradeReverted(chr.ID, mesos, TransactionID);
                    Mesos[charslot] = 0;
                }
            }

            base.OnLeave(chr, leaveType);
        }
    }
}