using System;
using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public class Reward
    {
        public bool Mesos;
        public int Drop;
        private BaseItem Data;

        public long DateExpire
        {
            get
            {
                long Result = 0;
                if (Data != null) Result = Data.Expiration;
                return (Result == 0) ? BaseItem.NoItemExpiration : Result;
            }
        }

        public short Amount => Data?.Amount ?? -1;
        public int ItemID => (!Mesos) ? Drop : 0;
        public BaseItem GetData() => Data;

        // Server drop rate
        public static double ms_fIncDropRate => Server.Instance.RateDropChance;
        // Server 'event time' droprate (between 1pm and 6pm)
        public static double ms_fIncDropRate_WSE => 1.0;
        // Used for MC drops, map the MCType prop of the item to some table calculated in CField_MonsterCarnival::GetMCRewardRate
        public static double MonsterCarnivalRewardRate => 1.0;

        public override string ToString()
        {
            if (Mesos) return $"reward of {Drop} mesos";
            return $"reward of itemid {ItemID}, {Amount}x";
        }


        public static IEnumerable<Reward> ShuffleSort(IEnumerable<Reward> input)
        {
            var a = input.ToArray();
            a.Shuffle();

            var questAndOnlys = a.Where(x =>
            {
                if (x.Mesos) return false;

                if (Constants.isEquip(x.ItemID) &&
                    DataProvider.Equips.TryGetValue(x.ItemID, out var ed))
                    return ed.Quest || ed.QuestLimited != null || ed.Only;

                if (DataProvider.Items.TryGetValue(x.ItemID, out var id))
                    return id.IsQuest || id.QuestLimited != null || id.Only;

                return false;
            }).ToArray();

            var offset = 0;

            // Drop first half
            for (; offset < questAndOnlys.Length / 2; offset++)
            {
                yield return questAndOnlys[offset];
            }

            foreach (var reward in a)
            {
                if (questAndOnlys.Contains(reward)) continue;
                yield return reward;
            }

            // Drop second half
            for (; offset < questAndOnlys.Length; offset++)
            {
                yield return questAndOnlys[offset];
            }
        }

        public static IEnumerable<Reward> GetRewards(Character Owner, Map Field, int ID, bool PremiumMap, bool IncludingGlobalRewards = true)
        {
            if (!DataProvider.Drops.TryGetValue(ID, out var MobRewards)) yield break;
            if (!DataProvider.Mobs.TryGetValue(ID, out var mobTemplate)) yield break;

            var Rewards = MobRewards;
            if (IncludingGlobalRewards)
            {
                var globalRewards = DataProvider.GlobalDrops.Where(x => mobTemplate.Level >= x.MobMinLevel && mobTemplate.Level <= x.MobMaxLevel);

                Rewards = Rewards.Concat(globalRewards).ToArray();
            }

            foreach (var reward in GetRewards(Owner, Field, PremiumMap, Rewards))
            {
                yield return reward;
            }
        }

        public static IEnumerable<Reward> GetRewards(Character Owner, Map Field, bool PremiumMap, IEnumerable<DropData> Rewards)
        {
            double HourDropRateIncrease = 1.0;
            var curDate = MasterThread.CurrentDate;
            if (curDate.Hour >= 13 && curDate.Hour < 19)
            {
                HourDropRateIncrease = ms_fIncDropRate_WSE;
            }

            double dRegionalIncRate = Field.m_dIncRate_Drop;
            double dwOwnerDropRate = Owner?.m_dIncDropRate ?? 1.0;
            double dwOwnerDropRate_Ticket = Owner?.m_dIncDropRate_Ticket ?? 1.0;

            // We only multiply it once
            double? creditsDropRate = null;
            double? creditsMesoRate = null;

            var shuffledRewards = Rewards.ToArray();
            shuffledRewards.Shuffle();

            foreach (var Drop in shuffledRewards)
            {
                // Check if drop is supposed to drop here
                if (Drop.Premium && !PremiumMap)
                    continue;

                // Check if drop should drop *now*
                if (!Drop.IsActive())
                    continue;

                // Don't care about items that are 'expired'
                if (curDate > Drop.DateExpire) continue;


                creditsDropRate ??= Owner?.RateCredits.GetDropRate();
                creditsDropRate ??= 1.0;

                var itemDropRate = 1.0;
                if (Drop.Mesos == 0)
                    itemDropRate = dwOwnerDropRate_Ticket;

                var maxDropChance = DropData.DropChanceCalcFloat;
                maxDropChance /= ms_fIncDropRate * HourDropRateIncrease;
                maxDropChance /= dRegionalIncRate;
                // maxDropChance /= Showdown;
                maxDropChance /= dwOwnerDropRate;
                maxDropChance /= itemDropRate;
                maxDropChance /= MonsterCarnivalRewardRate;
                maxDropChance /= creditsDropRate.Value;


                var luckyNumber = Rand32.Next() % (long)maxDropChance;

                if (luckyNumber >= Drop.Chance) continue;

                var Reward = new Reward()
                {
                    Mesos = Drop.Mesos != 0,
                };

                if (!Reward.Mesos)
                {
                    Reward.Drop = Drop.ItemID;
                    Reward.Data = BaseItem.CreateFromItemID(Drop.ItemID, GetItemAmount(Drop.ItemID, Drop.Min, Drop.Max));

                    var itemVariation = Drop.ItemVariation;

                    // If the equip has special item variation field, then use that.
                    if (itemVariation == ItemVariation.Normal && Reward.Data is EquipItem ei)
                        itemVariation = ei.Template.ItemVariation;


                    Reward.Data.GiveStats(itemVariation);

                    if (Drop.Period > 0)
                    {
                        Reward.Data.Expiration = Tools.GetDateExpireFromPeriodMinutes(Drop.Period);
                    }
                    else if (Drop.DateExpire != DateTime.MaxValue && Drop.DateExpire != DateTime.MinValue)
                    {
                        Reward.Data.Expiration = Drop.DateExpire.ToFileTimeUtc();
                    }
                }
                else
                {
                    Reward.Drop = Drop.Mesos;
                    if (!Drop.Premium || PremiumMap)
                    {
                        int baseDrop = 4 * Reward.Drop / 5;
                        int additionalDrop = 2 * Reward.Drop / 5 + 1;
                        double DroppedMesos = (int)(baseDrop + Rand32.Next() % additionalDrop);

                        if (DroppedMesos <= 1)
                            DroppedMesos = 1;

                        creditsMesoRate ??= Owner?.RateCredits.GetMesoRate();
                        creditsMesoRate ??= 1.0;

                        DroppedMesos *= dwOwnerDropRate_Ticket;
                        DroppedMesos *= Server.Instance.RateMesoAmount;
                        DroppedMesos *= creditsMesoRate.Value;

                        Reward.Drop = (int)DroppedMesos;
                    }
                }

                yield return Reward;
            }

        }

        public static Reward Create(BaseItem Item)
        {
            return new Reward()
            {
                Mesos = false,
                Data = Item,
                Drop = Item.ItemID
            };
        }

        public static Reward Create(double Mesos)
        {
            return new Reward()
            {
                Mesos = true,
                Drop = Convert.ToInt32(Mesos)
            };
        }

        private static short GetItemAmount(int ItemID, int Min, int Max)
        {
            var ItemType = ItemID / 1000000;
            if (Max > 0 && (ItemType == 2 || ItemType == 3 || ItemType == 4))
                return (short)(Min + Rand32.Next() % (Max - Min + 1));
            return 1;
        }

        public void EncodeForMigration(Packet pw)
        {
            pw.WriteBool(Mesos);
            pw.WriteInt(Drop);
            if (!Mesos)
            {
                Data.EncodeForMigration(pw);
            }
        }

        public static Reward DecodeForMigration(Packet pr)
        {
            var reward = new Reward();
            reward.Mesos = pr.ReadBool();
            reward.Drop = pr.ReadInt();
            if (!reward.Mesos)
            {
                reward.Data = BaseItem.DecodeForMigration(pr);
            }
            return reward;
        }
    }
}
