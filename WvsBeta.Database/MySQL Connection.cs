using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using WvsBeta.Common;

namespace WvsBeta.Database
{
    /// <summary>
    /// Known issues:
    /// MySQL calls Abort while inside NextResult while in ExecuteReader (causing initial reconnect logic)
    /// then while reconnecting, MySQL calls Abort while inside Connect on exception.
    /// </summary>
    public class MySQL_Connection
    {
        public MySqlDataReader Reader { get; private set; }
        public bool Stop { get; set; }

        public const int MAX_QUERY_LIST_BACKTRACE = 5;

        private MySqlConnection _connection;
        private MySqlCommand _command;
        private string _connectionString;
        private Common.Logfile _logFile;
        private Stack<Tuple<string, object[], string>> _queryList = new Stack<Tuple<string, object[], string>>(MAX_QUERY_LIST_BACKTRACE);

        private void AddQuery(string pQuery, object[] pParameters)
        {
            if (_queryList.Count > MAX_QUERY_LIST_BACKTRACE) _queryList.Pop();
            _queryList.Push(new Tuple<string, object[], string>(pQuery, pParameters, new StackTrace().ToString()));
        }

        private string GetLastQueries()
        {
            var ret = new StringBuilder();
            ret.AppendLine("---------- BEGIN LIST -------------");
            foreach (var kvp in _queryList)
            {
                ret.AppendLine("Query: " + kvp.Item1);
                var parametersLength = kvp.Item2?.Length ?? 0;
                if (parametersLength > 0)
                {
                    ret.AppendLine("Parameters:");

                    for (var i = 0; i < parametersLength; i += 2)
                    {
                        ret.AppendFormat("\t{0}: {1}", kvp.Item2[i + 0], kvp.Item2[i + 1]).AppendLine();
                    }
                }

                ret.AppendLine("Stacktrace:").AppendLine(kvp.Item3);
            }

            ret.AppendLine("------------- END LIST ---------------");
            return ret.ToString();
        }

        public MySQL_Connection(MasterThread pMasterThread, ConfigReader configReader) :
            this(pMasterThread,
                configReader["dbUsername"].GetString(),
                configReader["dbPassword"].GetString(),
                configReader["dbDatabase"].GetString(),
                configReader["dbHost"].GetString(),
                configReader["dbPort"]?.GetUShort() ?? 3306
            )
        {
        }

        public MySQL_Connection(MasterThread pMasterThread, string pUsername, string pPassword, string pDatabase, string pHost, ushort pPort = 3306)
        {
            if (pMasterThread == null)
            {
                throw new Exception("MasterThread shouldn't be NULL at this time!");
            }

            _logFile = new Common.Logfile("Database", true, Path.Combine("Logs", pMasterThread.ServerName, "Database"));
            Stop = false; 
            _connectionString = "Server=" + pHost + "; Port=" + pPort + "; Database=" + pDatabase + "; Uid=" + pUsername + "; Pwd=" + pPassword;
            RecoverConnection();
            SetupPinger();
        }

        private void SetupPinger()
        {
            MasterThread.RepeatingAction.Start(
                "Database Pinger",
                (date) =>
                {
                    var pingOK = false;
                    try
                    {
                        pingOK = Ping();
                    }
                    catch (Exception ex)
                    {
                        _logFile.WriteLine("Exception while pinging server!!! Ex: {0}", ex);
                        pingOK = false;
                    }

                    if (!pingOK)
                    {
                        _logFile.WriteLine("Failure pinging the server!!!! Disconnecting so it will reconnect");
                        try
                        {
                            _connection.Close();
                        }
                        catch (Exception ex)
                        {
                            _logFile.WriteLine("Unable to close connection!!! {0}", ex);
                        }
                    }
                },
                5 * 1000, 5 * 1000);
        }

        private bool alreadyConnecting = false;

