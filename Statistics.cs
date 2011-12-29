using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InputFrequency
{
    sealed class Statistics
    {
        private int RuntimeMinutes = 0;
        private Dictionary<Key, int> KeyCounts = new Dictionary<Key, int>();
        private Dictionary<KeyCombo, int> ComboCounts = new Dictionary<KeyCombo, int>();
        private Dictionary<KeyChord, int> ChordCounts = new Dictionary<KeyChord, int>();
        private Dictionary<Key, double> DownFor = new Dictionary<Key, double>();

        private object _lock = new object();
        private KeyCombo _previousCombo = null;

        public void CountMinutes(int minutes)
        {
            lock (_lock)
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
                if (_previousCombo != null)
                    ChordCounts.IncSafe(new KeyChord(_previousCombo, combo));
                _previousCombo = combo;
            }
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
                        file.WriteLine("=== KEY USAGE ===");
                        foreach (var line in KeyCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:0,0}".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                        file.WriteLine();
                        file.WriteLine("=== KEY DOWN DURATION ===");
                        foreach (var line in DownFor.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,15} {1,7:0,0} seconds".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                        file.WriteLine();
                        file.WriteLine("=== COMBO USAGE ===");
                        foreach (var line in ComboCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "  {0,30} {1,7:0,0}".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                        file.WriteLine();
                        file.WriteLine("=== CHORD USAGE ===");
                        foreach (var line in ChordCounts.OrderByDescending(kvp => kvp.Value).Take(100).Select(kvp => "  {0,45} {1,7:0,0}".Fmt(kvp.Key, kvp.Value)))
                            file.WriteLine(line);
                    }
                }
                catch { }
        }
    }

}
