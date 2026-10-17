using System.Windows.Input;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Settings;
public sealed class HotkeyEditor(AppCommand command,string text)
{
    public AppCommand Command { get; } = command;
    public string Name => CommandNames.Get(Command);
    public string Text { get; set; } = text;
    public static string Format(HotkeyBinding? binding)
    {
        if(binding is null) return "";
        var keys = new List<string>();
        if((binding.Modifiers & 2) != 0) keys.Add("Ctrl"); if((binding.Modifiers & 1) != 0) keys.Add("Alt");
        if((binding.Modifiers & 4) != 0) keys.Add("Shift"); if((binding.Modifiers & 8) != 0) keys.Add("Win");
        keys.Add(KeyInterop.KeyFromVirtualKey((int)binding.VirtualKey).ToString());
        return string.Join("+",keys);
    }
    public HotkeyBinding? Parse()
    {
        if(string.IsNullOrWhiteSpace(Text)) return null;
        var parts = Text.Split('+',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        uint modifiers = 0;
        foreach(var part in parts[..^1])
            modifiers |= part.ToLowerInvariant() switch { "ctrl" => 2u,"alt" => 1u,"shift" => 4u,"win" => 8u,_ => throw new ArgumentException($"{Name}：快捷键修饰键无效。") };
        var last = parts[^1]; if(last.Length == 1 && char.IsAsciiDigit(last[0])) last = "D" + last;
        if(modifiers == 0 || !Enum.TryParse<Key>(last,true,out var key) || key is Key.None or Key.Escape || KeyInterop.VirtualKeyFromKey(key) == 0)
            throw new ArgumentException($"{Name}：请使用 Ctrl／Alt／Shift／Win 加按键；Esc 保留给移动操作。");
        return new(Command,modifiers,(uint)KeyInterop.VirtualKeyFromKey(key));
    }
}
public static class CommandNames
{
    public static string Get(AppCommand command) => command switch
    {
        AppCommand.CaptureAndSend => "截图并立即发送",AppCommand.CaptureToDraft => "截图加入草稿",AppCommand.ToggleRecording => "开始／停止录音",
        AppCommand.Send => "发送",AppCommand.ClearInput => "清空输入",AppCommand.NewConversation => "新对话",AppCommand.StopAnswer => "停止回答",
        AppCommand.ToggleVisibility => "隐藏／显示",AppCommand.ToggleCollapse => "折叠／展开",AppCommand.ReturnHome => "返回主页",AppCommand.Exit => "退出",
        AppCommand.UndoSpeech => "撤销本次语音",AppCommand.RetrySpeech => "重试识别",AppCommand.DiscardSpeech => "丢弃录音",
        AppCommand.RetryRequest => "重试回答",AppCommand.DiscardRequest => "丢弃失败请求",_ => command.ToString()
    };
}