        public void RecoverConnection()
        {
            // if (alreadyConnecting) return;
            alreadyConnecting = true;
            try
            {
                _logFile.WriteLine("Connecting to database... Stacktrace:");
                _logFile.WriteLine(new StackTrace().ToString());

                
                void stateChangeHandler(object sender, System.Data.StateChangeEventArgs e)
                {
                    _logFile.WriteLine($"State change from {e.OriginalState} to {e.CurrentState}");
                }
                
                _connection ??= new MySqlConnection(_connectionString);
                while (true)
                {
                    _connection.StateChange -= stateChangeHandler;
                    _connection.StateChange -= connection_StateChange;
                    try
                    {
                        
                        _connection.StateChange += stateChangeHandler;
                        _connection.StateChange -= connection_StateChange;

                        if (_connection.State == ConnectionState.Open)
                        {
                            _logFile.WriteLine("Connection was active in RecoverConnection. Closing...");
                            try
                            {
                                _connection.Close();
                            }
                            catch (Exception ex)
                            {
                                _logFile.WriteLine("Exception while closing! {0} EHHHHH", ex);
                            }
                        }

                        _logFile.WriteLine("Opening connection");
                        _connection.Open();


                        _logFile.WriteLine($"Connected with MySQL server with version info: {_connection.ServerVersion} and uses {(_connection.UseCompression ? "" : "no ")}compression");

                        _logFile.WriteLine("Make sure we are making our reads from committed data.");
                        try
                        {
                            RunQuery("SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED");
                        }
                        catch (Exception ex)
                        {
                            _logFile.WriteLine($"Exception while running first query??? {ex}. Trying to reconnect...");
                            continue;
                        }

                        if (_connection.State != ConnectionState.Open)
                        {
                            throw new Exception($"Connection closed after initial query? State: {_connection.State}");
                        }

                        _connection.StateChange -= stateChangeHandler;
                        _connection.StateChange += connection_StateChange;
                        break;
                    }
                    catch (Exception ex)
                    {
                        var line = $"Got exception at MySQL_Connection.Connect():\r\n {ex}";
                        _logFile.WriteLine(line);
                        _logFile.WriteLine("retrying...");
                    }
                }

            }
            catch (Exception ex)
            {
                var line = $"Got exception at MySQL_Connection.Connect():\r\n {ex}";
                _logFile.WriteLine(line);

                ////Console.WriteLine(ex.ToString());
                throw new Exception(line);
            }
            finally
            {
                _logFile.WriteLine("RecoverConnection completed");
                alreadyConnecting = false;
            }
        }

        /// <summary>
        /// This handler is really nasty. It operates outside of the MasterThread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void connection_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            _logFile.WriteLine("StateChange: From {0} to {1}", e.OriginalState, e.CurrentState);
            if (e.CurrentState == System.Data.ConnectionState.Closed)
            {
                if (Stop) return;
                if (alreadyConnecting)
                {
                    _logFile.WriteLine("Lost connection to server, but ignoring (alreadyConnecting)");
                }
                else
                {
                    ////Console.WriteLine("MySQL connection closed. Reconnecting!");
                    _logFile.WriteLine("Lost connection (connection Closed). Reconnecting.");
                    _connection.StateChange -= connection_StateChange;
                    RecoverConnection();
                }
            }
            else if (e.CurrentState == System.Data.ConnectionState.Open)
            {
                _logFile.WriteLine("Connected to server!");
                ////Console.WriteLine("MySQL connection opened!");
            }
            else
            {
                _logFile.WriteLine("State change: " + e.CurrentState);
            }
        }

        public int? CharacterIdByName(string characterName)
        {
            using var mdr = (MySqlDataReader)RunQuery(
                "SELECT `ID` FROM characters WHERE `name` = @name AND deleted_at IS NULL",
                "@name", characterName
            );
            if (mdr.Read())
            {
                return mdr.GetInt32(0);
            }

            return null;
        }

