using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

/// <summary>
/// One job object per PS5Kitsune process, with kill-on-close: Windows closes the handle when PS5Kitsune ends, however it ends, and
/// ends every tool in the job. PS5Kitsune itself is not in the job, so the games it launches are not either.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsChildProcessJob : IChildProcessJob, IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessSetQuota = 0x0100;

    private readonly JobHandle _job;
    private readonly ILogger<WindowsChildProcessJob> _logger;

    public WindowsChildProcessJob(ILogger<WindowsChildProcessJob> logger)
    {
        _logger = logger;
        _job = CreateJobObject(0, null);
        if (_job.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var limits = new ExtendedLimitInformation { BasicLimitInformation = new BasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose } };
        if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
        {
            var error = Marshal.GetLastPInvokeError();
            _job.Dispose();
            throw new Win32Exception(error);
        }
    }

    public void Assign(int processId)
    {
        using var process = OpenProcess(ProcessSetQuota | ProcessTerminate, inheritHandle: false, processId);
        if (process.IsInvalid || !AssignProcessToJobObject(_job, process))
        {
            var error = Marshal.GetLastPInvokeError();
            LogAssignFailed(processId, error);
        }
    }

    public void Dispose() => _job.Dispose();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not tie process {ProcessId} to PS5Kitsune (error {Error}); it may have exited already.")]
    private partial void LogAssignFailed(int processId, int error);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial JobHandle CreateJobObject(nint jobAttributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(JobHandle job, int infoClass, ref ExtendedLimitInformation info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(JobHandle job, SafeProcessHandle process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    private sealed class JobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public JobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

#pragma warning disable CS0649 // Native layouts: Windows reads fields the C# code never touches.
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
#pragma warning restore CS0649
}
