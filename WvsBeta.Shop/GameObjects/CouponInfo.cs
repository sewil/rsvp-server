using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using MySql.Data.MySqlClient;
using WvsBeta.Common;
using WvsBeta.Game;

namespace WvsBeta.Shop
{
    public class CouponInfo
    {
        public const string V12UpdateCoupon = "39281AF7HE62KZ96UWP1304AZK5X";

        private static ILog _log = LogManager.GetLogger(typeof(CouponInfo));

        private static Dictionary<string, CouponInfo> _knownCoupons = new Dictionary<string, CouponInfo>();

        public class ItemInfo
        {
            public int ItemID { get; set; }
            public short Amount { get; set; }
            public short DaysUsable { get; set; }

            public ItemInfo(int itemId, short amount, short daysUsable)
            {
                ItemID = itemId;
                Amount = amount;
                DaysUsable = daysUsable;
            }
        }

        public string CouponCode { get; set; }
        public List<ItemInfo> CashItems { get; set; } = new List<ItemInfo>();
        public List<ItemInfo> NormalItems { get; set; } = new List<ItemInfo>();
        public List<ItemInfo> RandomCashItems { get; set; } = new List<ItemInfo>();
        public List<ItemInfo> RandomNormalItems { get; set; } = new List<ItemInfo>();
        public int MaplePoints { get; set; }
        public int Mesos { get; set; }

        private bool _used;

        public Action<Character, CouponInfo> LoadData { get; set; }

        [Flags]
        public enum CouponFlags
        {
            None = 0,
            NoGift = 0x01,

            // Implies that this coupon can always be used, but never multiple times by the same account,
            // and also prevents it from being gifted
            OncePerAccount = 0x02 | NoGift,
        }

        public CouponFlags Flags { get; set; } = CouponFlags.None;

        public static void Load()
        {
            _knownCoupons.Clear();

            void AddCoupon(CouponInfo ci)
            {
                _knownCoupons[ci.CouponCode] = ci;
            }

            // Thirdflight coupon
            AddCoupon(new CouponInfo
            {
                MaplePoints = 250,
                CouponCode = "MC40NDC4OTU5OTE3NDQZODUYMTEZOS",
                _used = false,
                Mesos = 0,
                Flags = CouponFlags.OncePerAccount,
            });

            // Ludi Mailinglist
            AddCoupon(new CouponInfo
            {
                MaplePoints = 250,
                CouponCode = "GH93LS70YC457L3F41RY74LE095321",
                _used = false,
                Mesos = 0,
                Flags = CouponFlags.OncePerAccount,
                RandomCashItems = new List<ItemInfo>
                {
                    new ItemInfo(1002260, 1, 60),
                    new ItemInfo(1002261, 1, 60),
                    new ItemInfo(1002262, 1, 60),
                    new ItemInfo(1002263, 1, 60),
                }
            });

            // V.12 update coupon, that gives 2 sets of items depending on your gender.
            AddCoupon(new CouponInfo
            {
                MaplePoints = 500,
                CouponCode = V12UpdateCoupon,
                Mesos = 0,
                Flags = CouponFlags.OncePerAccount,
                _used = false,
                LoadData = (character, info) =>
                {
                    info.CashItems.Clear();

                    // Load mesoranger items based on gender

                    var items = character.Gender == 0
                        ? new[] {1000010, 1050085, 1072202, 1082124}
                        : new[] {1001016, 1051089, 1072202, 1082124};

                    foreach (var item in items)
                    {
                        info.CashItems.Add(new ItemInfo(
                            item,
                            1,
                            90
                        ));
                    }
                }
            });


            // V.15 TOTP coupon
            AddCoupon(new CouponInfo
            {
                MaplePoints = 800,
                CouponCode = "D1K230FL2",
                _used = false,
                Mesos = 0,
                Flags = CouponFlags.OncePerAccount,
            });

            // V.17 Anniversary coupon
            AddCoupon(new CouponInfo
            {
                CouponCode = "005241046672100",
                _used = false,
                Mesos = 0,
                MaplePoints = 1000,
                Flags = CouponFlags.OncePerAccount,
                CashItems = new List<ItemInfo>
                {
                    new ItemInfo(1002345, 1, 90)
                }
            });
            
            // Lunar New Year coupon
            AddCoupon(new CouponInfo
            {
                CouponCode = "HappyTiger22",
                _used = false,
                Mesos = 0,
                MaplePoints = 3000,
                Flags = CouponFlags.OncePerAccount,
            });

            // We load all coupons so you see an error that it has been used or not

            using (var reader = (MySqlDataReader) Server.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM cashshop_coupon_codes"
            ))
            {
                while (reader.Read())
                {
                    AddCoupon(new CouponInfo
                    {
                        CouponCode = reader.GetString("couponcode"),
                        Mesos = reader.GetInt32("mesos"),
                        MaplePoints = reader.GetInt32("maplepoints"),
                        _used = reader.GetBoolean("used"),
                    });
                }
            }

