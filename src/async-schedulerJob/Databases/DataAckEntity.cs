using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Async.SchedulerJob;

/// <summary>
/// Represents an entity of the server and client consumed ACK entry.
/// </summary>
[Table("DataAck")]
[Description("Stores the server and client consumed ACK data.")]
public class DataAckEntity
{
    /// <summary>
    /// The ID.
    /// </summary>
    [Key]
    [Description("The ID.")]
    public Guid Id { get; set; }

    /// <summary>
    /// The consumer Id.
    /// </summary>
    [Required]
    [Index("IX_DailyId", IsUnique = false, Order = 0)]
    [MaxLength(64)]
    [Description("The consumer Id.")]
    public string ConsumerId { get; set; }

    /// <summary>
    /// The daily Id.
    /// </summary>
    [Required]
    [Index("IX_DailyId", IsUnique = false, Order = 1)]
    [Description("The daily Id.")]
    public Guid DailyId { get; set; }
}