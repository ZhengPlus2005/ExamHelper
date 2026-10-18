using System.Collections.Immutable;
namespace ExamHelper.Core.Settings;
public enum ThinkingKind { ServiceDefault, Enabled, Disabled, Effort }
public sealed record ThinkingChoice(ThinkingKind Kind = ThinkingKind.ServiceDefault,string? Effort = null)
{
    public override string ToString() => Kind switch { ThinkingKind.Enabled => "开启",ThinkingKind.Disabled => "关闭",ThinkingKind.Effort => Effort ?? "",_ => "服务默认值" };
}
public enum ThinkingControl { None, Toggle, Effort, ToggleAndEffort }
public enum ThinkingWireFormat { None, ThinkingObject, ReasoningEffort, ThinkingObjectAndEffort }
public sealed record ThinkingCapability
{
    public ThinkingControl Control { get; init; }
    public ThinkingWireFormat WireFormat { get; init; }
    public ImmutableArray<string> Levels { get; init; }
    public ThinkingCapability(ThinkingControl control = ThinkingControl.None,ThinkingWireFormat wireFormat = ThinkingWireFormat.None,ImmutableArray<string> levels = default)
    { Control = control; WireFormat = wireFormat; Levels = levels.IsDefault ? ImmutableArray<string>.Empty : levels; }
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ThinkingChoice> Choices => new[] { new ThinkingChoice() }
        .Concat(Control is ThinkingControl.Toggle or ThinkingControl.ToggleAndEffort ? [new(ThinkingKind.Enabled),new(ThinkingKind.Disabled)] : Array.Empty<ThinkingChoice>())
        .Concat(Control is ThinkingControl.Effort or ThinkingControl.ToggleAndEffort && !Levels.IsDefault ? Levels.Select(s => new ThinkingChoice(ThinkingKind.Effort,s)) : []).ToArray();
    public bool Allows(ThinkingChoice choice) => Choices.Contains(choice);
    public static ThinkingCapability For(AiModelProfile model)
    {
        if(model.CapabilityOverride is { } manual) return manual;
        if(model.DiscoveryMetadata?.EffortLevels is { IsDefaultOrEmpty: false } levels)
            return new(ThinkingControl.Effort,ThinkingWireFormat.ReasoningEffort,levels);
        var id = model.Name.ToLowerInvariant();
        if(id == "kimi-k3" || id.StartsWith("kimi-k3-")) return new(ThinkingControl.Effort,ThinkingWireFormat.ReasoningEffort,["low","high","max"]);
        if(id == "kimi-k2.6" || id.StartsWith("kimi-k2.6-")) return new(ThinkingControl.Toggle,ThinkingWireFormat.ThinkingObject);
        return new();
    }
}
public sealed record ModelMetadata(string Id,bool? SupportsImages = null,bool? SupportsReasoning = null,ImmutableArray<string>? EffortLevels = null);

