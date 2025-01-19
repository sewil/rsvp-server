using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Providers;
using WzTools.FileSystem;
using WzTools.Objects;
using TranslatedTexts = System.Collections.Generic.Dictionary<string, string>;

namespace WvsBeta.Game.GameObjects
{
    public class CommunicatorProvider : TemplateProvider
    {
        public static Dictionary<byte, Selector> Selectors { get; private set; } = new Dictionary<byte, Selector>();
        public static Dictionary<byte, MessageType> MessageTypes { get; private set; } = new Dictionary<byte, MessageType>();

        protected CommunicatorProvider(WzFileSystem fileSystem)
        : base(fileSystem)
        {

        }

        private new static ILog _log = LogManager.GetLogger(typeof(MapProvider));

        public static void Load()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            new CommunicatorProvider(fileSystem).LoadAll();
            _log.Info($"Selectors: {Selectors.Count}");
            _log.Info($"MessageTypes: {MessageTypes.Count}");
        }

        public void LoadAll()
        {
            LoadMessageTypes();
            LoadSelectors();
        }

        private static void LoadTranslatedTexts(WzProperty prop, TranslatedTexts tt)
        {
            // Load key = value pairs
            // en = Hello
            // nl = Hallo
            tt.Clear();
            foreach (var p in prop.Keys)
            {
                tt[p] = prop.GetString(p);
            }
        }

        private void LoadMessageTypes()
        {
            var messageTypes = new Dictionary<byte, MessageType>();
            IterateOverIndexed(FileSystem.GetProperty("Etc/Communicator.img"), (id, mtNode) =>
            {
                MessageTypes.TryGetValue((byte)id, out var messageType);
                messageType ??= new MessageType();

                messageType.ID = (byte)id;
                LoadTranslatedTexts(mtNode.GetProperty("name"), messageType.Names);

                var messages = new Dictionary<byte, Message>();

                IterateOverIndexed(mtNode, (index, prop) =>
                {
                    messageType.Messages.TryGetValue((byte)index, out var msg);
                    msg ??= new Message();

                    msg.ID = (byte)index;
                    msg.SelectorID = prop.GetUInt8("type") ?? 0;
                    msg.MessageTypeID = messageType.ID;
                    LoadTranslatedTexts(prop, msg.Lines);
                    
                        
                    msg.LogText = msg.Lines["en"]
                        .Replace("...", " %s ")
                        .Replace("  ", " ")
                        .Replace("%s ?", "%s?").Trim();

                    messages[msg.ID] = msg;
                });

                // We don't know which ones were deleted, so replace them
                messageType.Messages = messages;

                messageTypes[messageType.ID] = messageType;
            });


            // We don't know which ones were deleted, so replace them
            MessageTypes = messageTypes;
        }


