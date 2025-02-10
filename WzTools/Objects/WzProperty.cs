using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Linq;
using WvsBeta.WzTools.Helpers;
using WzTools.Helpers;
using Int8 = System.SByte;
using UInt8 = System.Byte;

namespace WzTools.Objects
{
    public class WzProperty : PcomObject, IEnumerable<KeyValuePair<string, object>>
    {
        public static bool DebugOffsets = false;

        protected ObjectStore _objects;

        // Old versions use Dispatch for blobs. In newer versions, there is an explicit conversion when
        // writing Dispatch to save it as Unknown instead.
        private static bool UseDispatchForBlobs = true;

        public enum WzVariantType
        {
            // https://msdn.microsoft.com/en-us/library/cc237865.aspx
            EmptyVariant = 0,
            Int16Variant = 2,
            Int32Variant = 3,
            Float32Variant = 4,
            Float64Variant = 5,
            CYVariant = 6, // Currency
            DateVariant = 7,
            BStrVariant = 8,
            DispatchVariant = 9,  // In MS terms, sub PcomObject
            BoolVariant = 11, // 16-bit, because 'typedef __int16 VARIANT_BOOL'
            UnknownVariant = 13,
            Int8Variant = 16,
            Uint8Variant = 17,
            Uint16Variant = 18,
            Uint32Variant = 19,
            Int64Variant = 20,
            Uint64Variant = 21,
            // NullVariant  = 1
            // ErrorVariant  = 10
            // VariantVariant  = 12
            // DecimalVariant  = 14
            // .. does not exist

            // IntVariant  = 22
            // UintVariant  = 23
            // VoidVariant  = 24
            // HResultVariant  = 25
            // PtrVariant  = 26
            // SafeArrayVariant  = 27
            // CArrayVariant  = 28
            // UserDefinedVariant  = 29
            // LPStrVariant  = 30
            // LPWStrVariant  = 31
            // RecordVariant  = 32
            // IntPtrVariant  = 33
            // UintPtrVariant  = 34
            // ArrayVariant  = 35
            // ByRefVariant  = 36
        }

        public WzProperty() { }

