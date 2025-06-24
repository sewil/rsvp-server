using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WvsBeta.Common;
using WvsBeta.Common.Tracking;

namespace WvsBeta.Game
{
    public class Exchange
    {
        private static ILog _log = LogManager.GetLogger("Exchange");

        public Character Character { get; private set; }

        private readonly List<(int templateId, int quantity)> _items = new List<(int, int)>();
        private readonly List<BaseItem> _giveItems = new List<BaseItem>();
        private readonly List<int> _money = new List<int>();
        private int _multiply = 1;

        private readonly bool _showInChat = true;

        // The reference is the reference for the trade, eg NPC ID or other player info.
        private readonly object _reference;
        private readonly string _transferID;
        private ItemVariation _variation = ItemVariation.None;

        public Exchange(Character character, object reference, bool showInChat = true, string transferID = null)
        {
            Character = character;
            _showInChat = showInChat;
            _reference = reference;

            if (_reference is IScriptV2 script) _variation = script.ItemVariation;

            if (transferID != null)
                _transferID = transferID;
            else
                _transferID = "" + Character.ID + "-exchange-" + Rand32.NextBetween();
        }

        public Exchange SetVariation(ItemVariation variation)
        {
            _variation = variation;
            return this;
        }

        private void AddToQuantity(int templateId, int addAmount)
        {
            if ((
                    addAmount < 0 ||
                    (
                        !Constants.isEquip(templateId) &&
                        !Constants.isRechargeable(templateId)
                    )
                ) &&
                _items.Exists(tuple => tuple.templateId == templateId))
            {
                int newQuantity = _items.First(tuple => tuple.templateId == templateId).quantity + addAmount;
                _items.Remove(_items.First(tuple => tuple.templateId == templateId));
                _items.Add((templateId, newQuantity));
                return;
            }

            _items.Add((templateId, addAmount));
        }

        public Exchange GiveItem(int templateId, int quantity = 1)
        {
            if (Constants.isEquip(templateId) && quantity > 0)
            {
                for (int i = 0; i < quantity; i++)
                {
                    // Roll new item for the user
                    var equip = new EquipItem();
                    equip.ItemID = templateId;
                    equip.GiveStats(_variation);

                    GiveItem(equip);
                }
            }
            else
            {
                AddToQuantity(templateId, quantity);
            }

            return this;
        }

        public Exchange GiveItem(BaseItem equip)
        {
            _giveItems.Add(equip);
            return this;
        }

        public Exchange TakeItem(int templateId, int quantity = 1)
        {
            AddToQuantity(templateId, -quantity);

            return this;
        }

        public Exchange Multiply(int times)
        {
            _multiply = times;

            return this;
        }

        public Exchange GiveMoney(int money)
        {
            _money.Add(money);

            return this;
        }

        public Exchange TakeMoney(int money)
        {
            _money.Add(-money);

            return this;
        }

        private void ApplyPerReferenceType(Action<int> whenNPC, Action<Character> whenCharacter)
        {
            if (_reference is Character chr) whenCharacter(chr);
            else if (_reference is IScriptV2 script) whenNPC(script.NpcID);
        }

