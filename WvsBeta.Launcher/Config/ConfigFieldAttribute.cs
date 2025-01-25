using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WvsBeta.Launcher.Config
{
    internal class ConfigFieldAttribute : Attribute
    {
        public string Name { get; set; }

        public string? Formatter { get; set; }

        public string? DefaultValue { get; set; }

        public ConfigFieldAttribute(string name, string? defaultValue = null, string? formatter = null)
        {
            Name = name;
            Formatter = formatter;
            DefaultValue = defaultValue;
        }

    }
}