        public new object this[string key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        public ulong? GetUInt64(string key)
        {
            var obj = this[key];
            if (obj == null) return null;

            if (IsCastableToUInt64(obj)) return Convert.ToUInt64(obj);
            if (ulong.TryParse(obj.ToString(), out var parsed)) return parsed;
            throw new Exception($"Don't know how to parse this value: {obj} (key {key})");
        }

        public long? GetInt64(string key)
        {
            var obj = this[key];
            if (obj == null) return null;

            if (IsCastableToInt64(obj)) return Convert.ToInt64(obj);
            if (long.TryParse(obj.ToString(), out var parsed)) return parsed;
            throw new Exception($"Don't know how to parse this value: {obj} (key {key})");
        }

        public double? GetDouble(string key)
        {
            var obj = this[key];
            if (obj == null) return null;

            if (obj is double) return Convert.ToDouble(obj);
            if (double.TryParse(obj.ToString(), out var parsed)) return parsed;
            throw new Exception($"Don't know how to parse this value: {obj} (key {key})");
        }

        public float? GetFloat(string key)
        {
            var obj = this[key];
            if (obj == null) return null;

            if (obj is float) return Convert.ToSingle(obj);
            if (float.TryParse(obj.ToString(), out var parsed)) return parsed;
            throw new Exception($"Don't know how to parse this value: {obj} (key {key})");
        }

        private bool IsCastableToInt64(object x)
        {
            return x is short ||
                   x is int ||
                   x is long ||
                   x is sbyte;
        }
        private bool IsCastableToUInt64(object x)
        {
            return x is ushort ||
                   x is uint ||
                   x is ulong ||
                   x is byte;
        }

        public bool? GetBool(string key)
        {
            var obj = this[key];
            if (obj == null) return null;

            if (obj is bool value) return value;

            return GetInt32(key) == 1;
        }

        public T Get<T>(int key, T defaultVal = default) => Get<T>(key.ToString(), defaultVal);
        public T Get<T>(string key, T defaultVal = default)
        {
            var obj = this[key];
            if (obj == null) return defaultVal;

            if (obj is T value)
                return value;

            return default;
        }


        public WzProperty GetProperty(int key) => GetProperty("" + key);

        public WzProperty GetProperty(string key) => this[key] as WzProperty;


        public int? GetInt32(string key) => (int?)GetInt64(key);
        public short? GetInt16(string key) => (short?)GetInt64(key);
        public sbyte? GetInt8(string key) => (sbyte?)GetInt64(key);

        public uint? GetUInt32(string key) => (uint?)GetUInt64(key);
        public ushort? GetUInt16(string key) => (ushort?)GetUInt64(key);
        public byte? GetUInt8(string key) => (byte?)GetUInt64(key);

        public string GetString(string key)
        {
            var obj = this[key];
            if (obj == null) return null;
            return obj as string ?? obj.ToString();
        }

        public override ICollection<object> Children => _objects.Values;

        public IEnumerable<string> Keys => _objects.Keys;
        public IEnumerable<WzProperty> PropertyChildren => _objects.Values.OfType<WzProperty>();
        public override bool HasChild(string key) => _objects.ContainsKey(key);

        public override void Dispose()
        {
            _objects.Clear();
        }

        public override void Read(ArchiveReader reader)
        {
            Debug.WriteLineIf(DebugOffsets, $"Start reading WzProperty at {reader.BaseStream.Position}");

            var b = reader.ReadByte();
            if (b != 0)
            {
                reader.BaseStream.Position -= 1;
                _objects = new ObjectStore();
                // Note: do not use disposing, as it would dispose the stream
                parse_ascii(new StringReader(Encoding.ASCII.GetString(reader.ReadBytes(BlobSize))));
            }
            else
            {
                reader.ReadByte();
                var amount = reader.ReadCompressedInt();
                _objects = new ObjectStore(amount);
                for (var i = 0; i < amount; i++)
                {
                    var name = reader.ReadString(1, 0);
                    var type = (WzVariantType)reader.ReadByte();

                    Debug.WriteLineIf(DebugOffsets, $"Subprop {name} type {type} at {reader.BaseStream.Position}");


                    if (type == WzVariantType.DispatchVariant)
                        type = WzVariantType.UnknownVariant;

                    object obj = null;
                    switch (type)
                    {
                        case WzVariantType.EmptyVariant: break;

                        case WzVariantType.Uint8Variant: obj = reader.ReadByte(); break;
                        case WzVariantType.Int8Variant: obj = reader.ReadSByte(); break;

                        case WzVariantType.Uint16Variant: obj = reader.ReadUInt16(); break;
                        case WzVariantType.Int16Variant: obj = reader.ReadInt16(); break;
                        case WzVariantType.BoolVariant: obj = reader.ReadInt16() != 0; break;

                        case WzVariantType.Uint32Variant: obj = (uint)reader.ReadCompressedInt(); break;
                        case WzVariantType.Int32Variant: obj = reader.ReadCompressedInt(); break;

                        case WzVariantType.Float32Variant:
                            if (reader.ReadByte() == 0x80) obj = reader.ReadSingle();
                            else obj = 0.0f;
                            break;

                        case WzVariantType.Float64Variant:
                            obj = reader.ReadDouble();
                            break;

                        case WzVariantType.BStrVariant:
                            obj = reader.ReadString(1, 0);
                            break;

                        case WzVariantType.DateVariant: obj = DateTime.FromFileTime(reader.ReadInt64()); break;

                        // Currency (CY)
                        case WzVariantType.CYVariant: obj = reader.ReadCompressedLong(); break;
                        case WzVariantType.Int64Variant: obj = reader.ReadCompressedLong(); break;
                        case WzVariantType.Uint64Variant: obj = (ulong)reader.ReadCompressedLong(); break;

                        case WzVariantType.UnknownVariant:
                            // blob
                            int size = reader.ReadInt32();
                            var pos = reader.BaseStream.Position;
                            var actualObject = PcomObject.LoadFromBlob(reader, size, name);
                            if (actualObject == null)
                            {
                                reader.BaseStream.Position = pos;
                                obj = reader.ReadBytes(size);
                            }
                            else
                            {
                                actualObject.Parent = this;
                                obj = actualObject;
                            }
                            reader.BaseStream.Position = pos + size;

                            break;

                        default:
                            throw new Exception($"Unknown type: {type} in property!");
                    }


                    _objects[name] = obj;
                }
            }

            Debug.WriteLineIf(DebugOffsets, $"Finish reading WzProperty at {reader.BaseStream.Position}");
        }


        public static void WriteObj(ArchiveWriter writer, string name, object obj)
        {
            void WriteVariant(WzVariantType vt)
            {
                writer.Write(name, 1, 0);
                writer.Write((byte)vt);
                Debug.WriteLineIf(DebugOffsets, $"Subprop {name} type {vt} at {writer.BaseStream.Position}");
            }

            switch (obj)
            {
                case null: WriteVariant(WzVariantType.EmptyVariant); break;
                case bool x:
                    WriteVariant(WzVariantType.BoolVariant);
                    writer.Write((short)(x ? 1 : 0));
                    break;

                case UInt8 x:
                    WriteVariant(WzVariantType.Uint8Variant);
                    writer.Write(x);
                    break;
                case Int8 x:
                    WriteVariant(WzVariantType.Int8Variant);
                    writer.Write(x);
                    break;

                case UInt16 x:
                    WriteVariant(WzVariantType.Uint16Variant);
                    writer.Write(x);
                    break;
                case Int16 x:
                    WriteVariant(WzVariantType.Int16Variant);
                    writer.Write(x);
                    break;

                case UInt32 x:
                    WriteVariant(WzVariantType.Uint32Variant);
                    writer.WriteCompressedInt((int)x);
                    break;
                case Int32 x:
                    WriteVariant(WzVariantType.Int32Variant);
                    writer.WriteCompressedInt((int)x);
                    break;

                case Single x:
                    WriteVariant(WzVariantType.Float32Variant);
                    if (Math.Abs(x) > 0.0)
                    {
                        writer.Write((byte)0x80);
                        writer.Write(x);
                    }
                    else writer.Write((byte)0);
                    break;

                case Double x:
                    WriteVariant(WzVariantType.Float64Variant);
                    writer.Write((double)x);
                    break;

                case string x:
                    WriteVariant(WzVariantType.BStrVariant);
                    writer.Write(x, 1, 0);
                    break;

                case DateTime x:
                    WriteVariant(WzVariantType.DateVariant);
                    writer.Write((long)x.ToFileTime());
                    break;

                // CYVariant is not handled
                case Int64 x:
                    WriteVariant(WzVariantType.Int64Variant);
                    writer.WriteCompressedLong(x);
                    break;
                case UInt64 x:
                    WriteVariant(WzVariantType.Uint64Variant);
                    writer.WriteCompressedLong((long)x);
                    break;

                case PcomObject po:
                    WriteVariant(UseDispatchForBlobs ? WzVariantType.DispatchVariant : WzVariantType.UnknownVariant);
                    writer.Write((int)0);
                    var tmp = writer.BaseStream.Position;

                    WriteToBlob(writer, po);

                    var cur = writer.BaseStream.Position;
                    writer.BaseStream.Position = tmp - 4;
                    var size = (int)(cur - tmp);
                    writer.Write(size);
                    writer.BaseStream.Position = cur;

                    break;

                default:
                    throw new Exception($"Unknown type: {obj?.GetType()} in property!");
            }
        }

        public override void Write(ArchiveWriter writer)
        {
            Debug.WriteLineIf(DebugOffsets, $"Start writing WzProperty at {writer.BaseStream.Position}");

            writer.Write((byte)0); // ASCII
            writer.Write((byte)0);

            if (_objects == null)
            {
                writer.WriteCompressedInt(0);
                return;
            }

            writer.WriteCompressedInt(_objects.Count);

            foreach (var o in _objects)
            {
                WriteObj(writer, o.Key, o.Value);
            }

            Debug.WriteLineIf(DebugOffsets, $"Finish writing WzProperty at {writer.BaseStream.Position}");
        }

        public override void Set(string key, object value)
        {
            _objects ??= new ObjectStore();

            _objects[key] = value;
            if (value is PcomObject po)
            {
                po.Parent = this;
                po.Name = key;
            }
        }

        public override object Get(string key)
        {
            _objects ??= new ObjectStore();

            _objects.TryGetValue(key, out var x);
            if (x is WzUOL uol) return uol.ActualObject();
            return x;
        }

        public void Remove(string key)
        {
            _objects ??= new ObjectStore();

            _objects.Remove(key);
        }

        public bool HasMembers => _objects.Count > 0;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _objects.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _objects.GetEnumerator();

        /// <summary>
        /// Allow multiline strings using """ """ blocks
        /// </summary>
        public static bool ExtendedASCIIFeatures = false;

        #region ASCII Loading

        // In the Wvs logic, 's' is the key, and 'v' is the value

        private void parse_ascii(TextReader stream)
        {
            WzProperty currentProperty = this;
            string v = "", s = "";
            while (read_line(stream, out var line))
            {
                parse_line(line, ref s, ref v);

                add_line(ref currentProperty, s, v);
            }
        }

        public static void add_line(ref WzProperty currentProperty, string s, string sv)
        {
            var parent = currentProperty.Parent as WzProperty;
            object v;
            if (s == isBlockStartStop)
            {
                // close brace
                parent.Set(currentProperty.Name, currentProperty);
                // go back to our parent
                currentProperty = parent;
                return;
            }
            else if (sv == isBlockStartStop)
            {
                // open brace
                currentProperty = new WzProperty()
                {
                    Name = s,
                    _objects = new ObjectStore(),
                    Parent = currentProperty,
                };
                return;
            }
            else if (sv.Length > 0 && sv[0] == isAtSign)
            {
                // Its a UOL whoop
                v = new WzUOL()
                {
                    Name = s,
                    Absolute = false,
                    Path = sv.Substring(1),
                    Parent = currentProperty,
                };
            }
            else
            {
                // Create key-value pair as string
                v = sv; // Use the string as-is
            }

            currentProperty.Set(s, v);
        }



        // Skips all lines that start with #, / or '
        // ' == old comment logic for like Basic, lol
        public static bool read_line(TextReader stream, out string foundLine)
        {
            foundLine = "";
            var multilineMode = false;
            while (true)
            {
                var line = stream.ReadLine();
                if (line == null) break;

                if (multilineMode)
                {
                    if (line == multilineBlock) break;
                    foundLine += "\r\n" + line;
                    continue;
                }

                line = line.Trim();
                if (line.Length == 0) continue;
                var firstChar = line[0];

                if (firstChar == '#' || firstChar == '/' || firstChar == '\'') continue;
                foundLine = line;

                if (ExtendedASCIIFeatures && foundLine.EndsWith(multilineBlock))
                {
                    foundLine = foundLine.Substring(0, foundLine.Length - multilineBlock.Length);
                    multilineMode = true;
                    continue;
                }

                break;
            }


            foundLine = foundLine.Trim().Trim('"');
            return foundLine != "";
        }

        private const string multilineBlock = "\"\"\"";
        // Used by '{' and '}'
        private const string isBlockStartStop = "\x07";
        // Used by '@'
        private const char isAtSign = '\x08';

        public static string escape_str(string str)
        {
            // Remove all slashes. lol
            // return str.Replace("\\", "");

            return str; // Not sure why nexon does this, it will nuke newlines in ascii strings
        }

        public static void parse_line(string line, ref string s, ref string v)
        {
            bool isEscape = false;
            int equalPos = 0;
            for (; equalPos < line.Length; equalPos++)
            {
                if (isEscape)
                    isEscape = false;
                else if (line[equalPos] == '\\')
                    isEscape = true;
                else if (line[equalPos] == '=')
                    break;
            }

            if (equalPos != line.Length)
            {
                // We've got a value

                s = line.Substring(0, equalPos);
                s = s.Trim();

                // skipping null check
                if (s.Length == 1 && s[0] == '{')
                {
                    s = isBlockStartStop;
                }
                s = escape_str(s);


                // The code does not check if you actually filled in a variable!

                v = line.Substring(equalPos + 1);
                v = v.Trim().Trim('"');
                // skipping null check

                if (v.Length == 1 && v[0] == '{')
                    v = isBlockStartStop;
                else if (v.Length > 1 && v[0] == '@')
                {
                    // skip the @, replace with the at identifier
                    v = "" + isAtSign + v.Substring(1);
                }


                v = escape_str(v);
            }
            else
            {
                v = null;
                if (line.Length == 1 && line[0] == '}')
                    s = isBlockStartStop;
                else
                    s = escape_str(v);
            }
        }
        #endregion

        #region ASCII Writing

        public void write_ascii(StreamWriter sw)
        {
            sw.WriteLine("#Property");
            write_ascii_nodes(sw);
        }

        private void write_ascii_nodes(StreamWriter sw, string indent = "")
        {
            foreach (var kvp in _objects)
            {
                sw.Write(indent);
                sw.Write(kvp.Key);
                sw.Write(" = ");

                if (IsCastableToInt64(kvp.Value))
                {
                    sw.WriteLine(kvp.Value);
                }
                else if (kvp.Value is WzUOL uol)
                {
                    sw.Write("@");
                    sw.WriteLine(uol.Path);
                }
                else if (kvp.Value is string s)
                {
                    if (s.Contains("\n"))
                    {
                        if (!ExtendedASCIIFeatures)
                        {
                            throw new Exception($"Unable to export {this.GetFullPath()}/{kvp.Key}, contains multiline string!");
                        }
                        // Multiline!
                        sw.WriteLine(multilineBlock);
                        sw.WriteLine(s);
                        sw.WriteLine(multilineBlock);
                    }
                    else
                    {
                        sw.WriteLine(s);
                    }
                }
                else if (kvp.Value is WzProperty subprop)
                {
                    sw.WriteLine("{");
                    subprop.write_ascii_nodes(sw, indent + "\t");
                    sw.Write(indent);
                    sw.WriteLine("}");
                }
                else
                {
                    throw new Exception($"Unable to export {this.GetFullPath()}/{kvp.Key} type {kvp.Value.GetType()}");
                    // sw.WriteLine("unable_to_export " + kvp.Value.GetType().Name);
                }
            }
        }

        #endregion
    }

    public class WzFileProperty : WzProperty
    {
        // This is the reference to the actual 'filesystem'
        public NameSpaceNode FileNode { get; set; }

        public override object GetParent() => FileNode.GetParent();

    }
}
