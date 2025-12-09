using StackExchange.Redis;

namespace TorreClou.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private const string StreamKey = "jobs:stream";
    private const string GroupName = "workers";
    private readonly string _consumerName;

    public Worker(ILogger<Worker> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
        _consumerName = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        // Ensure consumer group exists
        await EnsureConsumerGroupExistsAsync(db);

        // First, claim any pending messages from previous runs (retry logic)
        await ClaimPendingMessagesAsync(db, stoppingToken);

        _logger.LogInformation(
            "Worker started | Consumer: {Consumer} | Stream: {Stream} | Group: {Group}",
            _consumerName, StreamKey, GroupName
        );

        // Main processing loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read new messages from stream (blocking for 5 seconds)
                var entries = await db.StreamReadGroupAsync(
                    StreamKey,
                    GroupName,
                    _consumerName,
                    ">",  // Only new messages
                    count: 10,
                    noAck: false
                );

                if (entries.Length == 0)
                {
                    // No new messages, wait a bit before trying again
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessMessageAsync(db, entry, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error while reading from stream. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Worker stopping | Consumer: {Consumer}", _consumerName);
    }

    private async Task EnsureConsumerGroupExistsAsync(IDatabase db)
    {
        try
        {
            // Try to create the consumer group (MKSTREAM creates the stream if it doesn't exist)
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, "0", createStream: true);
            _logger.LogInformation("Created consumer group: {Group} on stream: {Stream}", GroupName, StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists, that's fine
            _logger.LogDebug("Consumer group {Group} already exists", GroupName);
        }
    }

    private async Task ClaimPendingMessagesAsync(IDatabase db, CancellationToken stoppingToken)
    {
        try
        {
            // Get pending messages that have been idle for more than 30 seconds
            var pendingInfo = await db.StreamPendingAsync(StreamKey, GroupName);
            
            if (pendingInfo.PendingMessageCount > 0)
            {
                _logger.LogInformation(
                    "Found {Count} pending messages in stream. Attempting to claim...",
                    pendingInfo.PendingMessageCount
                );

                // Claim messages that have been pending for more than 30 seconds
                var claimed = await db.StreamAutoClaimAsync(
                    StreamKey,
                    GroupName,
                    _consumerName,
                    30000,  // minIdleTimeInMs: 30 seconds
                    "0-0",  // start position
                    100     // count
                );

                foreach (var entry in claimed.ClaimedEntries)
                {
                    _logger.LogInformation(
                        "[RETRY] Claimed pending message | MessageId: {MessageId}",
                        entry.Id
                    );
                    await ProcessMessageAsync(db, entry, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error claiming pending messages. Will process new messages only.");
        }
    }

    private async Task ProcessMessageAsync(IDatabase db, StreamEntry entry, CancellationToken stoppingToken)
    {
        var messageId = entry.Id;
        var jobId = entry["jobId"].ToString();
        var userId = entry["userId"].ToString();
        var createdAt = entry["createdAt"].ToString();

        try
        {
            _logger.LogInformation(
                "[ACK] Received job from Redis Stream | JobId: {JobId} | UserId: {UserId} | MessageId: {MessageId} | CreatedAt: {CreatedAt}",
                jobId,
                userId,
                messageId,
                createdAt
            );

            // TODO: Future implementation will process the job here
            // - Fetch job details from DB using jobId
            // - Execute job based on type
            // - Update job status to PROCESSING, then COMPLETED/FAILED

            // Acknowledge the message after successful processing
            await db.StreamAcknowledgeAsync(StreamKey, GroupName, messageId);
            
            _logger.LogInformation(
                "[COMPLETED] Job acknowledged | JobId: {JobId} | MessageId: {MessageId}",
                jobId,
                messageId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[FAILED] Error processing job | JobId: {JobId} | MessageId: {MessageId} | Error: {Error}",
                jobId,
                messageId,
                ex.Message
            );
            // Don't acknowledge - message will be retried via pending claims
        }
    }
}
