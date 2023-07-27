using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Async.SchedulerJob;

/// <summary>
/// Represents an entity of the log entry.
/// </summary>
[Table("Log")]
[Description("Stores the spectro log.")]
public class LogEntity
{
    /// <summary>
    /// The ID.
    /// </summary>
    [Key]
    [Description("The ID.")]
    public Guid L0_Id { get; set; }

    /// <summary>
    /// The device Id.
    /// </summary>
    [Required]
    [Description("The device Id.")]
    public Guid L0_DeviceId { get; set; }

    /// <summary>
    /// The SSH host IP.
    /// </summary>
    [MaxLength(32)]
    [Description("The SSH host IP.")]
    public string L0_HostIp { get; set; }

    /// <summary>
    /// The device IP.
    /// </summary>
    [MaxLength(32)]
    [Description("The device IP.")]
    public string L0_DeviceIp { get; set; }

    /// <summary>
    /// The SSH connection status.
    /// </summary>
    [Description("The SSH connection status.")]
    public SshStatus L0_SSHStatus { get; set; }

    /// <summary>
    /// The exception message.
    /// </summary>
    [Description("The exception message.")]
    public string L0_Exception { get; set; }

    /// <summary>
    /// The log date time.
    /// </summary>
    [Index("IX_CreateDate", IsUnique = false, Order = 0)]
    [Description("The log date time.")]
    public DateTime L0_DateTime { get; set; }
}

/// <summary>
/// The SSH connection status.
/// </summary>
public enum SshStatus
{
    /// <summary>
    /// Connect failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Connect successful.
    /// </summary>
    Success
}