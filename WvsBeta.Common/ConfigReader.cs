using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace WvsBeta.Common
{
    public class ConfigReader : Node
    {
        public string Filename { get; }
        public Node RootNode => this;

        public ConfigReader(string path, bool read = true)
        {
            Filename = path;
            if (read)
            {
                using var sr = new StreamReader(File.OpenRead(path));
                int row = 0;
                var tmp = ReadInnerNode("RootNode", sr, ref row, 0);
                SubNodes = tmp.SubNodes;
            }
            else
            {
                SubNodes = new List<Node>();
            }
        }
        
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

        public void Write(string path = null)
        {
            using var sw = new StreamWriter(File.Open(path ?? Filename, FileMode.Create));

            sw.WriteLine("#Property");
            
            foreach (var sub in RootNode)
            {
                writeNode(sub, "");
            }
            void writeNode(Node node, string indent)
            {

                if (node.SubNodes == null)
                {
                    // Regular entry
                    sw.WriteLine($"{indent}{node.Name} = {node.Value}");
                    return;
                }

                sw.WriteLine($"{indent}{node.Name} = {{");

                foreach (var sub in node)
                {
                    writeNode(sub, indent + "\t");
                }

                sw.WriteLine($"{indent}}}");
            }
        }
    }

    public class Node : IEnumerable<Node>
    {
        public List<Node> SubNodes { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public static readonly IFormatProvider NumberFormat = new CultureInfo("en-US");

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
            set { SubNodes?.Add(value); }
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return SubNodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) SubNodes).GetEnumerator();
        }

        public Node()
        {
            // defaults
        }

        public Node(string name)
        {
            Name = name;
            SubNodes = null;
            Value = null;
        }

        public Node(string name, string value) : this(name)
        {
            Value = value;
        }

        public Node GetOrAdd(string name)
        {
            var node = this[name];
            if (node == null)
            {
                node = new Node(name);
                SubNodes ??= new List<Node>();
                SubNodes.Add(node);
            }

            return node;
        }
            
        public Node Set(string name, params Node[] subNodes)
        {
            var tmp = GetOrAdd(name);
            tmp.SubNodes = new List<Node>(subNodes);
            return tmp;
        }

        public Node Set(string name, string value)
        {
            var tmp = GetOrAdd(name);
            tmp.Value = value;
            return tmp;
        }

        public Node Set(string name)
        {
            // Just create it
            return GetOrAdd(name);
        }
    }
}