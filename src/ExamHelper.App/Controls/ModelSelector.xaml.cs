using System.Windows;
using System.Windows.Controls;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Controls;
public sealed record ModelChoice(AiModelProfile Model,bool IsEnabled,string? Reason);
public partial class ModelSelector : UserControl
{
    private bool _sync;
    private AppSettings? _settings,_lastInput;
    private ModelSelection? _selection,_lastSelection;
    private ServiceVerification? _verification;
    public bool RequireAuxiliaryVerified { get; private set; }
    public event Action<ModelSelection>? SelectionRequested;
    public event Action<string>? Failed;
    public IReadOnlyList<AiServiceProfile> ProfileChoices { get; private set; } = [];
    public IReadOnlyList<ModelChoice> ModelChoices { get; private set; } = [];
    public IReadOnlyList<ThinkingChoice> ThinkingChoices { get; private set; } = [];
    public ModelSelector() { InitializeComponent(); InitializeHover(); }
    public void Configure(AppSettings settings,ModelSelection? selection,bool auxiliary,ServiceVerification verification)
    {
        if(ReferenceEquals(settings,_lastInput) && Equals(selection,_lastSelection) && auxiliary == RequireAuxiliaryVerified) return;
        var reopen=IsMenuOpen && _browsingProfile?.Id==selection?.ProfileId && selection?.ModelId==Guid.Empty;
        CloseHoverMenu();
        _lastInput = settings; _lastSelection = selection; _selection = selection; _verification = verification; RequireAuxiliaryVerified = auxiliary;
        _settings = AiProfileCatalog.Normalize(settings); _sync = true;
        try
        {
            ProfileChoices = _settings.AiProfiles!.Value.ToArray();
            Profiles.ItemsSource = ProfileChoices;
            var profile = ProfileChoices.FirstOrDefault(p => p.Id == selection?.ProfileId); Profiles.SelectedItem = profile;
            ModelChoices = profile?.Models?.Select(m => Wrap(profile,m)).ToArray() ?? [];
            Models.ItemsSource = ModelChoices; var choice = ModelChoices.FirstOrDefault(m => m.Model.Id == selection?.ModelId); Models.SelectedItem = choice;
            ThinkingChoices = choice is not { IsEnabled:true } ? [] : ThinkingCapability.For(choice.Model).Choices;
            Thinking.ItemsSource = ThinkingChoices; Thinking.SelectedItem = selection?.Thinking;
            var unavailable = selection is not null && (profile is null || choice is not { IsEnabled:true } || !ThinkingChoices.Contains(selection.Thinking));
            Unavailable.Text = unavailable ? $"选择不可用：{selection!.ProfileName} / {selection.ModelName}。请完成配置与验证或重新选择。" : "";
            Unavailable.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _sync = false; RefreshHoverLabels(); }
        if(reopen && ProfileChoices.FirstOrDefault(p=>p.Id==selection?.ProfileId) is { } browsing) OpenHoverMenu(ModelSelectorPart.Model,browsing);
    }
    private bool Qualified(AiServiceProfile p,AiModelProfile m) => _settings is not null && _verification?.CanUseInAuxiliary(_settings,ModelCatalog.Select(p,m)) == true;
    private ModelChoice Wrap(AiServiceProfile p,AiModelProfile m)
    {
        var enabled = !RequireAuxiliaryVerified || Qualified(p,m);
        return new(m,enabled,enabled ? null : "此模型须通过文字及图片测试。");
    }
    private void Request(ModelSelection choice) { _lastInput = null; SelectionRequested?.Invoke(choice); }
    private void ProfileChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_sync || Profiles.SelectedItem is not AiServiceProfile p) return;
        if(!ProfileCompleteness.Evaluate(p).Complete) { Request(ModelCatalog.Pending(p)); return; }
        try { Request(ModelCatalog.Default(p)); } catch(InvalidOperationException error) { Request(ModelCatalog.Pending(p)); Failed?.Invoke(error.Message); }
    }
    private void ModelChanged(object sender,SelectionChangedEventArgs e)
    { if(!_sync && Profiles.SelectedItem is AiServiceProfile p && Models.SelectedItem is ModelChoice { IsEnabled:true } m) Request(ModelCatalog.Select(p,m.Model)); }
    private void ThinkingChanged(object sender,SelectionChangedEventArgs e)
    { if(!_sync && Profiles.SelectedItem is AiServiceProfile p && Models.SelectedItem is ModelChoice { IsEnabled:true } m && Thinking.SelectedItem is ThinkingChoice choice) Request(ModelCatalog.Select(p,m.Model,choice)); }
}
