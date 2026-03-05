using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Enums.JsonLibrary;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.JsonElements;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Json;
using Soenneker.Utils.Yaml.Abstract;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Soenneker.Utils.Yaml;

///<inheritdoc cref="IYamlUtil"/>
public sealed class YamlUtil : IYamlUtil
{
    private static readonly ISerializer _serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
                                                                             .Build();

    private static readonly IDeserializer _deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
                                                                                   .IgnoreUnmatchedProperties()
                                                                                   .Build();

    private IFileUtil _fileUtil;

    public YamlUtil(IFileUtil fileUtil)
    {
        _fileUtil = fileUtil;
    }

    public string ToYaml(object? value)
    {
        if (value is null)
            return string.Empty;

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        _serializer.Serialize(writer, value);
        return writer.ToString();
    }

    public T? FromYaml<T>(string? yaml)
    {
        if (yaml.IsNullOrWhiteSpace())
            return default;

        return _deserializer.Deserialize<T>(yaml);
    }

    public object? FromYaml(string? yaml)
    {
        if (yaml.IsNullOrWhiteSpace())
            return null;

        return _deserializer.Deserialize(yaml);
    }

    public string JsonToYaml(string? json)
    {
        if (json.IsNullOrWhiteSpace())
            return string.Empty;

        using JsonDocument doc = JsonDocument.Parse(json);
        object? graph = doc.RootElement.JsonElementToObject();
        return ToYaml(graph);
    }

    public string? YamlToJson(string? yaml)
    {
        if (yaml.IsNullOrWhiteSpace())
            return "{}";

        object? obj = FromYaml(yaml);
        object? jsonSafe = YamlObjectToJsonSafe(obj);

        return JsonUtil.Serialize(jsonSafe, optionType: JsonOptionType.Web, JsonLibraryType.SystemTextJson);
    }

    public string YamlToJson(string? yaml, JsonSerializerOptions options)
    {
        if (yaml.IsNullOrWhiteSpace())
            return "{}";

        object? obj = FromYaml(yaml);
        object? jsonSafe = YamlObjectToJsonSafe(obj);

        return JsonSerializer.Serialize(jsonSafe, options);
    }

    public bool IsValidYaml(string? yaml)
    {
        if (yaml.IsNullOrWhiteSpace())
            return false;

        try
        {
            _deserializer.Deserialize(yaml);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryFromYaml<T>(string? yaml, out T? result)
    {
        result = default;

        if (yaml.IsNullOrWhiteSpace())
            return false;

        try
        {
            result = _deserializer.Deserialize<T>(yaml);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask SaveAsYaml(string sourcePath, string destinationPath, bool log = true, CancellationToken cancellationToken = default)
    {
        string content = await _fileUtil.Read(sourcePath, log, cancellationToken).NoSync();
        string yaml = JsonToYaml(content) ?? string.Empty;
        await _fileUtil.Write(destinationPath, yaml, log, cancellationToken).NoSync();
    }

    public async ValueTask SaveAsJson(string sourcePath, string destinationPath, bool log = true, CancellationToken cancellationToken = default)
    {
        string content = await _fileUtil.Read(sourcePath, log, cancellationToken).NoSync();
        string json = YamlToJson(content) ?? "{}";
        await _fileUtil.Write(destinationPath, json, log, cancellationToken).NoSync();
    }

    private static object? YamlObjectToJsonSafe(object? value)
    {
        if (value is null)
            return null;

        // primitives fast-path
        if (value is string or bool)
            return value;

        if (value is int or long or double or float or decimal or short or byte or sbyte or uint or ulong or ushort)
            return value;

        if (value is IDictionary dict)
        {
            var result = new Dictionary<string, object?>(dict.Count, StringComparer.Ordinal);

            foreach (DictionaryEntry entry in dict)
            {
                string key = entry.Key switch
                {
                    null => string.Empty,
                    string s => s,
                    _ => entry.Key.ToString() ?? string.Empty
                };

                result[key] = YamlObjectToJsonSafe(entry.Value);
            }

            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            if (value is ICollection col)
            {
                var list = new List<object?>(col.Count);
                foreach (object? item in enumerable)
                    list.Add(YamlObjectToJsonSafe(item));
                return list;
            }

            var list2 = new List<object?>();

            foreach (object? item in enumerable)
                list2.Add(YamlObjectToJsonSafe(item));
            return list2;
        }

        return value;
    }
}