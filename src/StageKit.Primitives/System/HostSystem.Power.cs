using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
    [Flags]
    private enum ExecutionState : uint
    {
        Continuous = 0x80000000,
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        UserPresent = 0x00000004,
        AwayModeRequired = 0x00000040
    }

    /// <summary>
    /// Prevents the operating system from entering sleep or idle power-saving modes while the returned token is alive.
    /// </summary>
    /// <param name="keepDisplayOn">
    /// <see langword="true"/> to also prevent the display from turning off; otherwise, <see langword="false"/> to allow the display to turn off while keeping the system awake.
    /// </param>
    /// <returns>An <see cref="IDisposable"/> token that restores the normal system power state when disposed.</returns>
    /// <remarks>
    /// On Windows, sets thread execution state flags (<c>ES_SYSTEM_REQUIRED</c> and optionally <c>ES_DISPLAY_REQUIRED</c>).
    /// On macOS, runs a background <c>caffeinate</c> process until disposed.
    /// On Linux, invokes <c>systemd-inhibit</c> if available.
    /// </remarks>
    public static IDisposable PreventSleep(bool keepDisplayOn = false)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
                if (keepDisplayOn)
                    flags |= ExecutionState.DisplayRequired;

                SetThreadExecutionState(flags);
                return new SleepPreventionToken();
            }
            catch
            {
                return new SleepPreventionToken();
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var args = keepDisplayOn ? "-d -i" : "-i";
                var startInfo = new ProcessStartInfo("/usr/bin/caffeinate", args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var process = Process.Start(startInfo);
                return new SleepPreventionToken(process);
            }
            catch
            {
                return new SleepPreventionToken();
            }
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                var what = keepDisplayOn ? "idle:sleep" : "sleep";
                var startInfo = new ProcessStartInfo("systemd-inhibit",
                    $"--what={what} --why=\"StageKit task in progress\" sleep infinity")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var process = Process.Start(startInfo);
                return new SleepPreventionToken(process);
            }
            catch
            {
                return new SleepPreventionToken();
            }
        }

        return new SleepPreventionToken();
    }

    /// <summary>
    /// Gets a value indicating whether the current host is operating on battery power.
    /// </summary>
    public static bool IsOnBatteryPower => GetPowerStatus().IsOnBatteryPower;

    /// <summary>
    /// Gets the battery charge percentage (0-100), or <see langword="null"/> if the host has no battery or charge cannot be determined.
    /// </summary>
    public static int? BatteryChargePercentage => GetPowerStatus().BatteryChargePercentage;

    /// <summary>
    /// Gets a snapshot of the current power and battery status for the host.
    /// </summary>
    /// <returns>A <see cref="HostPowerStatus"/> snapshot representing the current power state.</returns>
    public static HostPowerStatus GetPowerStatus()
    {
        TryGetPowerStatus(out var status);
        return status;
    }

    /// <summary>
    /// Tries to get a snapshot of the current power and battery status for the host.
    /// </summary>
    /// <param name="status">When this method returns, contains the current power status if determined; otherwise, default.</param>
    /// <returns><see langword="true"/> if power information was successfully queried; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetPowerStatus(out HostPowerStatus status)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return TryGetWindowsPowerStatus(out status);

            if (OperatingSystem.IsLinux())
                return TryGetLinuxPowerStatus(out status);

            if (OperatingSystem.IsMacOS())
                return TryGetMacPowerStatus(out status);
        }
        catch
        {
            // Best effort
        }

        status = default;
        return false;
    }

    private static bool TryGetWindowsPowerStatus(out HostPowerStatus status)
    {
        if (GetSystemPowerStatus(out var power))
        {
            var hasBattery = (power.BatteryFlag & 128) == 0 && power.BatteryFlag != 255;
            var isOnBattery = power.ACLineStatus == 0;
            var isCharging = (power.BatteryFlag & 8) != 0;
            int? chargePercent = power.BatteryLifePercent <= 100 ? (int)power.BatteryLifePercent : null;
            TimeSpan? estimatedLife = power.BatteryLifeTime > 0 ? TimeSpan.FromSeconds(power.BatteryLifeTime) : null;

            status = new HostPowerStatus(hasBattery, isOnBattery, chargePercent, estimatedLife, isCharging);
            return true;
        }

        status = default;
        return false;
    }

    private static bool TryGetLinuxPowerStatus(out HostPowerStatus status)
    {
        if (Directory.Exists("/sys/class/power_supply"))
        {
            var hasBattery = false;
            var isOnBattery = false;
            var isCharging = false;
            int? chargePercent = null;
            var acOnline = false;

            foreach (var dir in Directory.GetDirectories("/sys/class/power_supply"))
            {
                var typeFile = Path.Combine(dir, "type");
                if (!File.Exists(typeFile)) continue;
                var type = File.ReadAllText(typeFile).Trim();

                if (type.Equals("Battery", StringComparison.OrdinalIgnoreCase))
                {
                    hasBattery = true;
                    var statusFile = Path.Combine(dir, "status");
                    if (File.Exists(statusFile))
                    {
                        var battStatus = File.ReadAllText(statusFile).Trim();
                        if (battStatus.Equals("Discharging", StringComparison.OrdinalIgnoreCase))
                            isOnBattery = true;
                        else if (battStatus.Equals("Charging", StringComparison.OrdinalIgnoreCase))
                            isCharging = true;
                    }

                    var capFile = Path.Combine(dir, "capacity");
                    if (File.Exists(capFile) && int.TryParse(File.ReadAllText(capFile).Trim(), out var cap))
                    {
                        chargePercent = cap;
                    }
                }
                else if (type.Equals("Mains", StringComparison.OrdinalIgnoreCase))
                {
                    var onlineFile = Path.Combine(dir, "online");
                    if (File.Exists(onlineFile) && int.TryParse(File.ReadAllText(onlineFile).Trim(), out var online) && online == 1)
                    {
                        acOnline = true;
                    }
                }
            }

            if (hasBattery)
            {
                if (acOnline)
                    isOnBattery = false;

                status = new HostPowerStatus(hasBattery, isOnBattery, chargePercent, null, isCharging);
                return true;
            }

            if (acOnline)
            {
                status = new HostPowerStatus(false, false, null, null, false);
                return true;
            }
        }

        status = default;
        return false;
    }

    private static bool TryGetMacPowerStatus(out HostPowerStatus status)
    {
        var output = GetBoundedProcessOutput("/usr/bin/pmset", ["-g", "batt"]);
        if (!string.IsNullOrWhiteSpace(output))
        {
            var isOnBattery = output.Contains("'Battery Power'", StringComparison.OrdinalIgnoreCase);
            var hasBattery = output.Contains("InternalBattery", StringComparison.OrdinalIgnoreCase);
            var isCharging = output.Contains("charging", StringComparison.OrdinalIgnoreCase) &&
                             !output.Contains("discharging", StringComparison.OrdinalIgnoreCase);
            int? chargePercent = null;
            TimeSpan? estimatedLife = null;

            var percentIndex = output.IndexOf('%');
            if (percentIndex > 0)
            {
                var start = percentIndex - 1;
                while (start >= 0 && char.IsDigit(output[start]))
                    start--;
                if (int.TryParse(output.AsSpan(start + 1, percentIndex - start - 1), out var percent))
                    chargePercent = percent;
            }

            var remainingIndex = output.IndexOf("remaining", StringComparison.OrdinalIgnoreCase);
            if (remainingIndex > 0)
            {
                var timePart = output[..remainingIndex].TrimEnd(';', ' ');
                var colon = timePart.LastIndexOf(':');
                if (colon > 0)
                {
                    var hourStart = colon - 1;
                    while (hourStart >= 0 && char.IsDigit(timePart[hourStart]))
                        hourStart--;
                    var minuteEnd = colon + 1;
                    while (minuteEnd < timePart.Length && char.IsDigit(timePart[minuteEnd]))
                        minuteEnd++;
                    if (int.TryParse(timePart.AsSpan(hourStart + 1, colon - hourStart - 1), out var hours) &&
                        int.TryParse(timePart.AsSpan(colon + 1, minuteEnd - colon - 1), out var minutes))
                    {
                        estimatedLife = new TimeSpan(hours, minutes, 0);
                    }
                }
            }

            status = new HostPowerStatus(hasBattery, isOnBattery, chargePercent, estimatedLife, isCharging);
            return true;
        }

        status = default;
        return false;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    private sealed class SleepPreventionToken : DisposableObject
    {
        private readonly Process? _process;

        public SleepPreventionToken(Process? process = null)
        {
            _process = process;
        }

        protected override void DisposeManaged()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    SetThreadExecutionState(ExecutionState.Continuous);
                }
                catch
                {
                    // Best effort
                }
            }
            else if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                        _process.Kill();
                    _process.Dispose();
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }
}
