using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using WvsBeta.Game.GameObjects;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public static class CashPacket
    {
        private static ILog _log = LogManager.GetLogger(typeof(CashPacket));

        // Thank you, Bui :D
        public enum RockModes
        {
            Delete = 0x02,
            Add = 0x03
        };

        public enum RockErrors
        {
            CannotGo2 = 0x05, // This is unused
            DifficultToLocate = 0x06,
            DifficultToLocate2 = 0x07, // This is unused
            CannotGo = 0x08,
            AlreadyThere = 0x09,
            CannotSaveMap = 0x0A
        };

        public static void HandleTeleRockFunction(Character chr, Packet packet)
        {
            bool AddCurrentMap = packet.ReadBool();
            if (AddCurrentMap)
            {
                if (chr.Inventory.AddRockLocation(chr.MapID))
                {
                    SendRockUpdate(chr, RockModes.Add);
                }
                else
                {
                    SendRockError(chr, RockErrors.CannotSaveMap);
                }
            }
            else
            {
                int map = packet.ReadInt();
                chr.Inventory.RemoveRockLocation(map);
                SendRockUpdate(chr, RockModes.Delete);
            }
        }

        public static void HandleCashItem(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to use cash item while !CanAttachAdditionalProcess"))
            {
                return;
            }

            var slot = packet.ReadShort();
            var itemid = packet.ReadInt();

            const byte UseInventory = 2;

            var item = chr.Inventory.GetItem(UseInventory, slot);

            if (chr.AssertForHack(item == null, "HandleCashItem with null item") ||
                chr.AssertForHack(item.ItemID != itemid, "HandleCashItem with itemid inconsistency") ||
                chr.AssertForHack(!DataProvider.Items.TryGetValue(itemid, out var data), "HandleCashItem with unknown item") ||
                chr.AssertForHack(!data.Cash, "HandleCashItem with non-cash item"))
            {
                return;
            }

            var itemType = (Constants.Items.Types.ItemTypes)Constants.getItemType(itemid);

            var used = false;

            switch (itemType)
            {
                case Constants.Items.Types.ItemTypes.ItemWeather:
                    {
                        var message = packet.ReadLocalizedString(chr.ClientActiveCodePage);
                        if (MessagePacket.ShowMuteMessage(chr))
                        {
                            used = false;
                        }
                        else
                        {
                            used = chr.Field.MakeWeatherEffect(itemid, message, new TimeSpan(0, 0, 30));
                        }
                    }
                    break;
                case Constants.Items.Types.ItemTypes.ItemJukebox:
                    used = chr.Field.MakeJukeboxEffect(itemid, chr.Name, TimeSpan.FromMilliseconds(packet.ReadInt()));
                    break;

                case Constants.Items.Types.ItemTypes.ItemPetTag:
                    {
                        var name = packet.ReadString();
                        var petItem = chr.GetSpawnedPet();
                        if (petItem != null &&
                            !chr.IsInvalidTextInput("Pet name tag", name, Constants.MaxPetName, Constants.MinPetName))
                        {
                            petItem.Name = name;
                            PetsPacket.SendPetNamechange(chr);
                            used = true;
                        }
                    }

                    break;

                case Constants.Items.Types.ItemTypes.ItemMegaPhone:
                    {
                        var text = packet.ReadString();
                        if (MessagePacket.ShowMuteMessage(chr))
                        {
                            used = false;
                        }
                        else if (!chr.IsInvalidTextInput("Megaphone item", text, Constants.MaxSpeakerTextLength))
                        {
                            switch (itemid)
                            {
                                case Constants.Items.ItemMegaphone:
                                    MessagePacket.SendMegaphoneMessage(chr.Name + " : " + text);
                                    used = true;
                                    break;

                                case Constants.Items.ItemSuperMegaphone:
                                    Server.Instance.CenterConnection.PlayerSuperMegaphone(
                                        chr.Name + " : " + text,
                                        packet.ReadBool()
                                    );
                                    used = true;
                                    break;
                            }
                        }
                    }
                    break;

                case Constants.Items.Types.ItemTypes.ItemMessageBox:

                    if (MessagePacket.ShowMuteMessage(chr))
                    {
                        used = false;
                    }
                    else if (!chr.Field.CheckBalloonAvailable(chr.Position, Map.BalloonType.MessageBox))
                    {
                        MapPacket.ShowMessageBoxCreateFailed(chr);
                    }
                    else
                    {
                        var message = packet.ReadString();
                        var msgBox = new MessageBox(chr, itemid, message, chr.Field);
                        msgBox.Spawn();

                        used = true;
                    }
                    break;

                case Constants.Items.Types.ItemTypes.ItemNote:
                    {
                        var name = packet.ReadString();
                        var message = packet.ReadString();
                        if (MessagePacket.ShowMuteMessage(chr))
                        {
                            used = false;
                        }
                        else
                        {
                            used = MemoPacket.SendNewMemo(chr, name, message);
                        }

                        break;
                    }
                case Constants.Items.Types.ItemTypes.ItemMesoSack:
                    if (data.Mesos > 0)
                    {
                        int amountGot = chr.AddMesos(data.Mesos);

                        MiscPacket.SendGotMesosFromLucksack(chr, amountGot);
                        used = true;
                    }
                    break;
                case Constants.Items.Types.ItemTypes.ItemTeleportRock:
                    {
                        var mode = packet.ReadByte();
                        int map = -1;
                        if (mode == 1)
                        {
                            var name = packet.ReadString();
                            var target = Server.Instance.GetCharacter(name);
                            if (target != null && target != chr)
                            {
                                map = target.MapID;
                                used = true;
                            }
                            else
                            {
                                SendRockError(chr, RockErrors.DifficultToLocate);
                            }
                        }
                        else
                        {
                            map = packet.ReadInt();
                            if (!chr.Inventory.HasRockLocation(map))
                            {
                                map = -1;
                            }
                        }

                        if (map != -1)
                        {
                            //I don't think it's even possible for you to be in a map that doesn't exist and use a Teleport rock?
                            var from = chr.Field;
                            MapProvider.Maps.TryGetValue(map, out var to);

                            if (to == from)
                            {
                                SendRockError(chr, RockErrors.AlreadyThere);
                            }
                            else if (from.Limitations.HasFlag(FieldLimit.TeleportItemLimit))
                            {
                                SendRockError(chr, RockErrors.CannotGo);
                            }
                            else if (chr.AssertForHack(chr.PrimaryStats.Level < 7, "Using telerock while not lvl 8 or higher."))
                            {
                                // Hacks.
                            }
                            else
                            {
                                chr.ChangeMap(map);
                                used = true;
                            }
                        }

                        break;
                    }

                case Constants.Items.Types.ItemTypes.ItemAPSPReset:
                    {
                        if (itemid == Constants.Items.ItemAPReset)
                        {
                            used = ConsumeStatChange(chr, packet);
                        }
                        else
                        {
                            used = ConsumeSkillChange(chr, packet, itemid % 10);
                        }

                        break;
                    }
                default:
                    Program.MainForm.LogAppend("Unknown cashitem used: {0} {1} {2}", itemType, itemid, packet.ToString());
                    break;
            }

            if (used)
            {
                ItemTransfer.ItemUsed(chr.ID, item.ItemID, 1, "");
                if (!chr.Inventory.SubtractAmountFromSlot(UseInventory, slot, 1))
                {
                    _log.Error("Unable to use cash item, can't remove it from inventory???");
                }
            }
        }

        public static bool CanStatChange(Character chr, StatFlags inc, StatFlags dec)
        {
            var ps = chr.PrimaryStats;
            var nStr = ps.Str;
            var nDex = ps.Dex;
            var nInt = ps.Int;
            var nLuk = ps.Luk;
            var nJob = ps.Job;
            var nJobTrack = (Constants.JobTracks.Tracks)Constants.getJobTrack(nJob);

            if (nJobTrack < 0 || (short)nJobTrack >= 5)
            {
                _log.Error($"CanStatChange: {nJobTrack} Wrong job?");
                return false;
            }

            if (inc == dec)
            {
                _log.Error($"Trying to put the stat in the same stat: {inc}");
                return false;
            }

            switch (dec)
            {
                case StatFlags.Str: nStr--; break;
                case StatFlags.Dex: nDex--; break;
                case StatFlags.Int: nInt--; break;
                case StatFlags.Luk: nLuk--; break;
                default:
                    _log.Error($"Trying to reduce {dec} stat!!!");
                    return false;
            }

            if (!IsValidStat(chr, nStr, nDex, nInt, nLuk, ps.AP + 1))
            {
                return false;
            }

            switch (inc)
            {
                case StatFlags.Str: nStr++; break;
                case StatFlags.Dex: nDex++; break;
                case StatFlags.Int: nInt++; break;
                case StatFlags.Luk: nLuk++; break;
                default:
                    _log.Error($"Trying to increase {inc} stat!!!");
                    return false;
            }

            if (!IsValidStat(chr, nStr, nDex, nInt, nLuk, ps.AP))
            {
                return false;
            }

            return true;
        }

        public static bool ConsumeStatChange(Character chr, Packet packet)
        {
            var incFlag = packet.ReadInt<StatFlags>();
            var decFlag = packet.ReadInt<StatFlags>();

            if (!CanStatChange(chr, incFlag, decFlag))
            {
                _log.Warn("CanStatChange failed");
                return false;
            }


            switch (decFlag)
            {
                case StatFlags.Str: chr.AddStr(-1); break;
                case StatFlags.Dex: chr.AddDex(-1); break;
                case StatFlags.Int: chr.AddInt(-1); break;
                case StatFlags.Luk: chr.AddLuk(-1); break;
                default:
                    _log.Error($"Trying to reduce {decFlag} stat!!!");
                    return false;
            }

            switch (incFlag)
            {
                case StatFlags.Str: chr.AddStr(1); break;
                case StatFlags.Dex: chr.AddDex(1); break;
                case StatFlags.Int: chr.AddInt(1); break;
                case StatFlags.Luk: chr.AddLuk(1); break;
                default:
                    _log.Error($"Trying to reduce {decFlag} stat!!!");
                    return false;
            }

            return true;
        }

        public static bool IsValidStat(Character chr, int nStr, int nDex, int nInt, int nLuk, int nRemainAP)
        {
            bool _false(string why)
            {
                _log.Warn($"IsValidStat failed: {why}");
                return false;
            }

            if (nStr < 4)
                return _false($"STR under 4: {nStr}");
            if (nDex < 4)
                return _false($"DEX under 4: {nDex}");
            if (nInt < 4)
                return _false($"INT under 4: {nInt}");
            if (nLuk < 4)
                return _false($"LUK under 4: {nLuk}");

            var nJob = chr.Job;
            var jobCategory = nJob / 100;

            if (jobCategory == 1 && nStr < 35)
                return _false($"Not enough STR for Warrior: {nStr}");

            if (jobCategory == 2 && nInt < 20)
                return _false($"Not enough INT for Magician: {nInt}");

            if (jobCategory == 3 && nDex < 25)
                return _false($"Not enough DEX for Bowman: {nDex}");

            if (jobCategory == 4 && nDex < 25)
                return _false($"Not enough DEX for Thief: {nDex}");

            int v8;
            if (nJob % 10 == 1)
                v8 = 5;
            else if (nJob % 10 == 2)
                v8 = 10; // 4th job...
            else
                v8 = 0;

            var apApplied = nRemainAP + nLuk + nInt + nDex + nStr;
            var apRequired = v8
                             + 4 * (chr.Level + 4)
                             + chr.Level
                             + 4;

            if (apApplied > apRequired)
            {
                return _false($"More AP given than possible? {apApplied} > {apRequired}");
            }

            return true;
        }

        public static bool ConsumeSkillChange(Character chr, Packet packet, int itemLevel)
        {
            var incSkill = packet.ReadInt();
            var decSkill = packet.ReadInt();


            if (!CanSkillChange(chr, decSkill, incSkill, itemLevel)) return false;

            chr.Skills.SetSkillPoint(decSkill, (byte)(chr.Skills.GetSkillLevel(decSkill) - 1));
            chr.Skills.SetSkillPoint(incSkill, (byte)(chr.Skills.GetSkillLevel(incSkill) + 1));
            return true;

        }

        public static bool CanSkillChange(Character chr, int decSkill, int incSkill, int itemLevel)
        {
            if (!DataProvider.Skills.TryGetValue(decSkill, out var decSkillInfo))
            {
                _log.Error($"Tried to SP reset of nonexistent skill {decSkill}");
                return false;
            }

            var decSkillJob = Constants.getSkillJob(decSkill);
            var decSkillJobLevel = Constants.get_job_level(decSkillJob);

            if (!DataProvider.Skills.TryGetValue(incSkill, out var incSkillInfo))
            {
                _log.Error($"Tried to SP reset of nonexistent skill {incSkill}");
                return false;
            }

            var incSkillJob = Constants.getSkillJob(incSkill);
            var incSkillJobLevel = Constants.get_job_level(incSkillJob);

            var jobTrack = Constants.getJobTrack(chr.Job);
            var jobLevel = Constants.get_job_level(chr.Job);

            if (!(jobTrack >= 1 && jobTrack <= 4))
            {
                _log.Warn($"Using SP reset with wrong player job track: {jobTrack}");
                return false;
            }

            if (jobLevel < itemLevel)
            {
                _log.Warn($"Trying to use SP reset with wrong player 'job level'? {jobLevel} < {itemLevel}");
                return false;
            }

            if (itemLevel != incSkillJobLevel)
            {
                _log.Warn($"Trying to use SP reset with wrong skill 'job level'? {itemLevel} != {incSkillJobLevel}");
                return false;
            }

            var skillRoot = Constants.get_skill_root_from_job(chr.Job).ToArray();
            // Figure out if incSkill is part of our root
            if (!skillRoot.Contains(incSkillJob))
            {
                _log.Warn($"Trying to put SP in skill {incSkill} that is not part of the players 'job skill root'. {string.Join(", ", skillRoot)} and expected {incSkillJob}");
                return false;
            }

            // figure out if decSkill is part of our root
            if (!skillRoot.Contains(decSkillJob))
            {
                _log.Warn($"Trying to remove SP from skill {decSkill} that is not part of the players 'job skill root'. {string.Join(", ", skillRoot)} and expected {incSkillJob}");
                return false;
            }

            var skillMap = new Dictionary<int, byte>(chr.Skills.Skills);

            skillMap.TryGetValue(decSkill, out var decSkillLevel);
            skillMap.TryGetValue(incSkill, out var incSkillLevel);

            if (decSkillLevel < 1)
            {
                _log.Warn($"DecSkill {decSkill} has no points.");
                return false;
            }

            var newDecSkillLevel = decSkillLevel - 1;

            var incSkillLevelMax = incSkillInfo.MaxLevel;

            if (incSkillLevel >= incSkillLevelMax)
            {
                _log.Warn($"IncSkill is already max {incSkillLevel} >= {incSkillLevelMax}");
                return false;
            }


            // Check if user did not remove points from a skill that unlocks other skills, and would therefor
            // make those skills impossible to get.

            foreach (var kvp in skillMap)
            {
                if (kvp.Value == 0) continue;

                if (!DataProvider.Skills.TryGetValue(kvp.Key, out var skillInfo))
                {
                    _log.Error($"Nonexistent skill found in skilltree of user? {kvp.Key}");
                    return false;
                }

                if (skillInfo.RequiredSkills == null) continue;
                foreach (var reqSkillInfo in skillInfo.RequiredSkills)
                {
                    if (reqSkillInfo.Key != decSkill) continue;

                    var requiredPoints = reqSkillInfo.Value;

                    if (requiredPoints > newDecSkillLevel)
                    {
                        _log.Error($"Trying to remove points from skill {decSkill}, while skill {kvp.Key} needs to have {requiredPoints} points to exist");
                        return false;
                    }
                }
            }


            bool IsValidSkill(Dictionary<int, byte> newSkillRecord, int itemLevel, int skillRoot, bool dec)
            {
                if (itemLevel == Constants.get_job_level(skillRoot)) return true;

                int sp1 = 0;
                int sp2 = 0;
                int sp3 = 0;

                var chrJobLevel = Constants.get_job_level(chr.Job);
                foreach (var kvp in newSkillRecord)
                {
                    var skillPoints = kvp.Value;
                    var skillJob = Constants.getSkillJob(kvp.Key);
                    var skillJobLevel = Constants.get_job_level(skillJob);

                    switch (skillJobLevel)
                    {
                        case 1: sp1 += skillPoints; break;
                        case 2: sp2 += skillPoints; break;
                        case 3: sp3 += skillPoints; break;
                    }
                }



                var isMagician = chr.Job / 100 == 2;

                int spSum = 0;
                spSum += sp1;
                spSum += sp2;
                spSum += sp3;
                spSum += chr.PrimaryStats.SP;
                spSum += isMagician ? 6 : 0;
                var firstJobChangeLevel = isMagician ? 8 : 10;
                spSum *= 3 * (firstJobChangeLevel - chr.Level);
                spSum -= chrJobLevel;
                spSum += 61;

                var maxSP3 = 121 - (dec ? 1 : 0);

                switch (itemLevel)
                {
                    case 1: return true;
                    case 2:
                        return sp1 - spSum >= 0;
                    case 3:
                        return sp1 + sp2 - (spSum + maxSP3) >= 0;
                }

                _log.Error("Unhandled case");
                return false;
            }

            skillMap[decSkill] = (byte)newDecSkillLevel;


            if (IsValidSkill(skillMap, itemLevel, decSkillJob, true) == false)
            {
                _log.Warn("IsValidSkill failed for dec");
                return false;
            }

            skillMap[incSkill] = (byte)(incSkillLevel + 1);

            if (IsValidSkill(skillMap, itemLevel, incSkillJob, false) == false)
            {
                _log.Warn("IsValidSkill failed for inc");
                return false;
            }


            return true;
        }

        public static void SendRockError(Character chr, RockErrors code)
        {
            Packet pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte((byte)code);
            chr.SendPacket(pw);
        }

        public static void SendRockUpdate(Character chr, RockModes mode)
        {
            Packet pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte((byte)mode);
            chr.Inventory.AddRockPacket(pw);
            chr.SendPacket(pw);
        }
    }
}