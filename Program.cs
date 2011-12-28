using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using RT.Util;
using RT.Util.ExtensionMethods;

// don't store key names in settings file (migrate off RT.Util so that others can run this from source?)
// make sure all key names are good
// time spent using keyboard
// mouse
// time spent using mouse

namespace InputFrequency
{
    static class Program
    {
        static void Main()
        {
            Settings settings;
            SettingsUtil.LoadSettings(out settings);

            var keyValues = EnumStrong.GetValues<Keys>().Zip(Enum.GetNames(typeof(Keys)), (key, name) => new { key, name }).ToLookup(x => x.key, x => x.name);
            var names = keyValues.ToDictionary(kgroup => kgroup.Key, kgroup => kgroup.First());
            var down = new bool[256];
            var downAt = new DateTime[256];
            var modifiers = new[] { Keys.LWin, Keys.RWin, Keys.LControlKey, Keys.RControlKey, Keys.LMenu, Keys.RMenu, Keys.LShiftKey, Keys.RShiftKey };
            Keys? lastPressedKey = null;
            string previous = null;

            // Fixup some names
            names[Keys.LControlKey] = "LCtrl";
            names[Keys.RControlKey] = "RCtrl";
            names[Keys.LMenu] = "LAlt";
            names[Keys.RMenu] = "RAlt";
            names[Keys.LShiftKey] = "LShift";
            names[Keys.RShiftKey] = "RShift";
            names[Keys.Snapshot] = "PrintScreen";
            names[Keys.Scroll] = "ScrollLock";
            names[Keys.Prior] = "PageUp";
            names[Keys.Oemplus] = "OemPlus";
            names[Keys.Oemcomma] = "OemComma";
            // Add anything missing
            for (int i = 0; i <= 255; i++)
                if (!names.ContainsKey((Keys) i))
                    names[(Keys) i] = "VirtualKey" + i;

            var listener = new GlobalKeyboardListener { HookAllKeys = true };

            listener.KeyDown += (_, e) =>
            {
                var key = e.VirtualKeyCode;
                var ikey = (int) key;
                if (ikey >= down.Length)
                {
                    settings.TooLarge++;
                    return;
                }
                if (!down[ikey])
                {
                    downAt[ikey] = DateTime.UtcNow;
                    lastPressedKey = key;
                    settings.KeyCounts.IncSafe(names[key]);
                }
                down[ikey] = true;
            };
            listener.KeyUp += (_, e) =>
            {
                var key = e.VirtualKeyCode;
                var ikey = (int) key;
                if (ikey >= down.Length)
                {
                    settings.TooLarge++;
                    return;
                }
                // alt shift h [H] SHIFT k [K] ALT   =>  Alt+Shift+H, Alt+K
                // alt shift SHIFT ALT   =>   Alt+Shift
                // alt shift [ALT] SHIFT   =>   Alt+Shift
                // ctrl alt shift [ALT] SHIFT CTRL =>  Ctrl+Alt+Shift
                // ctrl alt shift [ALT] SHIFT alt [ALT] CTRL =>  Ctrl+Alt+Shift   Ctrl+Alt
                // => output on the first released key after we had a pressed key
                if (lastPressedKey != null)
                {
                    string keyStr = modifiers.Contains(lastPressedKey.Value) ? "" : names[lastPressedKey.Value];
                    for (int i = modifiers.Length - 1; i >= 0; i--)
                        if (down[(int) modifiers[i]])
                            keyStr = names[modifiers[i]] + (keyStr.Length == 0 ? "" : "+") + keyStr;
                    settings.ComboCounts.IncSafe(keyStr);
                    if (settings.RecordChords)
                    {
                        if (previous != null)
                            settings.ChordCounts.IncSafe(previous + ", " + keyStr);
                        previous = keyStr;
                    }
                }
                lastPressedKey = null;
                down[ikey] = false;
                var downFor = DateTime.UtcNow - downAt[ikey];
                if (downFor < TimeSpan.FromMinutes(1)) // just make sure the data is sane
                    settings.DownFor.IncSafe(names[key], downFor.TotalSeconds);
            };

            var saver = new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    GenerateReport(settings);
                    for (int i = 0; i < 12; i++)
                    {
                        settings.SaveQuiet();
                        Thread.Sleep(TimeSpan.FromMinutes(5));
                        settings.RuntimeMinutes += 5;
                    }
                }
            }));
            saver.IsBackground = true;
            saver.Start();

            Application.Run();
        }

        private static void GenerateReport(Settings stats)
        {
            try
            {
                using (var file = new StreamWriter(File.Open(stats.Attribute.GetFileName() + ".report.txt", FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    file.WriteLine("=== KEY USAGE ===");
                    foreach (var line in stats.KeyCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:0,0}".Fmt(kvp.Key, kvp.Value)))
                        file.WriteLine(line);
                    file.WriteLine();
                    file.WriteLine("=== COMBO USAGE ===");
                    foreach (var line in stats.ComboCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:0,0}".Fmt(kvp.Key, kvp.Value)))
                        file.WriteLine(line);
                    file.WriteLine();
                    file.WriteLine("=== CHORD USAGE ===");
                    foreach (var line in stats.ChordCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:0,0}".Fmt(kvp.Key, kvp.Value)))
                        file.WriteLine(line);
                    file.WriteLine();
                    file.WriteLine("=== KEY DOWN DURATION ===");
                    foreach (var line in stats.DownFor.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:0,0} seconds".Fmt(kvp.Key, kvp.Value)))
                        file.WriteLine(line);
                }
            }
            catch { }
        }
    }

    [Settings("InputFrequency", SettingsKind.UserSpecific)]
    class Settings : SettingsBase
    {
        public bool RecordChords = false;
        public int RuntimeMinutes = 0;
        public int TooLarge = 0;
        public Dictionary<string, int> KeyCounts = new Dictionary<string, int>();
        public Dictionary<string, int> ComboCounts = new Dictionary<string, int>();
        public Dictionary<string, int> ChordCounts = new Dictionary<string, int>();
        public Dictionary<string, double> DownFor = new Dictionary<string, double>();
    }
}
