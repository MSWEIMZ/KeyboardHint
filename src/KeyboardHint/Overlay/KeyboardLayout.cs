namespace KeyboardHint.Overlay;

public record KeyDef(string Id, string Label, double Width = 1)
{
    public static KeyDef Spacer(double w) => new("", "", w);
}

public record RowDef(double Height, List<KeyDef> Keys);

/// <summary>Compact laptop-style layout (no numpad). Widths are in key units.</summary>
public static class KeyboardLayout
{
    public const double Unit = 52;

    public static List<RowDef> Build() => new()
    {
        new(30, new()
        {
            new("Esc", "Esc"), KeyDef.Spacer(0.4),
            new("F1", "F1"), new("F2", "F2"), new("F3", "F3"), new("F4", "F4"), KeyDef.Spacer(0.3),
            new("F5", "F5"), new("F6", "F6"), new("F7", "F7"), new("F8", "F8"), KeyDef.Spacer(0.3),
            new("F9", "F9"), new("F10", "F10"), new("F11", "F11"), new("F12", "F12"), KeyDef.Spacer(0.4),
            new("Ins", "Ins", 0.9), new("Del", "Del", 0.9),
            new("Home", "Home", 0.9), new("End", "End", 0.9), new("PgUp", "PgUp", 0.9), new("PgDn", "PgDn", 0.9),
        }),
        new(50, new()
        {
            new("Oem3", "`"),
            new("D1", "1"), new("D2", "2"), new("D3", "3"), new("D4", "4"), new("D5", "5"),
            new("D6", "6"), new("D7", "7"), new("D8", "8"), new("D9", "9"), new("D0", "0"),
            new("OemMinus", "-"), new("OemPlus", "="), new("Back", "Back", 2),
        }),
        new(50, new()
        {
            new("Tab", "Tab", 1.5),
            new("Q", "Q"), new("W", "W"), new("E", "E"), new("R", "R"), new("T", "T"),
            new("Y", "Y"), new("U", "U"), new("I", "I"), new("O", "O"), new("P", "P"),
            new("Oem4", "["), new("Oem6", "]"), new("Oem5", "\\", 1.5),
        }),
        new(50, new()
        {
            new("Caps", "Caps", 1.75),
            new("A", "A"), new("S", "S"), new("D", "D"), new("F", "F"), new("G", "G"),
            new("H", "H"), new("J", "J"), new("K", "K"), new("L", "L"),
            new("Oem1", ";"), new("Oem7", "'"), new("Enter", "Enter", 2.25),
        }),
        new(50, new()
        {
            new("LShift", "Shift", 2.25),
            new("Z", "Z"), new("X", "X"), new("C", "C"), new("V", "V"), new("B", "B"),
            new("N", "N"), new("M", "M"),
            new("OemComma", ","), new("OemPeriod", "."), new("Oem2", "/"),
            new("RShift", "Shift", 2.75),
        }),
        new(50, new()
        {
            new("LControl", "Ctrl", 1.4), new("LWin", "Win", 1.2), new("LAlt", "Alt", 1.2),
            new("Space", "", 6.4),
            new("RAlt", "Alt", 1.2), new("RControl", "Ctrl", 1.4),
            new("Left", "◀"), new("Up", "▲"), new("Down", "▼"), new("Right", "▶"),
        }),
    };
}
