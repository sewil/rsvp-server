using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

// https://amiradata.com/how-to-play-gomoku-gomoku-rules/

namespace WvsBeta.Game.GameObjects.MiniRooms
{
    class Omok : MiniRoomGame
    {
        public class StoneInfo
        {
            public int X;
            public int Y;
            public bool Put;
        }

        public byte[,] StoneCheckers { get; private set; } = new byte[15, 15];
        public Stack<StoneInfo> StoneInfoList { get; private set; } = new Stack<StoneInfo>();
        public byte[] PlayerColor { get; private set; } = new byte[2];
        public long LastStoneChecker { get; private set; }

        public Omok() : base(2)
        {
        }

        public override E_MINI_ROOM_TYPE GetTypeNumber() => E_MINI_ROOM_TYPE.MR_OmokRoom;

        protected override bool IsValidMiniRoomSpec()
            => MiniRoomSpec <= 11;

        protected override void OnUserStart(Character chr, Packet packet)
        {
            base.OnUserStart(chr, packet);

            LastStoneChecker = MasterThread.CurrentTime;
            
            // Calculate points

            var stats0 = FindUserSlot(0).GameStats;
            var stats1 = FindUserSlot(1).GameStats;

            var statsLoser = FindUserSlot(1 - WinnerIndex).GameStats;

            {
                var x = Math.Pow(-1.0, -WinnerIndex);
                var y = Math.Pow(10.0, (x * statsLoser.OmokScore * 0.05 + (stats1.OmokScore - stats0.OmokScore)) * 0.0025);

                PWin[0] = 1.0 / (y + 1.0);
            }
            {
                var x = Math.Pow(-1.0, -(1 - WinnerIndex));
                var y = Math.Pow(10.0, (x * statsLoser.OmokScore * 0.05 + (stats0.OmokScore - stats1.OmokScore)) * 0.0025);

                PWin[1] = 1.0 / (y + 1.0);
            }
        }

        protected override void EncodeGameStart(Packet packet)
        {
            
        }

        public override void OnPacket(Opcodes type, Character chr, Packet packet)
        {
            switch (type)
            {
                case Opcodes.ORP_PutStoneChecker:
                    OnPutStoneChecker(chr, packet);
                    break;
                default:
                    base.OnPacket(type, chr, packet);
                    break;
            }
        }

        public bool IsValidPoint(Point point) => point.X >= 0 && point.X < 15 && point.Y >= 0 && point.Y < 15;

        public void OnPutStoneChecker(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;

            var slot = FindUserSlot(chr);

            var point = packet.ReadIntPoint();
            if (!IsValidPoint(point))
            {
                _log.Error($"User tried to set stone at {point.X} {point.Y}");
                return;
            }

            var b = packet.ReadByte();

            if (slot == CurTurnUser && b == PlayerColor[CurTurnUser])
            {
                if (StoneCheckers[point.X, point.Y] != 0)
                {
                    var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                    p.WriteByte(Opcodes.ORP_InvalidStonePosition);
                    p.WriteByte(Opcodes.ORP_InvalidStonePosition_Normal);
                    chr.SendPacket(p);
                    return;
                }

                var gameIsSet = CheckGameSet(b, point.X, point.Y, out var threesRule);

                if (threesRule)
                {
                    var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                    p.WriteByte(Opcodes.ORP_InvalidStonePosition);
                    p.WriteByte(Opcodes.ORP_InvalidStonePosition_By33);
                    chr.SendPacket(p);

                    return;
                }

                StoneCheckers[point.X, point.Y] = b;

                {
                    var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                    p.WriteByte(Opcodes.ORP_PutStoneChecker);
                    p.WriteIntPoint(point);
                    p.WriteByte(b);
                    Broadcast(p, null);
                }

                StoneInfoList.Push(new StoneInfo
                {
                    X = point.X,
                    Y = point.Y,
                    Put = true,
                });
                LastStoneChecker = MasterThread.CurrentTime;

                if (gameIsSet)
                    OnGameSet(CurTurnUser);

                CurTurnUser = 1 - slot;
            }
        }

        struct RowStats
        {
            public int Count;
            public int CountWithSkip;
            public bool EndsInOtherColor;

            public bool ThreesRule => !EndsInOtherColor && CountWithSkip == 2;

            public RowStats Add(RowStats other)
            {
                var ret = new RowStats();
                ret.Count = Count + other.Count;
                ret.CountWithSkip = CountWithSkip + other.CountWithSkip;
                ret.EndsInOtherColor = EndsInOtherColor || other.EndsInOtherColor;
                return ret;
            }
        }


        public bool CheckGameSet(byte playerColor, int x, int y, out bool threesRule)
        {
            return CheckGameSet(StoneCheckers, playerColor, x, y, out threesRule);
        }

