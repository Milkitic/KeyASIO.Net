using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace KeyAsio.Gui.Utils;

public static class EncodeUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetBase64String(string value, Encoding encoding)
    {
        return Convert.ToBase64String(encoding.GetBytes(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FromBase64String(string value, Encoding encoding)
    {
        return encoding.GetString(Convert.FromBase64String(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FromBase64StringEmptyIfError(string value, Encoding encoding)
    {
        try
        {
            return FromBase64String(value, encoding);
        }
        catch
        {
            return "";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetBase64String(string value)
    {
        return GetBase64String(value, Encoding.UTF8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FromBase64String(string value)
    {
        return FromBase64String(value, Encoding.UTF8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FromBase64StringEmptyIfError(string value)
    {
        try
        {
            return FromBase64String(value, Encoding.UTF8);
        }
        catch
        {
            return "";
        }
    }
}