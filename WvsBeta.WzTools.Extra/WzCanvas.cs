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
        private Bitmap? Bitmap { get; set; }

        public WzPixFormat PixFormat
        {
            get => (WzPixFormat)RawPixFormat;
            set => RawPixFormat = (int)value;
        }

        public Bitmap GetImage()
        {
            return Bitmap ??= DeserializeRawData();
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

            return ConvertRawPixelsToBitmap(outputStream);
        }

        public Bitmap ConvertRawPixelsToBitmap(MemoryStream pixels)
        {
            return ConvertRawPixelsToBitmap(pixels, PixFormat, Width, Height);
        }

        public static Bitmap ConvertRawPixelsToBitmap(MemoryStream pixels, WzPixFormat pixFormat, Bitmap baseBitmap)
        {
            return ConvertRawPixelsToBitmap(pixels, pixFormat, baseBitmap.Width, baseBitmap.Height);
        }

        public static Bitmap ConvertRawPixelsToBitmap(MemoryStream pixels, WzPixFormat pixFormat, int width, int height)
        {
            var format = pixFormat switch
            {
                WzPixFormat.R5G6B5 => PixelFormat.Format16bppRgb565,
                _ => PixelFormat.Format32bppArgb,
            };

            using var convertedPixels = ConvertPixels(pixels, pixFormat);

            var output = new Bitmap(width, height, format);
            WritePixelsToImage(output, convertedPixels);

            return output;
        }

        private static void WritePixelsToImage(Bitmap bitmap, MemoryStream pixelsStream)
        {
            ProcessPixelLinesInBitmap(bitmap, (addr, lineSize, tempLineBuff) =>
            {
                pixelsStream.Read(tempLineBuff, 0, lineSize);
                Marshal.Copy(tempLineBuff, 0, addr, lineSize);
            });
        }

        private static void ReadPixelsFromImage(Bitmap bitmap, MemoryStream pixelsStream)
        {
            ProcessPixelLinesInBitmap(bitmap, (addr, lineSize, tempLineBuff) =>
            {
                Marshal.Copy(addr, tempLineBuff, 0, lineSize);
                pixelsStream.Write(tempLineBuff, 0, lineSize);
            });
        }


        private delegate void ProcessPixelLine(nint addr, int lineSize, byte[] tempLineBuff);

        private static void ProcessPixelLinesInBitmap(Bitmap bitmap, ProcessPixelLine process)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            var arrRowLength = rect.Width * (Image.GetPixelFormatSize(bitmap.PixelFormat) / 8);
            var ptr = bmpData.Scan0;
            var line = new byte[arrRowLength];
            for (var i = 0; i < rect.Height; i++)
            {
                process.Invoke(ptr, arrRowLength, line);
                ptr += bmpData.Stride;
            }

            bitmap.UnlockBits(bmpData);
        }

        private static MemoryStream ConvertPixels(MemoryStream input, WzPixFormat pixFormat)
        {
            return pixFormat switch
            {
                WzPixFormat.A4R4G4B4 => ARGB16toARGB32(input, input.Length),
                // can be read as-is
                WzPixFormat.A8R8G8B8 => input,
                // can be read as-is
                WzPixFormat.R5G6B5 => input,
                _ => throw new Exception($"Unsupported PixFormat {pixFormat}")
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

        private static MemoryStream ARGB32toARGB16(MemoryStream input, long inputLen, MemoryStream output)
        {
            for (var i = 0; i < inputLen; i += 2)
            {
                var a = input.ReadByte();
                var b = input.ReadByte();

                byte c = 0;
                c |= (byte)((a / 0x11) << 0);
                c |= (byte)((b / 0x11) << 4);

                output.WriteByte(c);
            }

            output.Position = 0;
            return output;
        }

        const byte bit6_mask = 0x3F;
        const byte bit5_mask = 0x1F;
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


            var output = new MemoryStream((int)(inputLen * 2));
            for (var i = 0; i < inputLen; i += 2)
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


        private static MemoryStream ARGB32toRGB565(MemoryStream input, long inputLen, MemoryStream output)
        {
            for (var i = 0; i < inputLen; i += 4)
            {
                var b = input.ReadByte();
                var g = input.ReadByte();
                var r = input.ReadByte();
                var a = input.ReadByte(); // ignored

                r /= 8;
                g /= 4;
                b /= 8;

                r &= 0b0001_1111;
                g &= 0b0011_1111;
                b &= 0b0001_1111;

                ushort tmp = 0;
                tmp |= (ushort)r;
                tmp <<= 6;
                tmp |= (ushort)g;
                tmp <<= 5;
                tmp |= (ushort)b;

                output.WriteByte((byte)(tmp & 0xFF));
                output.WriteByte((byte)((tmp >> 8) & 0xFF));
            }

            output.Position = 0;
            return output;
        }

        public static Bitmap Convert(Bitmap input, WzPixFormat targetFormat)
        {
            switch (input.PixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppRgb:
                    break;

                default:
                    // Convert it to something more usable
                    input.ConvertFormat(PixelFormat.Format32bppArgb);
                    break;
            }

            using var msInput = new MemoryStream();
            ReadPixelsFromImage(input, msInput);

            msInput.Position = 0;
            var inputLen = msInput.Length;

            using var msOutput = new MemoryStream();

            switch (targetFormat)
            {
                case WzPixFormat.A8R8G8B8: msInput.CopyTo(msOutput); break;
                case WzPixFormat.A4R4G4B4: ARGB32toARGB16(msInput, inputLen, msOutput); break;
                case WzPixFormat.R5G6B5: ARGB32toRGB565(msInput, inputLen, msOutput); break;
            }

            msOutput.Position = 0;

            return ConvertRawPixelsToBitmap(msOutput, targetFormat, input);
        }

        public void ChangeImage(Bitmap bitmap, WzPixFormat format)
        {
            Bitmap = bitmap;
            PixFormat = format;
            Width = Bitmap.Width;
            Height = Bitmap.Height;
        }
    }
}
