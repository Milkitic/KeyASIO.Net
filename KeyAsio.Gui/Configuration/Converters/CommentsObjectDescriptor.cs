using System;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace KeyAsio.Gui.Configuration.Converters;

public sealed class CommentsObjectDescriptor : IObjectDescriptor
{
    private readonly IObjectDescriptor innerDescriptor;

    public CommentsObjectDescriptor(IObjectDescriptor innerDescriptor, string comment)
    {
        this.innerDescriptor = innerDescriptor;
        this.Comment = comment;
    }

    public string Comment { get; private set; }

    public object Value { get { return innerDescriptor.Value; } }
    public Type Type { get { return innerDescriptor.Type; } }
    public Type StaticType { get { return innerDescriptor.StaticType; } }
    public ScalarStyle ScalarStyle { get { return innerDescriptor.ScalarStyle; } }
}