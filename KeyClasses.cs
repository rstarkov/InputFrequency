using System;
using System.Linq;

namespace InputFrequency
{
    sealed class KeyCombo : IEquatable<KeyCombo>
    {
        public bool LWin, RWin;
        public bool LCtrl, RCtrl, Ctrl;
        public bool LAlt, RAlt, Alt;
        public bool LShift, RShift, Shift;
        public Key Key;

        public KeyCombo(Key key, bool[] keyDown)
        {
            Key = key;

            LWin = key != Key.LWin && keyDown[(int) Key.LWin];
            RWin = key != Key.RWin && keyDown[(int) Key.RWin];

            LCtrl = key != Key.LCtrl && keyDown[(int) Key.LCtrl];
            RCtrl = key != Key.RCtrl && keyDown[(int) Key.RCtrl];
            Ctrl = key != Key.Ctrl && keyDown[(int) Key.Ctrl];

            LAlt = key != Key.LAlt && keyDown[(int) Key.LAlt];
            RAlt = key != Key.RAlt && keyDown[(int) Key.RAlt];
            Alt = key != Key.Alt && keyDown[(int) Key.Alt];

            LShift = key != Key.LShift && keyDown[(int) Key.LShift];
            RShift = key != Key.RShift && keyDown[(int) Key.RShift];
            Shift = key != Key.Shift && keyDown[(int) Key.Shift];
        }

        private KeyCombo() { }

        public override int GetHashCode()
        {
            int result = (int) Key;
            if (LWin) result |= 0x00000100;
            if (RWin) result |= 0x00000200;
            if (LCtrl) result |= 0x00001000;
            if (RCtrl) result |= 0x00002000;
            if (Ctrl) result |= 0x00004000;
            if (LAlt) result |= 0x00010000;
            if (RAlt) result |= 0x00020000;
            if (Alt) result |= 0x00040000;
            if (LShift) result |= 0x00100000;
            if (RShift) result |= 0x00200000;
            if (Shift) result |= 0x00400000;
            return result;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as KeyCombo);
        }

        public bool Equals(KeyCombo other)
        {
            return other != null && Key == other.Key
                && LWin == other.LWin && RWin == other.RWin
                && LCtrl == other.LCtrl && RCtrl == other.RCtrl && Ctrl == other.Ctrl
                && LAlt == other.LAlt && RAlt == other.RAlt && Alt == other.Alt
                && LShift == other.LShift && RShift == other.RShift && Shift == other.Shift;
        }

        public override string ToString()
        {
            return (LWin ? "LWin+" : "") + (RWin ? "RWin+" : "")
                + (LCtrl ? "LCtrl+" : "") + (RCtrl ? "RCtrl+" : "")
                + (LAlt ? "LAlt+" : "") + (RAlt ? "RAlt+" : "")
                + (LShift ? "LShift+" : "") + (RShift ? "RShift+" : "")
                + Key.ToName();
        }

        public string ToCsv()
        {
            return (LWin ? "LWin+" : "") + (RWin ? "RWin+" : "")
                + (LCtrl ? "LCtrl+" : "") + (RCtrl ? "RCtrl+" : "")
                + (LAlt ? "LAlt+" : "") + (RAlt ? "RAlt+" : "")
                + (LShift ? "LShift+" : "") + (RShift ? "RShift+" : "")
                + (int) Key;
        }

