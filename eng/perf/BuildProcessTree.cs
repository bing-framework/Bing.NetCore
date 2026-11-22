using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Bing.Sql.Performance
{
    /// <summary>
    /// 在 Windows Job Object 中运行并统计一个进程及其后代进程。
    /// </summary>
    /// <remarks>
    /// 此类型供性能脚本通过 PowerShell Add-Type 加载，不属于 Bing.Data.Sql 库公共 API。
    /// </remarks>
    public static class BuildProcessTree
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;

        private const uint StartfUseStdHandles = 0x00000100;

        private const uint CreateSuspended = 0x00000004;

        private const uint CreateNoWindow = 0x08000000;

        private const uint ExtendedStartupInfoPresent = 0x00080000;

        private static readonly IntPtr ProcThreadAttributeHandleList = new IntPtr(0x00020002);

        private const int JobObjectBasicAccountingInformationClass = 1;

        private const int JobObjectExtendedLimitInformationClass = 9;

        private const uint TimeoutExitCode = 124;

        private const int CleanupWaitMilliseconds = 3000;

        /// <summary>
        /// 获取或设置当前进程树的测量结果。
        /// </summary>
        public sealed class Result
        {
            internal Result(int exitCode, bool timedOut, uint totalProcesses,
                ulong peakJobCommittedBytes, ulong peakProcessCommittedBytes,
                long userTimeTicks, long kernelTimeTicks, long elapsedMilliseconds)
            {
                ExitCode = exitCode;
                TimedOut = timedOut;
                TotalProcesses = totalProcesses;
                PeakJobCommittedBytes = peakJobCommittedBytes;
                PeakProcessCommittedBytes = peakProcessCommittedBytes;
                UserTimeTicks = userTimeTicks;
                KernelTimeTicks = kernelTimeTicks;
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            /// <summary>
            /// 获取根进程的退出码。
            /// </summary>
            public int ExitCode { get; private set; }

            /// <summary>
            /// 获取进程树是否超过指定时限。
            /// </summary>
            public bool TimedOut { get; private set; }

            /// <summary>
            /// 获取加入 Job Object 的进程总数，包含已退出的短命子进程。
            /// </summary>
            public uint TotalProcesses { get; private set; }

            /// <summary>
            /// 获取 Job Object 全部成员同时使用的提交内存峰值，单位为字节。
            /// </summary>
            public ulong PeakJobCommittedBytes { get; private set; }

            /// <summary>
            /// 获取 Job Object 内单个进程的提交内存峰值，单位为字节。
            /// </summary>
            public ulong PeakProcessCommittedBytes { get; private set; }

            /// <summary>
            /// 获取 Job Object 累计用户态 CPU 时间，单位为 100 纳秒。
            /// </summary>
            public long UserTimeTicks { get; private set; }

            /// <summary>
            /// 获取 Job Object 累计内核态 CPU 时间，单位为 100 纳秒。
            /// </summary>
            public long KernelTimeTicks { get; private set; }

            /// <summary>
            /// 获取从启动测量到进程树结束的时长，单位为毫秒。
            /// </summary>
            public long ElapsedMilliseconds { get; private set; }
        }

        /// <summary>
        /// 在 Job Object 中启动可执行文件并等待整个进程树结束。
        /// </summary>
        /// <param name="executable">可执行文件路径。</param>
        /// <param name="arguments">按 Windows 命令行规则序列化的独立参数。</param>
        /// <param name="workingDirectory">子进程工作目录。</param>
        /// <param name="logFile">接收标准输出与标准错误的日志文件路径。</param>
        /// <param name="timeoutMilliseconds">等待整个进程树的时限，必须大于零。</param>
        /// <returns>包含退出码、进程数、CPU 时间、提交内存峰值和耗时的结果。</returns>
        /// <exception cref="ArgumentException">可执行文件、工作目录或日志路径无效，或参数数组含空引用时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">超时时限不大于零时抛出。</exception>
        /// <exception cref="Win32Exception">Windows 进程或 Job Object API 调用失败时抛出原始系统错误。</exception>
        public static Result Run(string executable, string[] arguments, string workingDirectory,
            string logFile, int timeoutMilliseconds)
        {
            ValidateArguments(executable, arguments, workingDirectory, logFile, timeoutMilliseconds);

            var stopwatch = Stopwatch.StartNew();
            var fullWorkingDirectory = Path.GetFullPath(workingDirectory);
            var fullLogPath = Path.GetFullPath(Path.IsPathRooted(logFile)
                ? logFile
                : Path.Combine(fullWorkingDirectory, logFile));

            IntPtr outputHandle = IntPtr.Zero;
            IntPtr inputHandle = IntPtr.Zero;
            IntPtr jobHandle = IntPtr.Zero;
            IntPtr processHandle = IntPtr.Zero;
            IntPtr threadHandle = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero;
            IntPtr inheritedHandles = IntPtr.Zero;
            IntPtr startupInfoPointer = IntPtr.Zero;
            var processAssignedToJob = false;
            var completed = false;

            try
            {
                var securityAttributes = CreateInheritableSecurityAttributes();
                outputHandle = CreateFileHandle(fullLogPath, GenericWrite, CreateAlways, securityAttributes);
                inputHandle = CreateFileHandle("NUL", GenericRead, OpenExisting, securityAttributes);

                jobHandle = CreateJobObjectW(IntPtr.Zero, null);
                if (jobHandle == IntPtr.Zero)
                    ThrowLastWin32Error();
                SetKillOnCloseLimit(jobHandle);

                attributeList = CreateHandleAttributeList(outputHandle, inputHandle, out inheritedHandles);
                var startupInfo = new StartupInfoEx();
                startupInfo.StartupInfo.Cb = (uint)Marshal.SizeOf(typeof(StartupInfoEx));
                startupInfo.StartupInfo.Flags = StartfUseStdHandles;
                startupInfo.StartupInfo.StdInput = inputHandle;
                startupInfo.StartupInfo.StdOutput = outputHandle;
                startupInfo.StartupInfo.StdError = outputHandle;
                startupInfo.AttributeList = attributeList;

                startupInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(StartupInfoEx)));
                Marshal.StructureToPtr(startupInfo, startupInfoPointer, false);

                var commandLine = new StringBuilder(BuildCommandLine(executable, arguments));
                ProcessInformation processInformation;
                var created = CreateProcessW(executable, commandLine, IntPtr.Zero, IntPtr.Zero,
                    true, CreateSuspended | CreateNoWindow | ExtendedStartupInfoPresent,
                    IntPtr.Zero, fullWorkingDirectory, startupInfoPointer, out processInformation);
                if (!created)
                    ThrowLastWin32Error();

                processHandle = processInformation.ProcessHandle;
                threadHandle = processInformation.ThreadHandle;

                if (!AssignProcessToJobObject(jobHandle, processHandle))
                    ThrowLastWin32Error();
                processAssignedToJob = true;

                if (ResumeThread(threadHandle) == UInt32.MaxValue)
                    ThrowLastWin32Error();

                var timedOut = WaitForJob(jobHandle, stopwatch, timeoutMilliseconds);
                if (timedOut)
                {
                    if (!TerminateJobObject(jobHandle, TimeoutExitCode))
                        ThrowLastWin32Error();
                    if (!WaitForJobToEmpty(jobHandle, CleanupWaitMilliseconds))
                        throw new TimeoutException("The process tree did not exit after Job Object termination.");
                }

                var processExitCode = GetProcessExitCode(processHandle);
                var accounting = QueryJobInformation<JobObjectBasicAccountingInformation>(
                    jobHandle, JobObjectBasicAccountingInformationClass);
                var limits = QueryJobInformation<JobObjectExtendedLimitInformation>(
                    jobHandle, JobObjectExtendedLimitInformationClass);
                completed = true;

                return new Result(processExitCode, timedOut, accounting.TotalProcesses,
                    limits.PeakJobMemoryUsed.ToUInt64(), limits.PeakProcessMemoryUsed.ToUInt64(),
                    accounting.TotalUserTime, accounting.TotalKernelTime, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                if (jobHandle != IntPtr.Zero && processAssignedToJob && !completed)
                    TryTerminateAndDrainJob(jobHandle);
                else if (processHandle != IntPtr.Zero && !processAssignedToJob)
                    TryTerminateProcessAndWait(processHandle);

                CloseHandleIfValid(threadHandle);
                CloseHandleIfValid(processHandle);
                CloseHandleIfValid(jobHandle);
                CloseHandleIfValid(inputHandle);
                CloseHandleIfValid(outputHandle);

                if (attributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }
                if (inheritedHandles != IntPtr.Zero)
                    Marshal.FreeHGlobal(inheritedHandles);
                if (startupInfoPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(startupInfoPointer);
            }
        }

        private static void ValidateArguments(string executable, string[] arguments, string workingDirectory,
            string logFile, int timeoutMilliseconds)
        {
            if (String.IsNullOrWhiteSpace(executable))
                throw new ArgumentException("Executable path is required.", "executable");
            if (arguments == null)
                throw new ArgumentNullException("arguments");
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] == null)
                    throw new ArgumentException("Arguments cannot contain null values.", "arguments");
            }
            if (String.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("Working directory is required.", "workingDirectory");
            if (!Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException("Working directory does not exist.");
            if (String.IsNullOrWhiteSpace(logFile))
                throw new ArgumentException("Log file path is required.", "logFile");
            if (timeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
        }

        private static string BuildCommandLine(string executable, string[] arguments)
        {
            var commandLine = new StringBuilder(QuoteArgument(executable));
            for (var index = 0; index < arguments.Length; index++)
            {
                commandLine.Append(' ');
                commandLine.Append(QuoteArgument(arguments[index]));
            }
            return commandLine.ToString();
        }

        private static string QuoteArgument(string argument)
        {
            var mustQuote = argument.Length == 0;
            for (var index = 0; index < argument.Length && !mustQuote; index++)
            {
                if (Char.IsWhiteSpace(argument[index]) || argument[index] == '"')
                    mustQuote = true;
            }
            if (!mustQuote)
                return argument;

            var quoted = new StringBuilder();
            quoted.Append('"');
            var backslashes = 0;
            for (var index = 0; index < argument.Length; index++)
            {
                var character = argument[index];
                if (character == '\\')
                {
                    backslashes++;
                }
                else if (character == '"')
                {
                    quoted.Append('\\', backslashes * 2 + 1);
                    quoted.Append('"');
                    backslashes = 0;
                }
                else
                {
                    quoted.Append('\\', backslashes);
                    quoted.Append(character);
                    backslashes = 0;
                }
            }
            quoted.Append('\\', backslashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }

        private static bool WaitForJob(IntPtr jobHandle, Stopwatch stopwatch, int timeoutMilliseconds)
        {
            while (true)
            {
                var accounting = QueryJobInformation<JobObjectBasicAccountingInformation>(
                    jobHandle, JobObjectBasicAccountingInformationClass);
                if (accounting.ActiveProcesses == 0)
                    return false;

                var remainingMilliseconds = timeoutMilliseconds - stopwatch.ElapsedMilliseconds;
                if (remainingMilliseconds <= 0)
                    return true;
                Thread.Sleep((int)Math.Min(10, remainingMilliseconds));
            }
        }

        private static bool WaitForJobToEmpty(IntPtr jobHandle, int timeoutMilliseconds)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var accounting = QueryJobInformation<JobObjectBasicAccountingInformation>(
                    jobHandle, JobObjectBasicAccountingInformationClass);
                if (accounting.ActiveProcesses == 0)
                    return true;
                var remainingMilliseconds = timeoutMilliseconds - stopwatch.ElapsedMilliseconds;
                if (remainingMilliseconds <= 0)
                    return false;
                Thread.Sleep((int)Math.Min(10, remainingMilliseconds));
            }
        }

        private static void TryTerminateAndDrainJob(IntPtr jobHandle)
        {
            try
            {
                if (TerminateJobObject(jobHandle, TimeoutExitCode))
                    WaitForJobToEmpty(jobHandle, CleanupWaitMilliseconds);
            }
            catch
            {
            }
        }

        private static void TryTerminateProcessAndWait(IntPtr processHandle)
        {
            try
            {
                TerminateProcess(processHandle, TimeoutExitCode);
                WaitForSingleObject(processHandle, CleanupWaitMilliseconds);
            }
            catch
            {
            }
        }

        private static IntPtr CreateHandleAttributeList(IntPtr outputHandle, IntPtr inputHandle,
            out IntPtr inheritedHandles)
        {
            inheritedHandles = IntPtr.Zero;
            var requiredSize = UIntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref requiredSize);
            if (requiredSize.ToUInt64() == 0 || requiredSize.ToUInt64() > Int32.MaxValue)
                ThrowLastWin32Error();

            var attributeList = Marshal.AllocHGlobal((int)requiredSize.ToUInt64());
            var initialized = false;
            try
            {
                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref requiredSize))
                    ThrowLastWin32Error();
                initialized = true;

                inheritedHandles = Marshal.AllocHGlobal(IntPtr.Size * 2);
                Marshal.WriteIntPtr(inheritedHandles, 0, inputHandle);
                Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, outputHandle);
                var handleListSize = new UIntPtr((uint)(IntPtr.Size * 2));
                if (!UpdateProcThreadAttribute(attributeList, 0, ProcThreadAttributeHandleList,
                    inheritedHandles, handleListSize, IntPtr.Zero, IntPtr.Zero))
                    ThrowLastWin32Error();
                return attributeList;
            }
            catch
            {
                if (inheritedHandles != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(inheritedHandles);
                    inheritedHandles = IntPtr.Zero;
                }
                if (initialized)
                    DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
                throw;
            }
        }

        private static SecurityAttributes CreateInheritableSecurityAttributes()
        {
            return new SecurityAttributes
            {
                Length = (uint)Marshal.SizeOf(typeof(SecurityAttributes)),
                InheritHandle = true
            };
        }

        private static IntPtr CreateFileHandle(string path, uint access, uint disposition,
            SecurityAttributes securityAttributes)
        {
            var handle = CreateFileW(path, access, FileShareRead | FileShareWrite | FileShareDelete,
                ref securityAttributes, disposition, FileAttributeNormal, IntPtr.Zero);
            if (handle == InvalidHandleValue)
                ThrowLastWin32Error();
            return handle;
        }

        private static void SetKillOnCloseLimit(IntPtr jobHandle)
        {
            var limits = new JobObjectExtendedLimitInformation();
            limits.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
            SetJobInformation(jobHandle, JobObjectExtendedLimitInformationClass, limits);
        }

        private static void SetJobInformation<T>(IntPtr jobHandle, int informationClass, T information)
            where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(information, buffer, false);
                if (!SetInformationJobObject(jobHandle, informationClass, buffer, (uint)size))
                    ThrowLastWin32Error();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static T QueryJobInformation<T>(IntPtr jobHandle, int informationClass)
            where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                uint returnedLength;
                if (!QueryInformationJobObject(jobHandle, informationClass, buffer, (uint)size, out returnedLength))
                    ThrowLastWin32Error();
                return (T)Marshal.PtrToStructure(buffer, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int GetProcessExitCode(IntPtr processHandle)
        {
            uint exitCode;
            if (!GetExitCodeProcess(processHandle, out exitCode))
                ThrowLastWin32Error();
            return unchecked((int)exitCode);
        }

        private static void ThrowLastWin32Error()
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        private static void CloseHandleIfValid(IntPtr handle)
        {
            if (handle != IntPtr.Zero && handle != InvalidHandleValue)
                CloseHandle(handle);
        }

        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        private const uint GenericWrite = 0x40000000;

        private const uint GenericRead = 0x80000000;

        private const uint CreateAlways = 2;

        private const uint OpenExisting = 3;

        private const uint FileAttributeNormal = 0x00000080;

        private const uint FileShareRead = 0x00000001;

        private const uint FileShareWrite = 0x00000002;

        private const uint FileShareDelete = 0x00000004;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObjectW(IntPtr securityAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr jobHandle, int informationClass,
            IntPtr information, uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(IntPtr jobHandle, int informationClass,
            IntPtr information, uint informationLength, out uint returnedLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr jobHandle, IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(IntPtr jobHandle, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(IntPtr processHandle, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr threadHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode,
            ref SecurityAttributes securityAttributes, uint creationDisposition,
            uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessW(string applicationName, StringBuilder commandLine,
            IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags, IntPtr environment, string currentDirectory, IntPtr startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr attributeList, int attributeCount,
            int flags, ref UIntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(IntPtr attributeList, uint flags,
            IntPtr attribute, IntPtr value, UIntPtr size, IntPtr previousValue, IntPtr returnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public uint Length;

            public IntPtr SecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            public bool InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public uint Cb;

            public string Reserved;

            public string Desktop;

            public string Title;

            public uint X;

            public uint Y;

            public uint XSize;

            public uint YSize;

            public uint XCountChars;

            public uint YCountChars;

            public uint FillAttribute;

            public uint Flags;

            public ushort ShowWindow;

            public ushort Reserved2Length;

            public IntPtr Reserved2;

            public IntPtr StdInput;

            public IntPtr StdOutput;

            public IntPtr StdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfoEx
        {
            public StartupInfo StartupInfo;

            public IntPtr AttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr ProcessHandle;

            public IntPtr ThreadHandle;

            public uint ProcessId;

            public uint ThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicAccountingInformation
        {
            public long TotalUserTime;

            public long TotalKernelTime;

            public long ThisPeriodTotalUserTime;

            public long ThisPeriodTotalKernelTime;

            public uint TotalPageFaults;

            public uint TotalProcesses;

            public uint ActiveProcesses;

            public uint TotalTerminatedProcesses;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;

            public long PerJobUserTimeLimit;

            public uint LimitFlags;

            public UIntPtr MinimumWorkingSetSize;

            public UIntPtr MaximumWorkingSetSize;

            public uint ActiveProcessLimit;

            public UIntPtr Affinity;

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
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;

            public IoCounters IoInfo;

            public UIntPtr ProcessMemoryLimit;

            public UIntPtr JobMemoryLimit;

            public UIntPtr PeakProcessMemoryUsed;

            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
