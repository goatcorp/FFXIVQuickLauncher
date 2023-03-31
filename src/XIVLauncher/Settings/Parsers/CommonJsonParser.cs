using System;
using System.Collections.Generic;
using System.Text.Json;
using Config.Net;

namespace XIVLauncher.Settings.Parsers;

public class CommonJsonParser<T> : ITypeParser
{
    public bool TryParse(string value, Type t, out object result)
    {
        try
        {
            result = JsonSerializer.Deserialize(value, t);
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
        return value == null ? null : JsonSerializer.Serialize(value);
    }

    public IEnumerable<Type> SupportedTypes => new[] { typeof(T) };
}
