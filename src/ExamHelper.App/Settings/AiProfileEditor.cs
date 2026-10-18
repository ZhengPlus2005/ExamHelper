using System.Collections.Immutable;
using System.Collections.ObjectModel;
using ExamHelper.App.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Settings;
public sealed class AiProfileEditor : ObservableObject
{
    private readonly ServiceVerification _verification;
    private readonly AiSettings _template;
    private string _name,_baseAddress,_apiKey,_secretId;
    private ProviderKind _kind;
    private bool _active;
    private ModelEditor? _selectedModel;
    private Guid? _defaultModelId;
    public Guid Id { get; }
    public ObservableCollection<ModelEditor> Models { get; } = [];
    public ModelEditor? SelectedModel { get => _selectedModel; set { if(Set(ref _selectedModel,value)) { Notify(nameof(Model)); Changed(); } } }
    public Guid? DefaultModelId { get => _defaultModelId; set { if(Set(ref _defaultModelId,value)) Changed(); } }
    public string Name { get => _name; set { if(Set(ref _name,value)) Changed(); } }
    public string BaseAddress { get => _baseAddress; set { if(Set(ref _baseAddress,value)) Changed(); } }
    public string Model { get => SelectedModel?.Name ?? ""; set { if(SelectedModel is { } m) m.Name = value; } }
    public string ApiKey { get => _apiKey; set { if(Set(ref _apiKey,value)) Changed(); } }
    public ProviderKind ProviderKind { get => _kind; set { if(Set(ref _kind,value)) Changed(); } }
    public bool IsActive { get => _active; set { if(Set(ref _active,value)) Changed(); } }
    public string? VerifiedFingerprint { get => SelectedModel?.VerifiedFingerprint; set { if(SelectedModel is { } m) m.VerifiedFingerprint = value; Changed(); } }
    public bool IsVerified { get { try { return _verification.IsAiVerified(TestSettings()); } catch { return false; } } }
    public string Status => (IsActive ? "启用 · " : "") + (IsVerified ? "图片已验证" : "未验证");
    public AiProfileEditor(AiServiceProfile profile,string key,ServiceVerification verification)
    {
        Id = profile.Id; _secretId = profile.Settings.SecretId; _name = profile.Name; _template = profile.Settings; _verification = verification;
        _baseAddress = _template.BaseUri.ToString(); _kind = _template.ProviderKind; _apiKey = key;
        var normalized = AiProfileCatalog.Normalize(new() { AiProfiles = [profile],ActiveAiProfileId = profile.Id }).AiProfiles!.Value[0];
        _defaultModelId = normalized.DefaultModelId;
        foreach(var model in normalized.Models!.Value) AddModel(model);
        SelectedModel = Models.FirstOrDefault(m => m.Id == DefaultModelId);
    }
    public ModelEditor AddModel(AiModelProfile? model = null)
    {
        var editor = new ModelEditor(model ?? new() { Name = "" });
        editor.PropertyChanged += (_,_) => { if(ReferenceEquals(editor,SelectedModel)) Notify(nameof(Model)); Changed(); };
        Models.Add(editor); SelectedModel = editor; Changed(); return editor;
    }
    public void DeleteSelectedModel() { if(SelectedModel is not { } m) return; Models.Remove(m); if(DefaultModelId == m.Id) DefaultModelId = null; SelectedModel = Models.FirstOrDefault(); Changed(); }
    public AiSettings BuildSettings() => _template with { SecretId = _secretId,BaseUri = new(BaseAddress.Trim()),Model = Model.Trim(),ProviderKind = ProviderKind,SecretFingerprint = _verification.SecretDigest(ApiKey),Thinking = SelectedModel?.DefaultThinking ?? new(),ThinkingCapability = SelectedModel is { } m ? ThinkingCapability.For(m.Build()) : new() };
    public AiServiceProfile BuildProfile() => new() { Id = Id,Name = Name.Trim(),Settings = BuildSettings(),Models = Models.Select(m => m.Build()).ToImmutableArray(),DefaultModelId = DefaultModelId,VerifiedFingerprint = VerifiedFingerprint };
    public AppSettings TestSettings() => new() { Ai = BuildSettings(),Evidence = new(VerifiedFingerprint) };
    internal void RebaseSecretId(string id) { _secretId = id; Changed(); }
    private void Changed() { Notify(nameof(IsVerified)); Notify(nameof(Status)); }
}
