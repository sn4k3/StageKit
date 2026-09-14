namespace StageKit.Primitives.System;

/// <summary>
/// Represents a snapshot of the host's power and battery status.
/// </summary>
public readonly record struct HostPowerStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostPowerStatus"/> struct.
    /// </summary>
    /// <param name="hasBattery">Indicates whether a battery is present on the host.</param>
    /// <param name="isOnBatteryPower">Indicates whether the host is currently discharging its battery.</param>
    /// <param name="batteryChargePercentage">The battery charge level from 0 to 100, or <see langword="null"/> if unavailable.</param>
    /// <param name="estimatedBatteryLife">The estimated battery life remaining, or <see langword="null"/> if unavailable.</param>
    /// <param name="isBatteryCharging">Indicates whether the battery is actively charging.</param>
    public HostPowerStatus(
        bool hasBattery,
        bool isOnBatteryPower,
        int? batteryChargePercentage = null,
        TimeSpan? estimatedBatteryLife = null,
        bool isBatteryCharging = false)
    {
        HasBattery = hasBattery;
        IsOnBatteryPower = isOnBatteryPower;
        BatteryChargePercentage = batteryChargePercentage is >= 0 and <= 100 ? batteryChargePercentage : null;
        EstimatedBatteryLife = estimatedBatteryLife;
        IsBatteryCharging = isBatteryCharging;
    }

    /// <summary>
    /// Gets a value indicating whether a battery is present on the host.
    /// </summary>
    public bool HasBattery { get; }

    /// <summary>
    /// Gets a value indicating whether the host is currently discharging its battery (not running on AC power).
    /// </summary>
    public bool IsOnBatteryPower { get; }

    /// <summary>
    /// Gets the battery charge level as a percentage from 0 to 100, or <see langword="null"/> when unavailable or no battery is detected.
    /// </summary>
    public int? BatteryChargePercentage { get; }

    /// <summary>
    /// Gets the estimated battery runtime remaining, or <see langword="null"/> if unavailable or not operating on battery.
    /// </summary>
    public TimeSpan? EstimatedBatteryLife { get; }

    /// <summary>
    /// Gets a value indicating whether the battery is actively charging.
    /// </summary>
    public bool IsBatteryCharging { get; }
}
