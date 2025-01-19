using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace WvsBeta.Common
{
    public class ConfigReader
    {

        public class Node : IEnumerable<Node>
        {
            public List<Node> SubNodes { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }

            private static IFormatProvider NumberFormat = new CultureInfo("en-US");

            private bool IsHex => Value.Length > 2 && Value[1] == 'x';

            public int GetInt() => IsHex ? int.Parse(Value[2..], NumberStyles.HexNumber, NumberFormat) : int.Parse(Value, NumberFormat);
            public uint GetUInt() => IsHex ? uint.Parse(Value[2..], NumberStyles.HexNumber, NumberFormat) : uint.Parse(Value, NumberFormat);
            public short GetShort() => IsHex ? short.Parse(Value[2..], NumberStyles.HexNumber, NumberFormat) : short.Parse(Value, NumberFormat);
            public ushort GetUShort() => IsHex ? ushort.Parse(Value[2..], NumberStyles.HexNumber, NumberFormat) : ushort.Parse(Value, NumberFormat);
            public byte GetByte() => IsHex ? byte.Parse(Value[2..], NumberStyles.HexNumber, NumberFormat) : byte.Parse(Value, NumberFormat);

            public bool GetBool()
            {
                if (byte.TryParse(Value, out var _byte)) return _byte != 0;
                if (bool.TryParse(Value, out var _bool)) return _bool;
                return Value == "true" || Value == "yes";
            }

            public string GetString() => Value;
            public double GetDouble() => double.Parse(Value, NumberFormat);
            public float GetFloat() => float.Parse(Value, NumberFormat);
            public T GetEnum<T>() => (T) Enum.Parse(typeof(T), Value);

            public Node this[string name]
            {
                get { return SubNodes?.Find(x => x.Name == name); }
            }

            public IEnumerator<Node> GetEnumerator()
            {
                return SubNodes.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable) SubNodes).GetEnumerator();
            }
        }

        public string Filename { get; }
        public Node RootNode { get; }

        public ConfigReader(string path)
        {
            using (var sr = new StreamReader(File.OpenRead(path)))
            {
                int row = 0;
                RootNode = ReadInnerNode("RootNode", sr, ref row, 0);
            }
        }

        public Node this[string name] => name == "" ? RootNode : RootNode[name];
        
        // Parser

        private static readonly Regex lineRegex = new Regex(@"^\s*([^ =]+)\s*=\s*([^\r\n$]*)\s*$");
        
        private Node ReadInnerNode(string nodeName, StreamReader sr, ref int currentLineNumber, int depth)
        {
            depth++;
            string line = "";

            var node = new Node
            {
                Name = nodeName,
                SubNodes = new List<Node>(),
                Value = null,
            };

            while (!sr.EndOfStream)
            {
                currentLineNumber++;
                line = sr.ReadLine().Trim();
                if (line == "" || line.StartsWith("#") || line.StartsWith("/")) continue;

                if (line == "}")
                {
                    // End of block
                    break;
                }

                if (line.Contains(" # "))
                {
                    line = line.Substring(0, line.IndexOf(" # "));
                }

                var matches = lineRegex.Match(line);
                if (!matches.Success)
                {
                    throw new Exception("Error on line " + currentLineNumber + " in node " + node.Name);
                }

                var name = matches.Groups[1].Captures[0].Value;
                var value = matches.Groups[2].Captures[0].Value;


                if (value == "{")
                {
                    var subNode = ReadInnerNode(name, sr, ref currentLineNumber, depth);
                    node.SubNodes.Add(subNode);
                }
                else
                {
                    node.SubNodes.Add(new Node
                    {
                        Name = name,
                        SubNodes = null,
                        Value = value.Trim(),
                    });
                }
            }

            if (line != "}" && depth > 1)
            {
                throw new Exception("Missing ending brace.");
            }
            return node;
        }

    }
}