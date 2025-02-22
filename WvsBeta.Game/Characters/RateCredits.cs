using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using log4net;
using MySqlConnector;
using WvsBeta.Common;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.Packets;
using WzTools.FileSystem;

// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace WvsBeta.Game
{
    public class RateCredits
    {
        private static ILog _log = LogManager.GetLogger(typeof(RateCredits));

        private const bool UseTimeBasedCredits = true;

        private static HashSet<int> ExcludedMapIDs { get; } = new HashSet<int>();

        static RateCredits()
        {
            if (!UseTimeBasedCredits) return;
            MasterThread.RepeatingAction.Start("Time Based Credits processor", t =>
            {
                Server.Instance.CharacterList.Values.ForEach(x => x.WrappedLogging(() => x.RateCredits.TryDeductCredits(t)));
            }, TimeSpan.Zero, TimeSpan.FromSeconds(60));
        }

        public static void LoadExcludedMaps()
        {
            ExcludedMapIDs.Clear();

            using var cr = new FSFile(Server.Instance.GetConfigPath("Server", "CreditExcl.img"));
            foreach (var mapCategory in cr)
            {
                foreach (var mapidStr in mapCategory.Keys)
                {
                    ExcludedMapIDs.Add(int.Parse(mapidStr));
                }
            }

            // Update all maps so we can safely reload the data
            foreach (var kvp in MapProvider.Maps)
            {
                kvp.Value.DisableCreditsUsage = ExcludedMapIDs.Contains(kvp.Key);
            }
        }

        private const int TimeCreditAccuracyMS = 100;

        public enum Type
        {
            EXP,
            Drop,
            Mesos
        }

        public static Type? TypeFromString(string str)
        {
            return str switch
            {
                "exp" => Type.EXP,
                "drop" => Type.Drop,
                "mesos" => Type.Mesos,
                _ => null
            };
        }

        public static string StringFromType(Type type)
        {
            return type switch
            {
                Type.EXP => "exp",
                Type.Drop => "drop",
                Type.Mesos => "mesos",
                _ => null
            };
        }

        public class Credit
        {
            public long UID { get; set; }

            public Type Type { get; set; }

            public double Rate { get; set; }

            public int CreditsLeft { get; set; }

            public int CreditsGiven { get; set; }

            public string Comment { get; set; }

            public DateTime CreatedAt { get; set; }

            public int Rolls { get; set; }

            public bool Enabled { get; set; }

            public override string ToString()
            {
                return $"UID {UID:X16} {Rate}x {Type} {CreditsLeft}/{CreditsGiven}, {Rolls}";
            }

            public bool Active => Enabled && CreditsLeft > 0;

            public TimeSpan DurationLeft => TimeSpan.FromMilliseconds(CreditsLeft * TimeCreditAccuracyMS);
            public TimeSpan DurationGiven => TimeSpan.FromMilliseconds(CreditsGiven * TimeCreditAccuracyMS);
        }

        private readonly List<Credit> _credits = new List<Credit>();

        private Character character;

        private long timeSinceLastDeduct;

        public bool CreditsCurrentlyUsable => !character.Field.DisableCreditsUsage;

        public RateCredits(Character chr)
        {
            character = chr;
        }

        public void Load()
        {
            using (var reader = (MySqlDataReader) Server.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM character_rate_credits WHERE charid = @charid",
                "@charid", character.ID
            ))
            {
                while (reader.Read())
                {
                    var credit = new Credit
                    {
                        UID = reader.GetInt64("uid"),
                        Rate = reader.GetDouble("rate"),
                        CreditsLeft = reader.GetInt32("credits_left"),
                        CreditsGiven = reader.GetInt32("credits_given"),

                        Comment = reader.GetString("comment"),
                        CreatedAt = reader.GetDateTime("created_at"),

                        Rolls = reader.GetInt32("rolls"),

                        Enabled = reader.GetBoolean("enabled")
                    };

                    var typeString = reader.GetString("type");
                    var t = TypeFromString(typeString);
                    if (t == null)
                    {
                        _log.Error($"Unable to handle credit {credit.UID:X16}, unknown type {typeString}?");
                        continue;
                    }

                    credit.Type = t.Value;

                    _credits.Add(credit);
                }
            }

            timeSinceLastDeduct = MasterThread.CurrentTime;

#if DEBUG
            if (_credits.Count == 0)
            {
                // Use 1 minute for time credits, or 1=1 kill
                var multiplier = UseTimeBasedCredits ? (60000 / TimeCreditAccuracyMS) : 1;

                AddCredits(Type.EXP, 5 * multiplier, 5, "Starter 1");
                AddCredits(Type.Drop, 5 * multiplier, 5, "Starter 1");
                AddCredits(Type.Mesos, 5 * multiplier, 5, "Starter 1");

                AddCredits(Type.EXP, 10 * multiplier, 2.5, "Starter 2");
                AddCredits(Type.Drop, 10 * multiplier, 2.5, "Starter 2");
                AddCredits(Type.Mesos, 10 * multiplier, 2.5, "Starter 2");


                AddCredits(Type.EXP, 15 * multiplier, 1.5, "Starter 3");
                AddCredits(Type.Drop, 15 * multiplier, 1.5, "Starter 3");
                AddCredits(Type.Mesos, 15 * multiplier, 1.5, "Starter 3");
            }
#endif
        }

        public void Save()
        {
            if (_credits.Count == 0) return;

            TryDeductCredits(MasterThread.CurrentTime);

            Server.Instance.CharacterDatabase.RunTransaction(cmd =>
            {
                cmd.CommandText = "INSERT INTO character_rate_credits (charid, uid, rate, type, credits_left, credits_given, rolls, comment, created_at, enabled) VALUES ";
                var first = true;
                foreach (var c in _credits)
                {
                    if (!first) cmd.CommandText += ",";
                    first = false;

                    var rateStr = c.Rate.ToString(CultureInfo.InvariantCulture);

                    var typeStr = StringFromType(c.Type);
                    if (typeStr == null)
                    {
                        _log.Error($"Unable to save credit! Unknown type: {c.Type}");
                        continue;
                    }

                    cmd.CommandText += $"\n({character.ID}, {c.UID}, {rateStr}, '{typeStr}', {c.CreditsLeft}, {c.CreditsGiven}, {c.Rolls}, '{MySqlHelper.EscapeString(c.Comment)}', '{c.CreatedAt:yyyy-MM-dd HH:mm:ss}', {c.Enabled})";
                }

                cmd.CommandText += "ON DUPLICATE KEY UPDATE credits_left = VALUES(credits_left), rolls = VALUES(rolls), enabled = VALUES(enabled)";

                cmd.ExecuteNonQuery();
            });
        }


        public void AddTimedCredits(Type type, TimeSpan ts, double rate, string comment)
        {
            AddCredits(type, (int)(ts.TotalMilliseconds / TimeCreditAccuracyMS), rate, comment);
        }

        public void AddCredits(Type type, int amount, double rate, string comment)
        {
            ulong uid = 0;
            do
            {
                uid |= Rand32.Next();
                uid <<= 32;
                uid |= Rand32.Next();
            } while (_credits.Exists(c => c.UID == (long) uid));

            _credits.Add(new Credit
            {
                CreditsLeft = amount,
                CreditsGiven = amount,
                CreatedAt = MasterThread.CurrentDate,
                Comment = comment,
                Rate = rate,
                Type = type,
                UID = (long) uid,
                Rolls = 0,
                Enabled = false,
            });
        }


        public bool TryGetCredit(Type type, out Credit credit)
        {
            credit = null;

            TryDeductCredits(MasterThread.CurrentTime);

            if (!CreditsCurrentlyUsable) return false;

            credit = _credits
                .Where(x => x.Type == type && x.CreditsLeft > 0 && x.Enabled)
                .OrderByDescending(x => x.Rate)
                .FirstOrDefault();

            return credit != null;
        }

        private bool TryUseCredit(Type type, out double rate)
        {
            rate = 0;

            if (!TryGetCredit(type, out var credit)) return false;

            rate = credit.Rate;
            credit.Rolls++;

            if (UseTimeBasedCredits) return true;

            credit.CreditsLeft--;
            if (credit.CreditsLeft == 0)
            {
                _log.Info($"Used up credits for {credit.Rate}x {credit.Type} UID {credit.UID:X16}");
            }

            return true;
        }

        public IEnumerable<Credit> GetCredits() => _credits;
        public IEnumerable<Credit> GetCreditsForType(Type type) => _credits.Where(x => x.Type == type);

        public IEnumerable<(Type Type, double Rate, int Amount)> GetAggregatedCredits()
        {
            TryDeductCredits(MasterThread.CurrentTime);

            return _credits
                .Where(x => x.CreditsLeft > 0)
                .GroupBy(x => (x.Type, x.Rate))
                .Select(x => (x.Key.Type, x.Key.Rate, x.Sum(x => x.CreditsLeft)))
                .OrderByDescending(x => x.Rate);
        }

        public IEnumerable<(Type Type, double Rate, TimeSpan Time)> GetAggregatedTimedCredits()
        {
            return GetAggregatedCredits().Select(x => (x.Type, x.Rate, TimeSpan.FromMilliseconds(x.Amount * TimeCreditAccuracyMS)));
        }

        public void TryDeductCredits(long time)
        {
            if (!UseTimeBasedCredits) return;

            // This code should prevent for a loop of TryGetCredit
            // We don't have to run code if we have nothing to update...!
            var millisSince = time - timeSinceLastDeduct;
            var creditsUsed = (int) (millisSince / TimeCreditAccuracyMS);

            if (creditsUsed == 0) return;

            timeSinceLastDeduct = time;

            if (!CreditsCurrentlyUsable)
            {
                // We ignore the time that has passed, otherwise it'll suddenly deduct a chunk when you switch
                // maps to one that does count.
                return;
            }

            var anyCreditExpired = false;

            void deductForType(Type type)
            {
                var timeLeft = creditsUsed;
                while (timeLeft > 0 && TryGetCredit(type, out var credit))
                {
                    var chunk = Math.Min(timeLeft, credit.CreditsLeft);

                    credit.CreditsLeft -= chunk;
                    timeLeft -= chunk;

                    if (credit.CreditsLeft == 0)
                    {
                        _log.Info($"Used up credits for {credit}");
                        credit.Enabled = false;
                        anyCreditExpired = true;
                    }
                }

                if (timeLeft > 0 && timeLeft != creditsUsed)
                {
                    // _log.Debug($"Possibly used more than {minutes} of credit in {type}");
                }
            }


            deductForType(Type.Drop);
            deductForType(Type.EXP);
            deductForType(Type.Mesos);

            if (anyCreditExpired)
            {
                SendUpdate(true);
            }
        }


        public double GetEXPRate()
        {
            return TryUseCredit(Type.EXP, out var rate) ? rate : 1.0;
        }

        public double GetMesoRate()
        {
            return TryUseCredit(Type.Mesos, out var rate) ? rate : 1.0;
        }

        public double GetDropRate()
        {
            return TryUseCredit(Type.Drop, out var rate) ? rate : 1.0;
        }

        private bool lastWasActive = false;

        public void SendUpdate(bool forced = false)
        {
            var isActive = CreditsCurrentlyUsable;
            if (lastWasActive == isActive)
            {
                if (!forced) return;
            }
            lastWasActive = isActive;
            CfgPacket.UpdateRateCredits(character);
        }
    }
}
