using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Async.SchedulerJob;

/// <summary>
/// Represents an entity of the laboratory device info entry.
/// </summary>
[Table("DeviceInfo")]
[Description("Stores the laboratory device info.")]
public class DeviceInfoEntity
{
    /// <summary>
    /// The ID.
    /// </summary>
    [Key]
    [Description("The ID.")]
    public Guid Di_Id { get; set; }

    /// <summary>
    /// The laboratory name.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Index("IX_DeviceName", IsUnique = true, Order = 0)]
    [Description("The laboratory name.")]
    public string Di_LabName { get; set; }

    /// <summary>
    /// The device name.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Index("IX_DeviceName", IsUnique = true, Order = 1)]
    [Description("The device name.")]
    public string Di_DeviceName { get; set; }

    /// <summary>
    /// The device ip address.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Description("The device ip address.")]
    public string Di_DeviceIp { get; set; }

    /// <summary>
    /// The host ip address.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Description("The host ip address.")]
    public string Di_HostIp { get; set; }

    /// <summary>
    /// The host port number.
    /// </summary>
    [Required]
    [Description("The host port number.")]
    public int Di_HostPort { get; set; }

    /// <summary>
    /// The host login name.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Description("The host login name.")]
    public string Di_HostUserName { get; set; }

    /// <summary>
    /// The host password.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Description("The host password.")]
    public string Di_HostPassword { get; set; }

    /// <summary>
    /// The device scan interval time(seconds).
    /// </summary>
    [Required]
    [Description("The device scan interval time.")]
    public int Di_ScanInterval { get; set; }

    /// <summary>
    /// The device description.
    /// </summary>
    [MaxLength(500)]
    [Description("The device description.")]
    public string Di_Desc { get; set; }

    /// <summary>
    /// The device status.
    /// </summary>
    [Description("The device status.")]
    public DeviceStatus Di_Status { get; set; }

    /// <summary>
    /// The create date.
    /// </summary>
    [Required]
    [Description("The create date.")]
    public DateTime Di_CreateDate { get; set; }

    /// <summary>
    /// The last run time.
    /// </summary>
    [Description("The last run time.")]
    public DateTime? Di_LastRunTime { get; set; }
}