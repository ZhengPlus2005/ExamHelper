using System.Windows;
using System.Windows.Controls;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Controls;
public enum ModelSelectorPart { Profile,Model,Thinking }
public partial class ModelSelector
{
    private bool _auxiliary,_hoverSuspended;
    private AiServiceProfile? _browsingProfile;
    public bool AuxiliaryInteraction
    {
        get => _auxiliary;
        set
        {
            _auxiliary=value; OrdinaryControls.Visibility=value ? Visibility.Collapsed : Visibility.Visible; HoverRoot.Visibility=value ? Visibility.Visible : Visibility.Collapsed;
            Profiles.Focusable=Models.Focusable=Thinking.Focusable=!value; if(!value) CloseHoverMenu(); RefreshHoverLabels();
        }
    }
    public bool IsMenuOpen => MenuBorder.Visibility==Visibility.Visible;
    private void InitializeHover()
    {
        HoverModelSelectorBehavior.Attach(ProfileEntry,() => OpenHoverMenu(ModelSelectorPart.Profile),() => !_hoverSuspended);
        HoverModelSelectorBehavior.Attach(ModelEntry,() => OpenHoverMenu(ModelSelectorPart.Model),() => !_hoverSuspended);
        HoverModelSelectorBehavior.Attach(ThinkingEntry,() => OpenHoverMenu(ModelSelectorPart.Thinking),() => !_hoverSuspended);
        MouseLeave+=(_,_) => { if(!IsMouseOver) CloseHoverMenu(); };
        Unloaded+=(_,_) => CloseHoverMenu();
    }
    private void RefreshHoverLabels()
    {
        ProfileLabel.Text="配置："+(_selection?.ProfileName ?? "未选择"); ModelLabel.Text="模型："+(_selection?.ModelName ?? "未选择"); ThinkingLabel.Text="思考："+(_selection?.Thinking.ToString() ?? "服务默认值");
    }
    public void SetHoverSuspended(bool suspended) { _hoverSuspended=suspended; if(suspended) CloseHoverMenu(); }
    public void CloseHoverMenu()
    {
        _browsingProfile=null; if(!IsMenuOpen) return; MenuBorder.Visibility=Visibility.Collapsed; MenuItems.Children.Clear();
        if(Window.GetWindow(this) is { } window) DwellBehavior.InvalidateWindow(window);
    }
    public void OpenHoverMenu(ModelSelectorPart part,AiServiceProfile? browsingProfile = null)
    {
        if(!AuxiliaryInteraction || _hoverSuspended) return; MenuItems.Children.Clear(); _browsingProfile=browsingProfile;
        if(browsingProfile is not null) MenuItems.Children.Add(new TextBlock { Text=browsingProfile.Name+"：默认模型不可用，请选择已完成文字及图片测试的模型。",Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6) });
        var choices=part switch { ModelSelectorPart.Profile => ProfileChoices.Cast<object>(),ModelSelectorPart.Model => browsingProfile is null ? ModelChoices.Cast<object>() : browsingProfile.Models!.Value.Select(m => Wrap(browsingProfile,m)).Cast<object>(),_ => ThinkingChoices.Cast<object>() };
        foreach(var item in choices)
        {
            var label=item switch { AiServiceProfile p => string.IsNullOrWhiteSpace(p.Name) ? "未命名配置" : p.Name,ModelChoice m => m.Model.Name,ThinkingChoice thinking => thinking.ToString(),_ => "" };
            var button=new Button { Content=new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap,FontSize=11 },Tag=item,HorizontalContentAlignment=HorizontalAlignment.Left,Focusable=false,Margin=new Thickness(3),Padding=new Thickness(7,5,7,5) };
            button.SetResourceReference(StyleProperty,"AuxiliaryDwellButtonStyle");
            if(item is ModelChoice model) { button.IsEnabled=model.IsEnabled; button.ToolTip=model.Reason;
                if(!model.IsEnabled) { button.Foreground=System.Windows.Media.Brushes.Gray; button.Opacity=0.55; } }
            HoverModelSelectorBehavior.Attach(button,() => Choose(item),() => !_hoverSuspended && button.IsEnabled); MenuItems.Children.Add(button);
        }
        if(MenuItems.Children.Count==0) MenuItems.Children.Add(new TextBlock { Text="暂无模型，请先在设置中添加。",Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6) });
        var width=Math.Min(Math.Max(80,ActualWidth),240); MenuBorder.Width=width;
        Canvas.SetLeft(MenuBorder,Math.Clamp((int)part*ActualWidth/3,0,Math.Max(0,ActualWidth-width))); MenuBorder.Visibility=Visibility.Visible; MenuScroll.ScrollToTop();
    }
    private void Choose(object item)
    {
        if(item is ModelChoice { IsEnabled:false }) return;
        if(item is AiServiceProfile candidate && RequireAuxiliaryVerified && (candidate.Models?.FirstOrDefault(m => m.Id==candidate.DefaultModelId) is not { } defaultModel || !Qualified(candidate,defaultModel)))
        {
            Request(ModelCatalog.Pending(candidate));
            CloseHoverMenu(); OpenHoverMenu(ModelSelectorPart.Model,candidate);
            if(Window.GetWindow(this) is { } window) DwellBehavior.InvalidateWindow(window);
            return;
        }
        try
        {
            var profile=_browsingProfile ?? _settings?.AiProfiles?.FirstOrDefault(p => p.Id==_selection?.ProfileId);
            var model=profile?.Models?.FirstOrDefault(m => m.Id==_selection?.ModelId);
            var choice=item switch { AiServiceProfile p => ModelCatalog.Default(p),ModelChoice m when profile is not null => ModelCatalog.Select(profile,m.Model),ThinkingChoice t when profile is not null && model is not null => ModelCatalog.Select(profile,model,t),_ => null };
            if(choice is not null) Request(choice);
        }
        catch(InvalidOperationException e) { Failed?.Invoke(e.Message); }
        finally { CloseHoverMenu(); }
    }
}
