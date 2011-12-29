using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

// stats on mouse drags and double-clicks
// timing stats between keypresses - distinguish letters
// all stats by application - e.g. Visual Studio very keyboardy but Photoshop much less so

namespace InputFrequency
{
    static class Program
    {
        static Statistics _stats;
        static IntPtr _keyboardHook;
        static IntPtr _mouseHook;

        static bool[] _keyDown = new bool[256];
        static DateTime[] _keyDownAt = new DateTime[256];
        static Key? _lastPressedModifier = null;
        static DateTime _lastKeyboardUseAt = DateTime.MinValue;
        static DateTime _lastMouseUseAt = DateTime.MinValue;
        static int _lastMouseX = int.MinValue, _lastMouseY = int.MinValue;

        /// <summary>
        /// Application entry point.
        /// </summary>
        static void Main()
        {
            _stats = Statistics.Load();

            var user32 = WinAPI.LoadLibrary("User32");
            _keyboardHook = WinAPI.SetWindowsHookEx(WinAPI.WH_KEYBOARD_LL, new WinAPI.KeyboardHookProc(KeyboardHookProc), user32, 0);
            _mouseHook = WinAPI.SetWindowsHookEx(WinAPI.WH_MOUSE_LL, new WinAPI.MouseHookProc(MouseHookProc), user32, 0);

            new Thread(new ThreadStart(StatsSaverThread)) { IsBackground = true }.Start();

            Application.Run();
        }

        /// <summary>
        /// The thread that periodically dumps the stats to disk and generates the report from time to time.
        /// </summary>
        static void StatsSaverThread()
        {
            try
            {
                // Don't generate the report straight away, to make it less expensive to start at boot time
                Thread.Sleep(TimeSpan.FromMinutes(1));
                _stats.CountMinutes(1);
                while (true)
                {
                    // Generate the report once an hour
                    _stats.GenerateReport();
                    // Save the stats every 5 minutes
                    for (int i = 0; i < 12; i++)
                    {
                        _stats.Save();
                        Thread.Sleep(TimeSpan.FromMinutes(5));
                        _stats.CountMinutes(5);
                    }
                }
            }
            catch (Exception e)
            {
                Statistics.SaveCrashReport("StatsSaverThread", e);
                throw;
            }
        }

        /// <summary>
        /// The callback for the keyboard hook.
        /// </summary>
        static int KeyboardHookProc(int code, int wParam, ref WinAPI.KeyboardHookStruct lParam)
        {
            try
            {
                if (code >= 0)
                {
                    if (lParam.vkCode >= 0 && lParam.vkCode <= 255)
                    {
                        if (wParam == WinAPI.WM_KEYDOWN || wParam == WinAPI.WM_SYSKEYDOWN)
                            ProcessKeyMouseDown((Key) lParam.vkCode);
                        else if (wParam == WinAPI.WM_KEYUP || wParam == WinAPI.WM_SYSKEYUP)
                            ProcessKeyMouseUp((Key) lParam.vkCode);
                        else
                            Statistics.SaveDebugLine("Unprocessed keyboard: wParam = {0}, vkCode = {1}, scanCode = {2}, flags = {3}, extraInfo = {4}".Fmt(wParam, lParam.vkCode, lParam.scanCode, lParam.flags, lParam.dwExtraInfo));
                    }
                    else
                        Statistics.SaveDebugLine("Unprocessed keyboard: wParam = {0}, vkCode = {1}, scanCode = {2}, flags = {3}, extraInfo = {4}".Fmt(wParam, lParam.vkCode, lParam.scanCode, lParam.flags, lParam.dwExtraInfo));
                }
                return WinAPI.CallNextHookEx(_keyboardHook, code, wParam, ref lParam);
            }
            catch (Exception e)
            {
                Statistics.SaveCrashReport("KeyboardHookProc", e);
                throw;
            }
        }

