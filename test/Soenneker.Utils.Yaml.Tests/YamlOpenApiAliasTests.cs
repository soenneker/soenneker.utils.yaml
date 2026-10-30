using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Soenneker.Tests.HostedUnit;
using Soenneker.Utils.Yaml.Abstract;

namespace Soenneker.Utils.Yaml.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class YamlOpenApiAliasTests : HostedUnitTest
{
    private readonly IYamlUtil _util;

    public YamlOpenApiAliasTests(Host host) : base(host) => _util = Resolve<IYamlUtil>(true);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void Recursive_schema_aliases_preserve_closed_objects_and_payload_examples(bool customOptions)
    {
        const string yaml = """
            openapi: 3.0.3
            info: {title: Recursive, version: '1'}
            paths: {}
            components:
              schemas:
                Node: &node
                  type: object
                  additionalProperties: false
                  properties:
                    id: {type: string}
                    next: *node
                  example: {schema: {type: literal}, '$ref': literal}
            """;
        string json = customOptions ? _util.YamlToJson(yaml, new JsonSerializerOptions()) : _util.YamlToJson(yaml)!;
        JsonNode node = JsonNode.Parse(json)!["components"]!["schemas"]!["Node"]!;
        node["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
        node["properties"]!["next"]!["$ref"]!.GetValue<string>().Should().Be("#/components/schemas/Node");
        node["example"]!["schema"]!["type"]!.GetValue<string>().Should().Be("literal");
        node["example"]!["$ref"]!.GetValue<string>().Should().Be("literal");
    }

    [Test]
    public void Recursive_property_map_aliases_never_become_references_inside_the_property_name_map()
    {
        const string yaml = """
            openapi: 3.0.3
            info: {title: Recursive, version: '1'}
            paths: {}
            components:
              schemas:
                Node:
                  type: object
                  properties: &fields
                    name: {type: string}
                    child:
                      type: object
                      additionalProperties: false
                      properties: *fields
            """;
        JsonObject schemas = JsonNode.Parse(_util.YamlToJson(yaml)!)!["components"]!["schemas"]!.AsObject();
        string reference = schemas["Node"]!["properties"]!["child"]!["$ref"]!.GetValue<string>();
        JsonNode child = schemas[reference[21..]]!;
        child["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
        child["properties"]!["child"]!["$ref"]!.GetValue<string>().Should().Be(reference);
        child["properties"]!.AsObject().ContainsKey("$ref").Should().BeFalse();
    }
}
