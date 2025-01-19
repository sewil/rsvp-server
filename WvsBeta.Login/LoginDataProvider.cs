using System;
using System.IO;
using System.Linq;
using WvsBeta.Common.Character;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.Login
{
    class LoginDataProvider
    {
        public static void Load()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            NameCheck.LoadForbiddenName(fileSystem);
            
            CreateCharacterInfo.Init(fileSystem.GetProperty("Etc", "MakeCharInfo.img")["Info"] as WzProperty);
        }
    }

    class CreateCharacterInfo
    {
        public readonly int[] Face;
        public readonly int[] Hair;
        public readonly int[] HairColor;
        public readonly int[] Skin;
        public readonly int[] Pants;
        public readonly int[] Coat;
        public readonly int[] Shoes;
        public readonly int[] Weapon;

        public static CreateCharacterInfo Female { get; private set; }
        public static CreateCharacterInfo Male { get; private set; }

        public CreateCharacterInfo(WzProperty node)
        {
            int[] getIds(WzProperty subNode)
            {
                return subNode.Select(x => x.Value).OfType<int>().ToArray();
            }

            Face = getIds(node.GetProperty("0"));
            Hair = getIds(node.GetProperty("1"));
            HairColor = getIds(node.GetProperty("2"));
            Skin = getIds(node.GetProperty("3"));
            Coat = getIds(node.GetProperty("4"));
            Pants = getIds(node.GetProperty("5"));
            Shoes = getIds(node.GetProperty("6"));
            Weapon = getIds(node.GetProperty("7"));
        }

        public static void Init(WzProperty mainNode)
        {
            Female = new CreateCharacterInfo(mainNode.GetProperty("CharFemale"));
            Male = new CreateCharacterInfo(mainNode.GetProperty("CharMale"));
        }
    }
}
