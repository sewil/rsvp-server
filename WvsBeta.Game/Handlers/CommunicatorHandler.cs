using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;

namespace WvsBeta.Game.Handlers
{
    public static class CommunicatorHandler
    {
        private static ILog _log = LogManager.GetLogger(typeof(CommunicatorHandler));
        private static ILog _chatLog = LogManager.GetLogger("CommunicatorChatLog");

        internal enum Opcodes
        {
            SendChatMessage,
            OnUserChat,
            CommunicatorData,
        }

        private static Dictionary<string, Packet> _communicatorData = new Dictionary<string, Packet>();

        public static Packet WriteCommunicationData(string lang, int codepage)
        {
            var communicatorDataKey = $"{lang}_{codepage}";

            if (_communicatorData.TryGetValue(communicatorDataKey, out var p))
            {
                return p;
            }

            var compressedPacket = new Packet(CfgServerMessages.CFG_COMMUNICATOR);
            compressedPacket.WriteByte(Opcodes.CommunicatorData);

            p = new Packet();

            string GetStringForUser(Dictionary<string, string> vals)
            {
                string str;
                if (vals.TryGetValue($"{lang}_{codepage}", out str)) return str;
                if (vals.TryGetValue(lang, out str)) return str;
                if (vals.TryGetValue("en", out str)) return str;
                return "???";
            }

            var selectors = CommunicatorProvider.Selectors;
            var messageTypes = CommunicatorProvider.MessageTypes;
            var encoding = Encoding.UTF8;
            
            p.WriteByte((byte)selectors.Count);
            foreach (var selector in selectors.Values)
            {
                p.WriteByte(selector.ID);
                p.WriteByte(selector.CategoryType);

                p.WriteByte((byte)selector.Categories.Count);
                foreach (var cat in selector.Categories.Values)
                {
                    p.WriteByte(cat.ID);
                    p.WriteString(GetStringForUser(cat.Names), encoding);
                    
                    p.WriteShort((short)cat.Options.Count);
                    foreach (var opt in cat.Options)
                    {
                        p.WriteShort(opt.Key);
                        p.WriteString(GetStringForUser(opt.Value), encoding);
                    }
                }
            }

            p.WriteByte((byte)messageTypes.Count);
            foreach (var messageType in messageTypes.Values)
            {
                p.WriteByte(messageType.ID);
                p.WriteString(GetStringForUser(messageType.Names), encoding);

                p.WriteByte((byte)messageType.Messages.Count);
                foreach (var msg in messageType.Messages.Values)
                {
                    p.WriteByte(msg.ID);
                    p.WriteByte(msg.SelectorID);
                    p.WriteString(GetStringForUser(msg.Lines), encoding);
                }
            }

            p.GzipCompress(compressedPacket);


            _communicatorData[communicatorDataKey] = compressedPacket;
            return compressedPacket;
        }


        public static void OnPacket(Character chr, Packet packet)
        {
            switch (packet.ReadByte<Opcodes>())
            {
                case Opcodes.SendChatMessage:
                    {
                        if (MessagePacket.TrySendCannotChatMessage(chr))
                        {
                            return;
                        }

                        var messageTypeID = packet.ReadByte();
                        var messageID = packet.ReadByte();
                        var data = packet.ReadString();
                        var emote = packet.ReadInt();
                        
                        var messageTypes = CommunicatorProvider.MessageTypes;

                        if (!messageTypes.TryGetValue(messageTypeID, out var mt))
                        {
                            _chatLog.Error($"User sent message with unknown messageTypeID {messageTypeID}");
                            return;
                        }

                        if (!mt.Messages.TryGetValue(messageID, out var msg))
                        {
                            _chatLog.Error($"User sent message with unknown message {messageID} in messageTypeID {messageTypeID}");
                            return;
                        }

                        var loggingText = msg.LogText.Replace("%s", data);

                        _chatLog.Info($"{chr.VisibleName}: {loggingText}");

                        var p = new Packet(CfgServerMessages.CFG_COMMUNICATOR);
                        p.WriteByte(Opcodes.OnUserChat);
                        p.WriteInt(chr.ID);
                        p.WriteByte(msg.MessageTypeID);
                        p.WriteByte(msg.ID);
                        p.WriteString(data);
                        p.WriteInt(emote);
                        chr.Field.SendPacket(p);

                        break;
                    }
            }
        }
    }
}
