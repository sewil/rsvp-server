using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WvsBeta.Common.Sessions;
using WvsBeta.SharedDataProvider.Providers;

namespace WvsBeta.Game.GameObjects
{
    class Map_OXQuiz : Map
    {
        public Map_OXQuiz(int id) : base(id) { }

        public byte Category { get; set; }
        public short Number { get; set; }
        public char? Answer { get; set; } = null;

        public override bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            switch (command.Command)
            {
                case "check": Banish(); return true;
                case "quiz":
                    if (command.Count >= 2 &&
                        command.Args[0].TryGetByte(out var category) &&
                        command.Args[1].TryGetInt16(out var number))
                    {
                        SetProblem(true, category, number);
                        return true;
                    }
                    return false;
                case "answer": SetProblem(false, 0, 0); return true;
            }

            return base.FilterAdminCommand(character, command);
        }
        
        public void Banish()
        {
            if (Answer == null) return;

            var charsInRightArea = GetCharactersInMapArea(Answer.Value == 'x' ? "x" : "o");

            var charsInWrongArea = new HashSet<Character>(Characters.Except(charsInRightArea));

            var eliminationCount = 0;
            foreach (var c in charsInWrongArea)
            {
                if (!c.IsGM)
                {
                    c.ChangeMap(ForcedReturn);
                }

                var packet = new Packet(ServerMessages.QUIZ);
                packet.WriteBool(true);
                packet.WriteInt(0);
                c.SendPacket(packet);
                eliminationCount++;
            }

            MessagePacket.SendNoticeMap($"{eliminationCount} people have been eliminated from the Speed OX Quiz.", ID);

            Number = 0;
            Category = 0;
            Answer = null;
        }


        // Problem? [trollface]
        public void SetProblem(bool isQuestion, byte category, short number)
        {
            if (isQuestion)
            {
                if (Category != 0 && category != 0)
                {
                    log.Warn("Already asking a question to players");
                    return;
                }

                if (!DataProvider.QuizQuestions.TryGetValue(category, out var quests))
                {
                    log.Error($"Did not find category {category} for OX Quiz");
                    return;
                }

                var question = quests.FirstOrDefault(x => x.Number == number);

                if (question == null)
                {
                    log.Error($"Did not find question {number} in category {category} for OX Quiz");
                    return;
                }


                Category = category;
                Number = number;
                Answer = question.Answer;
            }
            else
            {
                if (Category == 0 || category != 0)
                {
                    log.Error("Answer without category, or passing invalid category");
                    return;
                }

                // Do not update category/number/answer info
            }

            var packet = new Packet(ServerMessages.QUIZ);
            packet.WriteBool(isQuestion);
            packet.WriteByte(Category);
            packet.WriteShort(Number);
            SendPacket(packet);
        }
    }
}
