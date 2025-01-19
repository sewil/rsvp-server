using System;
using System.Collections.Generic;

namespace WvsBeta.Game
{
    public class NpcUsedLine
    {
        public NpcChatSimpleTypes Type { get; set; }
        public string Text { get; set; }
        public NpcUsedLine(NpcChatSimpleTypes what, string text)
        {
            Type = what;
            Text = text;
        }

    }

    public enum NpcChatSimpleTypes
    {
        Next = 0,
        BackNext = 1,
        BackOK = 2,
        OK = 3,
    }

    public enum NpcChatTypes : byte
    {
        INVALID = 0xFF,
        Simple = 0,
        RequestYesNo = 1,
        RequestText = 2,
        RequestInteger = 3,
        RequestMenu = 4,
        RequestStyle = 5,
        RequestPetID = 6,
    }

    public class NpcChatSession : IHost
    {
        public int mID { get; set; }
        public Character mCharacter { get; set; }
        private INpcScript _compiledScript = null;

        private List<NpcUsedLine> mLines { get; set; } = new List<NpcUsedLine>();
        private Dictionary<string, object> _savedObjects = new Dictionary<string, object>();

        private byte mState { get; set; } = 0;
        public NpcChatTypes mLastSentType { get; set; }
        public byte mRealState { get; set; }
        public bool WaitingForResponse { get; set; }

        public NpcChatSession(int id, Character chr)
        {
            mID = id;
            mCharacter = chr;
            mCharacter.NpcSession = this;
        }
                
        public static void Start(int npcId, INpcScript NPC, Character chr)
        {
            if (NPC == null) return;

            if (chr.NpcSession != null)
                return;

            var session = new NpcChatSession(npcId, chr);
            session.SetScript(Scripting.CreateClone(NPC));
            session.HandleThing();
        }

        public INpcScript CompiledScript
        {
            get { return _compiledScript; }
        }

        public void SetScript(INpcScript script)
        {
            _compiledScript = script;
        }

        public void HandleThing(byte state = 0, byte action = 0, string text = "", int integer = 0)
        {
            _compiledScript.Run(this, mCharacter, state, action, text, integer);
        }

        public void Stop()
        {
            WaitingForResponse = false;
            mCharacter.NpcSession = null;
            _compiledScript = null;
        }

        public void SendPreviousMessage()
        {
            if (mState == 0 || mLines.Count == 0) return;
            mState--;
            if (mLines.Count < mState) return;

            WaitingForResponse = true;
            NpcUsedLine line = mLines[mState];
            mLastSentType = NpcChatTypes.Simple;
            NpcPacket.SendNPCChatTextSimple(mCharacter, mID, line.Text, line.Type);
        }

        public void SendNextMessage()
        {
            //Program.MainForm.LogAppend("SENDNEXTMESSAGE START");
            if (mLines.Count == mState + 1)
            {
                HandleThing(mRealState, 0, "", 0);
            }
            else
            {
                mState++;
                if (mLines.Count < mState) return;

                WaitingForResponse = true;
                NpcUsedLine line = mLines[mState];
                mLastSentType = NpcChatTypes.Simple;
                NpcPacket.SendNPCChatTextSimple(mCharacter, mID, line.Text, line.Type);
            }
        }

        public void SendNext(string Message)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            // First line, always clear
            mLines.Clear();
            mLines.Add(new NpcUsedLine(NpcChatSimpleTypes.Next, Message));
            mState = 0;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.Simple;
            NpcPacket.SendNPCChatTextSimple(mCharacter, mID, Message, NpcChatSimpleTypes.Next);
        }

        public void SendBackNext(string Message)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mLines.Add(new NpcUsedLine(NpcChatSimpleTypes.BackNext, Message));
            mState++;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.Simple;
            NpcPacket.SendNPCChatTextSimple(mCharacter, mID, Message, NpcChatSimpleTypes.BackNext);
        }

        public void SendBackOK(string Message)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mLines.Add(new NpcUsedLine(NpcChatSimpleTypes.BackOK, Message));
            mState++;
            mRealState++;
            mLastSentType = NpcChatTypes.Simple;
            WaitingForResponse = true;
            NpcPacket.SendNPCChatTextSimple(mCharacter, mID, Message, NpcChatSimpleTypes.BackOK);
        }

        public void SendOK(string Message)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mLines.Clear();
            mLines.Add(new NpcUsedLine(NpcChatSimpleTypes.OK, Message));
            mState = 0;
            mRealState++;
            mLastSentType = NpcChatTypes.Simple;
            WaitingForResponse = true;
            NpcPacket.SendNPCChatTextSimple(mCharacter, mID, Message, NpcChatSimpleTypes.OK);
        }

        public void AskMenu(string Message)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mState = 0;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.RequestMenu;
            NpcPacket.SendNPCChatTextMenu(mCharacter, mID, Message);
        }

        public void AskYesNo(string Message)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mState = 0;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.RequestYesNo;
            NpcPacket.SendNPCChatTextYesNo(mCharacter, mID, Message);
        }

        public void AskText(string Message, string Default, short MinLength, short MaxLength)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mState = 0;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.RequestText;
            NpcPacket.SendNPCChatTextRequestText(mCharacter, mID, Message, Default, MinLength, MaxLength);
        }

        public void AskInteger(string Message, int Default, int MinValue, int MaxValue)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mState = 0;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.RequestInteger;
            NpcPacket.SendNPCChatTextRequestInteger(mCharacter, mID, Message, Default, MinValue, MaxValue);
        }

        public void AskStyle(string Message, List<int> Values)
        {
            if (mCharacter.NpcSession == null) throw new Exception("NpcSession has been nulled already!!!!");

            mState = 0;
            mRealState++;
            WaitingForResponse = true;
            mLastSentType = NpcChatTypes.RequestStyle;
            NpcPacket.SendNPCChatTextRequestStyle(mCharacter, mID, Message, Values);
        }

        public object GetSessionValue(string pName)
        {
            if (_savedObjects.ContainsKey(pName)) return _savedObjects[pName];
            return null;
        }

        public void SetSessionValue(string pName, object pValue)
        {
            if (!_savedObjects.ContainsKey(pName))
                _savedObjects.Add(pName, pValue);
            else
                _savedObjects[pName] = pValue;
        }

        public void SendErrorMessage(int npcId, int State)
        {
            Server.Instance.ServerTraceDiscordReporter.Enqueue($"NPC id {npcId} has an issue in state {State}");
            // ??
        }

        public void GetQuestIsAvailableFormat(string title, string questName, int idx)
        {
            AskMenu($"{title}\r\n\r\n#r#eQUEST AVAILABLE#k#n#l\r\n#L{idx}##b{questName}#k#l");
        }

        public void GetQuestIsCompletedFormat(string title, string questName, int idx)
        {
            AskMenu($"{title}\r\n\r\n#r#eQUEST IN PROGRESS#k#n#l\r\n#L{idx}##b{questName} (In Progress)#k#l");
        }

        public void GetQuestIsInProgressFormat(string title, string questName, int idx)
        {
            AskMenu($"{title}\r\n\r\n#r#eQUEST THAT CAN BE COMPLETED#k#n#l\r\n#L{idx}##b{questName} (Ready to complete.)#k#l");
        }
    }
}