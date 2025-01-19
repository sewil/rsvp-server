using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using log4net;

namespace WvsBeta.Common
{
    public class RedisBackend
    {
        private sideR.sideR _db;
        private static ILog _log = LogManager.GetLogger("OnlinePlayerManager");

        public static RedisBackend Instance { get; private set; }

        // This should be higher than ping timeout!
        private static readonly TimeSpan _onlineTimeout = TimeSpan.FromSeconds((Pinger.MaxLostPings + 3) * Pinger.PingCheckTimeSeconds);
        private static readonly TimeSpan _migrateTimeout = TimeSpan.FromSeconds(50);
        private static readonly TimeSpan _ccSaveTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _onlineCountTimeout = TimeSpan.FromSeconds(60);

        RedisBackend(ConfigReader configReader)
        {
            var cfg = configReader["redis"];
            if (cfg == null)
            {
#if DEBUG
                _log.Warn("No redis configuration. Falling back to...nothing.");
#else
                throw new Exception("Cannot run without redis config in production!");
#endif
            }
            else
            {

                _db = new sideR.sideR(
                    cfg["hostname"]?.GetString() ?? cfg.GetString() ?? "127.0.0.1",
                    cfg["port"]?.GetInt() ?? 6379,
                    cfg["password"]?.GetString() ?? ""
                );
            }
        }

        public static void Init(ConfigReader configReader)
        {
            Instance = new RedisBackend(configReader);
        }

        public bool RunningNormally => _db != null;

        private static string GetMutedCharacterIdKeyName(int characterId) => "muted-" + characterId;
        private static string GetUserIDKeyName(int userId) => "online-player-" + userId;
        private static string GetMigratingCharacterIdKeyName(int characterId) => "migrating-" + characterId;
        private static string GetUndercoverKeyName(int characterId) => "undercover-" + characterId;
        private static string GetImitateKeyName(int characterId) => "imitate-" + characterId;
        private static string GetCCProcessingKeyName(int characterId) => "processing-cc-" + characterId;
        private static string GetCCTokenKeyName(int characterId) => "cctoken-" + characterId;

        private static string GetNonGameHackDetectedKeyName(int userId) => "hack-detected-" + userId;

        public static int GetOnlineId(int world, int channel)
        {
            return 20000 + (world * 100) + channel;
        }

        public void SetPlayerOnline(int userId, int onlineId)
        {
            if (_db == null) return;
            var key = GetUserIDKeyName(userId);

            _db.SET(
                key,
                "" + onlineId,
                _onlineTimeout
            );
        }

        public void SetPlayerCCIsBeingProcessed(int characterId)
        {
            if (_db == null) return;
            _db.SET(
                GetCCProcessingKeyName(characterId),
                "",
                _ccSaveTimeout
            );
        }

        public void RemovePlayerCCIsBeingProcessed(int characterId)
        {
            if (_db == null) return;
            if (false)
            {
                // Free the key
                _db.DEL(GetCCProcessingKeyName(characterId));
            }
            else
            {
                // Do not accept them instantly
                _db.SET(
                    GetCCProcessingKeyName(characterId),
                    "",
                    TimeSpan.FromSeconds(1)
                );
            }
        }

        public bool HoldoffPlayerConnection(int characterId)
        {
            if (_db == null) return false;
            var result = _db.EXISTS(GetCCProcessingKeyName(characterId), out var failed);

            // Assume shits happening, keep holding off player
            if (failed)
            {
                _log.Warn($"Lookup failed, assuming player {characterId} should be held off");
                result = true;
            }

            return result;
        }

        public void RemovePlayerOnline(int userId)
        {
            if (_db == null) return;
            _db.DEL(GetUserIDKeyName(userId));
        }

        public bool IsPlayerOnline(int userId)
        {
            if (_db == null) return false;

            var result = _db.EXISTS(GetUserIDKeyName(userId), out var failed);
            // Assume shits happening, keep holding off player
            if (failed)
            {
                _log.Warn($"Lookup failed, assuming account {userId} is online");
                result = true;
            }

            return result;
        }

        private static string ToHex(byte[] data)
        {
            return string.Join("", data.Select(x => x.ToString("X2")));
        }

        private static IEnumerable<byte> FromHex(string data)
        {
            for (var i = 0; i < data.Length; i += 2)
            {
                yield return byte.Parse($"{data[i]}{data[i + 1]}", NumberStyles.HexNumber);
            }
        }

        public void SetCCToken(int characterId, byte[] token)
        {
            if (_db == null) return;

            _db.SET(GetCCTokenKeyName(characterId), ToHex(token));
        }

        public byte[] GetCCToken(int characterId)
        {
            if (_db == null) return null;

            return FromHex(_db.GET(GetCCTokenKeyName(characterId))).ToArray();
        }

        public void SetMigratingPlayer(int characterId)
        {
            if (_db == null) return;
            var key = GetMigratingCharacterIdKeyName(characterId);

            _db.SET(
                key,
                "",
                _migrateTimeout
            );
        }

        public bool PlayerIsMigrating(int characterId, bool fallbackValue)
        {
            if (_db == null) return fallbackValue;
            // Just delete the key; this is atomic so we are sure the person cannot login twice
            return _db.DEL(GetMigratingCharacterIdKeyName(characterId));
        }

        public void SetPlayerOnlineCount(int world, int channel, int count)
        {
            _db?.SET(
                $"online-players-{world}-{channel}",
                count.ToString(),
                _onlineCountTimeout
            );
        }

        public void MuteCharacter(int fucker, int characterId, int hours)
        {
            _db?.SET(
                GetMutedCharacterIdKeyName(characterId),
                fucker.ToString(),
                TimeSpan.FromHours(hours)
            );
        }

        public void UnmuteCharacter(int characterId)
        {
            _db?.DEL(GetMutedCharacterIdKeyName(characterId));
        }

        public TimeSpan? GetCharacterMuteTime(int characterId)
        {
            return _db?.TTL(GetMutedCharacterIdKeyName(characterId));
        }

        public bool IsUndercover(int characterId)
        {
            return _db?.EXISTS(
                GetUndercoverKeyName(characterId),
                out _
            ) ?? false;
        }

        public void SetUndercover(int characterId, bool undercover)
        {
            if (undercover == false)
            {
                _db?.DEL(GetUndercoverKeyName(characterId));
            }
            else
            {
                _db?.SET(
                    GetUndercoverKeyName(characterId),
                    ""
                );
            }
        }

        public int? GetImitateID(int characterId)
        {
            var possibleId = _db?.GET(GetImitateKeyName(characterId));
            if (possibleId != null && int.TryParse(possibleId, out var id)) return id;
            return null;
        }

        public void SetImitateID(int characterId, int victimId)
        {
            if (victimId == 0)
                _db?.DEL(GetImitateKeyName(characterId));
            else
                _db?.SET(GetImitateKeyName(characterId), victimId.ToString());
        }

        [Flags]
        public enum HackKind
        {
            MemoryEdits = 0x01,
            Speedhack = 0x02,
        }

        public bool TryGetNonGameHackDetect(int userId, out HackKind hk)
        {
            var key = GetNonGameHackDetectedKeyName(userId);
            var res = Enum.TryParse(_db?.GET(key) ?? "", out hk);
            if (res)
            {
                _db?.DEL(key);
            }
            return res;
        }

        public void RegisterNonGameHackDetection(int userId, HackKind hk)
        {
            _db?.SET(GetNonGameHackDetectedKeyName(userId), hk.ToString());
        }
    }
}