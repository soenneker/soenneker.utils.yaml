using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soenneker.Utils.Yaml.Tests;

public sealed class YamlAotTests
{
    [Test]
    public void Generated_metadata_roundtrips_models_and_preserves_json_names()
    {
        var util = new YamlUtil(null!);
        var model = new YamlAotModel { Name = "quoted: value", Count = 42 };
        string yaml = util.ToYaml(model, YamlAotContext.Default.YamlAotModel);
        YamlAotModel? result = util.FromYaml(yaml, YamlAotContext.Default.YamlAotModel);
        if (result?.Name != model.Name || result.Count != 42 || !yaml.Contains("custom_name:"))
            throw new Exception("Generated model metadata did not roundtrip.");
    }

    [Test]
    public void TryFromYaml_rejects_duplicate_keys_and_invalid_model_values()
    {
        var util = new YamlUtil(null!);
        if (util.TryFromYaml("count: 1\ncount: 2", YamlAotContext.Default.YamlAotModel, out _) ||
            util.TryFromYaml("count: invalid", YamlAotContext.Default.YamlAotModel, out _) ||
            util.TryFromYaml(" ", YamlAotContext.Default.YamlAotModel, out _))
            throw new Exception("Invalid input was accepted.");
    }

    [Test]
    public void Static_graph_preserves_nested_sequences_and_scalars()
    {
        var util = new YamlUtil(null!);
        string yaml = util.JsonToYaml("{\"items\":[true,7,\"text\",null,{\"value\":2.5}]}")!;
        using JsonDocument document = JsonDocument.Parse(util.YamlToJson(yaml, new JsonSerializerOptions()));
        JsonElement items = document.RootElement.GetProperty("items");
        if (items.GetArrayLength() != 5 || !items[0].GetBoolean() || items[1].GetInt32() != 7 ||
            items[2].GetString() != "text" || items[3].ValueKind != JsonValueKind.Null || items[4].GetProperty("value").GetDouble() != 2.5)
            throw new Exception("Static YAML graph conversion changed scalar values.");
    }

    [Test]
    public void Json_options_can_be_reused_and_remain_mutable()
    {
        var util = new YamlUtil(null!);
        var options = new JsonSerializerOptions();
        string first = util.YamlToJson("value: 1", options);
        options.WriteIndented = true;
        string second = util.YamlToJson("value: 2", options);
        if (options.IsReadOnly || !first.Contains("1") || !second.Contains("2"))
            throw new Exception("YAML conversion mutated or froze caller-owned JSON options.");
    }
}

internal sealed class YamlAotModel
{
    [JsonPropertyName("custom_name")]
    public string? Name { get; set; }
    public int Count { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(YamlAotModel))]
internal partial class YamlAotContext : JsonSerializerContext;
