using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KeyAsio.Net.Configuration;

public class YamlConverter
{
    public ConfigurationBase DeserializeSettings(string content, Type type)
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .IgnoreFields();
        ConfigDeserializeBuilder(builder);
        var list = ConfigTagMapping();
        if (list != null) InnerConfigTagMapping(list, builder);

        var ymlDeserializer = builder.Build();

        return (ConfigurationBase)ymlDeserializer.Deserialize(content, type);
    }

    public string SerializeSettings(ConfigurationBase @object)
    {
        var builder = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .IgnoreFields();
        ConfigSerializeBuilder(builder);
        var list = ConfigTagMapping();
        if (list != null) InnerConfigTagMapping(list, builder);
        var converter = builder.Build();
        var content = converter.Serialize(@object);
        return content;
    }

    private static void InnerConfigTagMapping<TBuilder>(IEnumerable<Type> list, BuilderSkeleton<TBuilder> builder)
        where TBuilder : BuilderSkeleton<TBuilder>
    {
        foreach (var type in list)
        {
            builder.WithTagMapping("tag:yaml.org,2002:" +
                                   PascalCaseNamingConvention.Instance.Apply(GetStandardGenericName(type)), type);
        }
    }

    protected virtual void ConfigSerializeBuilder(SerializerBuilder builder)
    {
    }

    protected virtual void ConfigDeserializeBuilder(DeserializerBuilder builder)
    {
    }

    protected virtual List<Type> ConfigTagMapping()
    {
        return null;
    }

    private static string GetStandardGenericName(Type type)
    {
        // demo: System.Collection.Generic.List`1[System.String] => System.Collection.Generic.List<System.String>

        if (!type.IsGenericType) return type.FullName;

        var genericType = type.GetGenericTypeDefinition();
        string fullName = genericType.FullName;
        if (fullName?.Contains("`") == true)
        {
            fullName = fullName.Substring(0, fullName.IndexOf("`", StringComparison.Ordinal));
        }

        var args = type.GetGenericArguments();
        var result = fullName + "<" + string.Join(",", args.Select(GetStandardGenericName)) + ">";
        return result;
    }
}