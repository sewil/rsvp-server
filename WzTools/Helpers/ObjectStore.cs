using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.WzTools.Helpers
{
    /// <summary>
    /// ObjectStore is a Case Sensitive dict<string, object>
    /// </summary>
    public class ObjectStore : Dictionary<string, object>
    {
        public ObjectStore() : base(StringComparer.InvariantCulture) {}
        public ObjectStore(int amount) : base(amount, StringComparer.InvariantCulture) {}
    }
}
