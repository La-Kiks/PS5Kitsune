using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PS5Kitsune.Platform.Linux;

/// <summary>The libc calls the game launcher needs. posix_spawn and its helpers return an error number instead of setting errno.</summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class PosixSpawn
{
    private const int ReadOnly = 0;          // O_RDONLY
    private const int LogFlags = 0x641;      // O_WRONLY | O_CREAT | O_TRUNC | O_APPEND
    private const int LogMode = 0x1A4;       // 0644
    private const short SetSid = 0x80;       // POSIX_SPAWN_SETSID
    private const short SetSigDefault = 0x04; // POSIX_SPAWN_SETSIGDEF
    private const short SetSigMask = 0x08;   // POSIX_SPAWN_SETSIGMASK
    private const int StructSize = 512;      // glibc: posix_spawn_file_actions_t is 80 bytes, posix_spawnattr_t 336
    private const int SigSetSize = 128;      // glibc: sigset_t
    private const int SigPipe = 13;

    public const int SigKill = 9;
    public const int SigTerm = 15;
    private const int Interrupted = 4;       // EINTR
    public const int NoSuchProcess = 3;      // ESRCH
    private const int IdTypePid = 1;            // P_PID
    private const int WaitExited = 4;           // WEXITED
    private const int WaitNoWait = 0x01000000;  // WNOWAIT
    private const int SigInfoSize = 128;        // sizeof(siginfo_t)

    /// <summary>
    /// Starts <paramref name="executable"/> in a new session, stdin on /dev/null, stdout and stderr on <paramref name="logFile"/>,
    /// SIGPIPE back to its default action and no signal blocked: an ignored signal stays ignored across exec, and .NET ignores SIGPIPE
    /// (Process.Start resets them the same way).
    /// </summary>
    /// <returns>The PID.</returns>
    /// <exception cref="Win32Exception">posix_spawn failed, for example ENOENT or EACCES.</exception>
    public static int Start(string executable, IReadOnlyList<string> arguments, string workingDirectory, string logFile, IEnumerable<string> environment)
    {
        var actions = (nint)NativeMemory.AllocZeroed(StructSize);
        var attributes = (nint)NativeMemory.AllocZeroed(StructSize);
        var defaultSignals = (nint)NativeMemory.AllocZeroed(SigSetSize);
        var mask = (nint)NativeMemory.AllocZeroed(SigSetSize);
        var owned = new List<nint>();
        try
        {
            Check(FileActionsInit(actions));
            try
            {
                Check(AddOpen(actions, 0, "/dev/null", ReadOnly, 0));
                Check(AddOpen(actions, 1, logFile, LogFlags, LogMode));
                Check(AddDup2(actions, 1, 2));
                Check(AddChdir(actions, workingDirectory));
                Check(AttributesInit(attributes));
                try
                {
                    CheckErrno(SigEmptySet(defaultSignals));
                    CheckErrno(SigAddSet(defaultSignals, SigPipe));
                    CheckErrno(SigEmptySet(mask));
                    Check(SetSigDefaultSet(attributes, defaultSignals));
                    Check(SetSigMaskSet(attributes, mask));
                    Check(SetFlags(attributes, SetSid | SetSigDefault | SetSigMask));
                    var argv = Block(owned, [executable, .. arguments]);
                    var envp = Block(owned, [.. environment]);
                    fixed (nint* argvPointer = argv)
                    fixed (nint* envpPointer = envp)
                    {
                        Check(Spawn(out var pid, executable, actions, attributes, argvPointer, envpPointer));
                        return pid;
                    }
                }
                finally
                {
                    _ = AttributesDestroy(attributes); // Only fails on an invalid object.
                }
            }
            finally
            {
                _ = FileActionsDestroy(actions); // Only fails on an invalid object.
            }
        }
        finally
        {
            foreach (var pointer in owned)
            {
                Marshal.FreeCoTaskMem(pointer);
            }

            NativeMemory.Free((void*)actions);
            NativeMemory.Free((void*)attributes);
            NativeMemory.Free((void*)defaultSignals);
            NativeMemory.Free((void*)mask);
        }
    }

    /// <summary>Waits for a child; returns its exit code, 128 + the signal that ended it, or -1 when it cannot be waited for.</summary>
    public static int WaitForExit(int pid)
    {
        int status;
        int result;
        while ((result = WaitPid(pid, out status, 0)) == -1 && Marshal.GetLastPInvokeError() == Interrupted)
        {
        }

        if (result != pid)
        {
            return -1;
        }

        var signal = status & 0x7F;
        return signal == 0 ? (status >> 8) & 0xFF : 128 + signal;
    }

    /// <summary>
    /// Waits until the child exits without reaping it: as a zombie it keeps its PID, and so its process group ID, from being reused,
    /// so the group can still be signalled safely. False when waitid fails (the caller then reaps as before).
    /// </summary>
    public static bool WaitForExitWithoutReaping(int pid)
    {
        var info = (nint)NativeMemory.AllocZeroed(SigInfoSize);
        try
        {
            int result;
            while ((result = WaitId(IdTypePid, pid, info, WaitExited | WaitNoWait)) == -1 && Marshal.GetLastPInvokeError() == Interrupted)
            {
            }

            return result == 0;
        }
        finally
        {
            NativeMemory.Free((void*)info);
        }
    }

    /// <summary>Sends <paramref name="signal"/> to the process group <paramref name="group"/>; false when no process is left in it.</summary>
    /// <exception cref="Win32Exception">kill failed for another reason.</exception>
    public static bool SignalGroup(int group, int signal)
    {
        if (Kill(-group, signal) == 0)
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        return error == NoSuchProcess ? false : throw new Win32Exception(error);
    }

    // A NULL-terminated array of UTF-8 strings; the strings are added to owned, which the caller frees.
    private static nint[] Block(List<nint> owned, IReadOnlyList<string> values)
    {
        var block = new nint[values.Count + 1];
        for (var index = 0; index < values.Count; index++)
        {
            block[index] = Marshal.StringToCoTaskMemUTF8(values[index]);
            owned.Add(block[index]);
        }

        return block;
    }

    /// <summary>The absolute path with every symbolic link, "." and ".." resolved, as /proc/&lt;pid&gt;/exe shows it; null when it cannot be resolved (for example, it does not exist).</summary>
    public static string? CanonicalPath(string path)
    {
        var resolved = RealPath(path, 0);
        if (resolved == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(resolved);
        }
        finally
        {
            Free(resolved);
        }
    }

    // For the calls that return -1 and set errno.
    private static void CheckErrno(int result)
    {
        if (result != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private static void Check(int error)
    {
        if (error != 0)
        {
            throw new Win32Exception(error);
        }
    }

    [LibraryImport("libc", EntryPoint = "posix_spawn", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Spawn(out int pid, string path, nint fileActions, nint attributes, nint* argv, nint* envp);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
    private static partial int FileActionsInit(nint fileActions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
    private static partial int FileActionsDestroy(nint fileActions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int AddOpen(nint fileActions, int descriptor, string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
    private static partial int AddDup2(nint fileActions, int descriptor, int newDescriptor);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addchdir_np", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int AddChdir(nint fileActions, string path);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_init")]
    private static partial int AttributesInit(nint attributes);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_destroy")]
    private static partial int AttributesDestroy(nint attributes);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_setflags")]
    private static partial int SetFlags(nint attributes, short flags);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_setsigdefault")]
    private static partial int SetSigDefaultSet(nint attributes, nint signals);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_setsigmask")]
    private static partial int SetSigMaskSet(nint attributes, nint signals);

    [LibraryImport("libc", EntryPoint = "sigemptyset", SetLastError = true)]
    private static partial int SigEmptySet(nint signals);

    [LibraryImport("libc", EntryPoint = "sigaddset", SetLastError = true)]
    private static partial int SigAddSet(nint signals, int signal);

    [LibraryImport("libc", EntryPoint = "realpath", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint RealPath(string path, nint resolved);

    [LibraryImport("libc", EntryPoint = "free")]
    private static partial void Free(nint pointer);

    [LibraryImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    private static partial int WaitPid(int pid, out int status, int options);

    [LibraryImport("libc", EntryPoint = "waitid", SetLastError = true)]
    private static partial int WaitId(int idType, int id, nint info, int options);

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);
}
