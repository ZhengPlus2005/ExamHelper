using ExamHelper.App.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Settings;
public sealed class AiProfileEditor : ObservableObject
{
    private readonly ServiceVerification _verification;
    private readonly AiSettings _template;
    private string _name,_baseAddress,_model,_apiKey,_secretId;
    private ProviderKind _kind;
    private bool _active;
    private string? _verified;
    public Guid Id { get; }
    public string Name { get => _name; set { if(Set(ref _name,value)) Notify(nameof(Status)); } }
    public string BaseAddress { get => _baseAddress; set { if(Set(ref _baseAddress,value)) Changed(); } }
    public string Model { get => _model; set { if(Set(ref _model,value)) Changed(); } }
    public string ApiKey { get => _apiKey; set { if(Set(ref _apiKey,value)) Changed(); } }
    public ProviderKind ProviderKind { get => _kind; set { if(Set(ref _kind,value)) Changed(); } }
    public bool IsActive { get => _active; set { if(Set(ref _active,value)) Notify(nameof(Status)); } }
    public string? VerifiedFingerprint { get => _verified; set { if(Set(ref _verified,value)) Changed(); } }
    public bool IsVerified
    {
        get { try { var s = TestSettings(); return _verification.IsAiVerified(s); } catch { return false; } }
    }
    public string Status => (IsActive ? "启用 · " : "") + (IsVerified ? "图片已验证" : "未验证");
    public AiProfileEditor(AiServiceProfile profile,string key,ServiceVerification verification)
    {
        Id = profile.Id; _secretId = profile.Settings.SecretId; _name = profile.Name; _template = profile.Settings; _verification = verification;
        _baseAddress = _template.BaseUri.ToString(); _model = _template.Model; _kind = _template.ProviderKind; _apiKey = key; _verified = profile.VerifiedFingerprint;
    }
    public AiSettings BuildSettings() => _template with { SecretId = _secretId,BaseUri = new(BaseAddress.Trim()),Model = Model.Trim(),ProviderKind = ProviderKind,SecretFingerprint = _verification.SecretDigest(ApiKey) };
    public AiServiceProfile BuildProfile() => new() { Id = Id,Name = Name.Trim(),Settings = BuildSettings(),VerifiedFingerprint = VerifiedFingerprint };
    public AppSettings TestSettings() => new() { Ai = BuildSettings(),Evidence = new(VerifiedFingerprint) };
    internal void RebaseSecretId(string id) { _secretId = id; Changed(); }
    private void Changed() { Notify(nameof(IsVerified)); Notify(nameof(Status)); }
}
