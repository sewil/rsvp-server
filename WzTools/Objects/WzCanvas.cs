using System;
using System.Collections.Generic;
using WvsBeta.WzTools.Helpers;
using WzTools.Helpers;

namespace WzTools.Objects
{
    public class WzCanvas : WzProperty
    {
        public override void Read(ArchiveReader reader)
        {
            if (reader.ReadByte() != 0) throw new Exception("Expected 0 is not zero");

            if (reader.ReadByte() != 0)
            {
                base.Read(reader);
                return;
            }

            _objects = new ObjectStore();
        }
    }
}
