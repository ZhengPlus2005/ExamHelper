using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Controls;
using ExamHelper.App.Home;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Export;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Markdown;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class SelfTestRevision04Tests
{
    [Theory] [InlineData(true,true)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(false,false)]
    public async Task ChatSendRetryAndDirectCaptureDoNotRequireModelTests(bool auxiliary,bool hasVerifier)
    {
        var settings=TestSettings.Configured();var stub=new StubServices();var ai=new DeferredAiClient();var store=new InMemorySessionStore();
        await using var runtime=new SessionCoordinator(ai,stub,stub,stub,stub,()=>settings,store,new ManualTimeProvider(),verification:hasVerifier?new(new byte[32]):null);
        await runtime.StartNewAsync(default);await runtime.SetAuxiliaryModeAsync(auxiliary,default);
        await runtime.SelectModelAsync(ModelCatalog.Default(settings),default);
        await runtime.UpdateDraftTextAsync("question",default);Assert.True(runtime.CanExecute(AppCommand.Send));
        var failed=ai.QueueAttempt();await runtime.ExecuteAsync(AppCommand.Send,default);await failed.Started.Task;failed.Fail(new IOException("self-owned failure"));await runtime.WaitForIdleAsync(default);
        Assert.True(runtime.CanExecute(AppCommand.RetryRequest));var retry=ai.QueueAttempt();await runtime.ExecuteAsync(AppCommand.RetryRequest,default);await retry.Started.Task;retry.Complete();await runtime.WaitForIdleAsync(default);
        if(auxiliary) { Assert.True(runtime.CanExecute(AppCommand.CaptureAndSend));var direct=ai.QueueAttempt();await runtime.ExecuteAsync(AppCommand.CaptureAndSend,default);await direct.Started.Task;direct.Complete();await runtime.WaitForIdleAsync(default); }
        Assert.False(runtime.CanExecute(AppCommand.ToggleRecording));
    }
    [Fact] public void ConfigurationStatusReportsMissingFieldsAndNeverReportsModelProofs()
    {
        var proof=new ServiceVerification(new byte[32]);var p=new AiProfileEditor(new(),"",proof) { IsActive=true };
        Assert.Contains("配置不完整",p.Status);Assert.DoesNotContain("验证",p.Status);
        p.BaseAddress="https://example.invalid/v1";p.Name="P";p.ApiKey="own-key";
        Assert.Contains("默认模型",p.Status);var m=p.AddModel("model-a");p.DefaultModelId=m.Id;
        Assert.Contains("配置完整",p.Status);Assert.Contains("考试默认",p.Status);p.ApiKey="";Assert.Contains("密钥",p.Status);
    }
    [Fact] public async Task SettingsModelPickerReplacesSeparateIdFieldsAndPersistsExamDefaults()
    {
        await StaTest.RunAsync(()=>
        {
            using var rig=new SettingsRig();var window=new SettingsWindow(rig.Vm,()=>Task.FromResult(SettingsCloseChoice.Discard));window.Show();window.UpdateLayout();StaTest.Pump();
            try
            {
                var picker=Assert.Single(MarkdownViewTests.Nodes(window).OfType<ComboBox>(),c=>c.IsEditable);
                Assert.DoesNotContain(MarkdownViewTests.Nodes(window).OfType<TextBlock>(),t=>t.Text is "添加模型 ID" or "真实模型 ID");
                Assert.DoesNotContain(MarkdownViewTests.Nodes(window).OfType<Button>(),b=>Equals(b.Content,"设为默认"));
                var old=rig.Vm.SelectedAiProfile!.SelectedModel!;picker.Text="kimi-k3";
                MarkdownViewTests.Nodes(window).OfType<Button>().Single(b=>Equals(b.Content,"添加模型")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));StaTest.Pump();
                var p=rig.Vm.SelectedAiProfile;var added=p.Models.Single(m=>m.Name=="kimi-k3");Assert.Equal("model-a",old.Name);Assert.Same(added,p.SelectedModel);
                p.DefaultModelId=added.Id;added.DefaultThinking=new(ThinkingKind.Effort,"high");rig.Vm.ActivateSelectedAiProfile();rig.Vm.SaveAsync(default).GetAwaiter().GetResult();
                var selected=ModelCatalog.Default(rig.Store.Value);Assert.Equal(p.Id,selected.ProfileId);Assert.Equal(added.Id,selected.ModelId);Assert.Equal("high",selected.Thinking.Effort);
                Assert.DoesNotContain("已验证",p.Status);
            }
            finally { window.TryCloseAsync().GetAwaiter().GetResult();StaTest.Pump(); }
        });
    }
    [Fact] public async Task UntestedProfileDefaultIsSelectableFromProtectedMenu()
    {
        await StaTest.RunAsync(()=>
        {
            var proof=new ServiceVerification(new byte[32]);var s=TestSettings.Configured();var pa=s.AiProfiles!.Value[0];
            var b=new AiModelProfile { Name="model-b" };var pb=pa with { Id=Guid.NewGuid(),Name="B",Models=[b],DefaultModelId=b.Id };
            s=s with { AiProfiles=[pa,pb] };var selector=new ModelSelector { AuxiliaryInteraction=true };selector.Configure(s,ModelCatalog.Default(s),true,proof);
            Assert.All(selector.ModelChoices,m=>Assert.True(m.IsEnabled));ModelSelection? requested=null;selector.SelectionRequested+=c=>requested=c;
            var window=new Window { Content=selector,Width=480,Height=360 };window.Show();window.UpdateLayout();StaTest.Pump();
            try
            {
                selector.OpenHoverMenu(ModelSelectorPart.Profile);window.UpdateLayout();StaTest.Pump();
                var button=MarkdownViewTests.Nodes(selector).OfType<Button>().Single(b=>b.Tag is AiServiceProfile p && p.Id==pb.Id);DwellBehavior.GetCommand(button)!.Execute(null);
                Assert.Equal(pb.Id,requested!.ProfileId);Assert.Equal(b.Id,requested.ModelId);Assert.False(selector.IsMenuOpen);
            }
            finally {window.Close();}
        });
    }
    [Theory] [InlineData("2026-10-18T23:59:59Z")] [InlineData("2026-10-19T03:59:59-04:00")]
    public async Task HtmlAndHomeUseTheSameUtc8MessageAndHistoryTimes(string timestamp)
    {
        var at=DateTimeOffset.Parse(timestamp);var message=new MessageSnapshot { Role=MessageRole.User,Text="owned",CreatedAt=at };
        var state=new SessionSnapshot { Messages=[message] };var display=DisplayTime.Format(at);
        var html=await new HtmlConversationExporter(new StubServices(),new MarkdownHtmlRenderer()).BuildAsync(state,default);
        Assert.Contains(display,new ConversationMessageViewModel(message).Header);
        Assert.Contains(">"+display+"</time>",html);
        Assert.Contains("datetime=\""+DisplayTime.ToUtc8(at).ToString("O")+"\"",html);
        Assert.Contains("最后消息："+DisplayTime.Format(ConversationChronology.SortAt(state)),html);
    }
    [Fact] public async Task SavedExamDefaultProfileModelAndThinkingInitializeANewAuxiliaryConversation()
    {
        using var rig=new SettingsRig();var profile=rig.Vm.AddAiProfile();profile.Name="B";profile.BaseAddress="https://second.invalid/v1";profile.ApiKey="second-own-key";
        var model=profile.AddModel("kimi-k3");profile.DefaultModelId=model.Id;model.DefaultThinking=new(ThinkingKind.Effort,"high");rig.Vm.ActivateSelectedAiProfile();await rig.Vm.SaveAsync(default);
        var stub=new StubServices();await using var runtime=new SessionCoordinator(stub,stub,stub,stub,stub,()=>rig.Store.Value,new InMemorySessionStore(),new ManualTimeProvider());await runtime.StartNewAsync(default);
        Assert.Equal(profile.Id,runtime.State.Selection!.ProfileId);Assert.Equal(model.Id,runtime.State.Selection.ModelId);Assert.Equal("high",runtime.State.Selection.Thinking.Effort);
        profile.SelectedModel!.DefaultThinking=new(ThinkingKind.Effort,"low");
        Assert.Equal("high",runtime.State.Selection.Thinking.Effort);
    }
    private sealed class SettingsRig:IDisposable
    {
        private readonly HttpClient _http=new(new FakeHttpHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        public InMemorySettingsStore Store {get;}=new(TestSettings.Configured());
        public SettingsViewModel Vm {get;}
        public SettingsRig(){var proof=new ServiceVerification(new byte[32]);var secrets=new InMemorySecretStore();Vm=new(Store.Value,"own-key","",Store,secrets,proof,new(_http,secrets,proof,[1]),new RecordingController(new StubServices(),new ManualTimeProvider()),_=>{});}
        public void Dispose(){Vm.DisposeAsync().AsTask().GetAwaiter().GetResult();_http.Dispose();}
    }
}
