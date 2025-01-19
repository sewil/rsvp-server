using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using WzTools.Helpers;

namespace WzTools.Objects
{
    public class WzVector2D : PcomObject
    {
        public int X { get; set; }
        public int Y { get; set; }

        public new int this[string key]
        {
            get => (int)Get(key);
            set => Set(key, value);
        }

        public override void Read(ArchiveReader reader)
        {
            X = reader.ReadCompressedInt();
            Y = reader.ReadCompressedInt();
        }

        public override void Write(ArchiveWriter writer)
        {
            writer.WriteCompressedInt(X);
            writer.WriteCompressedInt(Y);
        }

        public override void Set(string key, object value)
        {
            if (value is int x)
            {
                switch (key)
                {
                    case "X":
                    case "x": X = x; return;
                    case "Y":
                    case "y": Y = x; return;
                }
            }
            throw new InvalidDataException();
        }

        public override object Get(string key)
        {
            switch (key)
            {
                case "X":
                case "x": return X;
                case "Y":
                case "y": return Y;
            }

            return null;
        }

        public override ICollection<object> Children => new List<object>
        {
            X,
            Y
        };
        
        public override bool HasChild(string key) => key.ToLower() == "x" || key.ToLower() == "y";
        
        public override void Dispose()
        {
            
        }

        public static implicit operator Point(WzVector2D vec) => new Point(vec.X, vec.Y);
    }
}