        public bool Perform()
        {
            if (!Check(out var takeItems, out var rawItemsToGive, out int money))
                return false;

            try
            {
                rawItemsToGive.ForEach(item => Character.Inventory.DistributeItemInInventory(item));
                takeItems.ForEach(tuple => Character.Inventory.TakeItem(tuple.Item1, (short)tuple.Item2));
            }
            catch (AggregateException ex)
            {
                _log.Error("Unable to perform exchange", ex);
                //todo: Rollback? Shouldn't really happen if the above checks are correct
                return false;

                throw;
            }

            if (money != 0)
            {
                Character.AddMesos(money);


                ApplyPerReferenceType(npcId =>
                {
                    if (money > 0)
                    {
                        MesosTransfer.PlayerReceivedFromNPC(
                            Character.ID,
                            npcId,
                            money,
                            _transferID
                        );
                    }
                    else
                    {
                        MesosTransfer.PlayerGaveToNPC(
                            Character.ID,
                            npcId,
                            -money, // This is already converted internally
                            _transferID
                        );
                    }
                }, chr =>
                {
                    // The inverse will be processed by the other player' exchange
                    // We only give mesos here
                    if (money > 0)
                    {
                        MesosTransfer.PlayerTradeExchange(
                            chr.ID,
                            Character.ID,
                            money,
                            _transferID
                        );
                    }
                });
            }

            takeItems.ForEach(t =>
            {
                var templateId = t.Item1;
                var quantity = t.Item2;

                if (_showInChat)
                {
                    QuestPacket.SendGainItemChat(Character, (templateId, -quantity));
                }

                ApplyPerReferenceType(npcId =>
                {
                    ItemTransfer.PlayerGaveToNPC(
                        Character.ID,
                        npcId,
                        templateId,
                        (short)quantity,
                        _transferID,
                        null
                    );
                }, chr =>
                {
                    // The inverse will be processed by the other player' exchange
                    // We only give items here
                });
            });
            
            rawItemsToGive.ForEach(x =>
            {
                var (templateId, quantity, itemOpt) = (x.ItemID, x.Amount, x);
                if (_showInChat)
                {
                    // Could in theory optimize this by using rawItemsToGive.Select(), but at the same time that gives a bit more overhead for no real benefit (1 packet over 10? meh)
                    QuestPacket.SendGainItemChat(Character, (templateId, quantity));
                }

                ApplyPerReferenceType(npcId =>
                {
                    ItemTransfer.PlayerReceivedFromNPC(
                        Character.ID,
                        npcId,
                        templateId,
                        quantity,
                        _transferID,
                        itemOpt
                    );
                }, chr =>
                {
                    // The inverse will be processed by the other player' exchange
                    // We only give items here
                    ItemTransfer.PlayerTradeExchange(
                        chr.ID,
                        Character.ID,
                        templateId,
                        quantity,
                        _transferID,
                        itemOpt
                    );
                });
            });

            return true;
        }

        public bool Check()
            => Check(out _, out _, out _);

        // These arguments aren't the prettiest sorry I fix later
        private bool Check(out (int itemID, int amount)[] takeItems, out BaseItem[] giveItems, out int money)
        {
            if (_multiply > 1)
            {
                var oldList = _items.ToList();
                _items.Clear();
                oldList.ForEach(t => _items.Add((t.templateId, t.quantity * _multiply)));
            }

            takeItems = _items
                .Where(tuple => tuple.quantity < 0)
                .Select(tuple => (tuple.templateId, -tuple.quantity))
                .ToArray();

            giveItems = _items
                .Where(tuple => tuple.quantity > 0)
                .SelectMany(tuple =>
                {
                    (int templateId, int quantity) = tuple;
                    return BaseItem.CreateMultipleFromItemID(templateId, (short)quantity);
                })
                .Concat(_giveItems)
                .ToArray();

            money = _money.Sum(i => i) * _multiply;

            if (giveItems
                .Any(x => DataProvider.IsOnlyItem(x.ItemID) && Character.Inventory.HasItem(x.ItemID)))
            {
                _log.Warn("Exchange failed: user already has an only-item.");
                return false;
            }

            if (money < 0 && -money > Character.Inventory.Mesos)
            {
                _log.Warn($"Exchange failed: losing more mesos ({money}) than user has ({Character.Inventory.Mesos}.");
                return false;
            }

            if (money > 0 && (long)Character.Inventory.Mesos + money > int.MaxValue)
            {
                _log.Warn($"Exchange failed: gaining more mesos than user can hold ({Character.Inventory.Mesos} + {money}).");
                return false;
            }

            if (takeItems.Any(tuple => Character.Inventory.ItemCount(tuple.Item1) < tuple.Item2))
            {
                _log.Warn("Exchange failed: unable to take enough items");
                return false;
            }

            //todo: check for when items get taken away, if the items would fit afterwards
            //todo: check slots for all items at the same time, at the moment if you would get
            // a red potion and an orange potion at the same time, even if you have a non-full slot of 
            // red potions and 1 use slot available, it won't work
            if (!Character.Inventory.HasSlotsFreeForItems(giveItems))
            {
                _log.Warn("Exchange failed: not enough free slots for items");
                return false;
            }

            return true;
        }
    }
}