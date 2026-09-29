using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectFactories;
using YamlDotNet.Serialization.TypeResolvers;

namespace Soenneker.Utils.Yaml;

// Only the containers and scalars produced by untyped YAML/JSON parsing are supported here.
// Arbitrary user models use explicit JSON metadata or the separately annotated reflection APIs.
internal sealed class YamlGraphContext : StaticContext
{
    public override bool IsKnownType(Type type) => type == typeof(object) || type == typeof(string) ||
        type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime) ||
        type == typeof(Dictionary<object, object>) || type == typeof(Dictionary<string, object>) ||
        type == typeof(List<object>) || type == typeof(object[]);
    public override ITypeResolver GetTypeResolver() => new DynamicTypeResolver();
    public override StaticObjectFactory GetFactory() => new GraphFactory();
    public override ITypeInspector GetTypeInspector() => new GraphInspector();

    private sealed class GraphFactory : StaticObjectFactory
    {
        public override object Create(Type type)
        {
            if (type == typeof(Dictionary<object, object>)) return new Dictionary<object, object?>();
            if (type == typeof(Dictionary<string, object>)) return new Dictionary<string, object?>();
            if (type == typeof(List<object>)) return new List<object?>();
            throw new NotSupportedException($"The YAML graph context does not support {type}.");
        }
        public override Array CreateArray(Type type, int count) => type == typeof(object[]) ? new object?[count] : throw new NotSupportedException();
        public override bool IsDictionary(Type type) => type == typeof(Dictionary<object, object>) || type == typeof(Dictionary<string, object>);
        public override bool IsArray(Type type) => type == typeof(object[]);
        public override bool IsList(Type type) => type == typeof(List<object>);
        public override Type GetKeyType(Type type) => type == typeof(Dictionary<string, object>) ? typeof(string) : typeof(object);
        public override Type GetValueType(Type type) => typeof(object);
        public override void ExecuteOnDeserializing(object value) { }
        public override void ExecuteOnDeserialized(object value) { }
        public override void ExecuteOnSerializing(object value) { }
        public override void ExecuteOnSerialized(object value) { }
    }

    private sealed class GraphInspector : ITypeInspector
    {
        public IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container) => throw new NotSupportedException($"No YAML model metadata for {type}.");
        public IPropertyDescriptor GetProperty(Type type, object? container, string name, [MaybeNullWhen(true)] bool ignoreUnmatched, bool caseInsensitivePropertyMatching) =>
            ignoreUnmatched ? null! : throw new NotSupportedException($"No YAML property metadata for {type}.{name}.");
        public string GetEnumName(Type enumType, string name) => throw new NotSupportedException("Use generated model metadata for enums.");
        public string GetEnumValue(object enumValue) => throw new NotSupportedException("Use generated model metadata for enums.");
        public bool HasParseMethod(Type type) => false;
        public object? Parse(string value, Type expectedType) => throw new NotSupportedException();
    }
}
