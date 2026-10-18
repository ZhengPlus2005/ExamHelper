using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamHelper.App.Common;
using ExamHelper.App.Controls;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Home;
public partial class HomeMessageView:UserControl
{
    private MessageSnapshot? _message;
    private Guid[] _imageIds=[];
    private readonly Func<ImageRef,FrameworkElement>? _thumbnail;
    public string CopyText=>_message?.Text ?? "";
    public HomeMessageView(Func<ImageRef,FrameworkElement>? thumbnail=null,Action<string>? copy=null,Action<Exception>? report=null)
    {
        _thumbnail=thumbnail;InitializeComponent();
        CopyMessageButton.Command=new AsyncDelegateCommand(()=> { (copy ?? Clipboard.SetText)(CopyText);return Task.CompletedTask; },()=>_message is not null,error=> { CopyMessageButton.ToolTip=ObservableObject.Explain(error);report?.Invoke(error); });
    }
    public void Update(MessageSnapshot message)
    {
        var changed=_message is null || _message.Role!=message.Role || _message.Text!=message.Text;
        _message=message;((AsyncDelegateCommand)CopyMessageButton.Command).Raise();Tag=message.Id;Header.Text=new ConversationMessageViewModel(message).Header;UpdateTiming(message.Timing);
        if(changed)
        {
            if(message.Role==MessageRole.Assistant)
            { if(Body.Content is MarkdownView markdown)markdown.Text=message.Text;else Body.Content=new MarkdownView { Text=message.Text }; }
            else
            { if(Body.Content is TextBlock text)text.Text=message.Text;else Body.Content=new TextBlock { Text=message.Text,TextWrapping=TextWrapping.Wrap,FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(25,35,49)) }; }
        }
        var ids=message.Images.Select(i=>i.Id).ToArray();
        if(!_imageIds.SequenceEqual(ids))
        { _imageIds=ids;Pictures.Children.Clear();if(_thumbnail is not null)foreach(var image in message.Images)Pictures.Children.Add(_thumbnail(image)); }
    }
    public void UpdateTiming(ResponseTiming? timing)=>TimingLabel.Text=ResponseTimingFormatter.Format(timing);
}
