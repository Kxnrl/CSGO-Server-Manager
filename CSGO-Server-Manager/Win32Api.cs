using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Kxnrl.CSM.Win32Api
{
    class Window
    {
        private const uint SW_HIDE = 0;
        private const uint SW_SHOW = 1;
        private const uint GW_HWNDNEXT = 2; // The next window is below the specified window
        private const uint GW_HWNDPREV = 3; // The previous window is above

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hwnd, uint nCmdShow);
        [DllImport("user32.dll ")]
        static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetWindow", SetLastError = true)]
        static extern IntPtr GetNextWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.U4)] int wFlag);
        [DllImport("user32.dll")]
        static extern IntPtr GetTopWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern void GetClassName(IntPtr hwnd, StringBuilder sb, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        public static void Show(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_SHOW);
        }
        public static void Hide(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_HIDE);
        }
        public static void Active(IntPtr hwnd)
        {
            SetForegroundWindow(hwnd);
        }
    }

    class ConsoleCTRL
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        public static void ConsoleClosed(HandlerRoutine Handler)
        {
            SetConsoleCtrlHandler(Handler, true);
        }

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }
    }

    class PowerMode
    {
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        public static void NoSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        }
    }

    class Message
    {
        [DllImport("User32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private static byte[] bytes = new byte[1000 * 256];
        public static void Write(IntPtr hWnd, string message)
        {
            Send(hWnd);
            System.Threading.Thread.Sleep(50);
            bytes = Encoding.Unicode.GetBytes(message);
            foreach (byte b in bytes)
            {
                SendMessage(hWnd, 0x0102, b, 0);
            }
            System.Threading.Thread.Sleep(50);
        }

        public static bool Send(IntPtr hWnd)
        {
            return PostMessage(hWnd, 0x0100, 13, 0);
        }
    }
}
