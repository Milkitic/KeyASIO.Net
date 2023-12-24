namespace KeyAsio.MemoryReading.Logging;

public interface ILoggerFactory
{
    ILogger CreateLogger(string name);
    ILogger<T> CreateLogger<T>();
}