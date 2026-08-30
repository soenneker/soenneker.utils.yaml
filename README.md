[![](https://img.shields.io/nuget/v/soenneker.utils.yaml.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.yaml/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.yaml/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.yaml/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.yaml.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.yaml/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.yaml/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.yaml/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.Yaml
YAML serialization, typed and untyped deserialization, JSON conversion, validation, normalization, and file conversion through YamlDotNet.

## Installation

```bash
dotnet add package Soenneker.Utils.Yaml
```

## Registration

```csharp
using Soenneker.Utils.Yaml.Registrars;

services.AddYamlUtilAsSingleton();
```

Use `AddYamlUtilAsScoped()` when a scoped lifetime is preferred. The serializer and deserializer are shared static instances; the utility instance primarily supplies file access.

## Serialize and deserialize

```csharp
string yaml = yamlUtil.ToYaml(new
{
    DisplayName = "Ada",
    RetryCount = 3
});

Settings? settings = yamlUtil.FromYaml<Settings>(yaml);
object? graph = yamlUtil.FromYaml(yaml);
```

Serialization uses camel-case member names. `ToYaml(null)` returns an empty string. Typed and untyped deserialization return `default`/`null` for blank input; malformed or incompatible YAML otherwise throws.

Typed deserialization also uses camel-case naming, attempts to infer types for unquoted scalars, and ignores YAML properties that do not exist on the target type. If rejecting unknown fields matters, validate the input separately before binding.

## Convert YAML and JSON

```csharp
string yaml = yamlUtil.JsonToYaml(json);
string json = yamlUtil.YamlToJson(yaml);
```

Blank JSON becomes an empty YAML string. Blank YAML becomes `{}`. Invalid input propagates parser or serializer exceptions.

YAML mappings can have non-string keys, aliases, and recursive object graphs that JSON cannot represent directly. During YAML-to-JSON conversion:

- mapping keys are converted with `ToString()` and collisions overwrite earlier values;
- sequences become JSON arrays;
- recursive references become `null`;
- repeated non-recursive aliases are expanded as repeated values.

`YamlToJson(yaml, options)` uses caller-supplied `JsonSerializerOptions`. The overload without options uses the package's web-oriented System.Text.Json configuration.

`FixForJson` performs the same YAML parsing and JSON-safe graph conversion, then serializes the result back to YAML. It can therefore change scalar types, mapping-key representation, and alias structure; it is not a text-only cleanup.

## Validate and try-deserialize

```csharp
bool syntaxIsValid = yamlUtil.IsValidYaml(yaml);
bool bound = yamlUtil.TryFromYaml<Settings>(yaml, out Settings? settings);
```

`IsValidYaml` returns whether non-blank input can be deserialized as an untyped YAML value. Most plain text is valid YAML, so this is not schema or business validation. `TryFromYaml<T>` returns `false` and `default` when deserialization throws.

## Normalize indentation and line endings

```csharp
string normalized = yamlUtil.Normalize(yaml);
```

`Normalize` and `FixTabsInIndentation` remove a leading BOM, convert CRLF or CR line endings to LF, and replace each tab encountered in leading indentation with two spaces. Tabs inside scalar content are preserved. These methods do not parse or reformat the YAML and return an empty string for blank input.

## Convert files

```csharp
await yamlUtil.SaveAsYaml("settings.json", "settings.yaml", cancellationToken: cancellationToken);
await yamlUtil.SaveAsJson("settings.yaml", "settings.json", cancellationToken: cancellationToken);
```

File conversion reads the complete source, converts it in memory, and writes the destination through `IFileUtil`. The destination write is not transactional; use a temporary destination and replace the target after validation when partial or failed conversions must not overwrite an existing file.

No conversion method enforces input-size, nesting-depth, or alias-count limits beyond those of the underlying parsers. Constrain untrusted YAML and JSON before processing when resource exhaustion is a concern.
