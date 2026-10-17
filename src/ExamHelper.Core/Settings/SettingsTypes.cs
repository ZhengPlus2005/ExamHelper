using System.Collections.Immutable;
namespace ExamHelper.Core.Settings;
public enum ProviderKind { Kimi, Compatible }
public sealed record AiSettings
{
    public Uri BaseUri { get; init; } = new("https://api.moonshot.cn/v1");
    public string Model { get; init; } = "kimi-k2.6";
    public string SecretId { get; init; } = "ai";
    public string SecretFingerprint { get; init; } = "";
    public ProviderKind ProviderKind { get; init; } = ProviderKind.Kimi;
}
public sealed record AsrSettings
{
    public Uri BaseUri { get; init; } = new("https://dashscope.aliyuncs.com/compatible-mode/v1");
    public string Model { get; init; } = "qwen3-asr-flash";
    public string SecretId { get; init; } = "asr";
    public string SecretFingerprint { get; init; } = "";
    public string DeviceId { get; init; } = "";
}
public sealed record UiSettings
{
    public double Opacity { get; init; } = 0.85;
    public double Width { get; init; } = 640;
    public double Height { get; init; } = 720;
    public double? Left { get; init; }
    public double? Top { get; init; }
}
public sealed record HotkeySettings
{
    public ImmutableArray<HotkeyBinding> Bindings { get; init; } = HotkeyBinding.Defaults.ToImmutableArray();
}
public sealed record AppSettings
{
    public AiSettings Ai { get; init; } = new();
    // null identifies the legacy single-provider format; [] is an intentionally empty list.
    public ImmutableArray<AiServiceProfile>? AiProfiles { get; init; }
    public Guid? ActiveAiProfileId { get; init; }
    public AsrSettings Asr { get; init; } = new();
    public UiSettings Ui { get; init; } = new();
    public HotkeySettings Hotkeys { get; init; } = new();
    public ServiceEvidence Evidence { get; init; } = new();
    public string DefaultCodeLanguage { get; init; } = "C++";
    public string DirectCapturePrompt { get; init; } = "请回答截图中的题目。选择题、判断题先给出答案，再给出简要理由；简答题直接给出作答内容；编程题直接给出代码，并使用代码块。多道题按截图顺序分别作答。";
}