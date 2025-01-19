using System;
using System.IO;
using WzTools.Helpers;
using WzTools.Objects;

namespace WzTools
{
    public class NameSpaceFile : NameSpaceNode
    {
        public virtual ArchiveReader GetReader()
        {
            return null;
        }

        public override string ToString()
        {
            return "File: " + Name;
        }

        protected PcomObject _obj;

        public PcomObject Object
        {
            get
            {
                if (_obj == null)
                {
                    var reader = GetReader();

                    _obj = PcomObject.LoadFromBlob(reader, Size, null, true);

                    if (_obj != null && _obj is WzFileProperty wfp)
                    {
                        wfp.Name = Name;
                        wfp.FileNode = this;
                    }
                }

                return _obj;
            }
            set => _obj = value;
        }

        public override object GetChild(string key) => Object?[key];
        public override bool HasChild(string key) => Object?.HasChild(key) ?? false;
    }
}