        private void LoadSelectors()
        {
            var skillNamesImg = FileSystem.GetProperty("String/Skill.img");

            // These are not zero-indexed
            foreach (var csNode in FileSystem.GetProperty("Etc/CommunicatorSelection.img").PropertyChildren)
            {
                var id = Convert.ToByte(csNode.Name);
                Selectors.TryGetValue(id, out var selector);
                selector ??= new Selector();

                selector.ID = id;
                selector.Name = csNode.GetString("name");
                selector.CategoryType = csNode.GetUInt8("categoryType") ?? 0;

                var optionsNode = csNode.GetProperty("options");
                var categoryNode = csNode.GetProperty("category");
                var categories = new Dictionary<byte, Category>();

                if (categoryNode != null)
                {
                    IterateOverIndexed<object>(categoryNode, (index, prop) =>
                    {
                        selector.Categories.TryGetValue((byte)index, out var category);
                        category ??= new Category();

                        category.ID = (byte)index;

                        if (prop is WzProperty p)
                        {
                            // named data
                            LoadTranslatedTexts(p, category.Names);
                        }
                        else if (prop is string s)
                        {
                            // Plain string
                            category.Names.Clear();
                            category.Names["en"] = s;
                        }
                        else if (selector.ID == 6)
                        {
                            // Jobs
                            var jobId = (int)prop;
                            var jobName = DataProvider.Jobs[jobId];

                            category.Names.Clear();
                            category.Names["en"] = jobName;
                        }

                        categories[category.ID] = category;
                    });
                }
                else
                {
                    var defaultCategory = categories[0] = new Category();
                    defaultCategory.ID = 0;
                    defaultCategory.Names = new TranslatedTexts { { "en", "default" } };
                }
                
                selector.Categories = categories;

                IterateOverIndexed(optionsNode, (categoryID, optionNode) =>
                {
                    if (!selector.Categories.TryGetValue((byte)categoryID, out var category))
                    {
                        _log.Error($"Found missing category for options node {categoryID} for {optionsNode.GetFullPath()}");
                        return;
                    }


                    IterateOverIndexed<object>(optionNode, (categoryOptionID, categoryOptionNode) =>
                    {
                        category.Options.TryGetValue((short)categoryOptionID, out var names);
                        names ??= new TranslatedTexts();

                        if (categoryOptionNode is string tmpStr && int.TryParse(tmpStr, out var strId))
                            categoryOptionNode = strId;

                        if (categoryOptionNode is WzProperty prop)
                        {
                            LoadTranslatedTexts(prop, names);
                        }
                        else if (categoryOptionNode is string nodeName)
                        {
                            names.Clear();
                            names["en"] = nodeName;
                        }
                        else if (categoryOptionNode is int id)
                        {
                            var str = "???";
                            switch (selector.ID)
                            {
                                case 2:
                                    str = MapProvider.Maps[id].Name;
                                    break;
                                case 3:
                                    // Let the client render the mob instead.
                                    str = id.ToString();
                                    break;
                                case 4:
                                    str = Constants.isEquip(id) ? DataProvider.Equips[id].Name : DataProvider.Items[id].Name;
                                    break;
                                case 5:
                                    str = DataProvider.NPCs[id].Name;
                                    break;
                                case 6:
                                    str = skillNamesImg.GetProperty(id)?.GetString("name") ?? $"Unknown skill {id}";
                                    break;
                            }

                            if (str == null)
                            {
                                throw new Exception($"Unable to load string for option # {selector.ID}: {id}");
                            }

                            names.Clear();
                            names["en"] = str;
                        }
                        else
                        {
                            _log.Error($"Unable to process {categoryOptionNode} ({categoryOptionNode?.GetType()})");
                        }

                        category.Options[(short)categoryOptionID] = names;
                    });
                });

                Selectors[selector.ID] = selector;
            }
        }
    }

    /// <summary>
    /// A message is a line of text that can be selected and said by the player.
    /// Sometimes a message has a placeholder. This is then replaced by a value from a selector.
    /// </summary>
    public class Message
    {
        public byte ID;
        public byte MessageTypeID;
        // AKA "type" node
        public byte SelectorID;
        public TranslatedTexts Lines { get; } = new TranslatedTexts();
        // The text/formatter that is used in logging (on server side)
        public string LogText;
    }

    /// <summary>
    /// A "message type" has a name, and a list of messages to send.
    /// It is displayed on the left side of the UI.
    /// </summary>
    public class MessageType
    {
        public byte ID;
        public TranslatedTexts Names { get; } = new TranslatedTexts();
        public Dictionary<byte, Message> Messages { get; set; } = new Dictionary<byte, Message>();
    }

    /// <summary>
    /// A "selector" is a placeholder selector, that is displayed on the right side of the UI.
    /// </summary>
    public class Selector
    {
        public byte ID;
        public string Name;
        /// <summary>
        /// This is used to display the text left to the category dropdown
        /// </summary>
        public byte CategoryType;

        public Dictionary<byte, Category> Categories { get; set; } = new Dictionary<byte, Category>();
    }

    public class Category
    {
        public byte ID;
        public byte SelectorID;
        public TranslatedTexts Names { get; set; } = new TranslatedTexts();
        public Dictionary<short, TranslatedTexts> Options { get; set; } = new Dictionary<short, TranslatedTexts>();
    }


}
