﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Async.SchedulerJob;

/// <summary>
/// Represents an entity of the spectro daily list in laboratory device entry.
/// </summary>
[Table("SpectroDaily")]
[Description("Stores the spectro daily list in laboratory device.")]
public class SpectroDailyEntity
{
    /// <summary>
    /// The ID.
    /// </summary>
    [Key]
    [Description("The ID.")]
    public Guid Sd_Id { get; set; }

    /// <summary>
    /// The device Id.
    /// </summary>
    [Required]
    [Index("IX_DateTime", IsUnique = false, Order = 0)]
    [Description("The device Id.")]
    public Guid Sd_DeviceId { get; set; }

    /// <summary>
    /// The business date time.
    /// </summary>
    [Required]
    [Index("IX_DateTime", IsUnique = false, Order = 1)]
    [Description("The business date time.")]
    public DateTime Sd_BizDateTime { get; set; }

    /// <summary>
    /// The mode.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Description("The mode.")]
    public string Sd_Mode { get; set; }

    /// <summary>
    /// The item.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Description("The item.")]
    public string Sd_Item { get; set; }

    /// <summary>
    /// The url.
    /// </summary>
    [NotMapped]
    [Description("The url.")]
    public string Sd_Url { get; set; }

    /// <summary>
    /// The create date.
    /// </summary>
    [Required]
    [Description("The create date.")]
    public DateTime Sd_CreateDate { get; set; }
}