using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using WvsBeta.Common;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;
// ReSharper disable HeuristicUnreachableCode

#pragma warning disable 162

namespace WvsBeta.Game
{
    public abstract class IScriptV2 : IDisposable
    {
        private ILog _log = LogManager.GetLogger(typeof(IScriptV2));

        public const string Newline = "\r\n";

        public readonly TimeSpan MaxScriptExecutionTime = TimeSpan.FromSeconds(3);

        public string ScriptName { get; set; }

        // You are free to change NpcID to your likings if you want to chat with different ones. The script is not affected.
        public int NpcID { get; set; }

        protected Character chr { get; private set; }

        private Task _executionThread { get; set; }

        private CancellationTokenSource _cts;
        private CancellationToken _cancellationToken;

        public bool WaitingForResponse { get; set; }

        private string _transferID = "";

        public ItemVariation ItemVariation { get; private set; } = ItemVariation.None;

        private const bool DEBUG_SCRIPT_API = false;

        public void Setup(Character chr, int npcID)
        {
            // Rewrite it so we can initialize with the parent type
            _log = LogManager.GetLogger(GetType());
            this.chr = chr;
            this.NpcID = npcID;
            this._transferID = "" + chr.ID + "-" + ScriptName + "-" + RNG.Range.generate(0, Int64.MaxValue);
        }

        public void StartScript()
        {
            LastSentType = NpcChatTypes.INVALID;

            _cts = new CancellationTokenSource();
            _cancellationToken = _cts.Token;

            _executionThread = new Task(() =>
            {
                try
                {
                    Run();
                }
                catch (ObjectDisposedException)
                {
                    // This can be ignored.
                }
                catch (OperationCanceledException)
                {
                    // This can be ignored.
                }
                catch (Exception ex)
                {
                    Server.Instance.ServerTraceDiscordReporter.Enqueue($"Exception in script {ScriptName} {NpcID}: ```{ex}```");

                    Error($"Error handling a script! Script name {ScriptName} {NpcID}.", ex);
                    if (chr != null && chr.IsGM)
                    {
                        void InformGM(string msg)
                        {
                            MessagePacket.SendText(MessagePacket.MessageTypes.Notice, msg, chr,
                                MessagePacket.MessageMode.ToPlayer);
                        }

                        InformGM("This script threw an error! Good luck decoding:");
                        ex.ToString().Split("\n").ForEach(InformGM);
                    }
                }
                finally
                {
                    if (DEBUG_SCRIPT_API) Debug("Script finished!");
                    _scriptFinishedHandle.Set();

                    Dispose(); //todo: double check this fix, cause it bugs with NPCs that don't have any dialogue like KPQ stage 2/3/4
                }
            }, _cancellationToken);
            _executionThread.Start();

            WaitForClientInputRequest();
        }

        public void TerminateScript()
        {
            if (DEBUG_SCRIPT_API) Debug("Cancelling context");
            _cts.Cancel();
            if (DEBUG_SCRIPT_API) Debug("Waiting for executionThread");

            if (_executionThread != null && !_executionThread.Wait(TimeSpan.FromSeconds(2)))
            {
                Error("Unable to stop thread!");
            }
            else
            {
                Info("Thread stopped!");
            }

            if (chr != null)
                chr.NpcSession = null;

            Dispose();
        }

        public NpcChatTypes LastSentType { get; private set; } = NpcChatTypes.INVALID;

        // Note: this field is zero-indexed. -1 would mean no messages,
        // 0 would mean the first message.
        private int _currentMessageInHistory = -1;

        // So normally you'd use something like a stack for lines,
        // but as we can go back and forth between SendNext, BackNext, BackNext, BackOK,
        // we need to keep track what we have sent already, and what comes next (if any).
        private readonly List<NpcUsedLine> _history = new List<NpcUsedLine>();

        private void ResetMessageHistory()
        {
            _currentMessageInHistory = -1;
            _history.Clear();
        }

        private void PushMessageHistory(NpcChatSimpleTypes messageType, string fullMessage)
        {
            _history.Add(new NpcUsedLine(messageType, fullMessage));
            _currentMessageInHistory++;
        }

        protected bool HasHistory => _history.Count > 0;

        #region Packet -> Script API

        public bool TryRequestNextMessage()
        {
            if (_currentMessageInHistory + 1 >= _history.Count)
            {
                // Okay, we are out of history entries, so stop using history and send the next line.
                ProvideClientResponse(null);

                return true;
            }

            _currentMessageInHistory++;

            return SendNpcUsedLine(_history[_currentMessageInHistory]);
        }

        public bool TryRequestPreviousMessage()
        {
            // Okay, so user requested to send the previous message
            // Figure out if we got any messages to send
            if (_currentMessageInHistory - 1 < 0)
            {
                Warn("User tried to request to send a previous message, even though there's no slot left");
                return false;
            }

            _currentMessageInHistory--;

            if (SendNpcUsedLine(_history[_currentMessageInHistory])) return true;

            Warn($"Unable to send message from history! Message index in history: {_currentMessageInHistory}");
            return false;
        }

        private bool SendNpcUsedLine(NpcUsedLine line)
        {
            LastSentType = NpcChatTypes.Simple;
            WaitingForResponse = true;
            NpcPacket.SendNPCChatTextSimple(chr, NpcID, line.Text, line.Type);
            return true;
        }

        #endregion


        #region Cross-thread Comms

        private readonly ManualResetEventSlim _scriptFinishedHandle = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _clientResponseProvided = new ManualResetEventSlim(false);
        private object _clientResponse;