            using (var reader = (MySqlDataReader) Server.Instance.CharacterDatabase.RunQuery(
                "SELECT ccir.* FROM cashshop_coupon_item_rewards ccir JOIN cashshop_coupon_codes ccc ON ccc.couponcode = ccir.couponcode"
            ))
            {
                while (reader.Read())
                {
                    var code = reader.GetString("couponcode");

                    if (!_knownCoupons.TryGetValue(code, out var ci)) continue;

                    var item = new ItemInfo(
                        reader.GetInt32("itemid"),
                        reader.GetInt16("amount"),
                        reader.GetInt16("days_usable")
                    );

                    var cashItem = false;

                    if (DataProvider.Items.TryGetValue(item.ItemID, out var itemData))
                        cashItem = itemData.Cash;
                    else if (DataProvider.Equips.TryGetValue(item.ItemID, out var equipData))
                        cashItem = equipData.Cash;
                    else if (DataProvider.Pets.TryGetValue(item.ItemID, out _))
                        cashItem = true;
                    else
                    {
                        _log.Error($"Unknown item {item.ItemID} in coupon {code}!");
                        continue;
                    }

                    if (cashItem) ci.CashItems.Add(item);
                    else ci.NormalItems.Add(item);
                }
            }

            _log.Info($"Loaded {_knownCoupons.Count} coupons");
        }

        public bool IsUsed(Character chr)
        {
            if (_used) return true;
            if (Flags.HasFlag(CouponFlags.OncePerAccount))
            {
                return chr.UsedCoupons.Contains(CouponCode);
            }

            return false;
        }

        public bool MarkUsed(Character chr)
        {
            if (IsUsed(chr))
            {
                throw new Exception($"Coupon is already used?? {CouponCode}");
                return false;
            }

            if (Server.Tespia)
            {
                _log.Info("Not marking item as used, as we are running in Tespia mode...");
                return true;
            }

            if (Flags.HasFlag(CouponFlags.OncePerAccount))
            {
                chr.UsedCoupons.Add(CouponCode);

                return true;
            }

            _used = true;

            _log.Info($"Marking {CouponCode} as used in database");

            var rowsChanged = (int) Server.Instance.CharacterDatabase.RunQuery(
                "UPDATE cashshop_coupon_codes SET used = 1 WHERE couponcode = @couponcode",
                "@couponcode", CouponCode
            );
            if (rowsChanged == 0)
            {
                _log.Error($"Apparently coupon {CouponCode} is already used (database update failed?)");
                return false;
            }

            return true;
        }

        public static bool Get(string couponCode, out CouponInfo ci)
        {
            foreach (var x in _knownCoupons)
            {
                if (string.Equals(x.Key, couponCode, StringComparison.InvariantCultureIgnoreCase))
                {
                    ci = x.Value;
                    return true;
                }
            }

            ci = null;
            return false;
        }

        public IEnumerable<ItemInfo> GetRandomItem(List<ItemInfo> items)
        {
            if (items.Count == 0) yield break;
            yield return items.RandomElement();
        }


        public IEnumerable<ItemInfo> GetRandomCashItem() => GetRandomItem(RandomCashItems);
        public IEnumerable<ItemInfo> GetRandomNormalItem() => GetRandomItem(RandomNormalItems);
    }
}