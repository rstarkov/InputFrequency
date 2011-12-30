using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InputFrequency
{
    static class Extensions
    {
        static Dictionary<Key, string> _keyNames;
        static Extensions()
        {
            // Add all the names from the enum
            _keyNames = Enum.GetValues(typeof(Key)).Cast<Key>()
                .Select(key => new { key, name = Enum.GetName(typeof(Key), key) })
                .ToDictionary(x => x.key, x => x.name);
            // Add anything missing
            for (int i = 0; i <= 255; i++)
                if (!_keyNames.ContainsKey((Key) i))
                    _keyNames[(Key) i] = "VirtualKey" + i;
        }

        /// <summary>Gets the string for a key name efficiently.</summary>
        public static string ToName(this Key key) { return _keyNames[key]; }
        /// <summary>Shortcut for string.Format(...)</summary>
        public static string Fmt(this string formatString, params object[] args) { return string.Format(formatString, args); }

        /// <summary>Parses a double using the invariant culture - so that data can be loaded/saved correctly regardless of OS culture.</summary>
        public static double ParseDoubleInv(this string value) { return double.Parse(value, CultureInfo.InvariantCulture); }
        /// <summary>Parses a double using the invariant culture - so that data can be loaded/saved correctly regardless of OS culture.</summary>
        public static int ParseIntInv(this string value) { return int.Parse(value, CultureInfo.InvariantCulture); }
        /// <summary>Parses a Key using the invariant culture - so that data can be loaded/saved correctly regardless of OS culture.</summary>
        public static Key ParseKeyInv(this string value) { return (Key) int.Parse(value, CultureInfo.InvariantCulture); }
        /// <summary>Converts an int to string using the invariant culture - so that data can be loaded/saved correctly regardless of OS culture.</summary>
        public static string ToStringInv(this int value) { return value.ToString(CultureInfo.InvariantCulture); }
        /// <summary>Converts a double to string using the invariant culture - so that data can be loaded/saved correctly regardless of OS culture.</summary>
        public static string ToStringInv(this double value) { return value.ToString(CultureInfo.InvariantCulture); }
        /// <summary>Converts a Key to string using the invariant culture - so that data can be loaded/saved correctly regardless of OS culture.</summary>
        public static string ToStringInv(this Key value) { return ((int) value).ToString(CultureInfo.InvariantCulture); }

        /// <summary>Adds the specified number (or 1) to a dictionary value, even if the key is absent - in which case the value is deemed to be zero.</summary>
        public static void IncSafe<K>(this IDictionary<K, int> dic, K key, int amount = 1)
        {
            if (!dic.ContainsKey(key))
                dic[key] = amount;
            else
                dic[key] = dic[key] + amount;
        }

        /// <summary>Adds the specified number to a dictionary value, even if the key is absent - in which case the value is deemed to be zero.</summary>
        public static void IncSafe<K>(this IDictionary<K, double> dic, K key, double amount)
        {
            if (!dic.ContainsKey(key))
                dic[key] = amount;
            else
                dic[key] = dic[key] + amount;
        }

        /// <summary>Like string.Substring(int), but for arrays.</summary>
        public static T[] Subarray<T>(this T[] array, int startIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (startIndex < 0 || startIndex > array.Length)
                throw new ArgumentOutOfRangeException("startIndex", "startIndex cannot be negative or extend beyond the end of the array.");
            int length = array.Length - startIndex;
            T[] result = new T[length];
            Array.Copy(array, startIndex, result, 0, length);
            return result;
        }

        public static bool IsModifierKey(this Key key)
        {
            switch (key)
            {
                case Key.LWin:
                case Key.RWin:
                case Key.LCtrl:
                case Key.RCtrl:
                case Key.Ctrl:
                case Key.LAlt:
                case Key.RAlt:
                case Key.Alt:
                case Key.LShift:
                case Key.RShift:
                case Key.Shift:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMouseButton(this Key key)
        {
            switch (key)
            {
                case Key.MouseLeft:
                case Key.MouseMiddle:
                case Key.MouseRight:
                case Key.MouseBack:
                case Key.MouseForward:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMouseWheel(this Key key)
        {
            switch (key)
            {
                case Key.MouseWheelLeft:
                case Key.MouseWheelRight:
                case Key.MouseWheelUp:
                case Key.MouseWheelDown:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFunctionKey(this Key key)
        {
            return key >= Key.F1 && key <= Key.F24;
        }

        public static bool IsNavigationKey(this Key key)
        {
            return key.IsArrowKey() || key.IsHEPGKey();
        }

        public static bool IsArrowKey(this Key key)
        {
            switch (key)
            {
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsHEPGKey(this Key key)
        {
            switch (key)
            {
                case Key.Home:
                case Key.End:
                case Key.PageUp:
                case Key.PageDown:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsNumpadKey(this Key key)
        {
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return true;
            switch (key)
            {
                case Key.NumMultiply:
                case Key.NumAdd:
                case Key.NumSeparator:
                case Key.NumSubtract:
                case Key.NumDecimal:
                case Key.NumDivide:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMediaFancyKey(this Key key)
        {
            switch (key)
            {
                case Key.BrowserBack:
                case Key.BrowserForward:
                case Key.BrowserRefresh:
                case Key.BrowserStop:
                case Key.BrowserSearch:
                case Key.BrowserFavorites:
                case Key.BrowserHome:
                case Key.VolumeMute:
                case Key.VolumeDown:
                case Key.VolumeUp:
                case Key.MediaNextTrack:
                case Key.MediaPreviousTrack:
                case Key.MediaStop:
                case Key.MediaPlayPause:
                case Key.LaunchMail:
                case Key.LaunchMedia:
                case Key.LaunchApplication1:
                case Key.LaunchCalculator:
                    return true;
                default:
                    return false;
            }
        }

    }
}