        /// <summary>
        /// The callback for the mouse hook.
        /// </summary>
        static int MouseHookProc(int code, int wParam, ref WinAPI.MouseHookStruct lParam)
        {
            try
            {
                if (code >= 0)
                {
                    if (wParam == WinAPI.WM_MOUSEMOVE)
                        ProcessMouseMove(lParam.pt.X, lParam.pt.Y);
                    else if (wParam == WinAPI.WM_MOUSEWHEEL || wParam == WinAPI.WM_MOUSEHWHEEL)
                        ProcessMouseWheel(wParam == WinAPI.WM_MOUSEWHEEL, (lParam.mouseData >> 16) / 120);
                    else if (wParam == WinAPI.WM_LBUTTONDOWN)
                        ProcessKeyMouseDown(Key.MouseLeft);
                    else if (wParam == WinAPI.WM_LBUTTONUP)
                        ProcessKeyMouseUp(Key.MouseLeft);
                    else if (wParam == WinAPI.WM_MBUTTONDOWN)
                        ProcessKeyMouseDown(Key.MouseMiddle);
                    else if (wParam == WinAPI.WM_MBUTTONUP)
                        ProcessKeyMouseUp(Key.MouseMiddle);
                    else if (wParam == WinAPI.WM_RBUTTONDOWN)
                        ProcessKeyMouseDown(Key.MouseRight);
                    else if (wParam == WinAPI.WM_RBUTTONUP)
                        ProcessKeyMouseUp(Key.MouseRight);
                    else if (wParam == WinAPI.WM_XBUTTONDOWN && (lParam.mouseData >> 16) == 1)
                        ProcessKeyMouseDown(Key.MouseExtra1);
                    else if (wParam == WinAPI.WM_XBUTTONUP && (lParam.mouseData >> 16) == 1)
                        ProcessKeyMouseUp(Key.MouseExtra1);
                    else if (wParam == WinAPI.WM_XBUTTONDOWN && (lParam.mouseData >> 16) == 2)
                        ProcessKeyMouseDown(Key.MouseExtra2);
                    else if (wParam == WinAPI.WM_XBUTTONUP && (lParam.mouseData >> 16) == 2)
                        ProcessKeyMouseUp(Key.MouseExtra2);
                    else
                        Statistics.SaveDebugLine("Unprocessed mouse: wParam = {0}, mouseData = {1}, flags = {2}, extraInfo = {3}".Fmt(wParam, lParam.mouseData, lParam.flags, lParam.dwExtraInfo));
                }
                return WinAPI.CallNextHookEx(_mouseHook, code, wParam, ref lParam);
            }
            catch (Exception e)
            {
                Statistics.SaveCrashReport("MouseHookProc", e);
                throw;
            }
        }

        /// <summary>
        /// Handles the "key down" and "mouse button down" events. This is called for key autorepeats, which must be filtered out.
        /// </summary>
        static void ProcessKeyMouseDown(Key key)
        {
            if (KeyCombo.IsMouseButton(key))
                ProcessMouseUse();
            else
                ProcessKeyboardUse();

            var ikey = (byte) key;
            if (!_keyDown[ikey])
            {
                _keyDownAt[ikey] = DateTime.UtcNow;
                if (KeyCombo.IsModifier(key))
                    _lastPressedModifier = key;
                else
                {
                    _lastPressedModifier = null;
                    ProcessKeyCombo(new KeyCombo(key, _keyDown));
                }
            }
            _keyDown[ikey] = true;
        }

        /// <summary>
        /// Handles the "key up" and "mouse button up" events.
        /// </summary>
        static void ProcessKeyMouseUp(Key key)
        {
            if (KeyCombo.IsMouseButton(key))
                ProcessMouseUse();
            else
                ProcessKeyboardUse();

            if (_lastPressedModifier != null)
            {
                ProcessKeyCombo(new KeyCombo(_lastPressedModifier.Value, _keyDown));
                _lastPressedModifier = null;
            }

            var ikey = (byte) key;
            if (_keyDown[ikey])
            {
                _keyDown[ikey] = false;
                _stats.CountKey(key, DateTime.UtcNow - _keyDownAt[ikey]);
            }
        }

