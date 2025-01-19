using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public class DropPool
    {
        private const bool _MergeDrops = false;
        public Map Field { get; set; }
        private static LoopingID _DropIdCounter { get; } = new LoopingID();

        public const long DropExpireTime = 3 * 60 * 1000;
        public bool DropEverlasting { get; set; }
        public Dictionary<int, Drop> Drops { get; private set; } = new Dictionary<int, Drop>();

        public DropPool(Map field)
        {
            Field = field;
        }

        public Drop Create(Reward Reward, int OwnerID, int OwnPartyID, DropOwnType ownOwnType, int SourceID, Pos CurPos, int x2, short Delay, bool Admin, short Pos, bool ByPet)
        {
            var Foothold = Field.GetFootholdUnderneath(x2, CurPos.Y - 100, out var y2Short);
            int y2 = y2Short;

            if (Foothold == null || !Field.IsPointInMBR(x2, y2, true))
                Foothold = Field.GetFootholdClosest(x2, CurPos.Y, ref x2, ref y2, CurPos.X);

            Trace.WriteLine($"Dropping {Reward.ItemID} from {CurPos.X} {CurPos.Y} to {x2} {y2} (distance: {new Pos((short)x2, (short)y2) - CurPos})");

            var drop = new Drop(_DropIdCounter.NextValue(), Reward, OwnerID, OwnPartyID, ownOwnType, SourceID, CurPos.X, CurPos.Y, (short)x2, (short)y2, ByPet)
            {
                Field = Field,
                CreateTime = MasterThread.CurrentTime,
                Pos = Pos,
                Everlasting = DropEverlasting,
                ConsumeOnPickup = (!Reward.Mesos && false/*DataProvider.ConsumeOnPickup.Contains(Reward.ItemID)*/)
            };

            if (!Reward.Mesos)
            {
                QuestLimited ql = null;
                if (DataProvider.Equips.TryGetValue(Reward.ItemID, out var ed))
                    ql = ed.QuestLimited;
                else if (DataProvider.Items.TryGetValue(Reward.ItemID, out var id))
                    ql = id.QuestLimited;

                if (ql != null)
                {
                    drop.QuestID = ql.QuestID;
                    drop.QuestState = ql.QuestState;
                    drop.ShowMax = ql.MaxCount;
                }
            }


            if (!Admin &&
                drop.ByUser &&
                !Reward.Mesos &&
                (DataProvider.QuestItems.Contains(Reward.ItemID) || DataProvider.UntradeableDrops.Contains(Reward.ItemID)))
            {
                DropPacket.SendMakeEnterFieldPacket(drop, DropEnter.FadingOut, Delay);
            }
            else
            {
                Drops.Add(drop.DropID, drop);
                DropPacket.SendMakeEnterFieldPacket(drop, DropEnter.Create, Delay);
                // BMS also sends a regular 'ShowDrop' packet afterwards. 
                // I assume that Wizet waited for the animation (like the reactor logic), but now they do that on the client side.
            }
            return drop;
        }


        #region Update
        public void TryExpire(long tCur, bool removeAll = false)
        {
            if (DropEverlasting) return;

            foreach (var Drop in Drops.Values.Where(Drop => removeAll || Drop.IsTimeToRemove(tCur)).ToArray())
            {
                var reward = Drop.Reward;
                if (!reward.Mesos)
                {
                    if (Constants.isStar(reward.ItemID) || Constants.isEquip(reward.ItemID))
                    {
                        ItemTransfer.DropExpired(
                            Field.ID,
                            reward.ItemID,
                            reward.Amount,
                            reward.GetData(),
                            Drop.GetDropInfo()
                        );
                    }
                }

                RemoveDrop(Drop);
            }
        }
        #endregion

        public void Clear(DropLeave rlt = DropLeave.ByTimeOut)
        {
            foreach (var Drop in new List<Drop>(Drops.Values))
            {
                RemoveDrop(Drop, rlt);
            }
        }

        public void OnEnter(Character User)
        {
            foreach (var Drop in Drops.Values.Where(x => x.IsShownTo(User)))
            {
                DropPacket.SendMakeEnterFieldPacket(Drop, DropEnter.OnTheFoothold, 0, User);
            }
        }

        public void OnLeave(Character User)
        {
            if (!DropEverlasting) return;
            // Return items dropped by user

            var returnedDrops = Drops.Values.Where(x => x.CanTakeDrop(User, true));

            returnedDrops.ForEach(x => TakeDrop(x, User, false));
        }

        public void RemoveDrop(Drop Drop, DropLeave Type = DropLeave.ByTimeOut, int Option = 0)
        {
            if (Drops.Remove(Drop.DropID))
                DropPacket.SendMakeLeaveFieldPacket(Drop, Type, Option);
        }

        public void TakeDrop(Drop drop, Character chr, bool pet)
        {
            var SentDropNotice = false;
            var reward = drop.Reward;
            var dropNoticeItemIdOrMesos = reward.Drop;
            var pickupAmount = reward.Amount;
            
            if (!chr.CanAttachAdditionalProcess)
            {
                DropPacket.CannotLoot(chr, -1);
                return;
            }

            if (reward.Mesos)
            {
                // Party meso distribution
                if (drop.SourceID != 0 &&
                    chr.PartyID != 0 &&
                    drop.OwnPartyID == chr.PartyID)
                {
                    var PartyData = chr.Field.GetInParty(chr.PartyID);
                    var Count = PartyData.Count();

                    if (Count > 1)
                    {
                        SentDropNotice = true;
                        var Base = reward.Drop * 0.8 / Count + 0.5;
                        Base = Math.Floor(Base);
                        if (Base <= 0.0) Base = 0.0;

                        var Bonus = Convert.ToInt32(reward.Drop - Count * Base);
                        if (Bonus < 0) Bonus = 0;

                        reward.Drop = Convert.ToInt32(Base);

                        foreach (var BonusUser in PartyData)
                        {
                            int mesosGiven = reward.Drop;
                            if (chr.ID == BonusUser.ID)
                            {
                                mesosGiven += Bonus;
                            }
                            // Now figure out what we really gave the user
                            mesosGiven = BonusUser.AddMesos(mesosGiven);

                            Common.Tracking.MesosTransfer.PlayerLootMesos(
                                drop.SourceID, 
                                chr.ID, 
                                mesosGiven,
                                "Party " + chr.PartyID + ", " + chr.MapID + ", " + drop.GetHashCode(),
                                drop.GetDropInfo()
                            );

                            CharacterStatsPacket.SendGainDrop(BonusUser, true, mesosGiven, 0);
                        }
                    }
                }

                if (!SentDropNotice)
                {
                    dropNoticeItemIdOrMesos = chr.AddMesos(reward.Drop);
                    Common.Tracking.MesosTransfer.PlayerLootMesos(
                        drop.SourceID,
                        chr.ID,
                        dropNoticeItemIdOrMesos,
                        chr.MapID + ", " + drop.GetHashCode(),
                        drop.GetDropInfo()
                    );
                }
            }
            else
            {
                var rewardItemID = reward.ItemID;
                var rewardItem = reward.GetData();

                //This check is used for the Maple Island tutorial quest on Monster Book.
                if (rewardItemID == 4031144)
                {
                    if (chr.Quests.GetQuestData(500) == "s1")
                    {
                        chr.MonsterBook.TryAddCard(2380000);
                        chr.Quests.SetQuestData(500, "s2");
                    }

                    chr.Field.DropPool.RemoveDrop(drop, DropLeave.PickedUpByUser, chr.ID);

                    // return early to not show the looted message
                    return;
                }

                //This check is used for the "Lost in the Ocean" questline
                if (rewardItemID == 4031209)
                {
                    if (chr.Quests.GetQuestData(1007400) == "")
                        chr.Quests.SetQuestData(1007400, "s");
                }

                //This check is used for the "Destroying the Power of Evil" quest
                if (rewardItemID == 4031196)
                {
                    if (chr.Quests.GetQuestData(1006900) == "")
                        chr.Quests.SetQuestData(1006900, "s");
                }

                if (Constants.isMonsterBook(rewardItemID))
                {
                    chr.MonsterBook.TryAddCard(rewardItemID);
                    chr.Field.DropPool.RemoveDrop(drop, DropLeave.PickedUpByUser, chr.ID);

                    // return early to not show the looted message
                    return;
                }

                if (Constants.isInternetCafeMouse(rewardItemID) && chr.Field.ParentFieldSet != null)
                {
                    chr.Field.ParentFieldSet.Characters.ForEach(character =>
                    {
                        var currentPoints = character.Quests.GetQuestData(1001300, "0");

                        var amount = 10 * reward.Amount;

                        character.Quests.SetQuestData(1001300, (int.Parse(currentPoints) + amount).ToString());
                        MessagePacket.SendScrMessage(character, $"You have gained Internet Cafe Points (+{amount})", 0x7);
                    });

                    chr.Field.DropPool.RemoveDrop(drop, DropLeave.PickedUpByUser, chr.ID);

                    // return early to not show the looted message
                    return;

                    // internet cafe mice can be looted outside of icafe
                }

                if (DataProvider.IsOnlyItem(rewardItemID) && chr.Inventory.HasItem(rewardItemID))
                {
                    Trace.WriteLine($"Already got item {rewardItemID} in inventory.");
                    DropPacket.CannotLoot(chr, -2);
                    return;
                }

                if (DataProvider.IsPickupBlocked(rewardItemID))
                {
                    Trace.WriteLine($"Item {rewardItemID} cannot be picked up.");
                    DropPacket.CannotLoot(chr, -1);
                    return;
                }
                
                if (!chr.Inventory.HasSlotsFreeForItem(rewardItemID, reward.Amount))
                {
                    DropPacket.CannotLoot(chr, -1);
                    return;
                }

                if (Constants.isStar(rewardItemID))
                {
                    chr.Inventory.DistributeItemInInventory(rewardItem);
                    ItemTransfer.ItemPickedUp(
                        chr.ID,
                        chr.MapID, 
                        rewardItemID, 
                        reward.Amount,
                        chr.MapID + ", " + drop.GetHashCode() + ", " + drop.OwnerID, 
                        rewardItem,
                        drop.GetDropInfo()
                    );
                }
                else
                {
                    // Non-star rewards

                    if (chr.Inventory.DistributeItemInInventory(rewardItem) == reward.Amount)
                    {
                        DropPacket.CannotLoot(chr, -1);
                        return;
                    }

                    if (Constants.isEquip(reward.ItemID))
                    {
                        ItemTransfer.ItemPickedUp(
                            chr.ID, 
                            chr.MapID,
                            rewardItemID,
                            reward.Amount, 
                            chr.MapID + ", " + drop.GetHashCode() + ", " + drop.OwnerID,
                            rewardItem,
                            drop.GetDropInfo()
                        );
                        if (!drop.ByUser && rewardItem is EquipItem ed)
                        {
                            var equipQuality = ed.Quality;
                            Trace.WriteLine($"Got item with quality {equipQuality}");
                            if (equipQuality > 1)
                            {
                                Server.Instance.PlayerLogDiscordReporter.Enqueue($"{chr} has found an above-avg {ed.Template.Name} at {chr.Field.FullName}.\r\n```{ed.GetStatDescription()}```");
                            }
                        }
                    }
                }
            }

            if (!SentDropNotice)
            {
                CharacterStatsPacket.SendGainDrop(chr, reward.Mesos, dropNoticeItemIdOrMesos, pickupAmount);
            }

            chr.Field.DropPool.RemoveDrop(drop, pet ? DropLeave.PickedUpByPet : DropLeave.PickedUpByUser, chr.ID);
        }
        

        public IEnumerable<Drop> FindDropInRect(Rectangle rc, TimeSpan timeAfter)
        {
            var timeAfterTime = MasterThread.CurrentTime - timeAfter.TotalMilliseconds;
            return Drops.Values.Where(x => x.CreateTime <= timeAfterTime && rc.Contains(x.Pt2));
        }

        public void EncodeForMigration(Packet pw)
        {
            pw.WriteInt(_DropIdCounter.Current);
            pw.WriteInt(Drops.Count);
            Drops.ForEach(x => x.Value.EncodeForMigration(pw));
        }

        public void DecodeForMigration(Packet pr)
        {
            _DropIdCounter.Reset(pr.ReadInt());
            int amount = pr.ReadInt();
            Drops = new Dictionary<int, Drop>(amount);

            Program.MainForm.LogAppend(Field.ID + " has " + amount + " drops...");
            for (var i = 0; i < amount; i++)
            {
                var drop = Drop.DecodeForMigration(pr);
                drop.Field = Field;
                Drops.Add(drop.DropID, drop);
            }
        }
    }

    public enum DropOwnType : byte
    {
        UserOwn = 0,
        PartyOwn = 1,
        NoOwn = 2,
        Explosive_NoOwn = 3
    }

    public enum DropEnter : byte
    {
        JustShowing = 0,
        Create = 1,
        OnTheFoothold = 2,
        FadingOut = 3
    }

    public enum DropLeave : byte
    {
        ByTimeOut = 0,
        ByScreenScroll = 1,
        PickedUpByUser = 2,
        PickedUpByMob = 3,
        Explode = 4,
        PickedUpByPet = 5
    }
}
