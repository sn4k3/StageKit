using System.Globalization;
using System.Runtime.InteropServices;

namespace StageKit.Primitives.System;

public static partial class HostSystem
{
    private const string MacSystemLibrary = "/usr/lib/libSystem.B.dylib";

    /// <summary>
    /// Gets a fresh physical-memory snapshot for the current host.
    /// </summary>
    /// <returns>
    /// A snapshot for Windows, Linux, or macOS; otherwise, an empty snapshot if memory information is unavailable.
    /// </returns>
    /// <remarks>
    /// Use <see cref="TryGetMemoryStatus"/> to distinguish a host with no available memory from a failed query.
    /// Results are not cached and describe memory visible to the operating system, not a process or container limit.
    /// </remarks>
    public static HostMemoryStatus GetMemoryStatus()
    {
        TryGetMemoryStatus(out var status);
        return status;
    }

    /// <summary>
    /// Tries to get a fresh physical-memory snapshot for the current host.
    /// </summary>
    /// <param name="status">The snapshot, or an empty snapshot when the query fails.</param>
    /// <returns><see langword="true"/> if memory information was read; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Linux uses <c>MemAvailable</c>, falling back to <c>MemFree</c> on older kernels. macOS estimates available
    /// memory as free plus inactive pages. These operating-system estimates do not guarantee that an allocation will
    /// succeed.
    /// </remarks>
    public static bool TryGetMemoryStatus(out HostMemoryStatus status)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return TryGetWindowsMemoryStatus(out status);

            if (OperatingSystem.IsLinux())
                return TryParseLinuxMemoryStatus(File.ReadAllText("/proc/meminfo"), out status);

            if (OperatingSystem.IsMacOS())
                return TryGetMacMemoryStatus(out status);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or DllNotFoundException or EntryPointNotFoundException)
        {
            // Unavailable operating-system information is reported through the Try return value.
        }

        status = default;
        return false;
    }

    internal static bool TryParseLinuxMemoryStatus(ReadOnlySpan<char> text, out HostMemoryStatus status)
    {
        ulong total = 0;
        ulong? available = null;
        ulong? free = null;

        while (!text.IsEmpty)
        {
            var newline = text.IndexOf('\n');
            var line = newline < 0 ? text : text[..newline];
            text = newline < 0 ? [] : text[(newline + 1)..];

            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;

            var key = line[..colon];
            if (key is not ("MemTotal" or "MemAvailable" or "MemFree"))
                continue;

            var valueText = line[(colon + 1)..].Trim();
            if (!valueText.EndsWith("kB", StringComparison.Ordinal)
                || !ulong.TryParse(
                    valueText[..^2].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value)
                || value > ulong.MaxValue / 1024)
            {
                status = default;
                return false;
            }

            value *= 1024;
            switch (key)
            {
                case "MemTotal":
                    total = value;
                    break;
                case "MemAvailable":
                    available = value;
                    break;
                case "MemFree":
                    free = value;
                    break;
            }
        }

        if (total == 0 || (available is null && free is null))
        {
            status = default;
            return false;
        }

        status = new HostMemoryStatus(total, available ?? free ?? 0);
        return true;
    }

    private static bool TryGetWindowsMemoryStatus(out HostMemoryStatus status)
    {
        var nativeStatus = new WindowsMemoryStatus
        {
            Length = (uint)Marshal.SizeOf<WindowsMemoryStatus>()
        };

        if (!GlobalMemoryStatusEx(ref nativeStatus) || nativeStatus.TotalPhysicalBytes == 0)
        {
            status = default;
            return false;
        }

        status = new HostMemoryStatus(nativeStatus.TotalPhysicalBytes, nativeStatus.AvailablePhysicalBytes);
        return true;
    }

    private static bool TryGetMacMemoryStatus(out HostMemoryStatus status)
    {
        status = default;
        nuint length = sizeof(ulong);
        if (SysctlByName("hw.memsize", out var total, ref length, 0, 0) != 0
            || length != sizeof(ulong)
            || total == 0
            || !MacNative.TryGetTaskSelf(out var task))
        {
            return false;
        }

        var host = MachHostSelf();
        if (host == 0)
            return false;

        try
        {
            // vm_statistics64 rev2 is 160 bytes; Mach counts 32-bit integers, not bytes.
            uint count = 40;
            if (HostPageSize(host, out var pageSize) != 0
                || pageSize == 0
                || HostStatistics64(host, 4 /* HOST_VM_INFO64 */, out var statistics, ref count) != 0
                || count < 4)
            {
                return false;
            }

            // free_count already includes speculative pages; purgeable pages can overlap other queues.
            var available = ((ulong)statistics.FreeCount + statistics.InactiveCount) * pageSize;
            status = new HostMemoryStatus(total, available);
            return true;
        }
        finally
        {
            _ = MachPortDeallocate(task, host);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref WindowsMemoryStatus buffer);

    [LibraryImport(MacSystemLibrary, EntryPoint = "sysctlbyname", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SysctlByName(
        string name,
        out ulong value,
        ref nuint length,
        nint newValue,
        nuint newLength);

    [LibraryImport(MacSystemLibrary, EntryPoint = "mach_host_self")]
    private static partial uint MachHostSelf();

    [LibraryImport(MacSystemLibrary, EntryPoint = "mach_port_deallocate")]
    private static partial int MachPortDeallocate(uint task, uint name);

    [LibraryImport(MacSystemLibrary, EntryPoint = "host_page_size")]
    private static partial int HostPageSize(uint host, out nuint pageSize);

    [LibraryImport(MacSystemLibrary, EntryPoint = "host_statistics64")]
    private static partial int HostStatistics64(
        uint host,
        int flavor,
        out MacVmStatistics statistics,
        ref uint count);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsMemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    // Only the page counts used above are projected; reserve the full native output buffer.
    [StructLayout(LayoutKind.Explicit, Size = 160)]
    private struct MacVmStatistics
    {
        [FieldOffset(0)] public uint FreeCount;
        [FieldOffset(8)] public uint InactiveCount;
    }

    private static class MacNative
    {
        private static readonly uint TaskSelf = GetTaskSelf();

        public static bool TryGetTaskSelf(out uint task)
        {
            task = TaskSelf;
            return task != 0;
        }

        private static uint GetTaskSelf()
        {
            if (!NativeLibrary.TryLoad(MacSystemLibrary, out var library))
                return 0;

            try
            {
                return NativeLibrary.TryGetExport(library, "mach_task_self_", out var symbol)
                    ? unchecked((uint)Marshal.ReadInt32(symbol))
                    : 0;
            }
            finally
            {
                NativeLibrary.Free(library);
            }
        }
    }
}