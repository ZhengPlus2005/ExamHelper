using System.Windows;
using System.Windows.Controls;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Controls;
public enum ModelSelectorPart { Profile,Model,Thinking }
public partial class ModelSelector
{
    private bool _auxiliary,_hoverSuspended;
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
        if(!IsMenuOpen) return; MenuBorder.Visibility=Visibility.Collapsed; MenuItems.Children.Clear();
        if(Window.GetWindow(this) is { } window) DwellBehavior.InvalidateWindow(window);
    }
    public void OpenHoverMenu(ModelSelectorPart part)
    {
        if(!AuxiliaryInteraction || _hoverSuspended) return; MenuItems.Children.Clear();
        var choices=part switch { ModelSelectorPart.Profile => ProfileChoices.Cast<object>(),ModelSelectorPart.Model => ModelChoices.Cast<object>(),_ => ThinkingChoices.Cast<object>() };
        foreach(var item in choices)
        {
            var label=item switch { AiServiceProfile p => p.Name,AiModelProfile m => m.Name,ThinkingChoice thinking => thinking.ToString(),_ => "" };
            var button=new Button { Content=new TextBlock { Text=label,TextWrapping=TextWrapping.Wrap,FontSize=11 },Tag=item,HorizontalContentAlignment=HorizontalAlignment.Left,Focusable=false,Margin=new Thickness(1,2,1,2),Padding=new Thickness(7,5,7,5) };
            HoverModelSelectorBehavior.Attach(button,() => Choose(item),() => !_hoverSuspended); MenuItems.Children.Add(button);
        }
        if(MenuItems.Children.Count==0) MenuItems.Children.Add(new TextBlock { Text="暂无可选项，请先在设置中完成模型图片测试。",Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6) });
        var width=Math.Min(Math.Max(80,ActualWidth),240); MenuBorder.Width=width;
        Canvas.SetLeft(MenuBorder,Math.Clamp((int)part*ActualWidth/3,0,Math.Max(0,ActualWidth-width))); MenuBorder.Visibility=Visibility.Visible; MenuScroll.ScrollToTop();
    }
    private void Choose(object item)
    {
        try
        {
            var profile=_settings?.AiProfiles?.FirstOrDefault(p => p.Id==_selection?.ProfileId);
            var model=profile?.Models?.FirstOrDefault(m => m.Id==_selection?.ModelId);
            var choice=item switch { AiServiceProfile p => ModelCatalog.Default(p),AiModelProfile m when profile is not null => ModelCatalog.Select(profile,m),ThinkingChoice t when profile is not null && model is not null => ModelCatalog.Select(profile,model,t),_ => null };
            if(choice is not null) Request(choice);
        }
        catch(InvalidOperationException e) { Failed?.Invoke(e.Message); }
        finally { CloseHoverMenu(); }
    }
}
