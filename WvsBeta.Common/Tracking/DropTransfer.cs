using log4net;
using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.Common.Tracking
{
    public class DropInfo
    {
        public Pos dropPos { get; set; }
        public int ownerID { get; set; }
        public int ownPartyID { get; set; }
        public string ownType { get; set; }
        public int sourceID { get; set; }
        
        public int maxDamageCharacterID { get; set; }
    }
}
