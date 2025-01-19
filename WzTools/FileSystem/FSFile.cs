using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using WzTools.Helpers;
using WzTools.Objects;

namespace WzTools.FileSystem
{
    public class FSFile : NameSpaceFile, IEnumerable<WzProperty>
    {
        public string RealPath { get; set; }
        
        public override ArchiveReader GetReader()
        {
            var fs = new FileStream(RealPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Don't preload files larger than 10MB  
            if (fs.Length > 10 * 1024 * 1024)
            {
                return new ArchiveReader(fs);
            }
           
            var ms = new MemoryStream(new byte[fs.Length], true);

            fs.CopyTo(ms);
            fs.Dispose();
            ms.Position = 0;

            return new ArchiveReader(ms);
        }

        public FSFile() : base() {}
        public FSFile(string path)
        {
            Name = Path.GetFileName(path);
            RealPath = path.Substring(0, path.IndexOf(".img", StringComparison.Ordinal) + 4);
        }

        // Helper for iterating over a Property
        public IEnumerator<WzProperty> GetEnumerator()
        {
            return ((WzProperty)Object).PropertyChildren.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
