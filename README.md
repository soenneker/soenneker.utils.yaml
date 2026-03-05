[![](https://img.shields.io/nuget/v/soenneker.utils.yaml.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.yaml/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.yaml/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.yaml/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.yaml.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.yaml/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.Yaml
### A utility library handling useful YAML functionalities

## Installation

```
dotnet add package Soenneker.Utils.Yaml
```

## Usage

Register the utility (optional, for DI):

```csharp
services.AddYamlUtilAsSingleton(); // or AddYamlUtilAsScoped()
```

Use `IYamlUtil` for YAML/JSON conversion and validation:

- **ToYaml(object)** – Serialize any object to a YAML string.
- **FromYaml&lt;T&gt;(string)** – Deserialize YAML to a typed instance.
- **FromYaml(string)** – Deserialize YAML to an untyped object (dictionary/list).
- **JsonToYaml(string)** – Convert a JSON string to YAML.
- **YamlToJson(string)** – Convert a YAML string to JSON.
- **IsValidYaml(string)** – Return whether the string is valid YAML.
- **TryFromYaml&lt;T&gt;(string, out T?)** – Try to deserialize YAML to `T`.
