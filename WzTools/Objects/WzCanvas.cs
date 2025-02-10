using System;
using System.Collections.Generic;
using System.Diagnostics;
using WvsBeta.WzTools.Helpers;
using WzTools.Helpers;

namespace WzTools.Objects
{
    public class WzCanvas : WzProperty
    {
        public static bool DebugOffsets = false;
        byte[] RawData;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int PixFormat { get; private set; }
        public int MagLevel { get; private set; }


        public override void Read(ArchiveReader reader)
        {
            Debug.WriteLineIf(DebugOffsets, $"Start reading WzCanvas at {reader.BaseStream.Position}");

            if (reader.ReadByte() != 0) throw new Exception("Expected 0 is not zero");

            if (reader.ReadByte() != 0)
            {
                base.Read(reader);
            }
            else
            {
                _objects = new ObjectStore();
            }

            Width = reader.ReadCompressedInt();
            Height = reader.ReadCompressedInt();
            PixFormat = reader.ReadCompressedInt();
            MagLevel = reader.ReadCompressedInt();

            for (var i = 0; i < 4; i++)
                reader.ReadCompressedInt();

            var blobSize = reader.ReadInt32();

            RawData = reader.ReadBytes(blobSize);

            Debug.WriteLineIf(DebugOffsets, $"Finished reading WzCanvas at {reader.BaseStream.Position}");
        }

        public override void Write(ArchiveWriter writer)
        {
            Debug.WriteLineIf(DebugOffsets, $"Start writing WzCanvas at {writer.BaseStream.Position}");

            writer.Write((byte)0);

            if (_objects.Count > 0)
            {
                writer.Write((byte)1);
                base.Write(writer);
            }
            else
            {
                writer.Write((byte)0);
            }

            writer.WriteCompressedInt(Width);
            writer.WriteCompressedInt(Height);
            writer.WriteCompressedInt(PixFormat);
            writer.WriteCompressedInt(MagLevel);

            for (var i = 0; i < 4; i++)
                writer.WriteCompressedInt((int)0);

            // Note: not compressed
            writer.Write((int)RawData.Length);
             
            writer.Write(RawData);
            Debug.WriteLineIf(DebugOffsets, $"Finish writing WzCanvas at {writer.BaseStream.Position}");

        }
    }
}
