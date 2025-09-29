using Newtonsoft.Json;
using System;
using System.IO;

namespace XIVLauncher.Common.Patching.Rpc;

public static class IpcHelpers
{
    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }

    public class FileInfoConverted : JsonConverter<FileInfo>
    {
        public override void WriteJson(JsonWriter writer, FileInfo value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.FullName);
        }

        public override FileInfo ReadJson(JsonReader reader, Type objectType, FileInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var path = reader.Value as string;
            return string.IsNullOrEmpty(path) ? null : new FileInfo(path);
        }
    }

    public class DirectoryInfoConverter : JsonConverter<DirectoryInfo>
    {
        public override void WriteJson(JsonWriter writer, DirectoryInfo value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.FullName);
        }

        public override DirectoryInfo ReadJson(JsonReader reader, Type objectType, DirectoryInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var path = reader.Value as string;
            return string.IsNullOrEmpty(path) ? null : new DirectoryInfo(path);
        }
    }

    public static JsonSerializerSettings JsonSettings = new()
    {
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
        TypeNameHandling = TypeNameHandling.All,
        Converters = { new FileInfoConverted(), new DirectoryInfoConverter() }
    };
}