        /// <summary>
        /// Handles a "complete" combo like Ctrl+Alt+S.
        /// </summary>
        static void ProcessKeyCombo(KeyCombo combo)
        {
            _stats.CountCombo(combo);
#if DEBUG
            Debug.Print(combo.ToString());
#endif
        }

        /// <summary>
        /// Handles mouse move events. The coordinates are absolute screen coordinates.
        /// </summary>
        private static void ProcessMouseMove(int x, int y)
        {
            ProcessMouseUse();
            if (_lastMouseX != int.MinValue && _lastMouseY != int.MinValue)
                _stats.CountMouseMove(x - _lastMouseX, y - _lastMouseY);
            _lastMouseX = x;
            _lastMouseY = y;
        }

        /// <summary>
        /// Handles mouse wheel rotation. "amount" is in clicks, rather than the arbitrary 120x units.
        /// </summary>
        private static void ProcessMouseWheel(bool vertical, int amount)
        {
            ProcessMouseUse();
            var wheelKey = vertical ? (amount < 0 ? Key.MouseWheelDown : Key.MouseWheelUp) : (amount < 0 ? Key.MouseWheelRight : Key.MouseWheelLeft);
            _stats.CountKey(wheelKey, TimeSpan.Zero);
            _lastPressedModifier = null;
            ProcessKeyCombo(new KeyCombo(wheelKey, _keyDown));
        }

        /// <summary>
        /// Handles the occurrence of anything that counts as "using the keyboard".
        /// </summary>
        private static void ProcessKeyboardUse()
        {
            var now = DateTime.UtcNow;
            if (_lastKeyboardUseAt != DateTime.MinValue)
            {
                var timeSinceLastUse = now - _lastKeyboardUseAt;
                if (timeSinceLastUse <= TimeSpan.FromSeconds(12))
                    _stats.CountKeyboardUse(timeSinceLastUse);
                else
                    _stats.CountKeyboardUse(TimeSpan.FromSeconds(1));
            }
            _lastKeyboardUseAt = now;
#if DEBUG
            // Debug.Print("Keyboard used");
#endif
        }

        /// <summary>
        /// Handles the occurrence of anything that counts as "using the mouse".
        /// </summary>
        private static void ProcessMouseUse()
        {
            var now = DateTime.UtcNow;
            if (_lastMouseUseAt != DateTime.MinValue)
            {
                var timeSinceLastUse = now - _lastMouseUseAt;
                if (timeSinceLastUse <= TimeSpan.FromSeconds(12))
                    _stats.CountMouseUse(timeSinceLastUse);
                else
                    _stats.CountMouseUse(TimeSpan.FromSeconds(1));
            }
            _lastMouseUseAt = now;
#if DEBUG
            // Debug.Print("Mouse used");
#endif
        }
    }

    /*
     * Test cases for combo handling:  (keydown KEYUP)
     * 
     * alt shift [h] H SHIFT ALT   => Alt+Shift+H
     * alt shift [h] [g] H ctrl [G] CTRL ALT SHIFT   =>  Alt+Shift+H   Alt+Shift+G   Alt+Shift+Ctrl
     * alt shift [h] H SHIFT [k] K ALT   =>  Alt+Shift+H, Alt+K
     * alt shift [SHIFT] ALT   =>   Alt+Shift
     * alt shift [ALT] SHIFT   =>   Alt+Shift
     * shift alt [ALT] SHIFT   =>   Shift+Alt
     * ctrl alt shift [ALT] SHIFT CTRL =>  Ctrl+Alt+Shift
     * ctrl alt shift [ALT] SHIFT alt [ALT] CTRL =>  Ctrl+Alt+Shift   Ctrl+Alt
     * 
     * ctrl alt [lbutton] LBUTTON ALT shift [g] G CTRL SHIFT  => Ctrl+Alt+LButton   Ctrl+Shift+G
     * ctrl alt [wheel] ALT shift [g] G CTRL SHIFT => Ctrl+Alt+Wheel   Ctrl+Shift+G
     */
}
