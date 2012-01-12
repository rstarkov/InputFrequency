using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

// Stats: total key pushes; pressed alone; pressed with modifiers.
// shift+numpad produce an unwanted shift (perhaps it's possible to track them as Shift+NumPadX?)
// stats on mouse drags and double-clicks
// timing stats between keypresses - distinguish letters
// all stats by application - e.g. Visual Studio very keyboardy but Photoshop much less so

namespace InputFrequency
{
    static class Program
    {
        static Statistics _stats;

        static bool[] _keyDown = new bool[256];
        static DateTime[] _keyDownAt = new DateTime[256];
        static Key? _lastPressedModifier = null;
        static DateTime _lastKeyboardUseAt = DateTime.MinValue;
        static DateTime _lastMouseUseAt = DateTime.MinValue;
        static int _lastMouseX = int.MinValue, _lastMouseY = int.MinValue;

        /// <summary>Maximum time allowed between keyboard/mouse use events for the whole interval to count as "use".</summary>
        static readonly TimeSpan _useBetweenTimeout = TimeSpan.FromSeconds(12);
        /// <summary>The amount of time that's counted as keyboard/mouse "use" after the last (or, indeed, the only) use event.</summary>
        static readonly TimeSpan _useAfterTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Application entry point.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                _stats = Statistics.Load();

                if (args.Length == 1 && args[0] == "report")
                {
                    _stats.GenerateReport();
                    Console.WriteLine("Saved report. Exiting.");
                    return;
                }
                else if (args.Length != 0)
                {
                    Console.WriteLine("Unknown command line option. Try \"report\".");
                    return;
                }

                var user32 = WinAPI.LoadLibrary("User32");
                var keyboardCallback = new WinAPI.KeyboardHookProc(KeyboardHookProc);
                var mouseCallback = new WinAPI.MouseHookProc(MouseHookProc);
                WinAPI.SetWindowsHookEx(WinAPI.WH_KEYBOARD_LL, keyboardCallback, user32, 0);
                WinAPI.SetWindowsHookEx(WinAPI.WH_MOUSE_LL, mouseCallback, user32, 0);

                new Thread(new ThreadStart(StatsSaverThread)) { IsBackground = true }.Start();

                Application.Run();

                GC.KeepAlive(keyboardCallback);
                GC.KeepAlive(mouseCallback);
            }
            catch (Exception e)
            {
                Statistics.SaveCrashReport("Main", e);
                throw;
            }
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
                        if (Recording)
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
                if (Recording && code >= 0)
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
                return WinAPI.CallNextHookEx(IntPtr.Zero, code, wParam, ref lParam);
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
                if (Recording && code >= 0)
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
                        ProcessKeyMouseDown(Key.MouseBack);
                    else if (wParam == WinAPI.WM_XBUTTONUP && (lParam.mouseData >> 16) == 1)
                        ProcessKeyMouseUp(Key.MouseBack);
                    else if (wParam == WinAPI.WM_XBUTTONDOWN && (lParam.mouseData >> 16) == 2)
                        ProcessKeyMouseDown(Key.MouseForward);
                    else if (wParam == WinAPI.WM_XBUTTONUP && (lParam.mouseData >> 16) == 2)
                        ProcessKeyMouseUp(Key.MouseForward);
                    else
                        Statistics.SaveDebugLine("Unprocessed mouse: wParam = {0}, mouseData = {1}, flags = {2}, extraInfo = {3}".Fmt(wParam, lParam.mouseData, lParam.flags, lParam.dwExtraInfo));
                }
                return WinAPI.CallNextHookEx(IntPtr.Zero, code, wParam, ref lParam);
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
            if (key.IsMouseButton())
                ProcessMouseUse();
            else
                ProcessKeyboardUse();

            var ikey = (byte) key;
            if (!_keyDown[ikey])
            {
                _keyDownAt[ikey] = DateTime.UtcNow;
                if (key.IsModifierKey())
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
            if (key.IsMouseButton())
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
            var wheelKey = vertical ? (amount < 0 ? Key.MouseWheelDown : Key.MouseWheelUp) : (amount < 0 ? Key.MouseWheelLeft : Key.MouseWheelRight);
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
                if (timeSinceLastUse <= _useBetweenTimeout)
                    _stats.CountKeyboardUse(timeSinceLastUse);
                else
                    _stats.CountKeyboardUse(_useAfterTimeout);
            }
            _lastKeyboardUseAt = now;
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
                if (timeSinceLastUse <= _useBetweenTimeout)
                    _stats.CountMouseUse(timeSinceLastUse);
                else
                    _stats.CountMouseUse(_useAfterTimeout);
            }
            _lastMouseUseAt = now;
        }

        private static bool Recording
        {
            get { return !Control.IsKeyLocked(Keys.Scroll); }
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
