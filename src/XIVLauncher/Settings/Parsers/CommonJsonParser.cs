using System;
using System.Collections.Generic;
using Config.Net;
using Newtonsoft.Json;

namespace XIVLauncher.Settings.Parsers;

public class CommonJsonParser<T> : ITypeParser
{
    public bool TryParse(string value, Type t, out object result)
    {
        try
        {
            result = JsonConvert.DeserializeObject(value, t);
        }
        catch
        {
            result = null;
            return false;
        }

        return true;
    }

    public string ToRawString(object value)
    {
        return value == null ? null : JsonConvert.SerializeObject(value);
    }

    public IEnumerable<Type> SupportedTypes => new[] { typeof(T) };
}
