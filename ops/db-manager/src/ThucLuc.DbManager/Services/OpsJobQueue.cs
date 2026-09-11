using System.Collections.Concurrent;
using System.Threading.Channels;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IOpsJobQueue
{
    OperationEntry Enqueue(
        string operation,
        string environmentKey,
        string summary,
        Func<CancellationToken, Task<string>> handler);
}

public sealed class OpsJobQueue(
    IOperationJournal journal,
    ILogger<OpsJobQueue> logger) : BackgroundService, IOpsJobQueue
{
    private readonly Channel<QueuedOpsJob> _queue = Channel.CreateUnbounded<QueuedOpsJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _environmentLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public OperationEntry Enqueue(
        string operation,
        string environmentKey,
        string summary,
        Func<CancellationToken, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentKey);
        ArgumentNullException.ThrowIfNull(handler);

        var entry = journal.AddWaiting(operation, environmentKey, summary);
        if (!_queue.Writer.TryWrite(new QueuedOpsJob(entry, handler)))
        {
            journal.Complete(entry.Id, OperationState.Failed, "Không thể đưa thao tác vào hàng đợi.");
            throw new InvalidOperationException("Không thể đưa thao tác vào hàng đợi.");
        }

        return entry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var environmentLock = _environmentLocks.GetOrAdd(
                job.Entry.EnvironmentKey,
                _ => new SemaphoreSlim(1, 1));

            await environmentLock.WaitAsync(stoppingToken);
            try
            {
                journal.Complete(job.Entry.Id, OperationState.Running, "Đang thực hiện.");
                var result = await job.Handler(stoppingToken);
                journal.Complete(job.Entry.Id, OperationState.Succeeded, result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                journal.Complete(
                    job.Entry.Id,
                    OperationState.Failed,
                    "Ứng dụng đang dừng trước khi thao tác hoàn tất. Hãy kiểm tra trạng thái thực tế.");
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Ops job {OperationId} ({Operation}) thất bại",
                    job.Entry.Id,
                    job.Entry.Operation);
                journal.Complete(job.Entry.Id, OperationState.Failed, SafeMessage(exception));
            }
            finally
            {
                environmentLock.Release();
            }
        }
    }

    private static string SafeMessage(Exception exception)
    {
        var message = exception.GetBaseException().Message.Trim();
        return message.Length <= 2_000 ? message : message[..2_000];
    }

    private sealed record QueuedOpsJob(
        OperationEntry Entry,
        Func<CancellationToken, Task<string>> Handler);
}
