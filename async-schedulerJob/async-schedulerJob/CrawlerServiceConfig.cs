namespace Async.SchedulerJob;

/// <summary>
/// The CrawlerService config.
/// </summary>
public class CrawlerServiceConfig
{
    /// <summary>
    /// TraceBackTime config.
    /// </summary>
    public TraceBackTimeConfig TraceBackTime { get; set; }
}

/// <summary>
/// TraceBackTime config.
/// </summary>
public class TraceBackTimeConfig
{
    /// <summary>
    /// Minutes
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Hours
    /// </summary>
    public int Hours { get; set; }

    /// <summary>
    /// Days
    /// </summary>
    public int Days { get; set; } = -7;
}