using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game;
using WvsBeta.SharedDataProvider;

namespace WvsBeta.Shop
{
    public class CharacterInventory : BaseCharacterInventory
    {
        public CharacterInventory(Character character) : base(character.UserID, character.ID)
        {
        }

        public void SaveInventory()
        {
            base.SaveInventory(null);
        }

        public new void LoadInventory()
        {
            base.LoadInventory();
        }
    }
}
