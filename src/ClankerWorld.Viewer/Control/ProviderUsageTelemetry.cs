namespace ClankerWorld.Viewer.Control;

public static partial class ProviderUsageTelemetry
{
    [LoggerMessage(EventId = 2281, Level = LogLevel.Warning,
        Message = "provider_usage_limit_reached outcome=paused scope=installation tick={WorldTick}")]
    public static partial void LimitReached(ILogger logger, long worldTick);

    [LoggerMessage(EventId = 2282, Level = LogLevel.Information,
        Message = "provider_usage_limit_configured outcome=accepted enabled={Enabled} limit={AttemptLimit} attempts={Attempts}")]
    public static partial void LimitConfigured(ILogger logger, bool enabled, long? attemptLimit, long attempts);
}
