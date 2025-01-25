using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WvsBeta.Common;

namespace WvsBeta.Launcher.Config
{
    public static class ConfigReaderExt
    {
        private static IEnumerable<(Node configNode, PropertyInfo property, ConfigFieldAttribute attribute)> ProcessConfigProperties(Node reader, object obj)
        {
            foreach (var prop in obj.GetType().GetProperties())
            {
                var configFieldAttr = prop.GetCustomAttribute<ConfigFieldAttribute>();
                if (configFieldAttr == null) continue;

                var configPath = configFieldAttr.Name.Split(".");

                // find path in file
                var currentNode = configPath.Aggregate(reader, (current, elem) => current.GetOrAdd(elem));

                currentNode.Value ??= configFieldAttr.DefaultValue ?? "";

                yield return (currentNode, prop, configFieldAttr);
            }
        }

        public static void LoadObject(this Node reader, object obj)
        {
            foreach (var (currentNode, prop, attribute) in ProcessConfigProperties(reader, obj))
            {
                if (!prop.CanWrite) continue;

                var setter = prop.GetSetMethod();
                if (setter == null) continue;

                object objectToSet = prop.PropertyType switch
                {
                    var type when type == typeof(int) => currentNode.GetInt(),
                    var type when type == typeof(uint) => currentNode.GetUInt(),
                    var type when type == typeof(byte) => currentNode.GetByte(),
                    var type when type == typeof(bool) => currentNode.GetBool(),
                    var type when type == typeof(short) => currentNode.GetShort(),
                    var type when type == typeof(ushort) => currentNode.GetUShort(),
                    var type when type == typeof(string) => currentNode.GetString(),
                    var type when type == typeof(float) => currentNode.GetFloat(),
                    var type when type == typeof(double) => currentNode.GetDouble(),
                    var type => throw new Exception($"Unable to map {type} to ConfigReader Node getter")
                };

                // Try to set the value..
                setter.Invoke(obj, new[] {objectToSet});
            }
        }

        public static void WriteObject(this Node reader, object obj)
        {
            foreach (var (currentNode, prop, attribute) in ProcessConfigProperties(reader, obj))
            {
                string valueToSet = prop.GetValue(obj) switch
                {
                    int val => val.ToString(attribute.Formatter ?? "D"),
                    uint val => val.ToString(attribute.Formatter ?? "D"),
                    byte val => val.ToString(attribute.Formatter ?? "D"),
                    short val => val.ToString(attribute.Formatter ?? "D"),
                    ushort val => val.ToString(attribute.Formatter ?? "D"),
                    string val => val,
                    float val => val.ToString(attribute.Formatter ?? "R", Node.NumberFormat),
                    double val => val.ToString(attribute.Formatter ?? "R", Node.NumberFormat),
                    bool val => val ? "true" : "false",
                    var type => throw new Exception($"Unable to write {type} to config")
                };

                // Try to set the value..
                currentNode.Value = valueToSet;
            }
        }
    }
}