        public void ProvideClientResponse(object response)
        {
            _clientResponse = response;
            if (DEBUG_SCRIPT_API) Debug($"Signalling script that data is set! Data {response}");
            _clientResponseProvided.Set();

            // Now wait for script to have processed the request

            WaitForClientInputRequest();

            if (_cts.IsCancellationRequested)
            {
                this.Dispose();
            }
        }

        public void WaitForClientInputRequest()
        {
            if (DEBUG_SCRIPT_API) Debug("Waiting for script to do something, or getting cancelled...");

            if (!_scriptFinishedHandle.Wait(MaxScriptExecutionTime, _cancellationToken))
            {
                Error("It took too long for the script to run. Cancelling execution and returning to MasterThread");
                Server.Instance.ServerTraceDiscordReporter.Enqueue($"Script {ScriptName} ran too long! Check code if everything is OK.");
                TerminateScript();
                return;
            }

            _scriptFinishedHandle.Reset();

            if (DEBUG_SCRIPT_API) Debug("Wait finished in WaitForClientInputRequest (script has done something and is waiting for a response)");
        }


        private T RequestClientResponse<T>()
        {
            RequestClientResponse();
            return (T)_clientResponse;
        }

        private void RequestClientResponse()
        {
            _scriptFinishedHandle.Set();

            if (DEBUG_SCRIPT_API) Debug("Waiting for Client Response");
            WaitingForResponse = true; // For use with the packet handler

            // Now wait for the user to provide a response
            _clientResponseProvided.Wait(_cancellationToken);

            _clientResponseProvided.Reset();

            if (DEBUG_SCRIPT_API) Debug("WaitHandle.WaitAny finished in RequestClientResponse");
        }

        #endregion

        #region Script API

        #region Dialog

        protected string JoinLines(params string[] lines) => string.Join(Newline, lines);

        protected void Next(params string[] lines) => SendSimple(NpcChatSimpleTypes.Next, lines);
        protected void BackNext(params string[] lines) => SendSimple(NpcChatSimpleTypes.BackNext, lines);
        protected void BackOK(params string[] lines) => SendSimple(NpcChatSimpleTypes.BackOK, lines);
        protected void OK(params string[] lines) => SendSimple(NpcChatSimpleTypes.OK, lines);

        private void SendSimple(NpcChatSimpleTypes type, params string[] lines)
        {
            var text = JoinLines(lines);

            // Reset history for those without a back button
            if (type == NpcChatSimpleTypes.Next ||
                type == NpcChatSimpleTypes.OK)
            {
                ResetMessageHistory();
            }

            PushMessageHistory(type, text);

            LastSentType = NpcChatTypes.Simple;
            NpcPacket.SendNPCChatTextSimple(chr, NpcID, text, type);

            RequestClientResponse();
        }

        protected const byte AskMenu_NoMenuItems = 0xFF;

        protected void AskMenuCallback(string mainMessage, params (string Item, Action Callback)[] menuItems)
        {
            AskMenuCallback(mainMessage, menuItems.Select(x => (x.Item, x.Callback != null, x.Callback)).ToArray());
        }

        protected void AskMenuCallback(string mainMessage, params (string Item, bool Enabled, Action Callback)[] menuItems)
        {
            var selection = AskMenu(
                mainMessage,
                menuItems.Select((tuple, index) => (index, tuple.Item, tuple.Enabled && tuple.Callback != null)).ToArray()
            );

            if (selection == AskMenu_NoMenuItems)
            {
                return;
            }

            menuItems[selection].Callback.Invoke();
        }

        protected int AskMenu(string mainMessage, params string[] menuItems)
        {
            return AskMenu(mainMessage, menuItems.Select((line, index) => ((int)index, line)).ToArray());
        }

        protected int AskMenu(string mainMessage, params (int Index, string Item)[] menuItems)
        {
            return AskMenu(mainMessage, menuItems.Select(x => (x.Index, x.Item, true)).ToArray());
        }

        protected int AskMenu(string mainMessage, params (int Index, string Item, bool Enabled)[] menuItems)
        {
            const int listOffset = 1;
            var formattedMessage = mainMessage;

            menuItems = menuItems.Where(x => x.Enabled).ToArray();

            if (menuItems.Length == 0)
            {
                // Just show OK with the message
                OK(mainMessage);
                return AskMenu_NoMenuItems;
            }

            foreach (var (idx, item, enabled) in menuItems)
            {
                formattedMessage += Newline;
                formattedMessage += $"#L{listOffset + idx}#{item}#l";
            }

            ResetMessageHistory();

            while (true)
            {
                LastSentType = NpcChatTypes.RequestMenu;

                NpcPacket.SendNPCChatTextMenu(
                    chr,
                    NpcID,
                    formattedMessage
                );

                var selectedIdx = RequestClientResponse<int>();

                foreach (var (Index, _, Enabled) in menuItems)
                {
                    if (listOffset + Index == selectedIdx)
                        return Index;
                }
            }
        }

        protected int AskMenu(string everything)
        {
            ResetMessageHistory();

            LastSentType = NpcChatTypes.RequestMenu;

            NpcPacket.SendNPCChatTextMenu(
                chr,
                NpcID,
                everything
            );

            return RequestClientResponse<int>();
        }

        protected bool AskYesNo(params string[] lines)
        {
            ResetMessageHistory();

            LastSentType = NpcChatTypes.RequestYesNo;
            NpcPacket.SendNPCChatTextYesNo(chr, NpcID, JoinLines(lines));

            return RequestClientResponse<bool>();
        }

