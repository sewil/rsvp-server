using System;
 using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
 using WzTools.Objects;

 namespace WzTools.FileSystem
{
    public class WzFileSystem
    {
        public string RealPath; 
        
        public WzProperty GetProperty(string path)
        {
            if (!path.Contains(".img")) return null;
            
            return GetNode(Path.Combine(RealPath, path)) as WzProperty;
        }

        public WzProperty GetProperty(params string[] path)
            => GetProperty(string.Join('/', path));
        
        public INameSpaceNode GetNode(string path)
        {
            INameSpaceNode ret = new FSFile(path);
            
            foreach (string node in path.Substring(path.IndexOf(".img", StringComparison.Ordinal) + 4).Trim('/').Split('/'))
            {
                if (string.IsNullOrEmpty(node))
                    break;
                
                ret = ret?.GetChild(node) as INameSpaceNode;

                if (ret == null)
                    return null;
                if (ret is WzUOL uol)
                    ret = uol.ActualObject(true) as INameSpaceNode;
            }

            if (ret is FSFile file)
                ret = file.Object;

            return ret;
        }
        
        public IEnumerable<WzProperty> GetPropertiesInDirectory(string path)
        {
            if (path.Contains(".img")) return new WzProperty[]{};

            return Directory
                .GetFiles(Path.Combine(RealPath, path), "*.img", SearchOption.AllDirectories)
                .AsParallel()
                .Select(file => GetNode(Path.Combine(RealPath, path, file)) as WzProperty);
        }

        public void Init(string folder)
        {
            RealPath = folder;
        }
    }
}
