using System;
using System.Linq;
using System.Reflection.Metadata;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;

namespace WvsBeta.Game
{
    public static class DropPacket
    {
        public static void HandleDropMesos(Character chr, int amount)
        {
            chr.ExclRequestSet = true;

            //30 E8 03 00 00 
            if (chr.AssertForHack(amount < 10, "Trying to drop less than 10 mesos") ||
                chr.AssertForHack(amount > 50000, "Trying to drop more than 50k mesos") ||
                chr.AssertForHack(amount > chr.Inventory.Mesos, "Trying to drop more mesos than he's got") ||
                chr.AssertForHack(chr.IsInMiniRoom, "Trying to drop mesos while in a 'room'"))
            {
                return;
            }

            if (chr.IsGM && !chr.IsAdmin)
            {
                MessagePacket.SendNotice(chr, "You cannot drop mesos.");
                return;
            }

            chr.AddMesos(-amount);
            MesosTransfer.PlayerDropMesos(chr.ID, amount, chr.MapID.ToString());

            chr.Field.DropPool.Create(
                Reward.Create(amount),
                chr.ID,
                0,
                DropOwnType.UserOwn,
                0,
                new Pos(chr.Position),
                chr.Position.X,
                0,
                false,
                0,
                false
            );
        }

        public static void HandlePickupDrop(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            if (!chr.CanAttachAdditionalProcessSilent)
            {
                // Cannot loot while in chat or something
                return;
            }

            // 5F 18 FF 12 01 00 00 00 00 
            packet.Skip(4); // pos?

            var dropid = packet.ReadInt();
            if (!chr.Field.DropPool.Drops.TryGetValue(dropid, out var drop) ||
                !drop.CanTakeDrop(chr))
            {
                return;
            }

            var dropLootRange = drop.Pt2 - chr.Position;

            chr.AssertForHack(dropLootRange > 700, "Possible drop VAC! Distance: " + dropLootRange, dropLootRange > 250);

            chr.Field.DropPool.TakeDrop(drop, chr, false);
        }

        public static void SendMakeEnterFieldPacket(Drop drop, DropEnter enter, short Delay, Character chr = null)
        {
            var pw = new Packet(ServerMessages.DROP_ENTER_FIELD);
            pw.WriteByte(enter);
            pw.WriteInt(drop.DropID);
            pw.WriteBool(drop.Reward.Mesos);
            pw.WriteInt(drop.Reward.Drop);
            pw.WriteInt(drop.OwnType == DropOwnType.PartyOwn ? drop.OwnPartyID : drop.OwnerID);
            pw.WriteByte(drop.OwnType);
            pw.WriteShort(drop.Pt2.X);
            pw.WriteShort(drop.Pt2.Y);

            if (enter == DropEnter.JustShowing ||
                enter == DropEnter.Create ||
                enter == DropEnter.FadingOut)
            {
                pw.WriteInt(drop.SourceID);
                pw.WriteShort(drop.Pt1.X);
                pw.WriteShort(drop.Pt1.Y);
                pw.WriteShort(Delay);
            }

            if (!drop.Reward.Mesos)
                pw.WriteLong(drop.Reward.DateExpire);

            pw.WriteBool(drop.ByPet);

            if (enter != DropEnter.FadingOut && !drop.Reward.Mesos && !drop.Field.HideRewardInfo)
            {
                var rewardData = drop.Reward.GetData();
                if ((rewardData is EquipItem ei && !ei.Template.HideRewardInfo) ||
                    (rewardData is BundleItem bi && !bi.Template.HideRewardInfo) ||
                    rewardData is PetItem)
                {
                    pw.WriteBool(true);
                    // Encode the item data in the drop info
                    rewardData.Encode(pw);
                }
                else
                {
                    pw.WriteBool(false);
                }
            }

            if (chr != null)
                chr.SendPacket(pw);
            else
            {
                // We can see items that are disappearing in drop anim, but not those that would stay on the ground.
                if (enter == DropEnter.FadingOut)
                    drop.Field.SendPacket(pw);
                else
                    drop.Field.SendPacket(drop, pw);
            }
        }

        public static void SendMakeLeaveFieldPacket(Drop Drop, DropLeave leave, int Option = 0)
        {
            var pw = new Packet(ServerMessages.DROP_LEAVE_FIELD);
            pw.WriteByte(leave);
            pw.WriteInt(Drop.DropID);

            if (leave == DropLeave.PickedUpByUser ||
                leave == DropLeave.PickedUpByMob ||
                leave == DropLeave.PickedUpByPet)
                pw.WriteInt(Option);
            else if (leave == DropLeave.Explode)
                pw.WriteShort((short)Option);

            // Do not add drop argument, as you might cleared a Quest item limitation,
            // and that would prevent drops from despawning.
            Drop.Field.SendPacket(pw);
        }

        public static void CannotLoot(Character chr, sbyte reason)
        {
            chr.ExclRequestSet = false;
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(0);
            pw.WriteSByte(reason);
            chr.SendPacket(pw);
        }
    }
}