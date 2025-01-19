using System;
using System.Collections.Generic;
using System.Text;

namespace WzTools
{
    public interface INameSpaceNode : IDisposable
    {
        public ICollection<object> Children { get; }
        
        string GetName();
        object GetParent();
        object GetChild(string key);
        bool HasChild(string key);
    }
}
