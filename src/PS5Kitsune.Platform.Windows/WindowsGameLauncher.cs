using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

/// <summary>
/// Starts the game with CreateProcess, its stdout and stderr being an inherited handle to the log file:
/// no pipe slows a verbose game down, and closing the launcher neither blocks nor kills it: the game is started out of the launcher's job when the job allows it (see docs/architecture.md, "Launching").
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
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint Synchronize = 0x00100000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const int ErrorAccessDenied = 5;
    private const uint WaitObject0 = 0;

    /// <summary>How long Stop waits for the game to close its windows before ending it.</summary>
    internal TimeSpan StopGrace { get; init; } = TimeSpan.FromSeconds(5);

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
        var environment = request.Environment.Count == 0 ? null : EnvironmentBlock(request.Environment);
        ProcessInformation process;
        try
        {
            process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle(), environment, breakaway: true);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorAccessDenied)
        {
            // The job PS5Kitsune runs in (a terminal, another launcher) forbids breakaway: the game then stays in that job.
            var executable = request.Executable;
            LogBreakawayRefused(executable);
            process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle(), environment, breakaway: false);
        }

        CloseHandle(process.Thread);
        var handle = new SafeProcessHandle(process.Process, ownsHandle: true);
        DateTimeOffset startedAt;
        try
        {
            startedAt = CreationTime(handle);
        }
        catch
        {
            handle.Dispose();
            throw;
        }

        LogLaunched(request.Executable, process.ProcessId, logFile);
        return new LaunchedGame(handle, process.ProcessId, logFile, startedAt, StopGrace);
    }

    public ILaunchedGame? Attach(RunningGame record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var process = OpenProcess(ProcessQueryLimitedInformation | Synchronize | ProcessTerminate, inheritHandle: false, record.ProcessId);
        // Not opened: the process is gone, or belongs to someone else (another user, an elevated process): not our game either way.
        bool ours;
        try
        {
            ours = !process.IsInvalid
                && !HasExited(process)
                && CreationTime(process) == record.StartedAt
                && ImagePath(process) is { } path
                && string.Equals(Path.GetFullPath(path), Path.GetFullPath(record.Executable), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is Win32Exception or ArgumentException or NotSupportedException or PathTooLongException)
        {
            ours = false;
        }

        if (!ours)
        {
            process.Dispose();
            return null;
        }

        LogAttached(record.Executable, record.ProcessId);
        return new LaunchedGame(process, record.ProcessId, record.LogFile, record.StartedAt, StopGrace);
    }

    private static DateTimeOffset CreationTime(SafeProcessHandle process) =>
        GetProcessTimes(process, out var creation, out _, out _, out _)
            ? DateTimeOffset.FromFileTime(creation).ToUniversalTime()
            : throw new Win32Exception(Marshal.GetLastPInvokeError());

    private static string? ImagePath(SafeProcessHandle process)
    {
        var buffer = new char[32768];
        var size = buffer.Length;
        return QueryFullProcessImageName(process, 0, ref buffer[0], ref size) ? new string(buffer, 0, size) : null;
    }

    // bInheritHandles = TRUE alone would hand the game every inheritable handle of this process (a parent's
    // output pipe, a concurrent child's redirect pipe), keeping them open as long as the game runs.
    // The handle list attribute restricts inheritance to the log.
    private static unsafe ProcessInformation Start(string executable, string commandLine, string workingDirectory, nint log, string? environment, bool breakaway)
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
                var flags = CreateNoWindow | ExtendedStartupInfoPresent
                    | (breakaway ? CreateBreakawayFromJob : 0)
                    | (environment is null ? 0 : CreateUnicodeEnvironment);
                var commandLineBuffer = (commandLine + '\0').ToCharArray();
                fixed (char* block = environment)
                {
                    if (!CreateProcess(executable, ref commandLineBuffer[0], 0, 0, inheritHandles: true, flags, (nint)block, workingDirectory, ref startupInfo, out var process))
                    {
                        throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not start {executable}.");
                    }

                    return process;
                }
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

    // An exit code of 259 (STILL_ACTIVE) is also a real exit code: only the handle tells whether the process ended.
    private static bool HasExited(SafeProcessHandle process) => WaitForSingleObject(process, 0) == WaitObject0;

    // The launcher's environment with the game's variables on top, sorted by name as CreateProcess expects, double null-terminated.
    private static string EnvironmentBlock(IReadOnlyDictionary<string, string> overrides)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            variables[(string)entry.Key] = (string?)entry.Value ?? "";
        }

        foreach (var (name, value) in overrides)
        {
            variables[name] = value;
        }

        var block = new System.Text.StringBuilder();
        foreach (var (name, value) in variables)
        {
            block.Append(name).Append('=').Append(value).Append(' ');
        }

        return block.Append(' ').ToString();
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Executable} still running (PID {ProcessId})")]
    private partial void LogAttached(string executable, int processId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} inside PS5Kitsune's parent job: that job does not allow breakaway, so closing it also ends the game.")]
    private partial void LogBreakawayRefused(string executable);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

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
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle process, out long creationTime, out long exitTime, out long kernelTime, out long userTime);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, ref char name, ref int size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);

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

    private sealed class LaunchedGame(SafeProcessHandle process, int processId, string logFile, DateTimeOffset startedAt, TimeSpan stopGrace) : ILaunchedGame
    {
        public int ProcessId { get; } = processId;

        public string LogFile { get; } = logFile;

        public DateTimeOffset StartedAt { get; } = startedAt;

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            CloseWindows();
            var exit = WaitForExitAsync(cancellationToken);
            var finished = await Task.WhenAny(exit, Task.Delay(stopGrace, cancellationToken)).ConfigureAwait(false) == exit;

            // A cancelled delay also completes WhenAny: cancelling only abandons the wait, it never ends the game.
            cancellationToken.ThrowIfCancellationRequested();
            if (!finished && !TerminateProcess(process, 1))
            {
                var error = Marshal.GetLastPInvokeError();
                // Access denied once the process has already exited: nothing left to stop.
                if (!exit.IsCompleted && !HasExited(process))
                {
                    throw new Win32Exception(error);
                }
            }

            await exit.ConfigureAwait(false);
        }

        // The handle held here keeps the PID from being reused, so the process found by ID is the game.
        private void CloseWindows()
        {
            try
            {
                using var running = System.Diagnostics.Process.GetProcessById(ProcessId);
                running.CloseMainWindow();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                // Already exited.
            }
        }

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
