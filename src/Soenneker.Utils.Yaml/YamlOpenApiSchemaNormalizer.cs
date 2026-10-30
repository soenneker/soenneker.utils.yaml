using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Soenneker.Utils.Yaml;

internal static class YamlOpenApiSchemaNormalizer
{
    internal static JsonObject? Normalize(IDictionary document)
    {
        var counts = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        IEnumerable<object> Children(object value) => value is IDictionary map ? map.Values.Cast<object>().Where(v => v is IDictionary or IList)
            : value is IList list ? list.Cast<object>().Where(v => v is IDictionary or IList) : [];
        bool hasCycle = false;
        var active = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Count(object value)
        {
            if (active.Contains(value)) hasCycle = true;
            counts[value] = counts.GetValueOrDefault(value) + 1;
            if (!visited.Add(value)) return;
            active.Add(value);
            foreach (object child in Children(value)) Count(child);
            active.Remove(value);
        }
        Count(document);
        if (!hasCycle) return null;
        var shared = new HashSet<object>(counts.Where(p => p.Value > 1).Select(p => p.Key), ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>(shared);
        while (pending.TryDequeue(out object? container))
            foreach (object child in Children(container)) if (shared.Add(child)) pending.Enqueue(child);

        var names = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        var schemas = new JsonObject();
        bool swagger = document.Contains("swagger");
        IDictionary? originals = swagger ? document["definitions"] as IDictionary : (document["components"] as IDictionary)?["schemas"] as IDictionary;
        if (originals != null)
            foreach (DictionaryEntry entry in originals)
                if (entry.Value is IDictionary) names.TryAdd(entry.Value, Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!);
        var usedNames = new HashSet<string>(names.Values, StringComparer.Ordinal);
        string Reference(string name) => (swagger ? "#/definitions/" : "#/components/schemas/") + name.Replace("~", "~0").Replace("/", "~1");
        string Name(object value, string hint)
        {
            if (names.TryGetValue(value, out string? name)) return name;
            string stem = "Yaml_" + Regex.Replace(hint, "[^a-zA-Z0-9_]", "_");
            if (stem.Length > 100) stem = stem[..100];
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hint)))[..10];
            name = stem + "_" + hash;
            while (!usedNames.Add(name)) name += "Value";
            names[value] = name;
            return name;
        }
        var plainActive = new HashSet<object>(ReferenceEqualityComparer.Instance);
        JsonNode? Plain(object? value)
        {
            if (value is null) return null;
            if (value is IDictionary or IList)
            {
                if (!plainActive.Add(value)) throw new InvalidDataException("A cyclic YAML alias occurs outside an OpenAPI schema.");
                try
                {
                    if (value is IDictionary map)
                    {
                        var result = new JsonObject();
                        foreach (DictionaryEntry entry in map) result[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!] = Plain(entry.Value);
                        return result;
                    }
                    return new JsonArray(((IList)value).Cast<object?>().Select(Plain).ToArray());
                }
                finally { plainActive.Remove(value); }
            }
            return value switch
            {
                string v => JsonValue.Create(v), bool v => JsonValue.Create(v),
                int v => JsonValue.Create(v), long v => JsonValue.Create(v),
                uint v => JsonValue.Create(v), ulong v => JsonValue.Create(v),
                short v => JsonValue.Create(v), ushort v => JsonValue.Create(v),
                byte v => JsonValue.Create(v), sbyte v => JsonValue.Create(v),
                float v => JsonValue.Create(v), double v => JsonValue.Create(v), decimal v => JsonValue.Create(v),
                DateTime v => JsonValue.Create(v), DateTimeOffset v => JsonValue.Create(v),
                _ => throw new InvalidDataException("Unsupported YAML scalar type: " + value.GetType().FullName)
            };
        }
        JsonNode? Schema(object? value, string hint)
        {
            if (value is not IDictionary map) return Plain(value);
            bool reusable = names.ContainsKey(value) || shared.Contains(value);
            string? name = reusable ? Name(value, hint) : null;
            if (name != null && schemas.ContainsKey(name)) return new JsonObject { ["$ref"] = Reference(name) };
            var result = new JsonObject();
            if (name != null) schemas[name] = result;
            foreach (DictionaryEntry entry in map)
            {
                string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!;
                object? child = entry.Value;
                string next = hint + "_" + key;
                if (key is "properties" or "patternProperties" or "$defs" or "definitions" or "dependentSchemas" && child is IDictionary fields)
                {
                    var values = new JsonObject();
                    foreach (DictionaryEntry field in fields)
                    {
                        string fieldName = Convert.ToString(field.Key, CultureInfo.InvariantCulture)!;
                        values[fieldName] = Schema(field.Value, next + "_" + fieldName);
                    }
                    result[key] = values;
                }
                else if (key is "oneOf" or "anyOf" or "allOf" or "prefixItems" && child is IList branches)
                    result[key] = new JsonArray(branches.Cast<object?>().Select((branch, index) => Schema(branch, next + index)).ToArray());
                else if (key is "items" or "additionalProperties" or "not" or "contains" or "propertyNames" or "if" or "then" or "else" or "unevaluatedProperties" or "unevaluatedItems" or "contentSchema")
                    result[key] = Schema(child, next);
                else result[key] = Plain(child);
            }
            return name == null ? result : new JsonObject { ["$ref"] = Reference(name) };
        }
        JsonNode? OpenApi(object? value, string hint)
        {
            if (value is IList list) return new JsonArray(list.Cast<object?>().Select((entry, index) => OpenApi(entry, hint + index)).ToArray());
            if (value is not IDictionary map) return Plain(value);
            if (names.ContainsKey(value)) return Schema(value, hint);
            var result = new JsonObject();
            foreach (DictionaryEntry entry in map)
            {
                string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!;
                if ((key == "schemas" && hint == "components" || key == "definitions" && hint.Length == 0) && entry.Value is IDictionary definitions)
                {
                    foreach (DictionaryEntry definition in definitions) Schema(definition.Value, Convert.ToString(definition.Key, CultureInfo.InvariantCulture)!);
                    continue;
                }
                result[key] = key is "schema" or "itemSchema" ? Schema(entry.Value, hint + "_schema")
                    : key is "example" or "examples" or "default" or "enum" || key.StartsWith("x-", StringComparison.Ordinal) ? Plain(entry.Value)
                    : OpenApi(entry.Value, hint.Length == 0 ? key : hint + "_" + key);
            }
            return result;
        }
        JsonObject output = OpenApi(document, "")!.AsObject();
        if (swagger) output["definitions"] = schemas;
        else
        {
            output["components"] ??= new JsonObject();
            output["components"]!["schemas"] = schemas;
        }
        return output;
    }
}
