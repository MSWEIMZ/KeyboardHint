using KeyboardHint.Native;

namespace KeyboardHint.Overlay;

/// <summary>Maps Win32 virtual-key codes to the key ids used in the layout
/// model and in shortcuts/*.json files.</summary>
public static class KeyIds
{
    public static string? FromVk(int vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),                 // A-Z
        >= 0x30 and <= 0x39 => "D" + (char)vk,                        // D0..D9
        >= 0x70 and <= 0x7B => "F" + (vk - 0x70 + 1).ToString(),      // F1-F12
        Win32.VK_ESCAPE => "Esc",
        Win32.VK_BACK => "Back",
        Win32.VK_TAB => "Tab",
        Win32.VK_CAPITAL => "Caps",
        Win32.VK_RETURN => "Enter",
        Win32.VK_SPACE => "Space",
        Win32.VK_PRIOR => "PgUp",
        Win32.VK_NEXT => "PgDn",
        Win32.VK_END => "End",
        Win32.VK_HOME => "Home",
        Win32.VK_INSERT => "Ins",
        Win32.VK_DELETE => "Del",
        Win32.VK_LEFT => "Left",
        Win32.VK_UP => "Up",
        Win32.VK_RIGHT => "Right",
        Win32.VK_DOWN => "Down",
        Win32.VK_LSHIFT => "LShift",
        Win32.VK_RSHIFT => "RShift",
        Win32.VK_LCONTROL => "LControl",
        Win32.VK_RCONTROL => "RControl",
        Win32.VK_LMENU => "LAlt",
        Win32.VK_RMENU => "RAlt",
        Win32.VK_LWIN => "LWin",
        Win32.VK_RWIN => "RWin",
        Win32.VK_OEM_3 => "Oem3",
        Win32.VK_OEM_MINUS => "OemMinus",
        Win32.VK_OEM_PLUS => "OemPlus",
        Win32.VK_OEM_COMMA => "OemComma",
        Win32.VK_OEM_PERIOD => "OemPeriod",
        Win32.VK_OEM_1 => "Oem1",
        Win32.VK_OEM_2 => "Oem2",
        Win32.VK_OEM_4 => "Oem4",
        Win32.VK_OEM_5 => "Oem5",
        Win32.VK_OEM_6 => "Oem6",
        Win32.VK_OEM_7 => "Oem7",
        _ => null
    };
}
