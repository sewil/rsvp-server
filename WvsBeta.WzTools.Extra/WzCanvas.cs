using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using WzTools.Helpers.Helpers;
using WzTools.Objects;

namespace WzTools.Extra
{
    public class WzCanvas : WzBareCanvas
    {
        private Bitmap Bitmap { get; set; }

        public WzPixFormat PixFormat => (WzPixFormat)RawPixFormat;

        public Bitmap GetImage()
        {
            if (Bitmap != null) return Bitmap;

            return Bitmap = DeserializeRawData();

        }

        void ValidateHeader()
        {
            if (Width >= 0x10000)
                throw new Exception($"Invalid Width: {Width}");
            if (Height >= 0x10000)
                throw new Exception($"Invalid Height: {Height}");

            if (!(
                PixFormat == WzPixFormat.A4R4G4B4 ||
                PixFormat == WzPixFormat.A8R8G8B8 ||
                PixFormat == WzPixFormat.R5G6B5
            // DXT images are currently not supported
            //||
            //PixFormat == WzPixFormat.DXT3 ||
            //PixFormat == WzPixFormat.DXT5
            ))
            {
                throw new Exception($"Invalid PixFormat: {PixFormat:D}");
            }
        }

        Bitmap DeserializeRawData()
        {
            ValidateHeader();

            using var outputStream = new MemoryStream();
            using var inputStream = new MemoryStream(RawData);
            using var reader = new BinaryReader(inputStream);
            var dataSize = inputStream.Length;

            if (reader.ReadByte() != 0) throw new Exception("Expected 0 is not zero");

            var isZlibCompression = reader.PeekChar() == 0x78;

            var blob = new byte[Math.Min(0x20000, dataSize)];
            if (isZlibCompression)
            {
                // Seems to be a zlib stream
                // skip zlib header
                reader.ReadByte();
                reader.ReadByte();

                using var deflate = new DeflateStream(inputStream, CompressionMode.Decompress);
                while (inputStream.Position < inputStream.Length)
                {
                    deflate.CopyTo(outputStream);
                    deflate.Flush();
                }

            }
            else
            {
                // Not tested.
                inputStream.CopyTo(outputStream);
            }


            outputStream.Position = 0;
            using var convertedPixels = ConvertPixels(outputStream);

            var arr = convertedPixels.ToArray();
            var format = PixFormat switch
            {
                WzPixFormat.R5G6B5 => PixelFormat.Format16bppRgb565,
                _ => PixelFormat.Format32bppArgb,
            };

            var output = new Bitmap(Width, Height, format);
            var rect = new Rectangle(0, 0, output.Width, output.Height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);

            var arrRowLength = rect.Width * (Image.GetPixelFormatSize(output.PixelFormat) / 8);
            var ptr = bmpData.Scan0;
            var line = new byte[arrRowLength];
            for (var i = 0; i < rect.Height; i++)
            {
                convertedPixels.Read(line, 0, arrRowLength);
                Marshal.Copy(line, 0, ptr, arrRowLength);
                ptr += bmpData.Stride;
            }

            output.UnlockBits(bmpData);

            return output;
        }


        private MemoryStream ConvertPixels(MemoryStream input)
        {
            return PixFormat switch
            {
                WzPixFormat.A4R4G4B4 => ARGB16toARGB32(input, input.Length),
                WzPixFormat.A8R8G8B8 => input,
                WzPixFormat.R5G6B5 => input,
                _ => throw new Exception($"Unsupported PixFormat {PixFormat}")
            };
        }

        private static MemoryStream ARGB16toARGB32(MemoryStream input, long inputLen)
        {
            var output = new MemoryStream((int)(inputLen * 2));
            for (var i = 0; i < inputLen; i++)
            {
                var a = input.ReadByte();

                var c_g = (byte)((a & 0x0F) * 0x11);
                var c_b = (byte)((a >> 4) * 0x11);
                output.WriteByte(c_g);
                output.WriteByte(c_b);
            }

            output.Position = 0;
            return output;
        }

        private static MemoryStream ARGB32toARGB16(MemoryStream input, long inputLen)
        {
            var output = new MemoryStream((int)(inputLen / 2));
            for (var i = 0; i < inputLen; i++)
            {
                var a = input.ReadByte();
                var b = input.ReadByte();

                byte c = 0;
                c |= (byte)((a / 0x11) << 4);
                c |= (byte)((b / 0x11) << 0);

                output.WriteByte(c);
            }

            output.Position = 0;
            return output;
        }

        private static MemoryStream RGB565toARGB32(MemoryStream input, long inputLen)
        {
            // 16 bit colors to 32 bit colors...
            // 5 bit value = 256 / 32 = 8
            // 6 bit value = 256 / 64 = 4


            // 1011100101111100
            // |   |, shift right 3
            // 1011100101111100
            //      | xx |, byte 1 & 0x7 << 5 | byte 2 >> 5
            // 1011100101111100
            //            |   |, byte 2 & 0x

            const byte bit6_mask = 0x3F;
            const byte bit5_mask = 0x1F;

            var output = new MemoryStream((int)(inputLen * 2));
            for (var i = 0; i < inputLen; i++)
            {
                var low = input.ReadByte();
                var high = input.ReadByte();

                byte r = (byte)((low >> 3) & bit5_mask);
                r *= 8;

                byte g = (byte)((((low & 0x7) << 5 | high) >> 5) & bit6_mask);
                g *= 4;

                byte b = (byte)(high & bit5_mask);
                b *= 8;

                output.WriteByte(0xFF); // full alpha
                output.WriteByte(r);
                output.WriteByte(g);
                output.WriteByte(b);
            }

            output.Position = 0;
            return output;
        }
    }
}
