using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WzTools.FileSystem;
using WzTools.Helpers;

namespace WzTools.Objects
{
    public abstract class PcomObject : INameSpaceNode
    {
        public abstract string SerializedName { get; }

        public PcomObject Parent = null;

        public abstract ICollection<object> Children { get; }

        public string GetName() => Name;

        public string Name { get; set; }

        public int BlobSize { get; set; }

        public bool IsASCII { get; set; }

        public PcomObject this[string key]
        {
            get => Get(key) as PcomObject;
            set => Set(key, value);
        }

        public static Dictionary<string, Type> ObjectTypes { get; } = [];

        static PcomObject()
        {
            foreach (var t in new[] { typeof(WzBareCanvas), typeof(WzList), typeof(WzSound), typeof(WzVector2D), typeof(WzConvex2D), typeof(WzUOL) })
            {
                ConstructObject(t).Register();
            }
        }

        public void Register()
        {
            RegisterObjectType(SerializedName, GetType());
        }
        
        public static void RegisterObjectType<T>() where T : PcomObject
        {
            ConstructObject(typeof(T)).Register();
        }

        public static void RegisterObjectType(string typeName, Type type)
        {
            ObjectTypes[typeName] = type;
        }

        static PcomObject ConstructObject(Type objectType)
        {
            return Activator.CreateInstance(objectType) as PcomObject;
        }

        public static void PrepareEncryption(ArchiveReader reader)
        {
            var start = reader.BaseStream.Position;
            var t = reader.ReadByte();
            if (t == 'A' || t == '#')
            {
                // not needed
            }
            else
            {
                string type = reader.ReadStringWithID(t, 0x1B, 0x73);
                switch (type)
                {
                    // Only a Property is valid on this level
                    case "Property":
                        break;

                    default:
                        throw new Exception($"Don't know how to read this proptype: {type}");
                }
            }
            reader.BaseStream.Position = start;
        }

        public static PcomObject LoadFromBlob(ArchiveReader reader, int blobSize = 0, string name = null, bool isFileProp = false)
        {
            var start = reader.BaseStream.Position;
            var t = reader.ReadByte();
            var type = "";

            if (t == 'A')
            {
                return null;
            }

            PcomObject obj;
            bool ascii = false;
            if (t == '#')
            {
                blobSize = (int)reader.BaseStream.Length;
                type = reader.ReadAndReturn(() =>
                {
                    // Try to read #Property

                    var text = Encoding.ASCII.GetString(reader.ReadBytes(Math.Min(100, blobSize)));
                    var firstLine = text.Split('\n')[0].Trim();
                    return firstLine;
                });

                reader.BaseStream.Position += type.Length + 2; // \r\n
                ascii = true;
            }
            else
            {
                type = reader.ReadStringWithID(t, 0x1B, 0x73);
            }

            switch (type)
            {
                case "Property":
                    obj = isFileProp ? new WzFileProperty() : new WzProperty();
                    break;
                default:
                    if (ObjectTypes.TryGetValue(type, out var objectType))
                    {
                        obj = ConstructObject(objectType);
                    }
                    else
                    {
                        Console.WriteLine("Don't know how to read this proptype: {0}", type);
                        return null;
                    }
                    break;
            }

            if (t == '#' && !(obj is WzProperty))
            {
                // Unable to handle non-wzprops???
                return null;
            }

            obj.BlobSize = blobSize - (int)(reader.BaseStream.Position - start);
            obj.Name = name;
            obj.IsASCII = ascii;
            obj.Read(reader);
            return obj;
        }

        public static void WriteToBlob(ArchiveWriter writer, PcomObject obj)
        {
            if (obj is WzProperty prop && prop.IsASCII)
            {
                using var sw = new StreamWriter(writer.BaseStream);
                prop.write_ascii(sw);
                return;
            }

            writer.Write(obj.SerializedName, 0x1B, 0x73);
            obj.Write(writer);
        }

        public abstract void Read(ArchiveReader reader);

        public abstract void Write(ArchiveWriter writer);

        public abstract void Set(string key, object value);
        public abstract object Get(string key);

        public virtual bool HasChild(string key) => Get(key) != null;

        public string GetFullPath()
        {
            string ret = Name;
            var curParent = (INameSpaceNode)GetParent();
            while (curParent != null)
            {
                ret = curParent.GetName() + "/" + ret;
                curParent = (INameSpaceNode)curParent.GetParent();
            }

            return ret;
        }

        public override string ToString()
        {
            return base.ToString() + ", Path: " + GetFullPath();
        }

        public abstract void Dispose();

        public virtual object GetParent() => Parent;

        public object GetChild(string key) => Get(key);


        public INameSpaceNode GetNode(string path)
        {
            INameSpaceNode ret = this;
            foreach (string node in path.Trim('/').Split('/'))
            {
                if (string.IsNullOrEmpty(node))
                    break;

                ret = ret?.GetChild(node) as PcomObject;

                if (ret == null)
                    return null;
                if (ret is WzUOL uol)
                    ret = uol.ActualObject(true) as PcomObject;
            }

            if (ret is FSFile file)
                ret = file.Object;

            return ret;
        }

    }
}
