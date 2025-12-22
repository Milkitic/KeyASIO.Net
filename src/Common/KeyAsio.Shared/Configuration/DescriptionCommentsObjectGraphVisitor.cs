using System.ComponentModel;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace KeyAsio.Shared.Configuration;

public class DescriptionCommentsObjectGraphVisitor : ChainedObjectGraphVisitor
{
    public DescriptionCommentsObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
        : base(nextVisitor)
    {
    }

    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context, ObjectSerializer serializer)
    {
        var attr = key.GetCustomAttribute<DescriptionAttribute>();
        if (attr is { Description: not null })
        {
            context.Emit(new YamlDotNet.Core.Events.Comment(attr.Description, false));
        }

        return base.EnterMapping(key, value, context, serializer);
    }
}