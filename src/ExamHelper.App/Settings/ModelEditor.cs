using System.Collections.Immutable;
using ExamHelper.App.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Settings;
public sealed class ModelEditor : ObservableObject
{
    private AiModelProfile _template;
    private string _name,_levels;
    private ThinkingChoice _thinking;
    private ThinkingControl _control;
    private ThinkingWireFormat _wire;
    private bool _manual;
    public Guid Id => _template.Id;
    public string Name { get => _name; set { if(Set(ref _name,value)) { InvalidateVerification(); Changed(); } } }
    public string? TextVerifiedFingerprint { get => _template.TextVerifiedFingerprint; set { _template = _template with { TextVerifiedFingerprint = value }; Notify(); } }
    public void InvalidateVerification()
    {
        if(_template.TextVerifiedFingerprint is null && _template.VerifiedFingerprint is null) return;
        _template = _template with { TextVerifiedFingerprint = null,VerifiedFingerprint = null };
        Notify(nameof(TextVerifiedFingerprint)); Notify(nameof(VerifiedFingerprint));
    }
    public string? VerifiedFingerprint { get => _template.VerifiedFingerprint; set { _template = _template with { VerifiedFingerprint = value }; Notify(); } }
    public ThinkingChoice DefaultThinking { get => _thinking; set => Set(ref _thinking,value); }
    public bool ManualCapability { get => _manual; set { if(Set(ref _manual,value)) Changed(); } }
    public ThinkingControl Control { get => _control; set { if(Set(ref _control,value)) Changed(); } }
    public ThinkingWireFormat WireFormat { get => _wire; set { if(Set(ref _wire,value)) Changed(); } }
    public string Levels { get => _levels; set { if(Set(ref _levels,value)) Changed(); } }
    public IReadOnlyList<ThinkingChoice> ThinkingOptions => ThinkingCapability.For(Build()).Choices;
    public ModelEditor(AiModelProfile model)
    {
        _template = model; _name = model.Name; _thinking = model.DefaultThinking;
        var c = ThinkingCapability.For(model); _manual = model.CapabilityOverride is not null; _control = c.Control; _wire = c.WireFormat; _levels = c.Levels.IsDefault ? "" : string.Join(",",c.Levels);
    }
    public AiModelProfile Build() => _template with { Name = Name.Trim(),DefaultThinking = DefaultThinking,CapabilityOverride = ManualCapability ? new(Control,WireFormat,Levels.Split(',',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToImmutableArray()) : null };
    private void Changed() { Notify(nameof(ThinkingOptions)); if(!ThinkingOptions.Contains(DefaultThinking)) DefaultThinking = new(); }
}

