﻿﻿using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;

  namespace WzTools
{
    public class NameSpaceNode : INameSpaceNode
    {
        public int EndParsePos { get; set; }
        public int Size { get; set; }
        public string Name { get; set; }

        public NameSpaceNode Parent { get; set; }

        public virtual ICollection<object> Children => ImmutableList<object>.Empty;
        public string GetName() => Name;

        public virtual object GetParent() => Parent;

        public virtual object GetChild(string key) => throw new ArgumentException();
        public virtual bool HasChild(string key) => GetChild(key) != null;

        public string NodePath
        {
            get
            {
                var tmp = Parent;
                string ret = Name;
                while (tmp != null)
                {
                    ret = tmp.Name + "/" + ret;
                    tmp = tmp.Parent;
                }

                return ret;
            }
        }

        public virtual void Dispose()
        {
        }
    }
}