        protected int AskStyle(IEnumerable<int> items, params string[] lines)
        {
            var processedItems = items.ToList();
            ResetMessageHistory();

            LastSentType = NpcChatTypes.RequestStyle;
            NpcPacket.SendNPCChatTextRequestStyle(chr, NpcID, JoinLines(lines), processedItems);

            var idx = RequestClientResponse<byte>();

            if (idx < 0 || processedItems.Count < idx)
                return 0;

            return processedItems[idx];
        }

        protected string AskText(string defaultText, short minLength, short maxLength, params string[] lines)
        {
            ResetMessageHistory();

            while (true)
            {
                LastSentType = NpcChatTypes.RequestText;
                NpcPacket.SendNPCChatTextRequestText(chr, NpcID, JoinLines(lines), defaultText, minLength, maxLength);

                var line = RequestClientResponse<string>();
                if (line.Length < minLength || line.Length > maxLength)
                {
                    // Try again
                    continue;
                }

                return line;
            }
        }

        protected int AskNumber(int defaultNumber, params string[] lines) =>
            AskInteger(defaultNumber, 0, int.MaxValue, lines);

        protected int AskInteger(int defaultNumber, int minimumValue, int maximumValue, params string[] lines)
        {
            ResetMessageHistory();

            while (true)
            {
                LastSentType = NpcChatTypes.RequestInteger;
                NpcPacket.SendNPCChatTextRequestInteger(chr, NpcID, JoinLines(lines), defaultNumber, minimumValue, maximumValue);

                var number = RequestClientResponse<int>();
                if (number < minimumValue || number > maximumValue)
                {
                    Warn($"Possible exploiting going on. User entered {number}, but it should be between {minimumValue} and {maximumValue}");
                    continue;
                }

                return number;
            }
        }

        /// <summary>
        /// Asks the player to select a dead pet
        /// </summary>
        /// <param name="lines">text</param>
        /// <returns>Returns 0 if no pet, or the cashid</returns>
        protected long AskPet(params string[] lines)
        {
            var pets = chr.Inventory.GetPetItems()
                .Where(x => x.IsDead)
                .Select(x => (x.CashId, (byte)x.InventorySlot))
                .ToArray();

            return AskPet(pets, JoinLines(lines));
        }

        /// <summary>
        /// Asks the player to select a pet, except for the one provided
        /// </summary>
        /// <param name="exceptId">Pet CashID of the excluded pet</param>
        /// <param name="lines">text</param>
        /// <returns>Returns 0 if no pet, or the cashid</returns>
        protected long AskPetAllExcept(long exceptId, params string[] lines)
        {
            var pets = chr.Inventory.GetPetItems()
                .Where(x => x.CashId != exceptId)
                .Select(x => (x.CashId, (byte)x.InventorySlot))
                .ToArray();

            return AskPet(pets, JoinLines(lines));
        }

        /// <summary>
        /// Keep on asking the user to select a pet
        /// </summary>
        /// <param name="pets"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        private long AskPet((long CashID, byte Slot)[] pets, string text)
        {
            // If there are no pets, don't even ask.
            if (pets.Length == 0) return 0;

            while (true)
            {
                ResetMessageHistory();

                LastSentType = NpcChatTypes.RequestPetID;

                NpcPacket.SendNPCChatTextRequestPet(chr, NpcID, text, pets);

                var cashId = RequestClientResponse<long>();

                if (pets.Exists(x => x.CashID == cashId))
                    return cashId;
            }
        }

        #region Text Formatting

        private static readonly CultureInfo AmericanCulture = new CultureInfo("en-US");

        public static string number(int number) => number.ToString("n0", AmericanCulture);
        public static string number(double number) => number.ToString("n", AmericanCulture);

        public static string itemIcon(int itemID) => $"#i{itemID}#";
        public static string itemName(int itemID) => $"#t{itemID}#";
        public static string itemIconAndName(int itemID) => $"{itemIcon(itemID)} {itemName(itemID)}";
        public static string itemCount(int itemID) => $"#c{itemID}#";
        public static string npcName(int npcID) => $"#p{npcID}#";
        public static string mapName(int mapID) => $"#m{mapID}#";
        public static string skillName(int skillID) => $"#q{skillID}#";
        public static string mobName(int mobID) => $"#o{mobID}#";

        public const string red = "#r";
        public const string black = "#k";
        public const string green = "#g";
        public const string blue = "#b";

        public const string bold = "#e";
        public const string notbold = "#n";

        protected const string QuestAvailable = "\r\n#r#eQUEST AVAILABLE#k#n#l";
        protected const string QuestInProgress = "\r\n#r#eQUEST IN PROGRESS#k#n#l";
        protected const string QuestThatCanBeCompleted = "\r\n#r#eQUEST THAT CAN BE COMPLETED#k#n#l";

        #endregion

        #endregion

        #region Manipulation

        /// <summary>
        /// Change the Item Variation for this NPC, to like Gachapon stats or drops.
        /// </summary>
        /// <param name="variation">The variation</param>
        protected void SetItemVariation(ItemVariation variation)
        {
            ItemVariation = variation;
        }

        /// <summary>
        /// Give items to the user
        /// </summary>
        /// <param name="itemID">The item ID you want to give</param>
        /// <param name="amount">The amount of the item you want to give.</param>
        /// <returns>True if it succeeded</returns>
        protected bool GiveItem(int itemID, short amount = 1)
        {
            return Exchange(0, itemID, (int)amount);
        }

