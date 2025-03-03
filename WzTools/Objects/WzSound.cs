﻿﻿using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.IO;
  using WzTools.Helpers;

  namespace WzTools.Objects
{
    public class WzSound : PcomObject
    {
        public override string SerializedName => "Sound_DX8";
        public byte[] Blob = null;
        public override void Read(ArchiveReader reader)
        {
            Blob = reader.ReadBytes(BlobSize);
        }

        public override void Write(ArchiveWriter writer)
        {
            writer.Write(Blob);
        }

        public override void Set(string key, object value)
        {
            return;
        }

        public override object Get(string key)
        {
            return null;
        }

        public override ICollection<object> Children { get; } = ImmutableList<object>.Empty;

        public override bool HasChild(string key)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            Blob = null;
        }
    }
}
