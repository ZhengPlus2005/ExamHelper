using ExamHelper.Core.Interaction;
namespace ExamHelper.Core.Settings;
public sealed record HotkeyBinding(AppCommand Command, uint Modifiers, uint VirtualKey)
{
    public override string ToString() => $"{Command}: modifiers=0x{Modifiers:X}, key=0x{VirtualKey:X}";
    public static IReadOnlyList<HotkeyBinding> Defaults { get; } =
        Enum.GetValues<AppCommand>().Take(12).Select((command, index) => new HotkeyBinding(command, 3, (uint)(0x70 + index)))
            .Append(new HotkeyBinding(AppCommand.UndoSpeech, 3, 0x5A)).ToArray();
}
