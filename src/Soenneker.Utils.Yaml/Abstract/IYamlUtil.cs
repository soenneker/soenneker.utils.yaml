using System.Diagnostics.Contracts;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.Yaml.Abstract;

/// <summary>
/// Utility for serializing and deserializing YAML, and converting between YAML and JSON.
/// </summary>
public interface IYamlUtil
{
    /// <summary>
    /// Serializes an object graph to YAML.
    /// </summary>
    /// <param name="value">The object graph to serialize.</param>
    /// <returns>
    /// A YAML string, or <see cref="string.Empty"/> when <paramref name="value"/> is <c>null</c>.
    /// </returns>
    [Pure]
    string ToYaml(object? value);

    /// <summary>
    /// Deserializes a YAML string into a typed object.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>
    /// The deserialized value, or <c>default</c> when <paramref name="yaml"/> is <c>null</c> or whitespace.
    /// </returns>
    [Pure]
    T? FromYaml<T>(string? yaml);

    /// <summary>
    /// Deserializes a YAML string into an untyped object graph.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>
    /// The deserialized object graph, or <c>null</c> when <paramref name="yaml"/> is <c>null</c> or whitespace.
    /// </returns>
    [Pure]
    object? FromYaml(string? yaml);

    /// <summary>
    /// Converts JSON to YAML by parsing JSON into an object graph and then serializing to YAML.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>
    /// A YAML string, or <see cref="string.Empty"/> when <paramref name="json"/> is <c>null</c> or whitespace.
    /// </returns>
    [Pure]
    string? JsonToYaml(string? json);

    /// <summary>
    /// Converts YAML to JSON using default web-oriented JSON serializer options.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>
    /// A JSON string, or <c>{}</c> when <paramref name="yaml"/> is <c>null</c> or whitespace.
    /// </returns>
    [Pure]
    string? YamlToJson(string? yaml);

    /// <summary>
    /// Normalizes YAML into a JSON-safe shape by removing byte-order marks, normalizing line endings,
    /// and breaking recursive alias/object cycles that cannot be represented in JSON.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>
    /// A sanitized YAML string, or <see cref="string.Empty"/> when <paramref name="yaml"/> is <c>null</c> or whitespace.
    /// </returns>
    [Pure]
    string FixForJson(string? yaml);

    /// <summary>
    /// Converts YAML to JSON using the specified <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="options">The serializer options to use.</param>
    /// <returns>
    /// A JSON string, or <c>{}</c> when <paramref name="yaml"/> is <c>null</c> or whitespace.
    /// </returns>
    [Pure]
    string YamlToJson(string? yaml, JsonSerializerOptions options);

    /// <summary>
    /// Determines whether the provided YAML string can be successfully deserialized.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="yaml"/> is non-empty and deserialization succeeds; otherwise <c>false</c>.
    /// </returns>
    [Pure]
    bool IsValidYaml(string? yaml);

    /// <summary>
    /// Attempts to deserialize a YAML string into a typed object without throwing.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="result">The deserialized result when successful; otherwise <c>default</c>.</param>
    /// <returns>
    /// <c>true</c> if deserialization succeeds; otherwise <c>false</c>.
    /// </returns>
    bool TryFromYaml<T>(string? yaml, out T? result);

    /// <summary>
    /// Reads a JSON file and writes the content as YAML to the destination path.
    /// </summary>
    /// <param name="sourcePath">Path to the source JSON file.</param>
    /// <param name="destinationPath">Path where the YAML output is written.</param>
    /// <param name="log">Whether to log the file operations.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask SaveAsYaml(string sourcePath, string destinationPath, bool log = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a YAML file and writes the content as JSON to the destination path.
    /// </summary>
    /// <param name="sourcePath">Path to the source YAML file.</param>
    /// <param name="destinationPath">Path where the JSON output is written.</param>
    /// <param name="log">Whether to log the file operations.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask SaveAsJson(string sourcePath, string destinationPath, bool log = true, CancellationToken cancellationToken = default);

    string Normalize(string? yaml);
}