        /// <summary>
        /// Takes away items from the player
        /// </summary>
        /// <param name="itemID">The item ID</param>
        /// <param name="amount">Amount you want to get rid of</param>
        /// <returns>True if it was successful</returns>
        protected bool TakeItem(int itemID, short amount = 1)
        {
            return GiveItem(itemID, (short)-amount);
        }

        /// <summary>
        /// Gives an amount of mesos to the user
        /// </summary>
        /// <param name="mesos"></param>
        /// <returns>True if it was successful</returns>
        protected bool GiveMesos(int mesos)
        {
            return Exchange(mesos);
        }

        protected bool TakeMesos(int mesos)
        {
            return GiveMesos(-mesos);
        }

        /// <summary>
        /// Performs exchanges like BMS
        /// </summary>
        /// <param name="mesos">Money</param>
        /// <param name="args">( ItemID, Count ) * n</param>
        /// <returns>true if exchange is successful, false if either inventory is full, don't have the items required, meso > max int, or don't have the mesos required</returns>
        protected bool Exchange(int mesos, params int[] args) =>
            chr.WrappedLogging(() => chr.Inventory.Exchange(this, mesos, args));


        /// <summary>
        /// Performs exchanges like BMS
        /// </summary>
        /// <param name="mesos">Money</param>
        /// <param name="args">( ItemOpt, Count ) * n</param>
        /// <returns>true if exchange is successful, false if either inventory is full, don't have the items required, meso > max int, or don't have the mesos required</returns>
        protected bool ExchangeEx(int mesos, params object[] args) =>
            chr.WrappedLogging(() => chr.Inventory.ExchangeEx(this, mesos, args));


        /// <summary>
        /// Creates a fluent API exchange object
        /// </summary>
        /// <returns>new Exchange object</returns>
        protected Exchange Exchange() =>
            chr.WrappedLogging(() => chr.Inventory.Exchange(this));

        protected void AddPoints(int amount) => chr.AddPoints(amount);

        protected Map Map(int? mapID = null) => MapProvider.Maps.TryGetValue(mapID ?? chr.MapID, out var map) ? map : null;
        
        protected int UserCount(int? mapID = null) => Map(mapID)?.Characters.Count ?? 0;
        
        protected int MobCount(int? mapID = null) => Map(mapID)?.Mobs.Count ?? 0;
        protected int MobCount(int mapID, int mobID) => Map(mapID)?.Mobs.Count(x => x.Value.MobID == mobID) ?? 0;

        protected int GetReactorState(int mapID, int pageID, int pieceID)
        {
            var reactor = Map(mapID)?.Reactors.Values.FirstOrDefault(x => x.PieceID == pieceID && x.PageID == pageID);
            if (reactor != null) return reactor.State;

            Error($"Could not find Reactor at {mapID} on page {pageID} and piece {pieceID}...");
            return -1;
        }

        protected void SetReactorState(int mapID, int pageID, int pieceID, int state)
        {
            var reactor = Map(mapID)?.Reactors.Values.FirstOrDefault(x => x.PieceID == pieceID && x.PageID == pageID);
            if (reactor != null)
            {
                reactor.SetState((byte)state);
            }
            else
            {
                Error($"Could not find Reactor at {mapID} on page {pageID} and piece {pieceID}...");
            }

        }

        public Portal Portal { get; set; }

        protected void AddBuff(int itemID) => chr.Buffs.AddItemBuff(itemID);
        protected void RemoveBuff(int itemID) => chr.Buffs.RemoveItemBuff(itemID);

        protected int Mesos => GetMesos();
        protected int GetMesos() => chr.Inventory.Mesos;

        protected byte Level => GetLevel();
        protected byte GetLevel() => chr.Level;

        protected short Job => GetJob();
        protected short GetJob() => chr.Job;
        protected void ChangeJob(short job) => chr.WrappedLogging(() => chr.SetJob(job));

        protected int MapID
        {
            get => chr.MapID;
            set => chr.WrappedLogging(() => chr.ChangeMap(value));
        }

        protected void ChangeMap(int mapid, string portalName = null)
        {
            chr.WrappedLogging(() =>
            {
                if (portalName == null) chr.ChangeMap(mapid);
                else chr.ChangeMap(mapid, portalName);
            });
        }

        protected short STR => chr.PrimaryStats.Str;
        protected short DEX => chr.PrimaryStats.Dex;
        protected short INT => chr.PrimaryStats.Int;
        protected short LUK => chr.PrimaryStats.Luk;
        protected short AP => chr.PrimaryStats.AP;
        protected short SP => chr.PrimaryStats.SP;

        protected void AddSTR(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.AddStr(amount));
        protected void AddDEX(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.AddDex(amount));
        protected void AddINT(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.AddInt(amount));
        protected void AddLUK(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.AddLuk(amount));
        protected void AddAP(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.AddAP(amount));
        protected void AddSP(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.AddSP(amount));


        protected short Fame => GetFame();
        protected short GetFame() => chr.PrimaryStats.Fame;
        protected void AddFame(short amount) => chr.WrappedLogging(() => chr.AddFame(amount));

        protected short MP => chr.PrimaryStats.MP;
        protected void SetMP(short amount) => chr.WrappedLogging(() => chr.ModifyMP(amount));
        protected short MaxMP => chr.PrimaryStats.MaxMP;
        protected void SetMaxMP(short amount) => chr.WrappedLogging(() => chr.SetMaxMP(amount));

