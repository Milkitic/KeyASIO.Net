namespace KeyAsio.Secrets;

public class EmbeddedSentryConfiguration : IDisposable
{
    private readonly IDisposable _sentrySdk;

    public EmbeddedSentryConfiguration(Action<SentryOptions>? configureOptions = null)
    {
        _sentrySdk = SentrySdk.Init(options =>
        {
            Configure(options);
            configureOptions?.Invoke(options);
        });
    }

    public static void Configure(SentryOptions options)
    {
        options.Dsn = global::Secrets.Dsn;
#if !RELEASE
        options.Debug = true;
        options.Environment = "debug";
#else
        options.Debug = false;
        options.Environment = "production";
#endif
        options.TracesSampleRate = 1;
        options.SendDefaultPii = true;
        options.AttachStacktrace = true;
    }

    public void Dispose()
    {
        _sentrySdk.Dispose();
    }
}