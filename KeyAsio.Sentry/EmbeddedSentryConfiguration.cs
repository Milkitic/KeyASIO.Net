using Sentry;

// ReSharper disable once CheckNamespace
public partial class EmbeddedSentryConfiguration : IDisposable
{
    private readonly IDisposable _sentrySdk;

    public EmbeddedSentryConfiguration()
    {
        _sentrySdk = SentrySdk.Init(options =>
        {
            options.Dsn = __dsn;
#if DEBUG
            options.Debug = true;
            options.Environment = "debug";
#else
            options.Debug = false;
            options.Environment = "production";
#endif
            options.TracesSampleRate = 1;
            options.HttpProxy = HttpClient.DefaultProxy;
            options.SendDefaultPii = true;
            options.AttachStacktrace = true;
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });
    }

    public void Dispose()
    {
        _sentrySdk.Dispose();
    }
}