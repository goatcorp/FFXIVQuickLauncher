using System;
using System.Collections.Generic;
using Config.Net;
using System.Text.Json;
using XIVLauncher.Common.Addon;

namespace XIVLauncher.Settings.Parsers
{
    class AddonListParser : ITypeParser
    {
        public IEnumerable<Type> SupportedTypes => new[] { typeof(List<AddonEntry>) };

        public string ToRawString(object value)
        {
            if (value is List<AddonEntry> list)
                return JsonSerializer.Serialize(list);

            return null;
        }

        public bool TryParse(string value, Type t, out object result)
        {
            if (value == null)
            {
                result = null;
                return false;
            }

            if (t == typeof(List<AddonEntry>))
            {
                result = JsonSerializer.Deserialize<List<AddonEntry>>(value);
                return true;
            }

            result = null;
            return false;
        }
    }
}