using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Config.Net;
using Newtonsoft.Json;
using XIVLauncher.Addon;

namespace XIVLauncher.Common.Parsers
{
    class AddonListParser : ITypeParser
    {
        public IEnumerable<Type> SupportedTypes => new[] { typeof(List<AddonEntry>) };

        public string ToRawString(object value)
        {
            if (value is List<AddonEntry> list)
                return JsonConvert.SerializeObject(list);

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
                result = JsonConvert.DeserializeObject<List<AddonEntry>>(value);
                return true;
            }

            result = null;
            return false;
        }
    }
}
