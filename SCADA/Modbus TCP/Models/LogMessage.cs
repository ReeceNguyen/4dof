using System;

namespace MobusTCP.Models;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Tx,
    Rx
}

public class LogMessage
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;
    public string RawHex { get; set; } = string.Empty;

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public string ColorHex => Level switch
    {
        LogLevel.Success => "#10B981", // Green
        LogLevel.Warning => "#F59E0B", // Amber
        LogLevel.Error => "#EF4444",   // Red
        LogLevel.Tx => "#3B82F6",      // Blue (Transmit)
        LogLevel.Rx => "#8B5CF6",      // Purple (Receive)
        _ => "#9CA3AF"                 // Gray / Default
    };
}