        protected short HP => chr.PrimaryStats.HP;
        protected void SetHP(short amount) => chr.WrappedLogging(() => chr.ModifyHP(amount));
        protected void AddMaxMP(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.ModifyMaxMP(amount));
        protected short MaxHP => chr.PrimaryStats.MaxHP;
        protected void SetMaxHP(short amount) => chr.WrappedLogging(() => chr.SetMaxHP(amount));
        protected void AddMaxHP(short amount, byte onlyWhenFull = 0) => chr.WrappedLogging(() => chr.ModifyMaxHP(amount));

        protected PartyData Party => chr.Party;
        protected bool IsPartyLeader => Party?.Leader == chr?.ID;

        protected void AddInventorySlots(byte inventory, byte amount) =>
            chr.WrappedLogging(() =>
                chr.Inventory.SetInventorySlots(
                    inventory,
                    (byte)(chr.Inventory.GetInventorySlots(inventory) + amount)
                )
            );

        /// <summary>
        /// Figure out if there's slots free for an item ID.
        /// </summary>
        /// <param name="itemID">The item ID</param>
        /// <param name="amount">Amount of slots we want</param>
        /// <returns>True if there's a slot free</returns>
        protected bool HasSlotsFreeForItem(int itemID, short amount = 1) =>
            chr.Inventory.HasSlotsFreeForItem(itemID, amount);

        /// <summary>
        /// Get the amount of items that the character has with this item ID.
        /// </summary>
        /// <param name="itemID">The item ID</param>
        /// <returns>Amount of items</returns>
        protected int ItemCount(int itemID) => chr.Inventory.ItemCount(itemID);

        protected int SlotCount(byte inventory) => chr.Inventory.SlotCount(inventory);

        /// <summary>
        /// Adds EXP to the current character as a quest!
        /// </summary>
        /// <param name="exp">Amount of EXP to give</param>
        protected void AddEXP(uint exp) => chr.AddEXP(exp, true, true);

        protected int GetEquip(short slot, bool cash) => chr.Inventory.GetEquippedItemId(slot, cash);

        protected void Message(Character c, string message)
        {
            MessagePacket.ScriptNotice(c, message);
        }

        protected void Message(string message) => Message(chr, message);

        protected void Notice(string notice)
        {
            MessagePacket.SendText(MessagePacket.MessageTypes.Notice, notice, null,
                MessagePacket.MessageMode.ToChannel);
        }


        protected void QuestEndEffect()
        {
            MapPacket.SendQuestClearEffect(chr);
        }

        protected bool CharacterInArea(Character chr, string area) => chr.Field.CharacterInArea(chr, area);

        protected int GetUsersInArea(string area) => Map().GetCharactersInMapArea(area).Count;


        protected void AddEXP(int exp)
        {
            if (exp > 0) chr.WrappedLogging(() => AddEXP((uint)exp));
        }

        protected int Random(int min, int max)
        {
            var random = new Random();
            return random.Next(min, max);
        }

        /// <summary>
        /// Get a random element out of the supplied list of elements. Throws exception if list is empty
        /// </summary>
        /// <param name="elements"></param>
        /// <returns>Random element out of the supplied elements</returns>
        protected int Random(IEnumerable<int> elements)
        {
            var arr = elements.ToArray();
            return arr[Random(0, arr.Length)];
        }

        protected bool StartFieldSetManually(string name)
        {
            if (!FieldSet.Instances.TryGetValue(name, out var fs))
            {
                Error($"Unable to find FieldSet {name}");
                return false;
            }

            fs.StartFieldSetManually();
            return true;
        }
        
        #region Guild

        protected bool CreateGuild(string name, Character[] partyMembers, int price)
        {
            if (Server.Instance.GetGuild(name) != null)
            {
                Error($"Unable to register guild {name}, already exists.");
                return false;
            }
            
            if (!Exchange(price))
            {
                Error("Unable to rename guild, not enough mesos.");
                return false;
            }

            GuildPacket.CreateGuild(name, chr, partyMembers);
            return true;
        }

        protected bool RenameGuild(string newName, int price)
        {
            if (Server.Instance.GetGuild(newName) != null)
            {
                Error($"Unable to rename guild to {newName}, already exists.");
                return false;
            }

            if (!Exchange(price))
            {
                Error("Unable to rename guild, not enough mesos.");
                return false;
            }

            chr.Guild.RenameGuild(chr.ID, newName);

            return true;
        }
        
        protected bool DisbandGuild(int price)
        {
            if (!Exchange(price))
            {
                Error("Unable to disband guild, not enough mesos.");
                return false;
            }

            chr.Guild.DisbandGuild(chr.ID);
            return true;
        }

        protected bool ResizeGuild(byte newSize, int price)
        {
            if (!Exchange(price))
            {
                Error("Unable to resize guild, not enough mesos.");
                return false;
            }

            chr.Guild.ResizeGuild(chr.ID, newSize);
            return true;
        }

        protected bool SetGuildMark(int price)
        {
            // We do not remove mesos directly, only after the user has actually applied a change
            if (Mesos < price)
            {
                Error("Unable to set guild mark, not enough mesos.");
                return false;
            }

            chr.Guild.OpenChangeLogo(price);
            return true;
        }

        protected bool RemoveGuildMark(int price)
        {
            // We do not remove mesos directly, only after the user has actually applied a change
            if (Mesos < price)
            {
                Error("Unable to remove guild mark, not enough mesos.");
                return false;
            }

            if (!chr.Guild?.HasGuildMark ?? true)
            {
                Error("Unable to remove guild mark, theres no guild mark set.");
                return false;
            }
            
            chr.Guild.ChangeLogo(chr.ID, new GuildLogo
            {
                Background = 0,
                BackgroundColor = 0,
                Foreground = 0,
                ForegroundColor = 0,
            });
            Exchange(-price);
            return true;
        }