        public static void TestOmokLogic()
        {
            void testBoard(string pattern, bool threesRuleResult, bool gameSetResult)
            {
                var placeRow = 0;
                var placeCol = 0;

                // build board

                var lines = pattern.Trim().Split("\n").Select(x => x.Trim()).ToArray();
                var board = new byte[lines.Max(x => x.Length), lines.Length];
                var row = 0;
                foreach (var s in lines)
                {
                    Console.WriteLine(s);
                    var col = 0;
                    foreach (var c in s)
                    {
                        byte b = 0;
                        switch (c)
                        {
                            case '-':
                                b = 0;
                                break;
                            case 'o':
                                b = 1;
                                break;
                            case 'x':
                                b = 2;
                                break;
                            case 'n':
                                b = 0;
                                placeRow = row;
                                placeCol = col;
                                break;
                        }

                        board[col, row] = b;
                        col++;
                    }

                    row++;
                }

                var gameSet = CheckGameSet(board, 1, placeCol, placeRow, out var threesRule);

                if (gameSet != gameSetResult)
                {
                    Console.WriteLine($"[ERROR] Game Set check failed {gameSet} != {gameSetResult}");
                }
                else
                {
                    Console.WriteLine("Game set check succeeded");
                }

                if (threesRule != threesRuleResult)
                {
                    Console.WriteLine($"[ERROR] Threes rule check failed {threesRule} != {threesRuleResult}");
                }
                else
                {
                    Console.WriteLine("Threes rule succeeded");
                }
            }


            void board33(string pattern)
            {
                testBoard(pattern, true, false);
            }

            void boardSet(string pattern)
            {
                testBoard(pattern, false, true);
            }

            void boardOK(string pattern)
            {
                testBoard(pattern, false, false);
            }

            board33(@"
------
-oo-n-
------
----o-
----o-
------");

            board33(@"
-----
-oon-
---o-
-----
---o-
-----");

            board33(@"
-----
-ono-
-----
--o--
--o--
-----");

            board33(@"
--------
-oon----
--------
-----o--
------o-
--------");

            boardOK(@"
-------
--o----
-oon---
----o--
-----x-
-------");
            boardOK(@"
------
-xoon-
----o-
----o-
----x-");
            boardOK(@"
-------
---x---
-xonox-
---o---
---o---
---x---
-------");
            boardOK(@"
---------
---xxx---
--xxxxx--
-xoonxxx-
-xxxoxx--
--xxoxx--
----x----
---------");
            boardSet(@"
---------
--noooo--
---------");
            boardSet(@"
---------
--oooon--
---------");
            boardSet(@"
---------
--o--
--o--
--o--
--o--
--n--
---------");
            boardSet(@"
---------
--n--
--o--
--o--
--o--
--o--
---------");

            boardSet(@"
---------
--n--
---o-----
----o----
-----o---
------o--
---------");
            boardSet(@"
---------
-----n--
----o-----
---o----
--o---
-o--
---------");

            boardSet(@"
---------
--o--
---o-----
----o----
-----o---
------n--
---------");
            boardSet(@"
---------
-----o--
----o-----
---o----
--o---
-n--
---------");
            boardSet(@"
---------
--o--
--o--
--o--
--o--
--n--
---------");

            boardOK(@"
---------
--ooon--
---------");

            boardOK(@"
---------
--o--
--o--
--o--
--n--
---------");
        }

        public static bool CheckGameSet(byte[,] checkers, byte playerColor, int x, int y, out bool threesRule)
        {
            var cols = checkers.GetLength(0);
            var rows = checkers.GetLength(1);

            // http://gomokuworld.com/site/pictures/images/introduction_of_gomoku_011.gif

            var allDirections = new List<RowStats>();

            RowStats count(int xPos, int yPos, int xInc, int yInc)
            {
                RowStats ret = new RowStats();

                // We need to make sure we can skip 1 place and see if the next one is the right color

                byte? getStoneColor(int x, int y)
                {
                    if (x >= cols || y >= rows || x < 0 || y < 0) return null;
                    return checkers[x, y];
                }

                var skipping = false;

                while (true)
                {
                    xPos += xInc;
                    yPos += yInc;

                    var curColorOpt = getStoneColor(xPos, yPos);
                    if (curColorOpt == null) break;

                    var curColor = curColorOpt.Value;

                    if (curColor != playerColor)
                    {
                        // We can skip 1 empty spot
                        if (curColor == 0)
                        {
                            if (skipping) break;
                            skipping = true;

                            var nextStoneColorOpt = getStoneColor(xPos + xInc, yPos + yInc);
                            if (nextStoneColorOpt == null) break;
                            var nextStoneColor = nextStoneColorOpt.Value;
                            if (nextStoneColor != playerColor)
                            {
                                break;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            ret.EndsInOtherColor = true;
                            // blocked
                            break;
                        }
                    }

                    if (!skipping)
                        ret.Count += 1;

                    ret.CountWithSkip += 1;
                }


                allDirections.Add(ret);

                return ret;
            }

            var t = count(x, y, 0, -1);
            var r = count(x, y, 1, 0);
            var b = count(x, y, 0, 1);
            var l = count(x, y, -1, 0);

            var tl = count(x, y, -1, -1);
            var tr = count(x, y, 1, -1);
            var br = count(x, y, 1, 1);
            var bl = count(x, y, -1, 1);


            var tlbr = tl.Add(br);
            var trbl = tr.Add(bl);

            var tb = t.Add(b);
            var lr = l.Add(r);

            // Only take directions into account

            if (!tl.ThreesRule && !br.ThreesRule)
                allDirections.Add(tlbr);

            if (!tr.ThreesRule && !bl.ThreesRule)
                allDirections.Add(trbl);

            if (!t.ThreesRule && !b.ThreesRule)
                allDirections.Add(tb);

            if (!l.ThreesRule && !r.ThreesRule)
                allDirections.Add(lr);

            threesRule = allDirections.Where(x => x.ThreesRule).ToArray().Length > 1;

            var straightLines =
                tlbr.Count == 4 || trbl.Count == 4 ||
                tb.Count == 4 || lr.Count == 4;


            // If any line is 4 (and would thus make 5), code succeeds
            return straightLines;
        }

        protected override void OnRetreatResult(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;
            var slot = FindUserSlot(chr);

            var otherUser = FindUserSlot(1 - slot);
            if (otherUser == null) return;

            var curUserRetreats = slot != CurTurnUser;


            if (packet.ReadBool())
            {
                if (StoneInfoList.Count == 0)
                {
                    _log.Error("No stones set when retreating?");
                    return;
                }

                var lastInfo = StoneInfoList.Pop();

                var revertingFirst = lastInfo.Put;
                var revertingSecond = false;

                if (revertingFirst)
                    StoneCheckers[lastInfo.X, lastInfo.Y] = 0;


                if (curUserRetreats)
                {
                    if (StoneInfoList.Count == 0)
                    {
                        _log.Error("No stones set when retreating (step 2)?");
                        return;
                    }

                    lastInfo = StoneInfoList.Pop();
                    revertingSecond = lastInfo.Put;
                    if (revertingSecond)
                        StoneCheckers[lastInfo.X, lastInfo.Y] = 0;
                }

                OnChat(chr, MGChatMessage.UserRetreatSuccess);

                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MGRP_RetreatResult);
                p.WriteByte(1);
                var n = revertingFirst ? 1 : 0;
                if (curUserRetreats) n += revertingSecond ? 1 : 0;
                p.WriteByte((byte) n);
                p.WriteByte((byte) (1 - slot));
                Broadcast(p, null);

                CurTurnUser = 1 - slot;
                Retreat[1 - slot] = true;
            }
            else
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MGRP_RetreatResult);
                p.WriteByte(0);
                Broadcast(p, null);
            }
        }

        protected override void ResetMiniGameData(bool open)
        {
            if (open)
                WinnerIndex = 1;

            StoneCheckers = new byte[15, 15];
            LeaveBooked = new bool[2];
            Retreat = new bool[2];
            StoneInfoList.Clear();

            GameResult = GameResults.OnGoing;
            UserReady = false;
            GameOn = false;

            PlayerColor[WinnerIndex] = 2;
            PlayerColor[1 - WinnerIndex] = 1;
            CurTurnUser = 1 - WinnerIndex;
        }
        
        public override void StartGame()
        {
            var c = FindUserSlot(0);
            if (c == null) return;

            ResetMiniGameData(false);

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_Start);
            p.WriteByte(WinnerIndex);
            Broadcast(p, null);

            OnChat(c, MGChatMessage.GameStart);

            GameOn = true;

            LastStoneChecker = MasterThread.CurrentTime;
        }
        
        protected override void EncodeMiniGameRecord(Character chr, Packet packet)
        {
            packet.WriteInt((int) GetTypeNumber());
            packet.WriteInt(chr.GameStats.OmokWins);
            packet.WriteInt(chr.GameStats.OmokTies);
            packet.WriteInt(chr.GameStats.OmokLosses);
            packet.WriteInt(chr.GameStats.OmokScore);
        }

        protected override void UpdatePlayerScore(int idx, Character chr)
        {
            var gs = chr.GameStats;

            var s = gs.OmokWins + gs.OmokTies + gs.OmokLosses > 50 ? 30 : 50;
            if (gs.OmokScore > 3000)
                s = 20;

            var pwin = PWin[idx];

            if (GameResult == GameResults.Tie)
            {
                gs.OmokScore += (int) ((0.5 * pwin) * s);
                gs.OmokTies++;
            }
            else if (idx == WinnerIndex)
            {
                var x = 1.0 * pwin * s;
                if (GameResult == GameResults.GiveUp && StoneInfoList.Count < 6)
                {
                    x *= 0.1;
                }

                gs.OmokScore += (int) x;
                gs.OmokWins++;
            }
            else
            {
                gs.OmokScore -= (int)(pwin * s);
                gs.OmokLosses++;
            }
        }

        protected override void OnTimeOver(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (MasterThread.CurrentTime - LastStoneChecker < 29000) return;

            CurTurnUser = 1 - CurTurnUser;

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_TimeOver);
            p.WriteByte((byte) CurTurnUser);
            Broadcast(p, null);

            LastStoneChecker = MasterThread.CurrentTime;

            StoneInfoList.Push(new StoneInfo
            {
                X = 0,
                Y = 0,
                Put = false,
            });
        }
    }
}