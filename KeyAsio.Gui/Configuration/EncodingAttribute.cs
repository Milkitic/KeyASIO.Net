using System;

namespace KeyAsio.Gui.Configuration;

public class EncodingAttribute : Attribute
{
    public string EncodingString { get; }

    public EncodingAttribute(string encodingString)
    {
        EncodingString = encodingString;
    }
}