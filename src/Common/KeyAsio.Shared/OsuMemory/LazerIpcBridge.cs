using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipes;
using KeyAsio.Shared.Events;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.OsuMemory;

public sealed class LazerIpcBridge : IDisposable
{
    public const string PipeName = "KeyAsio.LazerBridge.v1";
    public const int ProtocolVersion = 1;
    private const int MaxFrameLength = 4 * 1024 * 1024;

    private readonly ILogger<LazerIpcBridge> _logger;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private int _clientCount;

    public LazerIpcBridge(ILogger<LazerIpcBridge> logger)
    {
        _logger = logger;
    }

    public event ValueChangedEventHandler<bool>? ConnectionChanged;
    public event Action<LazerIpcDeltaFrame>? FrameReceived;

    public void Start()
    {
        if (_acceptLoopTask != null) return;

        _cts = new CancellationTokenSource();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        await _cts.CancelAsync();

        if (_acceptLoopTask != null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _cts.Dispose();
        _cts = null;
        _acceptLoopTask = null;

        if (Interlocked.Exchange(ref _clientCount, 0) > 0)
        {
            ConnectionChanged?.Invoke(true, false);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(PipeName, PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(token);
                _ = Task.Run(() => HandleClientAsync(server, token), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                break;
            }
            catch (Exception ex)
            {
                await server.DisposeAsync();
                _logger.LogWarning(ex, "Failed to accept lazer IPC client.");

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

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken token)
    {
        var oldCount = Interlocked.Increment(ref _clientCount) - 1;
        if (oldCount == 0)
        {
            _logger.LogInformation("osu!lazer IPC bridge connected.");
            ConnectionChanged?.Invoke(false, true);
        }

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

                    FrameReceived?.Invoke(frame);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "osu!lazer IPC bridge disconnected.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected lazer IPC bridge error.");
            }
        }

        var newCount = Interlocked.Decrement(ref _clientCount);
        if (newCount == 0)
        {
            _logger.LogInformation("osu!lazer IPC bridge disconnected.");
            ConnectionChanged?.Invoke(true, false);
        }
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