        public string CharacterNameById(int characterID)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT name FROM characters WHERE `ID` = @id",
                "@id", characterID
            ))
            {
                if (mdr.Read())
                {
                    return mdr.GetString(0);
                }
            }

            return null;
        }

        public int UserIDByUsername(string username)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT `ID` FROM users WHERE `username` = @name",
                "@name", username
            ))
            {
                if (mdr.Read())
                {
                    return mdr.GetInt32(0);
                }
            }

            return -1;
        }


        public int UserIDByCharacterName(string charname)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT `userid` FROM characters WHERE `name` = @name AND deleted_at IS NULL",
                "@name", charname
            ))
            {
                if (mdr.Read())
                {
                    return mdr.GetInt32(0);
                }
            }

            return -1;
        }

        public int UserIDByCharID(int charid)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT `userid` FROM characters WHERE ID = @id",
                "@id", charid
            ))
            {
                if (mdr.Read())
                {
                    return mdr.GetInt32(0);
                }
            }

            return -1;
        }

        public bool ExistsUser(int UserId)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT * FROM users WHERE ID = @id",
                "@id", UserId
            ))
            {
                return mdr.HasRows;
            }
        }

        public bool IsBanned(int id)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT 1 FROM users WHERE ID = @id AND ban_expire >= CURRENT_TIMESTAMP",
                "@id", id
            ))
            {
                return mdr.Read();
            }
        }

        public (int machineIdBans, int ipBans, int uniqueIdBans) GetUserBanRecord(int userId)
        {
            string machineID = "", uniqueID = "", IP = "";

            using (var mdr = RunQuery("SELECT last_machine_id, last_unique_id, last_ip FROM users WHERE id = @id",
                "@id", userId) as MySqlDataReader)
            {
                if (!mdr.Read()) return (0, 0, 0);
                machineID = mdr.IsDBNull(0) ? "NEVERMATCHES" : mdr.GetString(0);
                uniqueID = mdr.IsDBNull(1) ? "NEVERMATCHES" : mdr.GetString(1);
                IP = mdr.IsDBNull(2) ? "NEVERMATCHES" : mdr.GetString(2);
            }

            return GetBanRecord(machineID, uniqueID, IP);
        }

        public (int machineIdBans, int ipBans, int uniqueIdBans) GetBanRecord(string machineID, string uniqueID, string IP)
        {
            using (var mdr = RunQuery(
                @"
SELECT 
	SUM(last_machine_id = @machineId), 
    SUM(last_unique_id = @uniqueId),
    SUM(last_ip = @ip)
FROM users WHERE ban_expire > NOW()",
                "@machineId", machineID,
                "@uniqueId", uniqueID,
                "@ip", IP
            ) as MySqlDataReader)
            {
                if (!mdr.Read() || mdr.IsDBNull(0)) return (0, 0, 0);
                int machineBanCount = mdr.GetInt32(0);
                int uniqueBanCount = mdr.GetInt32(1);
                int ipBanCount = mdr.GetInt32(2);

                return (machineBanCount, uniqueBanCount, ipBanCount);
            }
        }

        public (int machineIdBans, int ipBans, int uniqueIdBans) GetUserBanRecordLimit(int userId)
        {
            using (var mdr = RunQuery("SELECT 100, max_unique_id_ban_count + 0, max_ip_ban_count + 0 FROM users WHERE id = @id",
                "@id", userId) as MySqlDataReader)
            {
                // Weird here
                if (!mdr.Read() || mdr.IsDBNull(0)) return (0, 0, 0);
                int machineBanCount = mdr.GetInt32(0);
                int uniqueBanCount = mdr.GetInt32(1);
                int ipBanCount = mdr.GetInt32(2);

                return (machineBanCount, uniqueBanCount, ipBanCount);
            }
        }

        public bool IsIpBanned(string ip)
        {
            using (var mdr = (MySqlDataReader)RunQuery(
                "SELECT 1 FROM ipbans WHERE ip = @ip",
                "@ip", ip
            ))
            {
                if (mdr.Read())
                {
                    return true;
                }
            }

            return false;
        }

        public bool PermaBan(int userid, byte reason, string banner, string banmessage)
        {
            try
            {
                RunQuery(
                    "UPDATE users SET ban_expire = DATE_ADD(NOW(), INTERVAL 100 YEAR), ban_reason = @reason, ban_reason_message = @reasonmessage, banned_by = @banner, banned_at = NOW() WHERE ID = @id",
                    "@id", userid,
                    "@reason", reason,
                    "@banner", banner,
                    "@reasonmessage", banmessage
                );
            }
            catch (Exception e)
            {
                _logFile.WriteLine("Unable to permaban someone! {0}", e);
                return false;
            }

            return true;
        }

        public bool TempBan(int userid, byte reason, int hours, string banner)
        {
            try
            {
                RunQuery(
                    "UPDATE users SET ban_expire = DATE_ADD(NOW(), INTERVAL " + hours + " HOUR), ban_reason = @reason, banned_by = @banner, banned_at = NOW() WHERE ID = @id",
                    "@id", userid,
                    "@reason", reason,
                    "@banner", banner
                );
            }
            catch (Exception e)
            {
                _logFile.WriteLine("Unable to tempban someone! {0}", e);
                return false;
            }

            return true;
        }

        public bool MuteBan(int userid, byte reason, int hours)
        {
            try
            {
                RunQuery(
                    "UPDATE users SET quiet_ban_expire = DATE_ADD(NOW(), INTERVAL " + hours + " HOUR), quiet_ban_reason = @reason WHERE ID = @id",
                    "@id", userid,
                    "@reason", reason
                );
            }
            catch (Exception e)
            {
                _logFile.WriteLine("Unable to muteban someone! {0}", e);
                return false;
            }

            return true;
        }


        public void AddPointTransaction(int userId, int amount, string type, string note = "")
        {
            RunQuery("INSERT INTO user_point_transactions (`userid`, `amount`, `date`, `note`, `pointtype`) VALUES (@userid, @amount, NOW(), @note, @pointtype);",
                "userid", userId,
                "amount", amount,
                "note", note,
                "pointtype", type
            );
        }

        public void SendNoteToUser(string fromName, int toCharId, string message)
        {
            RunQuery("INSERT INTO memos (`from_name`, `to_charid`, `message`, `sent_time`) VALUES (@fromName, @toCharId, @message, NOW());",
                "@fromName", fromName,
                "@toCharId", toCharId,
                "@message", message
            );
        }

        private void CloseReaders()
        {
            if (Reader != null && !Reader.IsClosed)
            {
                _logFile.WriteLine("Closing reader!!! " + GetLastQueries() + ", current callstack: " + new StackTrace());
                Reader.Close();
                Reader.Dispose();
                Reader = null;
            }
        }

        private object ExecuteAndReturnPossibleReader(string pQuery)
        {
            pQuery = pQuery.Trim();
            if (pQuery.StartsWith("SELECT") || pQuery.StartsWith("SHOW"))
            {
                Reader = _command.ExecuteReader();
                return Reader;
            }

            return _command.ExecuteNonQuery();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pQuery"></param>
        /// <param name="parameters">Supply 2 pairs, first string (parameter name), second any type (parameter value)</param>
        /// <returns></returns>
        public object RunQuery(string pQuery, params object[] parameters)
        {
            TryAgain:
            var parametersLength = parameters.Length;
            if (parametersLength % 2 == 1)
            {
                throw new ArgumentException("Supplied uneven amount of parameters.");
            }

            try
            {
                CloseReaders();
                _command = new MySqlCommand(pQuery, _connection);
                _command.EnableCaching = false;
                if (parametersLength > 0)
                {
                    for (var i = 0; i < parametersLength; i += 2)
                    {
                        _command.Parameters.AddWithValue(
                            (string)parameters[i + 0],
                            parameters[i + 1]
                        );
                    }

                    // Changed in new Connector; prepare after AddWithValue. Documentation still shows before...
                    // https://stackoverflow.com/questions/71897708/parameter-was-not-found-during-prepare-using-prepared-statement-in-c-sharp
                    _command.Prepare();
                }

                AddQuery(pQuery, parameters);

                return ExecuteAndReturnPossibleReader(pQuery);
            }
            catch (InvalidOperationException ex)
            {
                _logFile.WriteLine("Lost connection (InvalidOperation). Reconnecting.");
                RecoverConnection();
                goto TryAgain;
            }
            catch (MySqlException ex)
            {
                var errString = ex.ToString();
                if (ex.Number == 2055 || errString.Contains("Unable to read data from the transport connection"))
                {
                    Console.WriteLine("Lost connection to DB... Trying to reconnect and wait a second before retrying to run query.");
                    _logFile.WriteLine("Lost connection (MySQL Exception?). Reconnecting.");
                    RecoverConnection();
                    goto TryAgain;
                }
                else
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine(pQuery);
                    _logFile.WriteLine(GetLastQueries());
                    _logFile.WriteLine("Got exception @ MySQL_Connection::RunQuery({0}) :\r\n{1}", pQuery, ex.ToString());
                    throw new Exception($"[{DateTime.Now}][DB LIB] Got exception @ MySQL_Connection::RunQuery({pQuery}, {string.Join(", ", parameters)}) : {ex}");
                }
            }

            return 0;
        }

        public object RunQuery(string pQuery)
        {
            TryAgain:
            try
            {
                CloseReaders();
                _command = new MySqlCommand(pQuery, _connection);
                _command.EnableCaching = false;
                AddQuery(pQuery, null);

                return ExecuteAndReturnPossibleReader(pQuery);
            }
            catch (InvalidOperationException)
            {
                if (alreadyConnecting) throw;
                _logFile.WriteLine("Lost connection (InvalidOperation). Reconnecting.");
                RecoverConnection();
                goto TryAgain;
            }
            catch (MySqlException ex)
            {
                if (alreadyConnecting) throw;

                var errString = ex.ToString();
                if (ex.Number == 2055 || errString.Contains("Unable to read data from the transport connection"))
                {
                    _logFile.WriteLine("Lost connection (MySQL Exception?). Reconnecting.");
                    RecoverConnection();
                    goto TryAgain;
                }
                else
                {
                    _logFile.WriteLine(GetLastQueries());
                    _logFile.WriteLine("Got exception @ MySQL_Connection::RunQuery({0}) :\r\n{1}", pQuery, ex.ToString());
                    throw new Exception($"[{DateTime.Now}][DB LIB] Got exception @ MySQL_Connection::RunQuery({pQuery}) : {ex}");
                }
            }

            return 0;
        }

        public delegate void LogAction(string s, params object[] args);

        public void RunTransaction(Action<MySqlCommand> Command, LogAction dbgCallback = null)
        {
            RetryCompletely:
            try
            {
                CloseReaders();
                Retry:
                var transaction = _connection.BeginTransaction();
                var executor = new MySqlCommand()
                {
                    Transaction = transaction,
                    Connection = _connection
                };
                executor.EnableCaching = false;

                try
                {
                    Command(executor);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    var error = "Error running transaction. Rolling back (if any). Error: " + ex + Environment.NewLine + " last Command: " + executor.CommandText;
                    _logFile.WriteLine(error);

                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        _logFile.WriteLine("Rollback of transaction failed!!! Error: " + ex2);
                    }

                    if (error.Contains("SocketException"))
                    {
                        dbgCallback?.Invoke("[RETRYING TRANSACTION!!!] " + error);
                        _logFile.WriteLine("Socket Exception, retrying transaction.");
                        goto Retry;
                    }
                    else
                    {
                        dbgCallback?.Invoke(error);
                    }
                }
                finally
                {
                    transaction.Dispose();
                    executor.Dispose();
                }
            }
            catch (InvalidOperationException)
            {
                _logFile.WriteLine("Lost connection (InvalidOperation). Reconnecting.");
                RecoverConnection();
                goto RetryCompletely;
            }
        }

        public void RunTransaction(string query, LogAction dbgCallback = null)
        {
            RunTransaction(comm =>
            {
                comm.CommandText = query;
                comm.ExecuteNonQuery();
            }, dbgCallback);
        }

        public int GetLastInsertId() => (int)_command.LastInsertedId;


        public bool Ping()
        {
            var rng = MasterThread.CurrentTime % 100000;

            using var mdr = (MySqlDataReader)RunQuery("SELECT " + rng);
            if (mdr.Read())
            {
                var value = mdr.GetInt32(0);
                if (value != rng)
                {
                    _logFile.WriteLine($"Invalid data returned from Ping command: {value} expected {rng}");
                }
                return value == rng;
            }

            return false;
        }
    }
}