        public static KeyCombo ParseCsv(string value)
        {
            var parts = value.Split('+');
            var result = new KeyCombo();
            result.Key = (Key) int.Parse(parts[parts.Length - 1]);
            for (int i = 0; i < parts.Length - 1; i++)
                switch (parts[i])
                {
                    case "LWin": result.LWin = true; continue;
                    case "RWin": result.RWin = true; continue;
                    case "LCtrl": result.LCtrl = true; continue;
                    case "RCtrl": result.RCtrl = true; continue;
                    case "Ctrl": result.Ctrl = true; continue;
                    case "LAlt": result.LAlt = true; continue;
                    case "RAlt": result.RAlt = true; continue;
                    case "Alt": result.Alt = true; continue;
                    case "LShift": result.LShift = true; continue;
                    case "RShift": result.RShift = true; continue;
                    case "Shift": result.Shift = true; continue;
                    default: throw new Exception();
                }
            return result;
        }
    }

    sealed class KeyChord : IEquatable<KeyChord>
    {
        public KeyCombo[] Combos { get; private set; }

        public KeyChord(params KeyCombo[] combos)
        {
            Combos = combos;
        }

        public override int GetHashCode()
        {
            int result = 0;
            foreach (var combo in Combos)
                result = result * 17 + combo.GetHashCode();
            return result;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as KeyChord);
        }

        public bool Equals(KeyChord other)
        {
            return other != null && Combos.SequenceEqual(other.Combos);
        }

        public override string ToString()
        {
            return string.Join(", ", Combos.Select(c => c.ToString()));
        }

        public string ToCsv()
        {
            return string.Join(",", Combos.Select(c => c.ToCsv()));
        }

        public static KeyChord ParseCsv(string[] parts)
        {
            return new KeyChord(parts.Select(p => KeyCombo.ParseCsv(p)).ToArray());
        }
    }

    enum Key
    {
        MouseLeft = 1,
        MouseRight = 2,
        Break = 3,
        MouseMiddle = 4,
        MouseBack = 5,
        MouseForward = 6,
        Backspace = 8,
        Tab = 9,
        LineFeed = 10,
        Clear = 12, // Produced by NumPad5 without NumLock
        Enter = 13,
        Shift = 16,
        Ctrl = 17,
        Alt = 18,
        Pause = 19,
        CapsLock = 20,
        KanaMode = 21,
        JunjaMode = 23,
        FinalMode = 24,
        KanjiMode = 25,
        Escape = 27,
        IMEConvert = 28,
        IMENonconvert = 29,
        IMEAccept = 30,
        IMEModeChange = 31,
        Space = 32,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Select = 41,
        Print = 42,
        Execute = 43,
        PrintScreen = 44,
        Insert = 45,
        Delete = 46,
        Help = 47,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        LWin = 91,
        RWin = 92,
        Apps = 93,
        Sleep = 95,
        NumPad0 = 96,
        NumPad1 = 97,
        NumPad2 = 98,
        NumPad3 = 99,
        NumPad4 = 100,
        NumPad5 = 101,
        NumPad6 = 102,
        NumPad7 = 103,
        NumPad8 = 104,
        NumPad9 = 105,
        NumMultiply = 106,
        NumAdd = 107,
        NumSeparator = 108,
        NumSubtract = 109,
        NumDecimal = 110,
        NumDivide = 111,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        F13 = 124,
        F14 = 125,
        F15 = 126,
        F16 = 127,
        F17 = 128,
        F18 = 129,
        F19 = 130,
        F20 = 131,
        F21 = 132,
        F22 = 133,
        F23 = 134,
        F24 = 135,
        NumLock = 144,
        ScrollLock = 145,
        LShift = 160,
        RShift = 161,
        LCtrl = 162,
        RCtrl = 163,
        LAlt = 164,
        RAlt = 165,
        BrowserBack = 166,
        BrowserForward = 167,
        BrowserRefresh = 168,
        BrowserStop = 169,
        BrowserSearch = 170,
        BrowserFavorites = 171,
        BrowserHome = 172,
        VolumeMute = 173,
        VolumeDown = 174,
        VolumeUp = 175,
        MediaNextTrack = 176,
        MediaPreviousTrack = 177,
        MediaStop = 178,
        MediaPlayPause = 179,
        LaunchMail = 180,
        LaunchMedia = 181,
        LaunchApplication1 = 182,
        LaunchCalculator = 183,
        OemSemicolon = 186,
        OemPlus = 187,
        OemComma = 188,
        OemMinus = 189,
        OemPeriod = 190,
        OemQuestion = 191,
        OemTilde = 192,
        OemOpenBracket = 219,
        OemPipe = 220,
        OemCloseBracket = 221,
        OemQuotes = 222,
        OemBacktick = 223,
        OemBackslash = 226,
        ProcessKey = 229,
        Packet = 231,
        Attn = 246,
        Crsel = 247,
        Exsel = 248,
        EraseEof = 249,
        Play = 250,
        Zoom = 251,
        NoName = 252,
        Pa1 = 253,
        OemClear = 254,
        // Not in the standard Keys enum
        MouseWheelUp = 256,
        MouseWheelDown = 257,
        MouseWheelLeft = 258,
        MouseWheelRight = 259,
    }
}
