using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

/// <summary>
/// Starts the game with CreateProcess, its stdout and stderr being an inherited handle to the log file:
/// no pipe slows a verbose game down, and closing the launcher neither blocks nor kills it (no job object).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsGameLauncher(ILogger<WindowsGameLauncher> logger) : IGameLauncher
{
    private const int KeptLogs = 10;
    private const uint CreateNoWindow = 0x08000000;
    private const int StartfUseStdHandles = 0x00000100;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const nint ProcThreadAttributeHandleList = 0x00020002;

    public ILaunchedGame Launch(GameLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(request.LogDirectory);
        DeleteOldLogs(request.LogDirectory);
        var logFile = Path.Combine(request.LogDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}.log");

        // A handle list entry must be inheritable; the handle is closed as soon as the game has started.
        using var log = File.OpenHandle(logFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        if (!SetHandleInformation(log, HandleFlagInherit, HandleFlagInherit))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var commandLine = WindowsCommandLine.Build(request.Executable, request.Arguments);
        var process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle());

        CloseHandle(process.Thread);
        LogLaunched(request.Executable, process.ProcessId, logFile);
        return new LaunchedGame(new SafeProcessHandle(process.Process, ownsHandle: true), process.ProcessId, logFile);
    }

    // bInheritHandles = TRUE alone would hand the game every inheritable handle of this process (a parent's
    // output pipe, a concurrent child's redirect pipe), keeping them open as long as the game runs.
    // The handle list attribute restricts inheritance to the log.
    private static unsafe ProcessInformation Start(string executable, string commandLine, string workingDirectory, nint log)
    {
        nuint size = 0;
        InitializeProcThreadAttributeList(0, 1, 0, ref size); // Size query: fails with ERROR_INSUFFICIENT_BUFFER.
        var attributes = (nint)NativeMemory.Alloc(size);
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                var inherited = log; // Read by CreateProcess, so it lives on this frame until then.
                if (!UpdateProcThreadAttribute(attributes, 0, ProcThreadAttributeHandleList, (nint)(&inherited), (nuint)sizeof(nint), 0, 0))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                var startupInfo = new StartupInfoEx
                {
                    StartupInfo = new StartupInfo
                    {
                        Size = sizeof(StartupInfoEx),
                        Flags = StartfUseStdHandles,
                        StdOutput = log,
                        StdError = log,
                    },
                    AttributeList = attributes,
                };
                var commandLineBuffer = (commandLine + '\0').ToCharArray();
                if (!CreateProcess(executable, ref commandLineBuffer[0], 0, 0, inheritHandles: true, CreateNoWindow | ExtendedStartupInfoPresent, 0, workingDirectory, ref startupInfo, out var process))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not start {executable}.");
                }

                return process;
            }
            finally
            {
                DeleteProcThreadAttributeList(attributes);
            }
        }
        finally
        {
            NativeMemory.Free((void*)attributes);
        }
    }

    // Makes room for the new log by keeping the newest KeptLogs - 1. Names sort by date.
    private static void DeleteOldLogs(string directory)
    {
        foreach (var old in Directory.GetFiles(directory, "*.log").OrderDescending(StringComparer.Ordinal).Skip(KeptLogs - 1))
        {
            File.Delete(old);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} (PID {ProcessId}), output in {LogFile}")]
    private partial void LogLaunched(string executable, int processId, string logFile);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcess(
        string applicationName,
        ref char commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        nint environment,
        string currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeProcThreadAttributeList(nint attributeList, int attributeCount, int flags, ref nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateProcThreadAttribute(nint attributeList, uint flags, nint attribute, nint value, nuint size, nint previousValue, nint returnSize);

    [LibraryImport("kernel32.dll")]
    private static partial void DeleteProcThreadAttributeList(nint attributeList);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

#pragma warning disable CS0649 // Native layouts: Windows fills or reads fields the C# code never touches.
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public nint Reserved2;
        public nint StdInput;
        public nint StdOutput;
        public nint StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public int ProcessId;
        public int ThreadId;
    }
#pragma warning restore CS0649

    private sealed class LaunchedGame(SafeProcessHandle process, int processId, string logFile) : ILaunchedGame
    {
        public int ProcessId { get; } = processId;

        public string LogFile { get; } = logFile;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            using var waitHandle = new ProcessWaitHandle(process);
            var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var registration = ThreadPool.RegisterWaitForSingleObject(waitHandle, (_, _) => exited.TrySetResult(), null, Timeout.Infinite, executeOnlyOnce: true);
            try
            {
                await exited.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                registration.Unregister(null);
            }

            if (!GetExitCodeProcess(process, out var exitCode))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return unchecked((int)exitCode);
        }

        public void Dispose() => process.Dispose();
    }

    // Lets the thread pool wait on the process handle without taking ownership of it.
    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process)
        {
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
