using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

// mouse
// time spent using mouse
// timing stats between keypresses - distinguish letters

namespace InputFrequency
{
    static class Program
    {
        static Statistics _stats;
        static IntPtr _hook;

        static bool[] _keyDown = new bool[256];
        static DateTime[] _keyDownAt = new DateTime[256];
        static Key? _lastPressedModifier = null;
        static DateTime _lastKeyboardUseAt = DateTime.MinValue;

        /// <summary>
        /// Application entry point.
        /// </summary>
        static void Main()
        {
            _stats = Statistics.Load();
            _hook = WinAPI.SetWindowsHookEx(WinAPI.WH_KEYBOARD_LL, new WinAPI.KeyboardHookProc(HookProc), WinAPI.LoadLibrary("User32"), 0);

            new Thread(new ThreadStart(StatsSaverThread)) { IsBackground = true }.Start();

            Application.Run();
        }

        /// <summary>
        /// The thread that periodically dumps the stats to disk and generates the report from time to time.
        /// </summary>
        static void StatsSaverThread()
        {
            // Don't generate the report straight away, to make it less expensive to start at boot time
            Thread.Sleep(TimeSpan.FromMinutes(5));
            _stats.CountMinutes(5);
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

        /// <summary>
        /// The callback for the keyboard hook.
        /// </summary>
        static int HookProc(int code, int wParam, ref WinAPI.KeyboardHookStruct lParam)
        {
            if (code >= 0 && lParam.vkCode >= 0 && lParam.vkCode <= 255)
            {
                if (wParam == WinAPI.WM_KEYDOWN || wParam == WinAPI.WM_SYSKEYDOWN)
                    ProcessKeyDown((Key) lParam.vkCode); // lParam.scanCode
                else if (wParam == WinAPI.WM_KEYUP || wParam == WinAPI.WM_SYSKEYUP)
                    ProcessKeyUp((Key) lParam.vkCode); // lParam.scanCode
            }
            return WinAPI.CallNextHookEx(_hook, code, wParam, ref lParam);
        }

        /// <summary>
        /// Handles the "key down" event, which is also sent for repeat keys.
        /// </summary>
        static void ProcessKeyDown(Key key)
        {
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
        /// Handles the "key up" event.
        /// </summary>
        static void ProcessKeyUp(Key key)
        {
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

        static void ProcessKeyCombo(KeyCombo combo)
        {
#if DEBUG
            Debug.Print(combo.ToString());
#endif
            _stats.CountCombo(combo);
        }

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
     */
}
