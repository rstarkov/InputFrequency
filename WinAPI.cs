using System;
using System.Runtime.InteropServices;

namespace InputFrequency
{
    /// <summary>
    /// WinAPI function wrappers.
    /// </summary>
    static class WinAPI
    {
        public const int HC_ACTION = 0;
        public const int LLKHF_EXTENDED = 0x1;
        public const int LLKHF_INJECTED = 0x10;
        public const int LLKHF_ALTDOWN = 0x20;
        public const int LLKHF_UP = 0x80;
        public const int LLMHF_INJECTED = 0x1;
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x100;
        public const int WM_KEYUP = 0x101;
        public const int WM_SYSKEYDOWN = 0x104;
        public const int WM_SYSKEYUP = 0x105;

        public const int WM_MOUSEMOVE = 0x200;
        public const int WM_LBUTTONDOWN = 0x201;
        public const int WM_LBUTTONUP = 0x202;
        public const int WM_RBUTTONDOWN = 0x204;
        public const int WM_RBUTTONUP = 0x205;
        public const int WM_MBUTTONDOWN = 0x207;
        public const int WM_MBUTTONUP = 0x208;
        public const int WM_XBUTTONDOWN = 0x20B;
        public const int WM_XBUTTONUP = 0x20C;
        public const int WM_MOUSEWHEEL = 0x20A;
        public const int WM_MOUSEHWHEEL = 0x20E;

        public struct KeyboardHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseHookStruct
        {
            public Point pt;
            public int mouseData;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        public delegate int KeyboardHookProc(int code, int wParam, ref KeyboardHookStruct lParam);
        public delegate int MouseHookProc(int code, int wParam, ref MouseHookStruct lParam);

        /// <summary>Sets the windows hook, do the desired event, one of hInstance or threadId must be non-null.</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(int idHook, WinAPI.KeyboardHookProc callback, IntPtr hInstance, uint threadId);

        /// <summary>Sets the windows hook, do the desired event, one of hInstance or threadId must be non-null.</summary>
        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(int idHook, WinAPI.MouseHookProc callback, IntPtr hInstance, uint threadId);

        /// <summary>Calls the next hook.</summary>
        [DllImport("user32.dll")]
        public static extern int CallNextHookEx(IntPtr idHook, int nCode, int wParam, ref KeyboardHookStruct lParam);
        /// <summary>Calls the next hook.</summary>
        [DllImport("user32.dll")]
        public static extern int CallNextHookEx(IntPtr idHook, int nCode, int wParam, ref MouseHookStruct lParam);

        /// <summary>Loads the library.</summary>
        /// <param name="lpFileName">Name of the library</param>
        /// <returns>A handle to the library</returns>
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string lpFileName);
    }
}
