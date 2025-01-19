using System;
using System.Collections.Generic;
using WvsBeta.Common;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Serialization;

namespace WvsBeta.Game.GameObjects
{
    class Sample : IScriptV2
    {

        class RedeemableCredit
        {
            public double Rate;
            public RateCredits.Type Type;
            public TimeSpan Duration;
            public string Comment;
            public int QuestID;
            public DateTime EndDate;

            public RedeemableCredit(double rate, RateCredits.Type type, TimeSpan duration, string comment, int questID, DateTime endDate)
            {
                Rate = rate;
                Type = type;
                Duration = duration;
                Comment = comment;
                QuestID = questID;
                EndDate = endDate;
            }
        }


        private void Credit()
        {
            var rc = chr.RateCredits;

            var redcreds = new List<RedeemableCredit>
            {
                new RedeemableCredit(2.0, RateCredits.Type.Drop, TimeSpan.FromHours(12), "6 month anniversary", 999100, DateTime.Parse("2021-05-01T00:00:00Z")),
            };

            // Add everything that is not yet activated
            foreach (var redcred in redcreds.Where(x => DateTime.Now < x.EndDate && GetQuestData(x.QuestID) != "1"))
            {
                SetQuestData(redcred.QuestID, "1");
                rc.AddTimedCredits(redcred.Type, redcred.Duration, redcred.Rate, redcred.Comment);
            }

            while (true)
            {
                AskMenuCallback(
                    $"Happy 6 month anniversary, {chr.Name}! What would you like to do?#b",
                    ("What are credits?", true, () =>
                        {
                            Next("During events, you will be awarded credits that you can redeem at your leisure. When the credit is redeemed, time will tick down, but only while you're online.");
                        }
                    ),
                    ("Manage my credits", true, () =>
                        {
                            var currentCredits = rc.GetCredits();

                            var calls = new List<(string Item, Action Callback)>();

                            foreach (var cr in currentCredits)
                            {
                                calls.Add(($"{cr.Comment}: {cr.Rate}x {cr.Type} for {cr.DurationGiven:hh} hours", () =>
                                        {
                                            while (true)
                                            {
                                                AskMenuCallback(
                                                    JoinLines(
                                                        $"Name: {cr.Comment}",
                                                        $"Time left: {cr.DurationLeft:hh\\:mm\\:ss} of {cr.DurationGiven:hh\\:mm\\:ss}",
                                                        $"Type: {cr.Rate}x {cr.Type}"
                                                    ),
                                                    ("Enable credit", !cr.Enabled && cr.CreditsLeft > 0, () =>
                                                        {
                                                            cr.Enabled = true;
                                                            OK("This credit has been enabled");
                                                        }
                                                    ),
                                                    ("Disable credit", cr.Enabled && cr.CreditsLeft > 0, () =>
                                                        {
                                                            cr.Enabled = false;
                                                            OK("This credit has been disabled");
                                                        }
                                                    )
                                                );
                                            }
                                        }
                                    ));
                            }

                            if (calls.Count == 0)
                            {
                                OK("There are currently no credits available to use");
                            }
                            else
                            {
                                AskMenuCallback("", calls.ToArray());
                            }
                        }
                    )
                );
            }
        }

