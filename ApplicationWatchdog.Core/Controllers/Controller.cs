using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;

namespace ApplicationWatchdog.Core
{
    public static class Controller
    {

        private static Timer _timer;

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int Length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int cb;
            public String lpReserved;
            public String lpDesktop;
            public String lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }
        #endregion

        #region Enums
        enum TOKEN_TYPE : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2
        }

        enum SECURITY_IMPERSONATION_LEVEL : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }
        #endregion

        #region Constants
        public const uint MAXIMUM_ALLOWED = 0x2000000;
        public const int TOKEN_DUPLICATE = 0x0002;
        public const int NORMAL_PRIORITY_CLASS = 0x20;
        public const int CREATE_NEW_CONSOLE = 0x00000010;
        #endregion

        #region Win32 API Imports
        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        public extern static bool DuplicateTokenEx(IntPtr ExistingTokenHandle, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES lpThreadAttributes, int TokenType, int ImpersonationLevel, ref IntPtr DuplicateTokenHandle);
        [DllImport("kernel32.dll")]
        static extern uint WTSGetActiveConsoleSessionId();
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hSnapshot);

        [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUser", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public extern static bool CreateProcessAsUser(IntPtr hToken, String lpApplicationName, String lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandle, int dwCreationFlags, IntPtr lpEnvironment, String lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
        #endregion

        private static List<string> GetExeInfoList()
        {
            List<string> exePathList = new List<string>();
            string _exePath = ConfigurationManager.AppSettings["ExePath"];
            foreach (string path in _exePath.Split(','))
            {
                exePathList.Add(path);
            }
            return exePathList;
        }

        public static void Start()
        {
            string _interval = ConfigurationManager.AppSettings["CheckIntervalAsMinutes"];
            var startTimeSpan = TimeSpan.Zero;
            var periodTimeSpan = TimeSpan.FromMinutes(int.Parse(_interval));
            _timer = new Timer((e) =>
            {
                foreach (string path in GetExeInfoList())
                {
                    string filename = Path.GetFileNameWithoutExtension(path);
                    Process[] pname = Process.GetProcessesByName(filename);
                    if (pname.Length == 0)
                    {
                        AppOpener(filename, path, out PROCESS_INFORMATION procInfo);
                    }

                }
            }, null, startTimeSpan, periodTimeSpan);

            Logger.WriteErrorLog("Service started.");
        }

        private static void AppOpener(string applicationName, string applicationPath, out PROCESS_INFORMATION procInfo)
        {
            uint winlogonPid = 0;
            uint dwSessionId = WTSGetActiveConsoleSessionId();
            IntPtr hUserTokenDup = IntPtr.Zero, hPToken = IntPtr.Zero, hProcess = IntPtr.Zero;
            procInfo = new PROCESS_INFORMATION();
            Process[] processes = Process.GetProcessesByName("winlogon");
            foreach (Process p in processes)
            {
                if ((uint)p.SessionId == dwSessionId)
                {
                    winlogonPid = (uint)p.Id;
                }
            }
            // obtain a handle to the winlogon process
            hProcess = OpenProcess(MAXIMUM_ALLOWED, false, winlogonPid);

            // obtain a handle to the access token of the winlogon process
            if (!OpenProcessToken(hProcess, TOKEN_DUPLICATE, ref hPToken))
            {
                CloseHandle(hProcess);
            }

            // Security attibute structure used in DuplicateTokenEx and   CreateProcessAsUser
            // I would prefer to not have to use a security attribute variable and to just 
            // simply pass null and inherit (by default) the security attributes
            // of the existing token. However, in C# structures are value types and   therefore
            // cannot be assigned the null value.
            SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
            sa.Length = Marshal.SizeOf(sa);

            // copy the access token of the winlogon process; 
            // the newly created token will be a primary token
            if (!DuplicateTokenEx(hPToken, MAXIMUM_ALLOWED, ref sa,
                (int)SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                (int)TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
            {
                CloseHandle(hProcess);
                CloseHandle(hPToken);
            }

            STARTUPINFO si = new STARTUPINFO();
            si.cb = (int)Marshal.SizeOf(si);

            // interactive window station parameter; basically this indicates 
            // that the process created can display a GUI on the desktop
            si.lpDesktop = @"winsta0\default";

            // flags that specify the priority and creation method of the process
            int dwCreationFlags = NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE;

            // create a new process in the current User's logon session
            bool result = CreateProcessAsUser(hUserTokenDup,  // client's access token
                                       applicationPath,  // file to execute
                                       applicationName,  // command line
                                       ref sa,           // pointer to process    SECURITY_ATTRIBUTES
                                       ref sa,           // pointer to thread SECURITY_ATTRIBUTES
                                       false,            // handles are not inheritable
                                       dwCreationFlags,  // creation flags
                                       IntPtr.Zero,      // pointer to new environment block 
                                       null,             // name of current directory 
                                       ref si,           // pointer to STARTUPINFO structure
                                       out procInfo      // receives information about new process
                                       );
            Logger.WriteErrorLog("SessionID = " + dwSessionId);
            Logger.WriteErrorLog("Application " + applicationName + " was not found in Task Manager, and has been opened succefuly.");
        }

        public static void Stop()
        {
            if (_timer != null)
                _timer.Dispose();
            Logger.WriteErrorLog("Service stopped.");
        }
    }
}
