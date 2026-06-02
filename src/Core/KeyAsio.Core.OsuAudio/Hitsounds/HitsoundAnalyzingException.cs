namespace KeyAsio.Shared.Hitsounds;

public sealed class HitsoundAnalyzingException : Exception
{
    public HitsoundAnalyzingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
