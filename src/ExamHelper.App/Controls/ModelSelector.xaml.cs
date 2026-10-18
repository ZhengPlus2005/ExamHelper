using System.Windows;
using System.Windows.Controls;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Controls;
public partial class ModelSelector : UserControl
{
    private bool _sync;
    private AppSettings? _settings,_lastInput;
    private ModelSelection? _selection,_lastSelection;
    private ServiceVerification? _verification;
    public bool RequireImageVerified { get; private set; }
    public event Action<ModelSelection>? SelectionRequested;
    public event Action<string>? Failed;
    public IReadOnlyList<AiServiceProfile> ProfileChoices { get; private set; } = [];
    public IReadOnlyList<AiModelProfile> ModelChoices { get; private set; } = [];
    public IReadOnlyList<ThinkingChoice> ThinkingChoices { get; private set; } = [];
    public ModelSelector() { InitializeComponent(); InitializeHover(); }
    public void Configure(AppSettings settings,ModelSelection? selection,bool requireImageVerified,ServiceVerification verification)
    {
        if(ReferenceEquals(settings,_lastInput) && Equals(selection,_lastSelection) && requireImageVerified == RequireImageVerified) return;
        _lastInput = settings; _lastSelection = selection; _selection = selection; _verification = verification; RequireImageVerified = requireImageVerified;
        _settings = AiProfileCatalog.Normalize(settings); _sync = true;
        try
        {
            ProfileChoices = _settings.AiProfiles!.Value.Where(p => !requireImageVerified || p.Models?.Any(m => Qualified(p,m)) == true).ToArray();
            Profiles.ItemsSource = ProfileChoices;
            var profile = ProfileChoices.FirstOrDefault(p => p.Id == selection?.ProfileId); Profiles.SelectedItem = profile;
            ModelChoices = profile?.Models?.Where(m => !requireImageVerified || Qualified(profile,m)).ToArray() ?? [];
            Models.ItemsSource = ModelChoices; var model = ModelChoices.FirstOrDefault(m => m.Id == selection?.ModelId); Models.SelectedItem = model;
            ThinkingChoices = model is null ? [] : ThinkingCapability.For(model).Choices; Thinking.ItemsSource = ThinkingChoices; Thinking.SelectedItem = selection?.Thinking;
            var unavailable = selection is not null && (profile is null || model is null || !ThinkingChoices.Contains(selection.Thinking));
            Unavailable.Text = unavailable ? $"选择不可用：{selection!.ProfileName} / {selection.ModelName}。请重新选择。" : "";
            Unavailable.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _sync = false; RefreshHoverLabels(); }
    }
    private bool Qualified(AiServiceProfile p,AiModelProfile m)
    { try { ModelCatalog.Resolve(_settings!,ModelCatalog.Select(p,m),true,_verification!); return true; } catch(InvalidOperationException) { return false; } }
    private void Request(ModelSelection choice)
    { _lastInput = null; SelectionRequested?.Invoke(choice); }
    private void ProfileChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_sync || Profiles.SelectedItem is not AiServiceProfile p) return;
        try { Request(ModelCatalog.Default(p)); } catch(InvalidOperationException error) { Failed?.Invoke(error.Message); }
    }
    private void ModelChanged(object sender,SelectionChangedEventArgs e)
    { if(!_sync && Profiles.SelectedItem is AiServiceProfile p && Models.SelectedItem is AiModelProfile m) Request(ModelCatalog.Select(p,m)); }
    private void ThinkingChanged(object sender,SelectionChangedEventArgs e)
    { if(!_sync && Profiles.SelectedItem is AiServiceProfile p && Models.SelectedItem is AiModelProfile m && Thinking.SelectedItem is ThinkingChoice choice) Request(ModelCatalog.Select(p,m,choice)); }
}
