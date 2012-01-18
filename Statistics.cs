using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace InputFrequency
{
    sealed class Statistics
    {
        private int RuntimeMinutes = 0;
        private double KeyboardUseSeconds = 0;
        private double MouseUseSeconds = 0;
        private int MouseTravelX = 0, MouseTravelY = 0;
        private double MouseTravel = 0;
        private double MouseTravelScreensX = 0, MouseTravelScreensY = 0;
        private double MouseTravelScreens = 0;
        private Dictionary<Key, int> KeyCounts = new Dictionary<Key, int>();
        private Dictionary<KeyCombo, int> ComboCounts = new Dictionary<KeyCombo, int>();
        private Dictionary<KeyChord, int> ChordCounts = new Dictionary<KeyChord, int>();
        private Dictionary<Key, double> DownFor = new Dictionary<Key, double>();

        private object _lock = new object();
        private KeyCombo _previousCombo = null;
        private DateTime _previousComboAt = DateTime.MinValue;
        private double _virtualDesktopWidth, _virtualDesktopHeight;
        private DateTime _virtualDesktopLastRefresh;

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
            if (DateTime.UtcNow - _virtualDesktopLastRefresh > TimeSpan.FromMinutes(5))
            {
                var size = SystemInformation.VirtualScreen;
                _virtualDesktopWidth = size.Width;
                _virtualDesktopHeight = size.Height;
                _virtualDesktopLastRefresh = DateTime.UtcNow;
            }
            double deltaScreensX = deltaX / _virtualDesktopWidth;
            double deltaScreensY = deltaY / _virtualDesktopHeight;
            MouseTravelScreensX += Math.Abs(deltaScreensX);
            MouseTravelScreensY += Math.Abs(deltaScreensY);
            MouseTravelScreens += Math.Sqrt(deltaScreensX * deltaScreensX + deltaScreensY * deltaScreensY);
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
                    file.WriteLine("RuntimeMinutes," + RuntimeMinutes.ToStringInv());
                    file.WriteLine("KeyboardUseSeconds," + KeyboardUseSeconds.ToStringInv());
                    file.WriteLine("MouseUseSeconds," + MouseUseSeconds.ToStringInv());
                    file.WriteLine("MouseTravelX," + MouseTravelX.ToStringInv());
                    file.WriteLine("MouseTravelY," + MouseTravelY.ToStringInv());
                    file.WriteLine("MouseTravel," + MouseTravel.ToStringInv());
                    file.WriteLine("MouseTravelScreensX," + MouseTravelScreensX.ToStringInv());
                    file.WriteLine("MouseTravelScreensY," + MouseTravelScreensY.ToStringInv());
                    file.WriteLine("MouseTravelScreens," + MouseTravelScreens.ToStringInv());
                    foreach (var kvp in KeyCounts)
                        file.WriteLine("KeyCounts," + kvp.Value.ToStringInv() + "," + kvp.Key.ToStringInv());
                    foreach (var kvp in ComboCounts)
                        file.WriteLine("ComboCounts," + kvp.Value.ToStringInv() + "," + kvp.Key.ToCsv());
                    foreach (var kvp in ChordCounts)
                        file.WriteLine("ChordCounts," + kvp.Value.ToStringInv() + "," + kvp.Key.ToCsv());
                    foreach (var kvp in DownFor)
                        file.WriteLine("DownFor," + kvp.Value.ToStringInv() + "," + kvp.Key.ToStringInv());
                }
        }

        public static Statistics Load(string filename = null)
        {
            var fname = filename ?? getFullFileName("Data.csv");
            if (filename == null && !File.Exists(fname))
                return new Statistics(); // without saving a crash report

            string lastLine = "";
            try
            {
                var result = new Statistics();
                foreach (var line in File.ReadLines(fname))
                {
                    lastLine = line;
                    var cols = line.Split(',');
                    if (cols.Length == 0)
                        continue;
                    if (cols[0] == "RuntimeMinutes")
                        result.RuntimeMinutes = cols[1].ParseIntInv();
                    else if (cols[0] == "KeyboardUseSeconds")
                        result.KeyboardUseSeconds = cols[1].ParseDoubleInv();
                    else if (cols[0] == "MouseUseSeconds")
                        result.MouseUseSeconds = cols[1].ParseDoubleInv();
                    else if (cols[0] == "MouseTravelX")
                        result.MouseTravelX = cols[1].ParseIntInv();
                    else if (cols[0] == "MouseTravelY")
                        result.MouseTravelY = cols[1].ParseIntInv();
                    else if (cols[0] == "MouseTravel")
                        result.MouseTravel = cols[1].ParseDoubleInv();
                    else if (cols[0] == "MouseTravelScreensX")
                        result.MouseTravelScreensX = cols[1].ParseDoubleInv();
                    else if (cols[0] == "MouseTravelScreensY")
                        result.MouseTravelScreensY = cols[1].ParseDoubleInv();
                    else if (cols[0] == "MouseTravelScreens")
                        result.MouseTravelScreens = cols[1].ParseDoubleInv();
                    else if (cols[0] == "KeyCounts")
                        result.KeyCounts.Add(cols[2].ParseKeyInv(), cols[1].ParseIntInv());
                    else if (cols[0] == "ComboCounts")
                        result.ComboCounts.Add(KeyCombo.ParseCsv(cols[2]), cols[1].ParseIntInv());
                    else if (cols[0] == "ChordCounts")
                        result.ChordCounts.Add(KeyChord.ParseCsv(cols.Subarray(2)), cols[1].ParseIntInv());
                    else if (cols[0] == "DownFor")
                        result.DownFor.Add(cols[2].ParseKeyInv(), cols[1].ParseDoubleInv());
                }
                return result;
            }
            catch (Exception e)
            {
                SaveDebugLine("Statistics.Load crashing line: " + lastLine);
                SaveCrashReport("Statistics.Load", e);
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
        public void GenerateReport(string filename = null)
        {
            lock (_lock)
                try
                {
                    using (var file = new StreamWriter(File.Open(filename ?? getFullFileName("InputFrequency Report.html"), FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {
                        file.WriteLine("<!DOCTYPE HTML>");
                        file.WriteLine("<html><head>");
                        file.WriteLine(@"<style type='text/css'>
                            table { border-collapse:collapse; }
                            table, th, td { border: 1px solid #ccc; }
                            td { text-align: right; }
                            th, td { padding: 2px 8px; }
                        ");
                        file.WriteLine("</style>");

                        file.WriteLine("</head><body>");

                        reportGeneralSummary(file);
                        reportKeyClasses(file);
                        reportModifiers(file);
                        reportKeyUsage(file);
                        reportKeyDownDuration(file);
                        reportComboUsage(file);
                        reportChordUsage(file);

                        file.WriteLine("</body></html>");
                    }
                }
                catch { }
        }

        private void reportGeneralSummary(StreamWriter file)
        {
            file.WriteLine("<h1>General</h1>");
            file.WriteLine("<p><b>Stats monitored for:</b> " + TimeSpan.FromMinutes(RuntimeMinutes).ToString("d' days 'h' hours 'm' minutes'"));
            file.WriteLine("<p><b>Keyboard used for:</b> " + TimeSpan.FromSeconds(KeyboardUseSeconds).ToString("d' days 'h' hours 'm' minutes'")
                                    + " ({0:0.0}% of computer ON time)".Fmt(KeyboardUseSeconds / 60.0 / RuntimeMinutes * 100.0));
            file.WriteLine("<br><b>Mouse used for:</b> " + TimeSpan.FromSeconds(MouseUseSeconds).ToString("d' days 'h' hours 'm' minutes'")
                                    + " ({0:0.0}% of computer ON time)".Fmt(MouseUseSeconds / 60.0 / RuntimeMinutes * 100.0));
            if (KeyboardUseSeconds > MouseUseSeconds)
                file.WriteLine("<br><b>Keyboard : Mouse ratio:</b> {0:0.00}".Fmt(KeyboardUseSeconds / MouseUseSeconds));
            else
                file.WriteLine("<br><b>Mouse : Keyboard ratio:</b> {0:0.00}".Fmt(MouseUseSeconds / KeyboardUseSeconds));
            int totalKeyCount = KeyCounts.Sum(kvp => kvp.Value);
            file.WriteLine("<p><b>Total key/button presses:</b> {0:#,0}".Fmt(totalKeyCount));
            file.WriteLine("<br><b>Total key/button-down-time:</b> {0} (note: two keys held for 1 second in parallel add up to 2 seconds key-down-time)".Fmt(
                TimeSpan.FromSeconds(DownFor.Sum(kvp => kvp.Value)).ToString("d' days 'h' hours 'm' minutes'")));
            file.WriteLine("<p><b>Total mouse travel:</b> {0:#,0} pixels (X axis: {1:#,0}, Y axis: {2:#,0})".Fmt(MouseTravel, MouseTravelX, MouseTravelY));
            file.WriteLine("<br><b>Total mouse travel:</b> {0:#,0.0} screens (X axis: {1:#,0.0}, Y axis: {2:#,0.0})".Fmt(MouseTravelScreens, MouseTravelScreensX, MouseTravelScreensY));
        }

        private void reportKeyClasses(StreamWriter file)
        {
            file.WriteLine("<h1>Key class usage</h1>");
            file.WriteLine("<p class='help'>Shows the total number of times each individual key was pushed down, regardless of other keys being pushed.</p>");
            file.WriteLine("<table>");
            file.WriteLine("<tr><th></th><th>Down events</th><th>Down duration</th></tr>");

            double totalKeyCount = KeyCounts.Sum(kvp => kvp.Value);
            double totalDownFor = DownFor.Sum(kvp => kvp.Value);

            var write = Ut.Lambda((string name, Func<Key, bool> filter) =>
            {
                int count = KeyCounts.Where(kvp => filter(kvp.Key)).Sum(kvp => kvp.Value);
                double down = DownFor.Where(kvp => filter(kvp.Key)).Sum(kvp => kvp.Value);
                file.WriteLine("<tr><th>{0}</th><td title='{1:#,0}'>{2:0.0}%</td><td title='{3:#,0} seconds'>{4:0.0}%</td></tr>".Fmt(name,
                    count, count / totalKeyCount * 100.0,
                    down, down / totalDownFor * 100.0));
            });

            write("Mouse buttons", key => key.IsMouseButton());
            write("Mouse wheel", key => key.IsMouseWheel());
            write("Arrow keys", key => key.IsArrowKey());
            write("Home/End/Page", key => key.IsHEPGKey());
            write("Function keys", key => key.IsFunctionKey());
            write("Numpad keys", key => key.IsNumpadKey());
            write("Media/fancy keys", key => key.IsMediaFancyKey());
            write("Modifier keys", key => key.IsModifierKey());
            write("Character keys", key => key.IsCharacterKey());
            write("Other", key => !key.IsMouseButton() && !key.IsMouseWheel() && !key.IsArrowKey() && !key.IsHEPGKey() && !key.IsFunctionKey()
                && !key.IsNumpadKey() && !key.IsMediaFancyKey() && !key.IsModifierKey() && !key.IsCharacterKey());
            file.WriteLine("</table>");
        }

        private void reportModifiers(StreamWriter file)
        {
            file.WriteLine("<h1>Modifier key usage</h1>");

            file.WriteLine("<h2>By combo count</h2>");
            file.WriteLine("<p class='help'>Shows the total number of key combos that used a modifier key; for example, Alt+Tab+Tab would count Alt twice.</p>");
            file.WriteLine("<table>");
            file.WriteLine("<tr><th></th><th>Ctrl</th><th>Alt</th><th>Shift</th><th>Win</th><th>TOTAL</th></tr>");
            int leftTotal = 0, rightTotal = 0;
            var modifierRow = Ut.Lambda((string title, bool left, bool right) =>
            {
                int ctrl = ComboCounts.Where(kvp => (left && kvp.Key.LCtrl) || (right && kvp.Key.RCtrl)).Sum(kvp => kvp.Value);
                int alt = ComboCounts.Where(kvp => (left && kvp.Key.LAlt) || (right && kvp.Key.RAlt)).Sum(kvp => kvp.Value);
                int shift = ComboCounts.Where(kvp => (left && kvp.Key.LShift) || (right && kvp.Key.RShift)).Sum(kvp => kvp.Value);
                int win = ComboCounts.Where(kvp => (left && kvp.Key.LWin) || (right && kvp.Key.RWin)).Sum(kvp => kvp.Value);
                file.WriteLine("<tr><th>{0}</th><td>{1:#,0}</td><td>{2:#,0}</td><td>{3:#,0}</td><td>{4:#,0}</td><td>{5:#,0}</td></tr>"
                    .Fmt(title, ctrl, alt, shift, win, ctrl + alt + shift + win));
                if (left && !right) leftTotal = ctrl + alt + shift + win;
                if (!left && right) rightTotal = ctrl + alt + shift + win;
            });
            modifierRow("Left", true, false);
            modifierRow("Right", false, true);
            modifierRow("TOTAL", true, true);
            file.WriteLine("</table>");
            file.WriteLine("<p><b>Left : Right:</b> {0:0.00}</p>".Fmt(leftTotal / (double) (0.01 + rightTotal)));

            file.WriteLine("<h2>By down duration</h2>");
            file.WriteLine("<p class='help'>Shows the total duration, in seconds, that each modifier key was held down.</p>");
            file.WriteLine("<table>");
            file.WriteLine("<tr><th></th><th>Ctrl</th><th>Alt</th><th>Shift</th><th>Win</th><th>TOTAL</th></tr>");
            double leftTotalDur = 0, rightTotalDur = 0;
            var modifierRowDur = Ut.Lambda((string title, bool left, bool right) =>
            {
                double ctrl = DownFor.Where(kvp => (left && kvp.Key == Key.LCtrl) || (right && kvp.Key == Key.RCtrl)).Sum(kvp => kvp.Value);
                double alt = DownFor.Where(kvp => (left && kvp.Key == Key.LAlt) || (right && kvp.Key == Key.RAlt)).Sum(kvp => kvp.Value);
                double shift = DownFor.Where(kvp => (left && kvp.Key == Key.LShift) || (right && kvp.Key == Key.RShift)).Sum(kvp => kvp.Value);
                double win = DownFor.Where(kvp => (left && kvp.Key == Key.LWin) || (right && kvp.Key == Key.RWin)).Sum(kvp => kvp.Value);
                file.WriteLine("<tr><th>{0}</th><td>{1:#,0}</td><td>{2:#,0}</td><td>{3:#,0}</td><td>{4:#,0}</td><td>{5:#,0}</td></tr>"
                    .Fmt(title, ctrl, alt, shift, win, ctrl + alt + shift + win));
                if (left && !right) leftTotalDur = ctrl + alt + shift + win;
                if (!left && right) rightTotalDur = ctrl + alt + shift + win;
            });
            modifierRowDur("Left", true, false);
            modifierRowDur("Right", false, true);
            modifierRowDur("TOTAL", true, true);
            file.WriteLine("</table>");
            file.WriteLine("<p><b>Left : Right:</b> {0:0.00}</p>".Fmt(leftTotalDur / (double) (0.01 + rightTotalDur)));
        }

        private void reportKeyUsage(StreamWriter file)
        {
            file.WriteLine("<h1>Key Usage</h1>");
            file.WriteLine("<p class='help'>A key is used once every time it is pushed down. Auto-repetitions are not counted. Alt+Tab+Tab+Tab counts Alt once.</p>");
            file.WriteLine("<table>");
            double total = KeyCounts.Sum(kvp => kvp.Value);
            foreach (var line in KeyCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "<tr><th>{0}</th><td title='{1:#,0}'>{2:0.0}%</td></tr>".Fmt(kvp.Key, kvp.Value, kvp.Value / total * 100.0)))
                file.WriteLine(line);
            file.WriteLine("</table>");
        }

        private void reportKeyDownDuration(StreamWriter file)
        {
            file.WriteLine("<h1>Key Down Duration</h1>");
            file.WriteLine("<table>");
            double total = DownFor.Sum(kvp => kvp.Value);
            foreach (var line in DownFor.OrderByDescending(kvp => kvp.Value).Select(kvp => "<tr><th>{0}</th><td title='{1:#,0} seconds'>{2:0.0}%</td></tr>".Fmt(kvp.Key, kvp.Value, kvp.Value / total * 100.0)))
                file.WriteLine(line);
            file.WriteLine("</table>");
        }

        private void reportComboUsage(StreamWriter file)
        {
            file.WriteLine("<h1>Combo Usage</h1>");
            file.WriteLine("<p class='help'>A combo excludes modifier keys except if nothing else is pressed. Ctrl+Shift+K doesn't count Ctrl or Ctrl+Shift, but Ctrl+Shift alone would be counted.</p>");
            file.WriteLine("<table>");
            double total = ComboCounts.Sum(kvp => kvp.Value);
            foreach (var line in ComboCounts.OrderByDescending(kvp => kvp.Value).Select(kvp => "<tr><th>{0}</th><td title='{1:#,0}'>{2:0.0}%</td></tr>".Fmt(kvp.Key, kvp.Value, kvp.Value / total * 100.0)))
                file.WriteLine(line);
            file.WriteLine("</table>");
        }

        private void reportChordUsage(StreamWriter file)
        {
            file.WriteLine("<h1>Chord Usage</h1>");
            file.WriteLine("<p class='help'>A chord consists of two consecutive combos pressed within at most 3 seconds. Repetitions of the same combo are filtered out. Only showing top 100 chords.</p>");
            file.WriteLine("<table>");
            double total = ChordCounts.Sum(kvp => kvp.Value);
            foreach (var line in ChordCounts.OrderByDescending(kvp => kvp.Value)
                .Where(kvp => kvp.Key.Combos.Length == 2 && !kvp.Key.Combos[0].Equals(kvp.Key.Combos[1])).Take(100)
                .Select(kvp => "<tr><th>{0}</th><td title='{1:#,0}'>{2:0.0}%</td></tr>".Fmt(kvp.Key, kvp.Value, kvp.Value / total * 100.0)))
                file.WriteLine(line);
            file.WriteLine("</table>");
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
