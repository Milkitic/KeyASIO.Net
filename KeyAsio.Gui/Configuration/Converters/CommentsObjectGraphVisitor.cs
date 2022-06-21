using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace KeyAsio.Gui.Configuration.Converters;

public class CommentsObjectGraphVisitor : ChainedObjectGraphVisitor
{
    public CommentsObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
        : base(nextVisitor)
    {
    }

    public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
    {
        if (value is CommentsObjectDescriptor commentsDescriptor && commentsDescriptor.Comment != null)
        {
            context.Emit(new YamlDotNet.Core.Events.Comment(commentsDescriptor.Comment, false));
        }

        return base.EnterMapping(key, value, context);
    }
}