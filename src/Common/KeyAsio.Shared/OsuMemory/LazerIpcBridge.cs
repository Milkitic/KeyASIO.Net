using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.OsuMemory;

public sealed class LazerIpcBridge : IDisposable
{
    public const string PipeName = "KeyAsio.LazerBridge.v1";
    public const string EventPipeName = "KeyAsio.LazerBridge.Events.v1";
    public const int ProtocolVersion = 1;
    private const int MaxFrameLength = 4 * 1024 * 1024;

    private readonly ILogger<LazerIpcBridge> _logger;
    private readonly ConcurrentDictionary<Task, byte> _clientTasks = new();
    private CancellationTokenSource? _cts;
    private Task[]? _acceptLoopTasks;
    private int _clientCount;
    private int _timingClientCount;
    private int _eventClientCount;

    public LazerIpcBridge(ILogger<LazerIpcBridge> logger)
    {
        _logger = logger;
    }

    public event Action<LazerIpcChannel, bool, bool>? ChannelConnectionChanged;
    public event Action<LazerIpcChannel, LazerIpcDeltaFrame>? FrameReceived;

    public void Start()
    {
        if (_acceptLoopTasks != null) return;

        _cts = new CancellationTokenSource();
        _acceptLoopTasks =
        [
            Task.Run(() => AcceptLoopAsync(LazerIpcChannel.Timing, PipeName, _cts.Token)),
            Task.Run(() => AcceptLoopAsync(LazerIpcChannel.Events, EventPipeName, _cts.Token)),
        ];
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        await _cts.CancelAsync();

        if (_acceptLoopTasks != null)
        {
            try
            {
                await Task.WhenAll(_acceptLoopTasks);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        var clientTasks = _clientTasks.Keys.ToArray();
        if (clientTasks.Length > 0)
        {
            await Task.WhenAll(clientTasks);
        }

        _cts.Dispose();
        _cts = null;
        _acceptLoopTasks = null;
    }

    private async Task AcceptLoopAsync(LazerIpcChannel channel, string pipeName, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(pipeName, PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(token);
                TrackClientTask(Task.Run(() => HandleClientAsync(server, channel, pipeName, token),
                    CancellationToken.None));
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                break;
            }
            catch (Exception ex)
            {
                await server.DisposeAsync();
                _logger.LogWarning(ex, "Failed to accept lazer IPC client on pipe {PipeName}.", pipeName);

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void TrackClientTask(Task task)
    {
        _clientTasks.TryAdd(task, 0);
        _ = task.ContinueWith(static (completedTask, state) =>
        {
            var tasks = (ConcurrentDictionary<Task, byte>)state!;
            tasks.TryRemove(completedTask, out _);
        }, _clientTasks, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, LazerIpcChannel channel, string pipeName,
        CancellationToken token)
    {
        var oldChannelCount = IncrementChannelClientCount(channel) - 1;
        if (oldChannelCount == 0)
        {
            ChannelConnectionChanged?.Invoke(channel, false, true);
        }

        var oldCount = Interlocked.Increment(ref _clientCount) - 1;
        if (oldCount == 0)
        {
            _logger.LogInformation("osu!lazer IPC bridge connected.");
        }

        _logger.LogDebug("osu!lazer IPC pipe connected: {PipeName}.", pipeName);

        await using (server)
        {
            var lengthBuffer = new byte[sizeof(int)];
            try
            {
                while (!token.IsCancellationRequested && server.IsConnected)
                {
                    LazerIpcDeltaFrame? frame;
                    try
                    {
                        frame = await ReadFrameAsync(server, lengthBuffer, token);
                    }
                    catch (InvalidDataException ex)
                    {
                        _logger.LogDebug(ex, "Ignoring malformed lazer IPC frame.");
                        continue;
                    }

                    if (frame == null) break;

                    if (frame.Version != ProtocolVersion)
                    {
                        _logger.LogDebug("Ignoring unsupported lazer IPC protocol version {Version}.", frame.Version);
                        continue;
                    }

                    FrameReceived?.Invoke(channel, frame);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "osu!lazer IPC pipe disconnected: {PipeName}.", pipeName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected lazer IPC bridge error on pipe {PipeName}.", pipeName);
            }
        }

        var newChannelCount = DecrementChannelClientCount(channel);
        if (newChannelCount == 0)
        {
            ChannelConnectionChanged?.Invoke(channel, true, false);
        }

        var newCount = Interlocked.Decrement(ref _clientCount);
        _logger.LogDebug("osu!lazer IPC pipe disconnected: {PipeName}.", pipeName);
        if (newCount == 0)
        {
            _logger.LogInformation("osu!lazer IPC bridge disconnected.");
        }
    }

    private int IncrementChannelClientCount(LazerIpcChannel channel)
    {
        return channel switch
        {
            LazerIpcChannel.Timing => Interlocked.Increment(ref _timingClientCount),
            LazerIpcChannel.Events => Interlocked.Increment(ref _eventClientCount),
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
        };
    }

    private int DecrementChannelClientCount(LazerIpcChannel channel)
    {
        return channel switch
        {
            LazerIpcChannel.Timing => Interlocked.Decrement(ref _timingClientCount),
            LazerIpcChannel.Events => Interlocked.Decrement(ref _eventClientCount),
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
        };
    }

    private async ValueTask<LazerIpcDeltaFrame?> ReadFrameAsync(Stream stream, byte[] lengthBuffer,
        CancellationToken token)
    {
        try
        {
            await stream.ReadExactlyAsync(lengthBuffer, token);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length <= 0 || length > MaxFrameLength)
        {
            throw new IOException($"Invalid lazer IPC frame length: {length}.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            await stream.ReadExactlyAsync(buffer.AsMemory(0, length), token);
            return LazerIpcDeltaFrame.Parse(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}

public enum LazerIpcChannel
{
    Timing,
    Events,
}
