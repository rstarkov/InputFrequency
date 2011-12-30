using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InputFrequency
{
    sealed class Statistics
    {
        private int RuntimeMinutes = 0;
        private double KeyboardUseSeconds = 0;
        private double MouseUseSeconds = 0;
        private int MouseTravelX = 0, MouseTravelY = 0;
        private double MouseTravel = 0;
        private Dictionary<Key, int> KeyCounts = new Dictionary<Key, int>();
        private Dictionary<KeyCombo, int> ComboCounts = new Dictionary<KeyCombo, int>();
        private Dictionary<KeyChord, int> ChordCounts = new Dictionary<KeyChord, int>();
        private Dictionary<Key, double> DownFor = new Dictionary<Key, double>();

        private object _lock = new object();
        private KeyCombo _previousCombo = null;
        private DateTime _previousComboAt = DateTime.MinValue;

        public void CountMinutes(int minutes)
        {
            RuntimeMinutes += minutes;
        }

        public void CountKey(Key key, TimeSpan downFor)
        {
            lock (_lock)
            {
                KeyCounts.IncSafe(key);
                if (downFor < TimeSpan.FromMinutes(2)) // just make sure the data is sane
                    DownFor.IncSafe(key, downFor.TotalSeconds);
            }
        }

        public void CountCombo(KeyCombo combo)
        {
            lock (_lock)
            {
                ComboCounts.IncSafe(combo);
                if (_previousCombo != null && DateTime.UtcNow - _previousComboAt < TimeSpan.FromSeconds(3))
                    ChordCounts.IncSafe(new KeyChord(_previousCombo, combo));
                _previousCombo = combo;
                _previousComboAt = DateTime.UtcNow;
            }
        }

        public void CountMouseMove(int deltaX, int deltaY)
        {
            MouseTravelX += Math.Abs(deltaX);
            MouseTravelY += Math.Abs(deltaY);
            MouseTravel += Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public void CountKeyboardUse(TimeSpan time)
        {
            KeyboardUseSeconds += time.TotalSeconds;
        }

        public void CountMouseUse(TimeSpan time)
        {
            MouseUseSeconds += time.TotalSeconds;
        }

        private static string getFullFileName(string file)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InputFrequency");
            Directory.CreateDirectory(path);
            return Path.Combine(path, file);
        }

        public void Save()
        {
            lock (_lock)
                using (var file = new StreamWriter(File.Open(getFullFileName("Data.csv"), FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    file.WriteLine("RuntimeMinutes," + RuntimeMinutes);
                    file.WriteLine("KeyboardUseSeconds," + KeyboardUseSeconds);
                    file.WriteLine("MouseUseSeconds," + MouseUseSeconds);
                    file.WriteLine("MouseTravelX," + MouseTravelX);
                    file.WriteLine("MouseTravelY," + MouseTravelY);
                    file.WriteLine("MouseTravel," + MouseTravel);
                    foreach (var kvp in KeyCounts)
                        file.WriteLine("KeyCounts," + kvp.Value + "," + (int) kvp.Key);
                    foreach (var kvp in ComboCounts)
                        file.WriteLine("ComboCounts," + kvp.Value + "," + kvp.Key.ToCsv());
                    foreach (var kvp in ChordCounts)
                        file.WriteLine("ChordCounts," + kvp.Value + "," + kvp.Key.ToCsv());
                    foreach (var kvp in DownFor)
                        file.WriteLine("DownFor," + kvp.Value + "," + (int) kvp.Key);
                }
        }

        public static Statistics Load()
        {
            try
            {
                var result = new Statistics();
                foreach (var line in File.ReadLines(getFullFileName("Data.csv")))
                {
                    var cols = line.Split(',');
                    if (cols.Length == 0)
                        continue;
                    if (cols[0] == "RuntimeMinutes")
                        result.RuntimeMinutes = int.Parse(cols[1]);
                    else if (cols[0] == "KeyboardUseSeconds")
                        result.KeyboardUseSeconds = double.Parse(cols[1]);
                    else if (cols[0] == "MouseUseSeconds")
                        result.MouseUseSeconds = double.Parse(cols[1]);
                    else if (cols[0] == "MouseTravelX")
                        result.MouseTravelX = int.Parse(cols[1]);
                    else if (cols[0] == "MouseTravelY")
                        result.MouseTravelY = int.Parse(cols[1]);
                    else if (cols[0] == "MouseTravel")
                        result.MouseTravel = double.Parse(cols[1]);
                    else if (cols[0] == "KeyCounts")
                        result.KeyCounts.Add((Key) int.Parse(cols[2]), int.Parse(cols[1]));
                    else if (cols[0] == "ComboCounts")
                        result.ComboCounts.Add(KeyCombo.ParseCsv(cols[2]), int.Parse(cols[1]));
                    else if (cols[0] == "ChordCounts")
                        result.ChordCounts.Add(KeyChord.ParseCsv(cols.Subarray(2)), int.Parse(cols[1]));
                    else if (cols[0] == "DownFor")
                        result.DownFor.Add((Key) int.Parse(cols[2]), double.Parse(cols[1]));
                }
                return result;
            }
            catch
            {
                try
                {
                    string newName;
                    int counter = 1;
                    while (File.Exists(newName = getFullFileName("Data.Old.{0}.csv".Fmt(counter))))
                        counter++;
                    File.Move(getFullFileName("Data.csv"), newName);
                }
                catch { }
                return new Statistics();
            }
        }

        /// <summary>
        /// Generates a human-readable report detailing the interesting stuff about the stats.
        /// </summary>
        public void GenerateReport()
        {
            lock (_lock)
                try
                {
                    using (var file = new StreamWriter(File.Open(getFullFileName("InputFrequency Report.txt"), FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {
                        file.WriteLine("=== GENERAL ===");
                        file.WriteLine("Stats monitored for " + TimeSpan.FromMinutes(RuntimeMinutes).ToString("d' days 'h' hours 'm' minutes'"));
                        file.WriteLine("Keyboard used for " + TimeSpan.FromSeconds(KeyboardUseSeconds).ToString("d' days 'h' hours 'm' minutes'"));
                        file.WriteLine("Mouse used for " + TimeSpan.FromSeconds(MouseUseSeconds).ToString("d' days 'h' hours 'm' minutes'"));
                        if (KeyboardUseSeconds > MouseUseSeconds)
                            file.WriteLine("Keyboard : Mouse ratio = {0:0.00}".Fmt(KeyboardUseSeconds / MouseUseSeconds));
                        else
                            file.WriteLine("Mouse : Keyboard ratio = {0:0.00}".Fmt(MouseUseSeconds / KeyboardUseSeconds));
                        int totalKeyCount = KeyCounts.Sum(kvp => kvp.Value);
                        file.WriteLine("Total key/button presses: {0:#,0}".Fmt(totalKeyCount));
                        file.WriteLine("Total key/button-down-time: {0} (note: two keys held for 1 second in parallel add up to 2 seconds key-down-time)".Fmt(
                            TimeSpan.FromSeconds(DownFor.Sum(kvp => kvp.Value)).ToString("d' days 'h' hours 'm' minutes'")));
                        file.WriteLine("Total mouse travel: {0:#,0} pixels (X axis: {1:#,0}, Y axis: {2:#,0})".Fmt(MouseTravel, MouseTravelX, MouseTravelY));
                        file.WriteLine();
                        file.WriteLine("=== KEY CLASS USAGE ===");
                        file.WriteLine("Mouse buttons:      " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsMouseButton()).Sum(kvp => kvp.Value)));
                        file.WriteLine("Mouse wheel:       " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsMouseWheel()).Sum(kvp => kvp.Value)));
                        file.WriteLine("Modifier keys:       " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsModifierKey()).Sum(kvp => kvp.Value)));
                        file.WriteLine("Navigation (Arrows/Home/End/PgUp/PgDn): " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsNavigationKey()).Sum(kvp => kvp.Value)));
                        file.WriteLine("Function keys:       " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsFunctionKey()).Sum(kvp => kvp.Value)));
                        file.WriteLine("Numpad keys:        " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsNumpadKey()).Sum(kvp => kvp.Value)));
                        file.WriteLine("Media/fancy keys:      " + keyAndPercentage(totalKeyCount, KeyCounts.Where(kvp => kvp.Key.IsMediaFancyKey()).Sum(kvp => kvp.Value)));
                        file.WriteLine();
                        file.WriteLine("=== KEY USAGE ===");
                        file.WriteLine("A key is used once every time it is pushed down. Auto-repetitions are not counted. Alt+Tab+Tab+Tab counts Alt once.");
                        foreach (var line in KeyCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:#,0}".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                        file.WriteLine();
                        file.WriteLine("=== KEY DOWN DURATION ===");
                        foreach (var line in DownFor.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:#,0} seconds".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                        file.WriteLine();
                        file.WriteLine("=== COMBO USAGE ===");
                        file.WriteLine("A combo excludes modifier keys except if nothing else is pressed. Ctrl+Shift+K doesn't count Ctrl or Ctrl+Shift, but Ctrl+Shift alone would be counted.");
                        foreach (var line in ComboCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,30} {1,7:#,0}".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                        file.WriteLine();
                        file.WriteLine("=== CHORD USAGE ===");
                        file.WriteLine("A chord consists of two consecutive combos pressed within at most 3 seconds. Repetitions of the same combo are filtered out. Only showing top 100 chords.");
                        foreach (var line in ChordCounts.OrderByDescending(kvp => kvp.Value)
                            .Where(kvp => kvp.Key.Combos.Length == 2 && !kvp.Key.Combos[0].Equals(kvp.Key.Combos[1])).Take(100)
                            .Select(kvp => "  {0,45} {1,7:#,0}".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                    }
                }
                catch { }
        }

        private string keyAndPercentage(int total, int value)
        {
            return "{0:0.00}% ({1:#,0})".Fmt(value / (double) total * 100.0, value);
        }

        public static void SaveDebugLine(string line)
        {
            using (var file = new StreamWriter(File.Open(getFullFileName("debug.txt"), FileMode.Append, FileAccess.Write, FileShare.Read)))
                file.WriteLine("[{0}] {1}", DateTime.Now, line);
        }

        public static void SaveCrashReport(string where, Exception e)
        {
            using (var file = new StreamWriter(File.Open(getFullFileName("debug.txt"), FileMode.Append, FileAccess.Write, FileShare.Read)))
            {
                file.WriteLine();
                file.WriteLine();
                file.WriteLine();
                file.WriteLine("Crash at " + DateTime.Now + " in " + where);
                while (e != null)
                {
                    file.WriteLine(e.GetType() + ": " + e.Message);
                    file.WriteLine(e.StackTrace);
                    file.WriteLine();
                    e = e.InnerException;
                }
            }
        }
    }

}
