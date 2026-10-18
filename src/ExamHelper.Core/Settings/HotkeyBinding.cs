using ExamHelper.Core.Interaction;
namespace ExamHelper.Core.Settings;
public sealed record HotkeyBinding(AppCommand Command, uint Modifiers, uint VirtualKey)
{
    public override string ToString() => $"{Command}: modifiers=0x{Modifiers:X}, key=0x{VirtualKey:X}";
    // Explicit mappings keep established shortcuts stable as commands are added or removed.
    // Ctrl+Alt+F5 is deliberately unassigned after removing keyboard editing.
    public static IReadOnlyList<HotkeyBinding> Defaults { get; } =
    [
        new(AppCommand.CaptureAndSend, 3, 0x70),
        new(AppCommand.CaptureToDraft, 3, 0x71),
        new(AppCommand.ToggleRecording, 3, 0x72),
        new(AppCommand.Send, 3, 0x73),
        new(AppCommand.ClearInput, 3, 0x75),
        new(AppCommand.NewConversation, 3, 0x76),
        new(AppCommand.StopAnswer, 3, 0x77),
        new(AppCommand.ToggleVisibility, 3, 0x78),
        new(AppCommand.ToggleCollapse, 3, 0x79),
        new(AppCommand.ReturnHome, 3, 0x7A),
        new(AppCommand.UndoSpeech, 3, 0x5A)
    ];
}

