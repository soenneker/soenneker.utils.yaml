using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
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
using Soenneker.Utils.PooledStringBuilders;
using Soenneker.Utils.Yaml.Abstract;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Soenneker.Utils.Yaml;

/// <inheritdoc cref="IYamlUtil" />
public sealed class YamlUtil : IYamlUtil
{
    private const string TabIndentReplacement = "  ";

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
        string json = YamlToJson(content) ?? "{}";
        await _fileUtil.Write(destinationPath, json, log, cancellationToken).NoSync();
    }

    public string Normalize(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return string.Empty;

        string normalized = FixTabsInIndentation(yaml);
        return NormalizeYamlLines(normalized);
    }

    private static string NormalizeYamlLines(string yaml)
    {
        string[] lines = yaml.Split('\n');
        StringBuilder? builder = null;
        bool inDoubleQuotedScalar = false;
        int blockScalarIndent = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string original = lines[i];
            int indentation = CountLeadingWhitespace(original);
            bool inBlockScalar = blockScalarIndent >= 0 && (string.IsNullOrWhiteSpace(original) || indentation > blockScalarIndent);

            if (blockScalarIndent >= 0 && !inBlockScalar)
                blockScalarIndent = -1;

            string normalized = string.IsNullOrWhiteSpace(original) ? string.Empty : inDoubleQuotedScalar || inBlockScalar
                ? original
                : QuoteUnsafePlainMappingScalar(original);

            if (!inBlockScalar && IsBlockScalarHeader(original))
                blockScalarIndent = indentation;

            if (!inBlockScalar && HasOddUnescapedDoubleQuoteCount(original))
                inDoubleQuotedScalar = !inDoubleQuotedScalar;

            if (builder == null && !string.Equals(original, normalized, StringComparison.Ordinal))
            {
                builder = new StringBuilder(yaml.Length + 16);
                for (int previous = 0; previous < i; previous++)
                    builder.Append(lines[previous]).Append('\n');
            }

            if (builder != null)
            {
                builder.Append(normalized);
                if (i < lines.Length - 1)
                    builder.Append('\n');
            }
        }

        return builder?.ToString() ?? yaml;
    }

    private static int CountLeadingWhitespace(string line)
    {
        int count = 0;
        while (count < line.Length && char.IsWhiteSpace(line[count]))
            count++;
        return count;
    }

    private static bool IsBlockScalarHeader(string line)
    {
        int separator = line.IndexOf(':');
        if (separator < 0)
            return false;

        string value = line[(separator + 1)..].Trim();
        return value is "|" or "|-" or "|+" or ">" or ">-" or ">+";
    }

    private static bool HasOddUnescapedDoubleQuoteCount(string line)
    {
        int count = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] != '"')
                continue;

            int precedingBackslashes = 0;
            for (int previous = i - 1; previous >= 0 && line[previous] == '\\'; previous--)
                precedingBackslashes++;

            if (precedingBackslashes % 2 == 0)
                count++;
        }

        return count % 2 != 0;
    }

    private static string QuoteUnsafePlainMappingScalar(string line)
    {
        int contentStart = 0;
        while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart]))
            contentStart++;

        if (contentStart >= line.Length || line[contentStart] == '#')
            return line;

        if (line[contentStart] == '-' && contentStart + 1 < line.Length && char.IsWhiteSpace(line[contentStart + 1]))
        {
            contentStart++;
            while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart]))
                contentStart++;
        }

        int separator = line.IndexOf(':', contentStart);
        if (separator < 0 || separator + 1 >= line.Length || !char.IsWhiteSpace(line[separator + 1]))
            return line;

        for (int i = contentStart; i < separator; i++)
        {
            if (char.IsWhiteSpace(line[i]))
                return line;
        }

        int valueStart = separator + 1;
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart]))
            valueStart++;

        if (valueStart >= line.Length || line[valueStart] is '\'' or '"' or '|' or '>' or '[' or '{' or '&' or '*' or '!' or '#')
            return line;

        int unsafeColon = line.IndexOf(": ", valueStart, StringComparison.Ordinal);
        if (unsafeColon < 0)
            return line;

        int commentStart = line.IndexOf(" #", valueStart, StringComparison.Ordinal);
        string value = (commentStart >= 0 ? line[valueStart..commentStart] : line[valueStart..]).TrimEnd();
        string comment = commentStart >= 0 ? line[commentStart..] : string.Empty;
        string quoted = JsonSerializer.Serialize(value);

        return string.Concat(line.AsSpan(0, valueStart), quoted, comment);
    }

    public string FixTabsInIndentation(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return string.Empty;

        var requiresNormalization = yaml[0] == '\uFEFF';
        var atLineStart = true;

        for (var i = 0; i < yaml.Length && !requiresNormalization; i++)
        {
            char c = yaml[i];
            requiresNormalization = c == '\r' || (atLineStart && c == '\t');

            if (c == '\n')
                atLineStart = true;
            else if (c != ' ' && c != '\t')
                atLineStart = false;
        }

        if (!requiresNormalization)
            return yaml;

        using var builder = new PooledStringBuilder(yaml.Length);
        atLineStart = true;

        for (int i = yaml[0] == '\uFEFF' ? 1 : 0; i < yaml.Length; i++)
        {
            char c = yaml[i];

            if (c == '\r')
            {
                builder.Append('\n');
                if (i + 1 < yaml.Length && yaml[i + 1] == '\n')
                    i++;
                atLineStart = true;
            }
            else if (c == '\n')
            {
                builder.Append(c);
                atLineStart = true;
            }
            else if (atLineStart && c == '\t')
            {
                builder.Append(TabIndentReplacement);
            }
            else
            {
                builder.Append(c);
                if (c != ' ')
                    atLineStart = false;
            }
        }

        return builder.ToString();
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
