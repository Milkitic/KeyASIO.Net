using KeyAsio.Common;

namespace KeyAsio.UnitTests;

public sealed class AsyncSequentialWorkerTests
{
    [Fact]
    public async Task EnqueueAsync_ExecutesWorkInSubmissionOrder()
    {
        await using var worker = new AsyncSequentialWorker();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> order = [];

        var first = worker.EnqueueAsync(async () =>
        {
            order.Add(1);
            firstStarted.SetResult();
            await releaseFirst.Task;
            order.Add(2);
        });
        await firstStarted.Task;

        var second = worker.EnqueueAsync(() =>
        {
            order.Add(3);
            return Task.CompletedTask;
        });
        releaseFirst.SetResult();

        await Task.WhenAll(first, second);
        Assert.Equal([1, 2, 3], order);
    }

    [Fact]
    public async Task BoundedWorker_RejectsSynchronousWriteWhenQueueIsFull()
    {
        await using var worker = new AsyncSequentialWorker(capacity: 1);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        worker.Enqueue(async () =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task;
        });
        await firstStarted.Task;
        worker.Enqueue(() => Task.CompletedTask);

        Assert.Throws<InvalidOperationException>(() => worker.Enqueue(() => Task.CompletedTask));
        releaseFirst.SetResult();
    }

    [Fact]
    public async Task DisposeAsync_CancelsRunningAndPendingWork()
    {
        var worker = new AsyncSequentialWorker(capacity: 1);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = worker.EnqueueAsync(async cancellationToken =>
        {
            firstStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        await firstStarted.Task;
        var pending = worker.EnqueueAsync(_ => Task.CompletedTask);

        await worker.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
