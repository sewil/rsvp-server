using System.Collections.Generic;
using System.IO;
using System.Linq;
using WvsBeta.Common;
using WzTools.FileSystem;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class PetProvider : TemplateProvider<PetData>
    {
        public PetProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }
        
        private static List<string> nonActions = new List<string>
        {
            "info", "monolog", "interact", "food", "move", 
            "stand0", "stand1", "jump", "hungry", "rest0", "rest1", "hang"
        };

        public override IDictionary<int, PetData> LoadAll()
        {
            var properties = FileSystem.GetPropertiesInDirectory("Item/Pet")
                .Where(property => int.TryParse(property.Name.Replace(".img", ""), out int id) && id >= 5000000)
                .Where(property => property.HasChild("info"));
            
            return IterateAllToDict(properties, property =>
            {
                var infoNode = property.GetProperty("info");

                var petData = new PetData
                {
                    ID = (int) Utils.ConvertNameToID(property.Name),
                    Hungry = infoNode.GetUInt8("hungry") ?? 0,
                    Life = infoNode.GetUInt8("life") ?? 0,
                    Reactions = new Dictionary<byte, PetReactionData>(),
                    Actions = new List<string>(),
                };

                petData.Name = FileSystem.GetProperty($"String/Item.img/Pet/{petData.ID}").GetString("name");

                foreach (var interactProperty in property.GetProperty("interact").PropertyChildren)
                {
                    petData.Reactions.Add(byte.Parse(interactProperty.Name), new PetReactionData
                    {
                        ReactionID = byte.Parse(interactProperty.Name),
                        Inc = interactProperty.GetUInt8("inc") ?? 0,
                        Prob = interactProperty.GetUInt8("prob") ?? 0,
                        LevelMin = interactProperty.GetUInt8("l0") ?? 0,
                        LevelMax = interactProperty.GetUInt8("l1") ?? 0,
                    });
                }

                foreach (var p in property.PropertyChildren)
                {
                    if (nonActions.Contains(p.Name)) continue;
                    petData.Actions.Add(p.Name);
                }
                
                return petData;
            }, x => x.ID);
        }

    }
}