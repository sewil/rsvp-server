using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using WvsBeta.Game.GameObjects;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public static class NpcPacket
    {
        private static ILog _log = LogManager.GetLogger(typeof(NpcPacket));


        private static IScriptV2 _sampleScript = new Sample();

        public static void HandleStartNpcChat(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            var npcMapID = packet.ReadInt();
            var Npc = chr.Field.GetNPC(npcMapID);

            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Tried to chat to npc while not able to attach additional process"))
            {
                return;
            }

            // Npc doesnt exist
            if (Npc == null)
            {
                _log.Error($"Player is chatting with an NPC that we don't have in our data! NPC 'map id' {npcMapID}");
                return;
            }

            var RealID = Npc.ID;
            if (!DataProvider.NPCs.TryGetValue(RealID, out var npc))
            {
                _log.Error("Found NPC that we don't have data for???");
                return;
            }

            if (npc.Shop.Count > 0)
            {
                // It's a shop!
                chr.ShopNPCID = RealID;
                SendShowNPCShop(chr, RealID);
                return;
            }

            if (npc.Trunk > 0)
            {
                chr.TrunkNPCID = RealID;
                chr.Storage.Load();
                StoragePacket.SendShowStorage(chr, chr.TrunkNPCID);
                return;
            }


            Action<string> errorHandlerFnc = null;
            if (chr.IsGM)
            {
                errorHandlerFnc = (script) =>
                {
                    MessagePacket.SendNotice(chr, "Error compiling script '" + script + "'!");
                };
            }

            object NPC = null;
            if (NPC == null && npc.Quest != null) NPC = Server.Instance.TryGetOrCompileScript(npc.Quest, errorHandlerFnc);
            if (NPC == null)
            {
                NPC = Server.Instance.TryGetOrCompileScript(npc.ID.ToString(), errorHandlerFnc);
                if (NPC != null && npc.Quest != null)
                {
                    if (chr.IsGM)
                    {
                        MessagePacket.SendNotice(chr, "[CLEANUP] Started npc chat by ID {0} while quest '{1}' exists!", npc.ID, npc.Quest);
                    }
                }
            }

            if (NPC == null)
            {
                if (chr.IsGM)
                {
                    MessagePacket.SendNotice(chr, "Unable to find NPC script '{1}' for NPC {0}!", npc.ID, npc.Quest);
                }

                return;
            }

            if (!StartScript(chr, RealID, NPC))
            {
            }
        }

        public static bool StartScript(Character chr, int npcID, object NPC, int portalID = 0)
        {
            if (chr.NpcSession != null)
            {
                _log.Error("Trying to start NPC chat while there's already one going on!");
                return false;
            }

            if (NPC == null)
            {
                return false;
            }

            switch (NPC)
            {
                case INpcScript script:
                    NpcChatSession.Start(npcID, script, chr);
                    break;
                case IScriptV2 v2Script:

                    var scriptName = v2Script.ScriptName;
                    v2Script = (IScriptV2) Activator.CreateInstance(v2Script.GetType());
                    if (v2Script == null)
                    {
                        _log.Error("Unable to create instance of v2script?");
                        return false;
                    }
                    v2Script.ScriptName = scriptName;
                    v2Script.PortalID = portalID;
                    
                    chr.NpcSession = v2Script;
                    v2Script.Setup(chr, npcID);
                    v2Script.StartScript();
                    break;
                default:
                    _log.Error($"Unable to start NPC {NPC}, because the type is unknown to us.");
                    return false;
            }

            return true;
        }

        public static bool StartScript(Character chr, string scriptName, int npcID = 9900000, int portalID = 0)
        {
            if (chr.NpcSession != null)
            {
                _log.Error("Trying to start NPC chat while there's already one going on!");
                return false;
            }


            Action<string> errorHandlerFnc = null;
            if (chr.IsGM)
            {
                errorHandlerFnc = (script) =>
                {
                    MessagePacket.SendNotice(chr, "Error compiling script '" + script + "'!");
                };
            }

            var NPC = Server.Instance.TryGetOrCompileScript(scriptName, errorHandlerFnc);

            if (NPC == null)
            {
                if (chr.IsGM)
                {
                    MessagePacket.SendNotice(chr, "Unable to find script '{0}'", scriptName);
                }

                return false;
            }

            return StartScript(chr, npcID, NPC, portalID);
        }

        public static void HandleNPCChat(Character chr, Packet packet)
        {
            switch (chr.NpcSession)
            {
                case null:
                    _log.Debug("Got NPC Chat packet while there is no session active.");
                    return;
                case NpcChatSession ncs:
                    HandleNPCChatOld(chr, packet, ncs);
                    break;
                case IScriptV2 v2:
                    if (!HandleNPCChatNew(chr, packet, v2))
                    {
                        _log.Error("Failed handling script command. Terminating script.");
                        v2.TerminateScript();
                    }

                    break;
            }
        }

        public static bool HandleNPCChatNew(Character chr, Packet packet, IScriptV2 session)
        {
            var chatResponseType = packet.ReadByte<NpcChatTypes>();
            if (chatResponseType != session.LastSentType)
            {
                _log.Warn($"Got a response on a npc chat type that we didnt ask a response to. Requested {session.LastSentType}, got {chatResponseType}. Unstucking.");
                return false;
            }

            if (!session.WaitingForResponse)
            {
                _log.Warn("Got a response, even though we were not waiting for one!");
                return false;
            }

            session.WaitingForResponse = false;

            Trace.WriteLine(packet.ToString());

            var option = packet.ReadByte();
            try
            {
                switch (chatResponseType)
                {
                    case NpcChatTypes.Simple:
                        switch (option)
                        {
                            case 0: // Back button...
                                session.TryRequestPreviousMessage();
                                break;
                            case 1: // Next button...
                                session.TryRequestNextMessage();
                                break;
                            case 255:
                                session.TerminateScript();
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }

                        break;

                    case NpcChatTypes.RequestYesNo:
                        switch (option)
                        {
                            case 0: // No.
                                session.ProvideClientResponse(false);
                                break;
                            case 1: // Yes.
                                session.ProvideClientResponse(true);
                                break;
                            case 255:
                                session.TerminateScript();
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }

                        break;

                    case NpcChatTypes.RequestText:
                        switch (option)
                        {
                            case 0: // No text :(
                                session.TerminateScript();
                                break;
                            case 1: // Oh yea, text
                                session.ProvideClientResponse(packet.ReadString());
                                break;
                            case 255:
                                session.TerminateScript();
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }

                        break;

                    case NpcChatTypes.RequestInteger:
                        switch (option)
                        {
                            case 0: // No int :(
                                session.TerminateScript();
                                break;
                            case 1: // Oh yea, int
                                session.ProvideClientResponse(packet.ReadInt());
                                break;
                            case 255:
                                session.TerminateScript();
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }

                        break;

                    case NpcChatTypes.RequestMenu:
                        switch (option)
                        {
                            case 0: // Stopping.
                                session.TerminateScript();
                                break;
                            case 1: // Got answer
                                var val = packet.ReadInt();
                                if (val == -1) val = 0; // Menus do not correctly work when holding enter key

                                session.ProvideClientResponse(val);
                                break;
                            case 255:
                                session.TerminateScript();
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }
                        break;

                    case NpcChatTypes.RequestStyle:
                        switch (option)
                        {
                            case 0: // Stopping.
                                session.TerminateScript();
                                break;
                            case 1: // Got answer
                                var val = packet.ReadByte();
                                if (val == 0xFF) val = 0; // Menus do not correctly work when holding enter key

                                session.ProvideClientResponse(val);
                                break;
                            case 255:
                                session.TerminateScript();
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }

                        break;
                    case NpcChatTypes.RequestPetID:
                        switch (option)
                        {
                            case 0: // Stopping.
                                session.TerminateScript();
                                break;
                            case 1: // Got answer
                                session.ProvideClientResponse(packet.ReadLong());
                                break;
                            default:
                                _log.Error($"Unexpected option {option} for {chatResponseType}");
                                return false;
                        }

                        break;

                    default:
                        _log.Error($"Unknown NPC chat action {chatResponseType} {option}. Packet: {packet}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception while handling NPC {session.NpcID} {session.ScriptName}. Packet: {packet}", ex);
                return false;
            }

            return true;
        }

        public static void HandleNPCChatOld(Character chr, Packet packet, NpcChatSession session)
        {
            var chatResponseType = packet.ReadByte();
            if (chatResponseType != (byte) session.mLastSentType)
            {
                _log.Warn($"Got a response on a npc chat type that we didnt ask a response to. Requested {session.mLastSentType}, got {chatResponseType}.");
                return;
            }

            if (!session.WaitingForResponse)
            {
                _log.Warn("Got a response, even though we were not waiting for one!");
                return;
            }

            session.WaitingForResponse = false;

            Trace.WriteLine(packet.ToString());

            var option = packet.ReadByte();
            try
            {
                switch (chatResponseType)
                {
                    case 0:
                        switch (option)
                        {
                            case 0: // Back button...
                                session.SendPreviousMessage();
                                break;
                            case 1: // Next button...
                                session.SendNextMessage();
                                break;
                            default:
                                session.Stop();
                                break;
                        }

                        break;

                    case 1:
                        switch (option)
                        {
                            case 0: // No.
                                session.HandleThing(session.mRealState, 0, "", 0);
                                break;
                            case 1: // Yes.
                                session.HandleThing(session.mRealState, 1, "", 0);
                                break;
                            default:
                                session.Stop();
                                break;
                        }

                        break;

                    case 2:
                        switch (option)
                        {
                            case 0: // No text :(
                                session.Stop();
                                break;
                            case 1: // Oh yea, text
                                session.HandleThing(session.mRealState, 1, packet.ReadString(), 0);
                                break;
                            default:
                                session.Stop();
                                break;
                        }

                        break;

                    case 3:
                        switch (option)
                        {
                            case 0: // No int :(
                                session.Stop();
                                break;
                            case 1: // Oh yea, int
                                session.HandleThing(session.mRealState, 1, "", packet.ReadShort());
                                break;
                            default:
                                session.Stop();
                                break;
                        }

                        break;

                    case 4:
                    case 5:
                        switch (option)
                        {
                            case 0: // Stopping.
                                session.Stop();
                                break;
                            case 1: // Got answer
                                var val = packet.ReadByte();
                                if (val == 255) val = 0; // Menus do not correctly work when holding enter key
                                session.HandleThing(session.mRealState, val, "", 0);
                                break;
                            default:
                                session.Stop();
                                break;
                        }

                        break;

                    default:
                        session.Stop();
                        _log.Error($"Unknown NPC chat action {chatResponseType} {option}. Packet: {packet}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception while handling NPC {session.mID} {session.mRealState}. Packet: " + packet, ex);
                session?.Stop();
            }
        }

        public enum ShopReq
        {
            Buy = 0,
            Sell,
            Recharge,
            Close,
        }

        public enum ShopRes
        {
            BuySuccess = 0,
            BuyNoStock,
            BuyNoMoney,
            BuyUnknown,
            SellSuccess,
            SellNoStock,
            SellIncorrectRequest,
            SellUnknown,
            RechargeSuccess,
            RechargeNoStock,
            RechargeNoMoney,
            RechargeIncorrectRequest,
            RechargeUnknown,
        }

        public static void HandleNPCShop(Character chr, Packet packet)
        {
            if (chr.ShopNPCID == 0) return;

            if (!DataProvider.NPCs.TryGetValue(chr.ShopNPCID, out var npcData)) return;

            var shopInfo = npcData.Shop;
            var transferId = "" + chr.ID + "-" + chr.ShopNPCID + "-" + RNG.Range.generate(0, Int64.MaxValue);

            var type = packet.ReadByte<ShopReq>();
            switch (type)
            {
                case ShopReq.Buy:
                {
                    var slot = packet.ReadShort();
                    var itemid = packet.ReadInt();
                    var amount = packet.ReadShort();

                    if (amount < 1 ||
                        (Constants.isEquip(itemid) && amount != 1))
                    {
                        Program.MainForm.LogAppend("Disconnecting player: trying to buy a negative amount of items OR multiple equips. " + packet);
                        chr.Disconnect();
                        return;
                    }

                    if (slot < 0 || slot >= shopInfo.Count)
                    {
                        SendShopResult(chr, ShopRes.BuyUnknown);
                        return;
                    }

                    var sid = shopInfo[slot];
                    var costs = amount * sid.Price;
                    if (false && sid.Stock == 0)
                    {
                        SendShopResult(chr, ShopRes.BuyNoStock);
                        return;
                    }

                    if (sid.ID != itemid)
                    {
                        SendShopResult(chr, ShopRes.BuyUnknown);
                        return;
                    }

                    if (costs > chr.Inventory.Mesos)
                    {
                        SendShopResult(chr, ShopRes.BuyNoMoney);
                        return;
                    }

                    if (Constants.isRechargeable(itemid))
                    {
                        costs = amount * sid.Price;
                        if (amount > DataProvider.Items[itemid].MaxSlot) // You can't but multiple sets at once
                        {
                            SendShopResult(chr, ShopRes.BuyUnknown);
                            return;
                        }
                    }


                    if (!chr.Inventory.HasSlotsFreeForItem(itemid, amount))
                    {
                        SendShopResult(chr, ShopRes.BuyUnknown);
                        return;
                    }

                    MesosTransfer.PlayerBuysFromShop(chr.ID, chr.ShopNPCID, costs,
                        transferId);
                    ItemTransfer.PlayerBuysFromShop(chr.ID, chr.ShopNPCID, itemid, amount,
                        transferId, null);

                    chr.Inventory.AddNewItem(itemid, amount);
                    SendShopResult(chr, ShopRes.BuySuccess);
                    sid.Stock -= amount;
                    chr.AddMesos(-costs);

                    break;
                }

                case ShopReq.Sell:
                {
                    var itemslot = packet.ReadShort();
                    var itemid = packet.ReadInt();
                    var amount = packet.ReadShort();
                    var inv = Constants.getInventory(itemid);

                    var item = chr.Inventory.GetItem(inv, itemslot);

                    if (item == null ||
                        item.ItemID != itemid ||
                        amount < 1 ||
                        // Do not trigger this when selling stars and such.
                        (!Constants.isRechargeable(itemid) && amount > item.Amount) ||
                        (Constants.isEquip(itemid)
                            ? DataProvider.Equips.ContainsKey(itemid) == false
                            : DataProvider.Items.ContainsKey(itemid) == false) ||
                        item.CashId != 0)
                    {
                        Program.MainForm.LogAppend("Disconnecting player: invalid trade packet: " + packet);
                        chr.Disconnect();
                        return;
                    }


                    var sellPrice = 0;
                    if (Constants.isEquip(itemid))
                    {
                        var ed = DataProvider.Equips[itemid];
                        sellPrice = ed.Price;
                    }
                    else
                    {
                        var id = DataProvider.Items[itemid];
                        sellPrice = id.Price * amount;
                    }

                    if (sellPrice < 0)
                    {
                        SendShopResult(chr, ShopRes.SellIncorrectRequest);
                        return;
                    }

                    // Change amount here (rechargeables are sold as 1)
                    if (Constants.isRechargeable(item.ItemID))
                    {
                        amount = item.Amount;
                    }

                    MesosTransfer.PlayerSellsToShop(chr.ID, chr.ShopNPCID, sellPrice, transferId);
                    ItemTransfer.PlayerSellsToShop(chr.ID, chr.ShopNPCID, item.ItemID, amount, transferId, item);

                    if (amount == item.Amount)
                    {
                        chr.Inventory.SetItem(inv, itemslot, null);
                        chr.Inventory.TryRemoveCashItem(item);
                        InventoryPacket.DeleteItems(chr, inv, itemslot);
                    }
                    else
                    {
                        item.Amount -= amount;
                        InventoryPacket.UpdateItems(chr, item);
                    }

                    chr.AddMesos(sellPrice);

                    SendShopResult(chr, ShopRes.SellSuccess);
                    break;
                }

                case ShopReq.Recharge:
                {
                    var itemslot = packet.ReadShort();

                    byte inv = 2;
                    var item = chr.Inventory.GetItem(inv, itemslot);
                    if (item == null ||
                        !Constants.isRechargeable(item.ItemID))
                    {
                        Program.MainForm.LogAppend("Disconnecting player: invalid trade packet: " + packet);
                        chr.Disconnect();
                        return;
                    }

                    var sid = shopInfo.FirstOrDefault((a) => a.ID == item.ItemID);
                    if (sid == null)
                    {
                        Program.MainForm.LogAppend("Disconnecting player: Item not found in shop; not rechargeable?");
                        chr.Disconnect();
                        return;
                    }

                    if (sid.UnitRechargeRate <= 0.0)
                    {
                        SendShopResult(chr, ShopRes.RechargeIncorrectRequest);
                        return;
                    }

                    var maxslot = (short) chr.Skills.GetBundleItemMaxPerSlot(item.ItemID);
                    var toFill = (short) (maxslot - item.Amount);

                    var rechargePrice = (int) Math.Ceiling(sid.UnitRechargeRate * toFill);

                    if (rechargePrice <= 0 || chr.Inventory.Mesos < rechargePrice)
                    {
                        SendShopResult(chr, ShopRes.RechargeNoMoney); // no muney? hier! suk a kok!
                        return;
                    }


                    MesosTransfer.PlayerBuysFromShop(chr.ID, chr.ShopNPCID, rechargePrice,
                        transferId);
                    ItemTransfer.PlayerBuysFromShop(chr.ID, chr.ShopNPCID, item.ItemID,
                        (short) (maxslot - item.Amount), transferId, item);

                    item.Amount = maxslot;

                    chr.AddMesos(-rechargePrice);
                    InventoryPacket.UpdateItems(chr, item);
                    SendShopResult(chr, ShopRes.RechargeSuccess);
                    break;
                }

                case ShopReq.Close:
                    chr.ShopNPCID = 0;
                    chr.NpcSession = null;
                    break;

                default:
                    Program.MainForm.LogAppend("Unknown NPC shop action: " + packet);
                    break;
            }
        }

        public static void SendShowNPCShop(Character chr, int NPCID)
        {
            var pw = new Packet(ServerMessages.SHOP);
            pw.WriteInt(NPCID);

            var ShopItems = DataProvider.NPCs[NPCID].Shop;


            pw.WriteShort((short) ShopItems.Count);
            foreach (var item in ShopItems)
            {
                pw.WriteInt(item.ID);
                pw.WriteInt(item.Price);

                if (Constants.isRechargeable(item.ID))
                {
                    pw.WriteLong(BitConverter.DoubleToInt64Bits(item.UnitRechargeRate));
                }

                pw.WriteUShort(chr.Skills.GetBundleItemMaxPerSlot(item.ID));
            }

            chr.SendPacket(pw);
        }

        public static void SendShopResult(Character chr, ShopRes ans)
        {
            var pw = new Packet(ServerMessages.SHOP_TRANSACTION);
            pw.WriteByte(ans);

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextSimple(Character chr, int NpcID, string Text, NpcChatSimpleTypes type)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(NpcChatTypes.Simple);
            pw.WriteString(Text);

            switch (type)
            {
                case NpcChatSimpleTypes.Next:
                    pw.WriteBool(false);
                    pw.WriteBool(true);
                    break;
                case NpcChatSimpleTypes.BackNext:
                    pw.WriteBool(true);
                    pw.WriteBool(true);
                    break;
                case NpcChatSimpleTypes.BackOK:
                    pw.WriteBool(true);
                    pw.WriteBool(false);
                    break;
                case NpcChatSimpleTypes.OK:
                    pw.WriteBool(false);
                    pw.WriteBool(false);
                    break;
            }

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextMenu(Character chr, int NpcID, string Text)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(0x04);
            pw.WriteString(Text);

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextYesNo(Character chr, int NpcID, string Text)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(NpcChatTypes.RequestYesNo);
            pw.WriteString(Text);

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextRequestText(Character chr, int NpcID, string Text, string Default, short MinLength, short MaxLength)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(NpcChatTypes.RequestText);
            pw.WriteString(Text);
            pw.WriteString(Default);
            pw.WriteShort(MinLength);
            pw.WriteShort(MaxLength);

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextRequestInteger(Character chr, int NpcID, string Text, int Default, int MinValue, int MaxValue)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(NpcChatTypes.RequestInteger);
            pw.WriteString(Text);
            pw.WriteInt(Default);
            pw.WriteInt(MinValue);
            pw.WriteInt(MaxValue);

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextRequestStyle(Character chr, int NpcID, string Text, List<int> values)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(NpcChatTypes.RequestStyle);
            pw.WriteString(Text);
            pw.WriteByte((byte) values.Count);
            foreach (var value in values)
            {
                pw.WriteInt(value);
            }

            chr.SendPacket(pw);
        }

        public static void SendNPCChatTextRequestPet(Character chr, int NpcID, string Text, params (long CashID, byte Slot)[] cashIds)
        {
            var pw = new Packet(ServerMessages.SCRIPT_MESSAGE);
            pw.WriteByte(0x04);
            pw.WriteInt(NpcID);
            pw.WriteByte(NpcChatTypes.RequestPetID);
            pw.WriteString(Text);

            pw.WriteByte((byte) cashIds.Length);
            foreach (var (CashID, Slot) in cashIds)
            {
                pw.WriteLong(CashID);
                pw.WriteByte(Slot);
            }

            chr.SendPacket(pw);
        }
        
        public static void SendMakeEnterFieldPacket(NpcLife npcLife, Character victim)
        {
            Packet pw;
            /*
            pw= new Packet(ServerMessages.NPC_ENTER_FIELD);
            pw.WriteUInt(npcLife.SpawnID);
            pw.WriteInt(npcLife.ID);
            pw.WriteShort(npcLife.X);
            pw.WriteShort(npcLife.Y);
            pw.WriteBool(!npcLife.FacesLeft);
            pw.WriteUShort(npcLife.Foothold);
            pw.WriteShort(npcLife.Rx0);
            pw.WriteShort(npcLife.Rx1);

            victim.SendPacket(pw);
            */
            
            pw = new Packet(ServerMessages.NPC_CHANGE_CONTROLLER);
            pw.WriteBool(true);
            pw.WriteUInt(npcLife.SpawnID);
            pw.WriteInt(npcLife.ID);
            pw.WriteShort(npcLife.X);
            pw.WriteShort(npcLife.Y);
            pw.WriteBool(!npcLife.FacesLeft);
            pw.WriteUShort(npcLife.Foothold);
            pw.WriteShort(npcLife.Rx0);
            pw.WriteShort(npcLife.Rx1);

            if (victim == null)
                npcLife.Field.SendPacket(pw);
            else
                victim.SendPacket(pw);
        }

        public static void SendMakeLeaveFieldPacket(NpcLife npcLife)
        {
            var pw = new Packet(ServerMessages.NPC_CHANGE_CONTROLLER);
            pw.WriteBool(false);
            pw.WriteUInt(npcLife.SpawnID);
            npcLife.Field.SendPacket(pw);

            pw = new Packet(ServerMessages.NPC_LEAVE_FIELD);
            pw.WriteUInt(npcLife.SpawnID);
            npcLife.Field.SendPacket(pw);
        }

        public static void HandleNPCAnimation(Character controller, Packet packet)
        {
            var pw = new Packet(ServerMessages.NPC_ANIMATE);
            pw.WriteBytes(packet.ReadLeftoverBytes());

            controller.SendPacket(pw);
        }
    }
}