        #endregion

        /// <summary>
        /// Check if the supplied itemID is actual item.
        /// </summary>
        /// <param name="itemID">The item ID</param>
        /// <returns>True if an item with the supplied ID exists</returns>
        protected bool IsKnownItem(int itemID)
        {
            return DataProvider.HasItem(itemID);
        }

        protected int GetMonsterBookCount(int cardId)
        {
            if (cardId > 2380000) cardId -= 2380000;
            chr.MonsterBook.Cards.TryGetValue(cardId, out var cardsInStack);
            return cardsInStack;
        }

        protected void GiveMonsterBookCard(int cardId)
        {
            chr.WrappedLogging(() => chr.MonsterBook.TryAddCard(cardId));
        }

        /// <summary>
        /// Increase the amount of buddies a user can have in their buddylist.
        /// </summary>
        /// <param name="slots">Amount of slots to add</param>
        /// <param name="mesosDiff">How your mesos is affected. Put in a negative number (BMS-like) to subtract.</param>
        protected bool IncFriendMax(byte slots, int mesosDiff)
        {
            if (chr.PrimaryStats.BuddyListCapacity + slots > Constants.MaxBuddyListCapacity)
            {
                Warn("Buddylist capacity limit reached");
                return false;
            }

            if (!Exchange(mesosDiff))
            {
                Warn("Not enough mesos for buddylist expansion");
                return false;
            }

            chr.IncreaseBuddySlots(slots);
            return true;
        }

        protected void ToggleUI(Constants.UIType type, bool open)
        {
            CfgPacket.ToggleUI(chr, type, open);
        }

        protected void ToggleUI(string type, bool open)
        {
            if (Enum.TryParse(typeof(Constants.UIType), type, true, out var ttype))
            {
                ToggleUI((Constants.UIType)ttype, open);
            }
            else
            {
                Error($"Unknown type {type} for ToggleUI");
            }
        }

        /// <summary>
        /// Get the Quest Data from the Quest ID.
        /// </summary>
        /// <param name="questID">The Quest ID</param>
        /// <returns>the data</returns>
        protected string GetQuestData(int questID) =>
            GetQuestData(chr, questID, "");


        protected string GetQuestData(Character chr, int questID) =>
            GetQuestData(chr, questID, "");

        protected T GetQuestData<T>(int questID, T fallback = default) =>
            GetQuestData<T>(chr, questID, fallback);

        protected T GetQuestData<T>(Character chr, int questID, T fallback = default)
        {
            if (!chr.Quests.HasQuest(questID)) return fallback;

            var qd = chr.Quests.GetQuestData(questID);

            if (typeof(T) == typeof(string)) return (T)(object)qd;

            if (typeof(T) == typeof(DateTime))
                return (T)(object)DateTime.FromFileTimeUtc(long.Parse(qd));

            if (typeof(T) == typeof(int)) return (T)(object)(qd == "" ? 0 : int.Parse(qd));
            if (typeof(T) == typeof(long)) return (T)(object)(qd == "" ? 0 : long.Parse(qd));
            if (typeof(T) == typeof(short)) return (T)(object)(qd == "" ? 0 : short.Parse(qd));

            throw new ArgumentException("Unsupported type", nameof(T));
        }


        /// <summary>
        /// Set (or make!) a quest in the data.
        /// </summary>
        /// <param name="questID">The Quest ID</param>
        /// <param name="data">The data</param>
        protected void SetQuestData(int questID, object data) =>
            SetQuestData(chr, questID, data);

        protected void SetQuestData(Character chr, int questID, object data)
        {
            string d;
            switch (data)
            {
                case DateTime dt:
                    d = dt.ToFileTimeUtc().ToString();
                    break;
                case string s:
                    d = s;
                    break;
                default:
                    d = data.ToString();
                    break;
            }

            chr.Quests.SetQuestData(questID, d);
        }

        protected void SetRepeatQuest(int questID)
        {
            SetQuestData(questID, DateTime.Now);
        }

        protected TimeSpan TimeSinceQuest(int questID)
        {
            return DateTime.Now - GetQuestData(questID, DateTime.MinValue);
        }

        protected bool CanRepeatQuest(int questID, int repetitionTimeSeconds)
        {
            return TimeSinceQuest(questID) >= TimeSpan.FromSeconds(repetitionTimeSeconds);
        }

        protected void StartWeather(int itemID, int runlengthSeconds)
        {
            chr.Field.MakeWeatherEffect(itemID, "", TimeSpan.FromSeconds(runlengthSeconds), true);
        }

        protected void RemoveQuest(int questID) => chr.Quests.RemoveQuest(questID);

        #region VarGet/Set for NPC

        protected Map GetMap(int fieldID)
        {
            if (!MapProvider.Maps.TryGetValue(fieldID, out var field))
            {
                Error($"Unable to find field {fieldID}");
            }

            return field;
        }

        protected NpcLife GetNpc(int fieldID, int npcID)
        {
            if (MapProvider.Maps.TryGetValue(fieldID, out var field))
            {
                return field.GetNpcByTemplate(npcID);
            }

            Error($"Did not find NPC {npcID} in map {fieldID}");

            return null;
        }

        // Get a variable from an NPC in a specific map
        protected string GetNpcVar(int fieldID, int npcID, string varName, string defaultValue = "")
        {
            return GetMap(fieldID)?.GetNpcVar(npcID, varName, defaultValue);
        }

