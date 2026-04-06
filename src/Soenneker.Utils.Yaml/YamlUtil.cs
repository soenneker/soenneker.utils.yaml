using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
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
                                                                                   .WithAttemptingUnquotedStringTypeDeserialization()
                                                                                   .IgnoreUnmatchedProperties()
                                                                                   .Build();

    private readonly IFileUtil _fileUtil;

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

        object? obj = FromYaml(Normalize(yaml));
        object? jsonSafe = YamlObjectToJsonSafe(obj);

        return JsonUtil.Serialize(jsonSafe, optionType: JsonOptionType.Web, JsonLibraryType.SystemTextJson);
    }

    public string FixForJson(string? yaml)
    {
        if (yaml.IsNullOrWhiteSpace())
            return string.Empty;

        object? obj = FromYaml(Normalize(yaml));
        object? jsonSafe = YamlObjectToJsonSafe(obj);

        return ToYaml(jsonSafe);
    }

    public string YamlToJson(string? yaml, JsonSerializerOptions options)
    {
        if (yaml.IsNullOrWhiteSpace())
            return "{}";

        object? obj = FromYaml(Normalize(yaml));
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
        string fixedYaml = FixForJson(content);
        string json = YamlToJson(fixedYaml) ?? "{}";
        await _fileUtil.Write(destinationPath, json, log, cancellationToken).NoSync();
    }

    public string Normalize(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return string.Empty;

        if (yaml[0] == '\uFEFF')
            yaml = yaml[1..];

        string text = yaml.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = text.Split('\n');

        var output = new List<string>(lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            output.Add(line);

            if (!IsBlockScalarHeader(line, out int headerIndent))
                continue;

            int j = i + 1;

            while (j < lines.Length && IsWhitespaceOnly(lines[j]))
            {
                j++;
            }

            if (j >= lines.Length)
                continue;

            if (CountLeadingSpaces(lines[j]) > headerIndent)
            {
                i = j - 1;
            }
        }

        return string.Join('\n', output);
    }

    private static bool IsBlockScalarHeader(string line, out int indent)
    {
        indent = 0;

        if (string.IsNullOrEmpty(line))
            return false;

        indent = CountLeadingSpaces(line);
        ReadOnlySpan<char> span = line.AsSpan(indent);

        int colonIndex = span.IndexOf(':');
        if (colonIndex < 0)
            return false;

        ReadOnlySpan<char> afterColon = span[(colonIndex + 1)..].TrimStart();

        if (afterColon.IsEmpty)
            return false;

        char c = afterColon[0];
        if (c is not ('|' or '>'))
            return false;

        if (afterColon.Length == 1)
            return true;

        char second = afterColon[1];
        return second is '-' or '+' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' or ' ' or '\t' or '#';
    }

    private static int CountLeadingSpaces(string value)
    {
        int i = 0;

        while (i < value.Length && value[i] == ' ')
        {
            i++;
        }

        return i;
    }

    private static bool IsWhitespaceOnly(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
                return false;
        }

        return true;
    }

    private static object? YamlObjectToJsonSafe(object? value)
    {
        return YamlObjectToJsonSafe(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static object? YamlObjectToJsonSafe(object? value, HashSet<object> recursionStack)
    {
        if (value is null)
            return null;

        // primitives fast-path
        if (value is string or bool)
            return value;

        if (value is int or long or double or float or decimal or short or byte or sbyte or uint or ulong or ushort)
            return value;

        bool shouldTrack = ShouldTrackByReference(value);

        if (shouldTrack && !recursionStack.Add(value))
            return null;

        try
        {
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

                    result[key] = YamlObjectToJsonSafe(entry.Value, recursionStack);
                }

                return result;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                if (value is ICollection col)
                {
                    var list = new List<object?>(col.Count);
                    foreach (object? item in enumerable)
                        list.Add(YamlObjectToJsonSafe(item, recursionStack));
                    return list;
                }

                var list2 = new List<object?>();

                foreach (object? item in enumerable)
                    list2.Add(YamlObjectToJsonSafe(item, recursionStack));
                return list2;
            }

            return value;
        }
        finally
        {
            if (shouldTrack)
                recursionStack.Remove(value);
        }
    }

    private static bool ShouldTrackByReference(object value)
    {
        return value is not string && !value.GetType().IsValueType;
    }
}