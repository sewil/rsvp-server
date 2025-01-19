using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WvsBeta.Center
{
    class MultiPeopleChatLog
    {
        public int[] characterIDs;
        public string[] characterNames;
        public string chatIdentifier;

        [JsonIgnore]
        private string message;

        public MultiPeopleChatLog(string message)
        {
            this.message = message;
        }

        public override string ToString()
        {
            return message;
        }
    }
}