        // Get a variable from the current npc in the current map.
        // Will always? return null if not talking to a NPC
        protected string GetNpcVar(string varName, string defaultValue = "") => GetNpcVar(chr.Field.ID, NpcID, varName, defaultValue);

        protected void SetNpcVar(int fieldID, int npcID, string varName, string value)
        {
            GetMap(fieldID)?.SetNpcVar(npcID, varName, value);
        }

        protected void SetNpcVar(string varName, string value) => SetNpcVar(chr.Field.ID, NpcID, varName, value);

        #endregion

        #region VarSet/Get for Server
        protected string GetServerVar(string varName)
        {
            Server.Instance.Vars.TryGetValue(varName, out var value);
            return value;
        }

        protected void SetServerVar(string varName, string value)
        {
            Debug($"Set ServerVar {varName} to {value}");
            Server.Instance.Vars[varName] = value;
        }

        #endregion

        #region VarSet/Get for Fieldsets
        protected string GetFieldsetVar(string varName, string fieldsetName = null)
        {
            fieldsetName ??= chr.Field.ParentFieldSet?.Name;
            if (fieldsetName == null)
            {
                Error($"Unable to get fieldset from current field ({chr.Field.ID})");
                return null;
            }

            var fs = FieldSet.Get(fieldsetName);
            if (fs == null)
            {
                Error($"Unable to get fieldset named '{fieldsetName}'");
                return null;
            }

            fs.Variables.TryGetValue(varName, out var value);
            return value;
        }

        protected void SetFieldsetVar(string varName, string value, string fieldsetName = null)
        {
            fieldsetName ??= chr.Field.ParentFieldSet?.Name;
            if (fieldsetName == null)
            {
                Error($"Unable to get fieldset from current field ({chr.Field.ID})");
                return;
            }
            
            var fs = FieldSet.Get(fieldsetName);
            if (fs == null)
            {
                Error($"Unable to get fieldset named '{fieldsetName}'");
                return;
            }

            Debug($"Set Fieldset '{fs.Name}' Var {varName} to {value}");
            fs.Variables[varName] = value;
        }

        #endregion

        #region Pet stuff

        protected bool SetPetLife(long cashId, int waterOfLifeItemID, int lifeScrollItemID)
        {
            if (ItemCount(waterOfLifeItemID) == 0 ||
                ItemCount(lifeScrollItemID) == 0)
            {
                Error($"SetPetLife: Not enough Water Of Life {waterOfLifeItemID} or Life Scrolls {lifeScrollItemID}");
                return false;
            }

            if (!DataProvider.Items.TryGetValue(waterOfLifeItemID, out var wolData))
            {
                Error($"SetPetLife: Could not find Water Of Life item data for itemid {waterOfLifeItemID}");
                return false;
            }

            var petItem = chr.Inventory.GetItemByCashID(cashId, 5) as PetItem;
            if (petItem == null)
            {
                Error($"SetPetLife: Could not find petItem for cash SN {cashId}");
                return false;
            }

            if (!petItem.IsDead)
            {
                Warn("SetPetLife: Pet isn't dead.");
                return false;
            }

            if (!Exchange(0, waterOfLifeItemID, -1, lifeScrollItemID, -1))
            {
                Error("SetPetLife: Unable to take Water Of Life and Life Scroll");
                return false;
            }


            petItem.DeadDate = Tools.GetDateExpireFromPeriodDays(wolData.PetLife);
            InventoryPacket.UpdateItems(chr, petItem);

            Info($"SetPetLife: Brought pet back to life, cash sn: {cashId}");


            return true;
        }

        /// <summary>
        /// Returns active pet name, otherwise "".
        /// </summary>
        protected string PetName
        {
            get
            {
                var activePet = chr.GetSpawnedPet();
                if (activePet == null) return "";
                return activePet.Name;
            }
        }

        protected void AddCloseness(int amount)
        {
            chr.WrappedLogging(() =>
            {
                var activePet = chr.GetSpawnedPet();
                if (activePet == null)
                {
                    Error("AddCloseness without active pet");
                    return;
                }

                Info($"Increasing pet {activePet.CashId} closeness with {amount}");
                Pet.IncreaseCloseness(chr, activePet, (short)amount);
            });
        }

        #endregion


        protected int WeekOfYear => AmericanCulture.Calendar.GetWeekOfYear(MasterThread.CurrentDate, AmericanCulture.DateTimeFormat.CalendarWeekRule, AmericanCulture.DateTimeFormat.FirstDayOfWeek);
        protected int DayOfYear => AmericanCulture.Calendar.GetDayOfYear(MasterThread.CurrentDate);

        protected ChineseLunisolarCalendar ChineseCalendar = new();
        protected string[] Zodiacs = { "Rat", "Ox", "Tiger", "Rabbit", "Dragon", "Snake", "Horse", "Goat", "Monkey", "Rooster", "Dog", "Pig" };

        protected bool eventActive(string eventName) => EventDateMan.IsEventActive(eventName);
        protected bool eventDone(string eventName) => EventDateMan.IsEventDone(eventName);

        protected DateTime eventStart(string eventName)
        {
            var tuple = EventDateMan.GetDateTupleForEvent(eventName);
            if (tuple == null) return DateTime.MaxValue;
            return tuple.Value.startDate;
        }
        protected DateTime eventEnd(string eventName)
        {
            var tuple = EventDateMan.GetDateTupleForEvent(eventName);
            if (tuple == null) return DateTime.MaxValue;
            return tuple.Value.endDate;
        }

