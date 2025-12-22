using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Utils;

public sealed class AsyncSequentialWorker : IDisposable
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;
    private readonly Task _workerLoop;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger? _logger;
    private readonly string _name;

    public AsyncSequentialWorker(ILogger? logger = null, string name = "AsyncSequentialWorker")
    {
        _logger = logger;
        _name = name;
        _channel = Channel.CreateUnbounded<Func<CancellationToken, Task>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _workerLoop = Task.Factory.StartNew(LoopAsync, TaskCreationOptions.LongRunning).Unwrap();
    }

    public void Enqueue(Func<CancellationToken, Task> workItem)
    {
        _channel.Writer.TryWrite(workItem);
    }

    public void Enqueue(Func<Task> workItem)
    {
        _channel.Writer.TryWrite(_ => workItem());
    }

    private async Task LoopAsync()
    {
        var reader = _channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token))
            {
                while (reader.TryRead(out var workItem))
                {
                    if (_cts.IsCancellationRequested) break;
                    try
                    {
                        await workItem(_cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing work item in {Name}", _name);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fatal error in {Name} loop", _name);
        }
    }

    public void Dispose()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