        public override void Run()
        {
            StartOfScript:

            Next("This is a sample NPC script");
            BackNext("This tries to have some newlines, and a back button.",
                "This is a second line...");
            BackOK("Final entry.");

            switch (AskMenu("What do you want to try today?",
                (0, "Go back to the beginning."),
                (1, "Please just stop this script"),
                (2, "Ask a question"),
                (3, "Give me EXP"),
                (4, "Give me an item"),
                (5, "Test formatters"),
                (6, "Remove an item"),
                (7, "Add mesos"),
                (8, "Ask pet"),
                (9, "Get Item"),
                (10, "Credits")
            ))
            {
                case 0:
                    goto StartOfScript;

                case 1:
                    OK("Okay, this is the last line.");
                    return;

                case 2:
                    var result = AskYesNo("Will donald trump win or lose the elections?",
                        "Only real answers!")
                        ? "yes"
                        : "no";
                    OK($"This doesn't make sense. Its either Yes or No and you responded with {result}");
                    return;

                case 3:
                    // Give me EXP
                    var expAmount = AskNumber(1, "How much EXP do you want?");
                    AddEXP((uint) expAmount);

                    OK($"You have gained {number(expAmount)} EXP. Good job!");
                    break;

                case 4:
                    // Give item
                    while (true)
                    {
                        var itemID = AskNumber(1002140, "What item do you want?");

                        if (!IsKnownItem(itemID))
                        {
                            OK("What? That's not an item I know...");
                            continue;
                        }

                        if (AskYesNo("Are you sure you want the following item?",
                            $"{itemIcon(itemID)}{red}{itemName(itemID)}"))
                        {
                            GiveItem(itemID);
                            OK("You've got it!");
                        }
                    }

                    break;

                case 5:
                    // Test formatters

                    OK(
                        $"item count: {itemCount(1002140)}",
                        $"item icon: {itemIcon(1002140)}",
                        $"item name: {itemName(1002140)}"
                    );

                    OK(
                        $"Number formatting",
                        $"{number(0)}",
                        $"{number(1000)}",
                        $"{number(10000000)}",
                        $"{number(12345.6789)}"
                    );

                    OK(
                        $"{red}this is red",
                        $"{blue}this is blue",
                        $"{black}this is black",
                        $"{green}and also some green text",
                        $"{bold}bold {notbold}not bold"
                    );

                    OK(
                        $"Mob name: {mobName(100100)}",
                        $"Map name: {mapName(0)}",
                        $"Skill name: {skillName(5001009)}",
                        $"NPC name: {npcName(2002001)}"
                    );

                    break;

                case 6:
                    GiveItem(1002140);
                    OK($"Now you've got a {itemName(1002140)}");
                    if (ItemCount(1002140) > 0)
                    {
                        TakeItem(1002140);
                        OK($"Poof, your {itemName(1002140)} is gone...");
                    }
                    else
                    {
                        OK("WHAT? HACKS");
                    }

                    break;

                case 7:
                    var addMesos = AskYesNo("Press Yes for adding mesos, No for removing mesos");
                    var amount = AskInteger(1, 0, GetMesos(), "How much mesos do you want to " + (addMesos ? "gain" : "lose") + "?");
                    if (addMesos) GiveMesos(amount);
                    else TakeMesos(amount);

                    OK("Done.");
                    break;

                case 8:
                    var petid = AskPet("Hello, choose a pet");
                    OK($"Pet ID: {petid}");
                    break;

                case 9:
                {
                    var categories = Enum.GetValues(typeof(Constants.Items.Types.ItemTypes))
                        .OfType<Constants.Items.Types.ItemTypes>()
                        .Select(x => ((int)x, x.ToString()))
                        .ToArray();

                    while (true)
                    {
                        var onlyForJob = AskYesNo("Limit by job?");

                        // Category selector
                        while (true)
                        {
                            var cat = AskMenu(
                                "Category?",
                                new[] { (9999000, "Go back"), (9999001, "stop") }.Union(categories).ToArray());
                            
                            if (cat == 9999000) break;
                            if (cat == 9999001) return;

                            var startId = cat * 10000;
                            var endId = (cat + 1) * 10000;

                            var inv = Constants.getInventory(startId);

                            IEnumerable<int> itemIDs;

                            if (inv == 1)
                                itemIDs = DataProvider.Equips.Where(x => x.Key >= startId && x.Key < endId && (!onlyForJob || Constants.isRequiredJob(Constants.getJobTrack(Job), x.Value.RequiredJob))).Select(x => x.Key);
                            else if (inv == 5)
                                itemIDs = DataProvider.Pets.Where(x => x.Key >= startId && x.Key < endId).Select(x => x.Key);
                            else
                                itemIDs = DataProvider.Items.Where(x => x.Key >= startId && x.Key < endId).Select(x => x.Key);

                            var entries = itemIDs.Select(x => (x, itemIconAndName(x))).ToArray();

                            var itemID = AskMenu("Item?",
                                new[] { (9999000, "Go back"), (9999001, "stop") }.Union(entries).ToArray()
                            );
                            if (itemID == 9999000) break;
                            if (itemID == 9999001) return;
                            Exchange(0, itemID, 1);
                        }
                    }
                    break;
                }

                case 10: Credit(); break;
            }
        }
    }
}