        protected string getZodiac(DateTime date, out int terrestrialBranch)
        {
            int sexagenaryYear = ChineseCalendar.GetSexagenaryYear(date);
            terrestrialBranch = ChineseCalendar.GetTerrestrialBranch(sexagenaryYear);
            return Zodiacs[terrestrialBranch - 1];
        }

        protected bool isEventDate(string eventName)
        {
            return isEventDate(eventName, out var _, out var __);
        }
        protected bool isEventDate(string eventName, out DateTime startDate, out DateTime endDate)
        {
            var now = DateTime.UtcNow;

            if (eventName == "newyear")
            {
                int newYear = now.Month == 12 ? now.Year + 1 : now.Year;
                startDate = DateTime.Parse(newYear - 1 + "-12-31");
                endDate = DateTime.Parse(newYear + "-01-02");
            }
            else if (eventName == "pride")
            {
                startDate = DateTime.Parse(now.Year + "-06-01");
                endDate = DateTime.Parse(now.Year + "-07-01");
            }
            else if (eventName == "summer")
            {
                startDate = DateTime.Parse(now.Year + "-08-12");
                endDate = DateTime.Parse(now.Year + "-09-01");
            }
            else if (eventName == "lunarnewyear")
            {
                startDate = ChineseCalendar.ToDateTime(now.Year, 1, 1, 0, 0, 0, 0);
                endDate = startDate.AddDays(16);
            }
            else
            {
                startDate = DateTime.UnixEpoch;
                endDate = DateTime.UnixEpoch;
                return false;
            }
            return startDate <= now && now < endDate;
        }
        #endregion

        #region Logging

        protected void WrappedLogging(Action cb)
        {
            if (chr != null)
            {
                chr.WrappedLogging(cb);
            }
            else
            {
                var oldScriptName = ThreadContext.Properties["NpcScript"];

                try
                {
                    cb();
                }
                finally
                {
                    ThreadContext.Properties["NpcScript"] = oldScriptName;
                }
            }
        }

        protected void Error(string msg)
        {
            if (!_log.IsErrorEnabled) return;
            WrappedLogging(() =>
            {
                _log.Error(msg);
            });
        }
        
        protected void Error(string msg, Exception ex)
        {
            if (!_log.IsErrorEnabled) return;
            WrappedLogging(() =>
            {
                _log.Error(msg, ex);
            });
        }
        protected void Info(string msg)
        {
            if (!_log.IsInfoEnabled) return;
            WrappedLogging(() =>
            {
                _log.Info(msg);
            });
        }
        protected void Debug(string msg)
        {
            if (!_log.IsDebugEnabled) return;
            WrappedLogging(() =>
            {
                _log.Debug(msg);
            });
        }
        protected void Warn(string msg)
        {
            if (!_log.IsWarnEnabled) return;
            WrappedLogging(() =>
            {
                _log.Warn(msg);
            });
        }

        #endregion

        #region ScriptVM Support

        protected class SelfObj
        {
            private IScriptV2 _parent;
            public SelfObj(IScriptV2 parent) => _parent = parent;

            public void say(string msg, bool canGoBack = true)
            {
                if (!canGoBack)
                    _parent.OK(msg);
                else if (!_parent.HasHistory)
                    _parent.Next(msg);
                else
                    _parent.BackNext(msg);
            }

            public void say(params string[] msg) => say(string.Join(Newline, msg), true);

            public int askYesNo(string msg) => _parent.AskYesNo(msg) ? 1 : 0;
            public int askMenu(string msg) => _parent.AskMenu(msg);
            public int askNumber(string msg, int min, int max, int def) => _parent.AskInteger(def, min, max, msg);

            public void ok(string msg) => _parent.OK(msg);


            private FieldObj _field;
            public FieldObj field => _field ??= new FieldObj(_parent);
        }

        protected class InventoryObj
        {
            private IScriptV2 _parent;
            public InventoryObj(IScriptV2 parent) => _parent = parent;

            public int itemCount(int itemId) => _parent.ItemCount(itemId);

            public int exchange(int mesos, params int[] args) => _parent.Exchange(mesos, args) ? 1 : 0;
        }

        protected class QuestRecordObj
        {
            private CharacterQuests _parent;
            public QuestRecordObj(CharacterQuests parent) => _parent = parent;

            public void set(int questId, string value) => _parent.SetQuestData(questId, value);
            public string get(int questId) => _parent.GetQuestData(questId);
            public void remove(int questId) => _parent.RemoveQuest(questId);
        }


        protected class FieldObj
        {
            private IScriptV2 _parent;
            public FieldObj(IScriptV2 parent) => _parent = parent;

            public int id => _parent.MapID;
        }

        protected class TargetObj
        {
            private IScriptV2 _parent;
            public TargetObj(IScriptV2 parent) => _parent = parent;


            private InventoryObj _inventory;
            public InventoryObj inventory => _inventory ??= new InventoryObj(_parent);

            private QuestRecordObj _qr;
            public QuestRecordObj qr => _qr ??= new QuestRecordObj(_parent.chr.Quests);
        }

        private SelfObj _self;
        protected SelfObj self => _self ??= new SelfObj(this);


        private TargetObj _target;
        protected TargetObj target => _target ??= new TargetObj(this);


        protected void registerTransferField(int field, string portal) => ChangeMap(field, portal == "" ? null : portal);

        #endregion

        #endregion

        public abstract void Run();

        public void Dispose()
        {
            Debug("Disposing script");
            _executionThread = null;

            if (chr != null)
                chr.NpcSession = null;
            chr = null;

            _scriptFinishedHandle?.Dispose();
            _clientResponseProvided?.Dispose();
        }
    }
}
