using System;
using System.Collections.Generic;
using System.Linq;

namespace InputFrequency
{
    static class Util
    {
    }

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

        public static string ToName(this Key key)
        {
            return _keyNames[key];
        }

        public static string Fmt(this string formatString, params object[] args)
        {
            return string.Format(formatString, args);
        }

        public static void IncSafe<K>(this IDictionary<K, int> dic, K key, int amount = 1)
        {
            if (!dic.ContainsKey(key))
                dic[key] = amount;
            else
                dic[key] = dic[key] + amount;
        }

        public static void IncSafe<K>(this IDictionary<K, double> dic, K key, double amount)
        {
            if (!dic.ContainsKey(key))
                dic[key] = amount;
            else
                dic[key] = dic[key] + amount;
        }

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
    }
}
