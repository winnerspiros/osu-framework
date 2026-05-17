// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Input
{
    /// <summary>
    /// Represents a key on the keyboard.
    /// </summary>
    public enum Key
    {
        Unknown = 0,

        // Modifier keys
        ShiftLeft = 1,
        ShiftRight = 2,
        ControlLeft = 3,
        ControlRight = 4,
        AltLeft = 5,
        AltRight = 6,
        WinLeft = 7,
        WinRight = 8,
        Menu = 9,
        CapsLock = 10,
        NumLock = 11,
        ScrollLock = 12,

        // Aliases for modifier keys (SDL2 / legacy naming)
        LShift = ShiftLeft,
        RShift = ShiftRight,
        LControl = ControlLeft,
        RControl = ControlRight,
        LAlt = AltLeft,
        RAlt = AltRight,
        LWin = WinLeft,
        RWin = WinRight,
        RSuper = WinRight,

        // Function keys
        F1 = 20,
        F2 = 21,
        F3 = 22,
        F4 = 23,
        F5 = 24,
        F6 = 25,
        F7 = 26,
        F8 = 27,
        F9 = 28,
        F10 = 29,
        F11 = 30,
        F12 = 31,
        F13 = 32,
        F14 = 33,
        F15 = 34,
        F16 = 35,
        F17 = 36,
        F18 = 37,
        F19 = 38,
        F20 = 39,
        F21 = 40,
        F22 = 41,
        F23 = 42,
        F24 = 43,

        // Navigation / editing
        Up = 50,
        Down = 51,
        Left = 52,
        Right = 53,
        Insert = 54,
        Delete = 55,
        Home = 56,
        End = 57,
        PageUp = 58,
        PageDown = 59,

        // Lock / system
        PrintScreen = 60,
        Pause = 61,

        // Whitespace / editing
        BackSpace = 70,
        Tab = 71,
        Clear = 72,
        Enter = 73,
        Escape = 74,
        Space = 75,

        // Alphanumeric — digits row
        Number0 = 80,
        Number1 = 81,
        Number2 = 82,
        Number3 = 83,
        Number4 = 84,
        Number5 = 85,
        Number6 = 86,
        Number7 = 87,
        Number8 = 88,
        Number9 = 89,

        // Alphanumeric — letters
        A = 100,
        B = 101,
        C = 102,
        D = 103,
        E = 104,
        F = 105,
        G = 106,
        H = 107,
        I = 108,
        J = 109,
        K = 110,
        L = 111,
        M = 112,
        N = 113,
        O = 114,
        P = 115,
        Q = 116,
        R = 117,
        S = 118,
        T = 119,
        U = 120,
        V = 121,
        W = 122,
        X = 123,
        Y = 124,
        Z = 125,

        // Punctuation / symbols
        Quote = 130,
        Comma = 131,
        Minus = 132,
        Period = 133,
        Slash = 134,
        Semicolon = 135,
        Plus = 136,
        BracketLeft = 137,
        BackSlash = 138,
        BracketRight = 139,
        Tilde = 140,
        NonUsBackSlash = 141,

        // Keypad
        Keypad0 = 150,
        Keypad1 = 151,
        Keypad2 = 152,
        Keypad3 = 153,
        Keypad4 = 154,
        Keypad5 = 155,
        Keypad6 = 156,
        Keypad7 = 157,
        Keypad8 = 158,
        Keypad9 = 159,
        KeypadDecimal = 160,
        KeypadPeriod = KeypadDecimal,
        KeypadDivide = 161,
        KeypadMultiply = 162,
        KeypadMinus = 163,
        KeypadPlus = 164,
        KeypadEnter = 165,

        // Media keys
        Mute = 170,
        VolumeDown = 171,
        VolumeUp = 172,
        TrackNext = 173,
        TrackPrevious = 174,
        Stop = 175,
        PlayPause = 176,
        Sleep = 177,
    }
}
