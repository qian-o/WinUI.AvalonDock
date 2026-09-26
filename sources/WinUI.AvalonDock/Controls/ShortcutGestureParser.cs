// Parsing/validation rules adapted from dotnet/wpf v10.0.0 KeyGestureConverter,
// KeyConverter, ModifierKeysConverter and KeyGesture (MIT); see THIRD-PARTY-NOTICES.md.
using System.ComponentModel;
using System.Globalization;
using Windows.System;

namespace AvalonDock.Controls;

/// <summary>Preserves the invariant WPF gesture grammar used by IToolbox.Shortcut.</summary>
internal static partial class ShortcutGestureParser
{
    internal static bool TryParse(string? text, out VirtualKey key, out VirtualKeyModifiers modifiers)
    {
        key = VirtualKey.None;
        modifiers = VirtualKeyModifiers.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string gesture = text.Trim();
        int comma = gesture.IndexOf(',');
        if (comma >= 0)
        {
            gesture = gesture[..comma].Trim();
        }

        int separator = gesture.LastIndexOf('+');
        string keyToken = (separator >= 0 ? gesture[(separator + 1)..] : gesture).Trim();
        string modifiersToken = separator >= 0 ? gesture[..separator] : string.Empty;
        if (!TryKey(keyToken, out (int Key, int VirtualKey) parsed))
        {
            return false;
        }

        foreach (string token in modifiersToken.Split('+'))
        {
            string modifier = token.Trim();
            // The pinned converter stops at the first empty modifier token, even when
            // another token follows. Preserve this accepted malformed-input behavior.
            if (modifier.Length == 0)
            {
                break;
            }

            switch (UpperAscii(modifier))
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= VirtualKeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= VirtualKeyModifiers.Menu;
                    break;
                case "SHIFT":
                    modifiers |= VirtualKeyModifiers.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= VirtualKeyModifiers.Windows;
                    break;
                default:
                    modifiers = VirtualKeyModifiers.None;
                    return false;
            }
        }
        if (parsed.Key < 0 || parsed.Key > KeyNames["OemClear"].Key)
        {
            throw new InvalidEnumArgumentException(nameof(key), parsed.Key, typeof(VirtualKey));
        }

        bool functionOrNumpad = parsed.Key >= KeyNames["F1"].Key && parsed.Key <= KeyNames["F24"].Key
            || parsed.Key >= KeyNames["NumPad0"].Key && parsed.Key <= KeyNames["Divide"].Key;
        if (!functionOrNumpad)
        {
            if ((modifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Windows)) != 0)
            {
                if (parsed.Key is 118 or 119 or 120 or 121 or 70 or 71)
                {
                    modifiers = VirtualKeyModifiers.None;
                    return false;
                }
            }
            else if (parsed.Key >= KeyNames["D0"].Key && parsed.Key <= KeyNames["D9"].Key
                || parsed.Key >= KeyNames["A"].Key && parsed.Key <= KeyNames["Z"].Key)
            {
                modifiers = VirtualKeyModifiers.None;
                return false;
            }
        }
        key = (VirtualKey)parsed.VirtualKey;
        return true;
    }

    private static bool TryKey(string token, out (int Key, int VirtualKey) key)
    {
        if (token.Length == 0)
        {
            key = KeyNames["None"];
            return true;
        }
        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            char character = token[0] is >= 'a' and <= 'z' ? (char)(token[0] - 'a' + 'A') : token[0];
            if (char.IsAsciiDigit(character))
            {
                return KeyNames.TryGetValue("D" + character, out key);
            }

            if (char.IsAsciiLetterUpper(character))
            {
                return KeyNames.TryGetValue(character.ToString(), out key);
            }

            throw new ArgumentException("The shortcut contains a non-ASCII letter or digit.", nameof(token));
        }
        string name = UpperAscii(token) switch
        {
            "BS" or "BKSP" or "BACKSPACE" => "Back",
            "ALT" => "LeftAlt",
            "CTRL" or "CONTROL" => "LeftCtrl",
            "SHIFT" => "LeftShift",
            "WIN" or "WINDOWS" or "LEFTWINDOWS" => "LWin",
            "RIGHTWINDOWS" => "RWin",
            "DEL" => "Delete",
            "ESC" => "Escape",
            "INS" => "Insert",
            "PGDN" => "PageDown",
            "PGUP" => "PageUp",
            "ENTER" => "Return",
            "PRTSC" => "PrintScreen",
            "PIPE" => "OemPipe",
            "PLUS" => "OemPlus",
            "BREAK" => "Cancel",
            "COMMA" => "OemComma",
            "MINUS" => "OemMinus",
            "TILDE" => "OemTilde",
            "FINISH" => "OemFinish",
            "PERIOD" => "OemPeriod",
            "QUOTES" => "OemQuotes",
            "QUESTION" => "OemQuestion",
            "BACKSLASH" => "OemBackslash",
            "SEMICOLON" => "OemSemicolon",
            "APPLICATION" => "Apps",
            "OPENBRACKETS" => "OemOpenBrackets",
            "CLOSEBRACKETS" => "OemCloseBrackets",
            _ => token
        };
        if (KeyNames.TryGetValue(name, out key))
        {
            return true;
        }

        if (token.Length == 0 || !(char.IsAsciiDigit(token[0]) || token[0] is '+' or '-'))
        {
            throw new ArgumentException("The shortcut key name is not defined by WPF.", nameof(token));
        }

        int value;
        try
        {
            value = int.Parse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        catch (FormatException) { throw new ArgumentException("The shortcut key name is invalid.", nameof(token)); }
        key = (value, KeyNames.Values.FirstOrDefault(candidate => candidate.Key == value).VirtualKey);
        return true;
    }
    private static string UpperAscii(string text) => string.Create(text.Length, text, static (characters, source) =>
    {
        for (int index = 0; index < characters.Length; index++)
        {
            characters[index] = source[index] is >= 'a' and <= 'z' ? (char)(source[index] - 'a' + 'A') : source[index];
        }
    });
}
