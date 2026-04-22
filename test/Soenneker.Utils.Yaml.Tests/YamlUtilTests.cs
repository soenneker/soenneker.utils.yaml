using AwesomeAssertions;
using Soenneker.Utils.Yaml.Abstract;
using Soenneker.Tests.HostedUnit;
using Soenneker.Utils.Yaml.Tests.Dtos;

namespace Soenneker.Utils.Yaml.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class YamlUtilTests : HostedUnitTest
{
    private readonly IYamlUtil _util;

    public YamlUtilTests(Host host) : base(host)
    {
        _util = Resolve<IYamlUtil>(true);
    }

    [Test]
    public void ToYaml_null_returns_empty_string()
    {
        string result = _util.ToYaml(null!);

        result.Should()
              .Be(string.Empty);
    }

    [Test]
    public void ToYaml_object_returns_yaml_string()
    {
        var obj = new { Name = "test", Count = 42 };

        string result = _util.ToYaml(obj);

        result.Should()
              .Contain("name")
              .And.Contain("test")
              .And.Contain("42");
    }

    [Test]
    public void FromYaml_generic_valid_yaml_returns_typed_instance()
    {
        const string yaml = "name: foo\ncount: 10";

        var result = _util.FromYaml<SimpleDto>(yaml);

        result.Should()
              .NotBeNull();
        result!.Name.Should()
               .Be("foo");
        result.Count.Should()
              .Be(10);
    }

    [Test]
    public void FromYaml_generic_null_returns_default()
    {
        var result = _util.FromYaml<string?>(null);

        result.Should()
              .BeNull();
    }

    [Test]
    public void FromYaml_generic_whitespace_returns_default()
    {
        var result = _util.FromYaml<string?>("   ");

        result.Should()
              .BeNull();
    }

    [Test]
    public void FromYaml_untyped_valid_yaml_returns_object_graph()
    {
        const string yaml = "key: value";

        object? result = _util.FromYaml(yaml);

        result.Should()
              .NotBeNull();
    }

    [Test]
    public void FromYaml_untyped_null_returns_null()
    {
        object? result = _util.FromYaml(null);

        result.Should()
              .BeNull();
    }

    [Test]
    public void FromYaml_untyped_whitespace_returns_null()
    {
        object? result = _util.FromYaml("  \t\n  ");

        result.Should()
              .BeNull();
    }

    [Test]
    public void JsonToYaml_valid_json_returns_yaml()
    {
        const string json = """{"name":"bar","count":5}""";

        string result = _util.JsonToYaml(json);

        result.Should()
              .NotBeNullOrWhiteSpace()
              .And.Contain("name")
              .And.Contain("bar");
    }

    [Test]
    public void JsonToYaml_null_returns_empty_string()
    {
        string result = _util.JsonToYaml(null);

        result.Should()
              .Be(string.Empty);
    }

    [Test]
    public void JsonToYaml_whitespace_returns_empty_string()
    {
        string result = _util.JsonToYaml("   ");

        result.Should()
              .Be(string.Empty);
    }

    [Test]
    public void YamlToJson_valid_yaml_returns_json()
    {
        const string yaml = "name: baz\ncount: 7";

        string result = _util.YamlToJson(yaml);

        result.Should()
              .NotBeNullOrWhiteSpace()
              .And.Contain("name")
              .And.Contain("baz")
              .And.Contain("7");
    }

    [Test]
    public void YamlToJson_null_returns_empty_object_json()
    {
        string result = _util.YamlToJson(null);

        result.Should()
              .Be("{}");
    }

    [Test]
    public void YamlToJson_whitespace_returns_empty_object_json()
    {
        string result = _util.YamlToJson("  \n  ");

        result.Should()
              .Be("{}");
    }

    [Test]
    public void IsValidYaml_valid_yaml_returns_true()
    {
        const string yaml = "key: value";

        bool result = _util.IsValidYaml(yaml);

        result.Should()
              .BeTrue();
    }

    [Test]
    public void IsValidYaml_invalid_yaml_returns_false()
    {
        const string invalid = "key: [unclosed";

        bool result = _util.IsValidYaml(invalid);

        result.Should()
              .BeFalse();
    }

    [Test]
    public void IsValidYaml_null_returns_false()
    {
        bool result = _util.IsValidYaml(null);

        result.Should()
              .BeFalse();
    }

    [Test]
    public void IsValidYaml_whitespace_returns_false()
    {
        bool result = _util.IsValidYaml("  \t  ");

        result.Should()
              .BeFalse();
    }

    [Test]
    public void TryFromYaml_valid_yaml_returns_true_and_result()
    {
        const string yaml = "name: qux";

        bool success = _util.TryFromYaml<SimpleDto>(yaml, out SimpleDto? result);

        success.Should()
               .BeTrue();
        result.Should()
              .NotBeNull();
        result!.Name.Should()
               .Be("qux");
    }

    [Test]
    public void TryFromYaml_invalid_yaml_returns_false_and_default()
    {
        const string invalid = "not: valid: yaml: here:";

        bool success = _util.TryFromYaml<object>(invalid, out object? result);

        success.Should()
               .BeFalse();
        result.Should()
              .BeNull();
    }

    [Test]
    public void TryFromYaml_null_returns_false()
    {
        bool success = _util.TryFromYaml<string?>(null, out _);

        success.Should()
               .BeFalse();
    }

    [Test]
    public void Roundtrip_yaml_to_json_to_yaml_preserves_structure()
    {
        const string yaml = "a: 1\nb: two\nlist:\n  - x\n  - y";

        string json = _util.YamlToJson(yaml);
        string backToYaml = _util.JsonToYaml(json);

        backToYaml.Should()
                  .Contain("a")
                  .And.Contain("1")
                  .And.Contain("b")
                  .And.Contain("two")
                  .And.Contain("list");
    }

    [Test]
    public void Roundtrip_json_to_yaml_to_json_preserves_structure()
    {
        const string json = """{"x":1,"y":"hello","items":[1,2,3]}""";

        string yaml = _util.JsonToYaml(json);
        string backToJson = _util.YamlToJson(yaml);

        backToJson.Should()
                  .Contain("x")
                  .And.Contain("1")
                  .And.Contain("y")
                  .And.Contain("hello")
                  .And.Contain("items");
    }
}