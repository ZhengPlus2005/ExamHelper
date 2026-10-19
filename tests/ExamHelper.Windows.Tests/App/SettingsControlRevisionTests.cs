using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Overlay;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class SettingsControlRevisionTests
{
    [Fact] public async Task AiOutputIsVisibleOnlyOnAiTabAndSpeechHasItsOwnVerificationStatus()
    {
        await StaTest.RunAsync(()=>
        {
            using var rig=new Rig();var window=new SettingsWindow(rig.Vm,()=>Task.FromResult(SettingsCloseChoice.Discard));window.Show();StaTest.Pump();
            try
            {
                var tabs=(TabControl)window.FindName("SettingsTabs");
                Assert.DoesNotContain(MarkdownViewTests.Nodes(window).OfType<TextBlock>(),t=>t.Text.StartsWith("AI："));
                var output=Assert.IsType<TextBox>(window.FindName("AiTestOutput"));
                Assert.True(output.IsVisible);SavePreview(window,"ai-settings.png");
                tabs.SelectedIndex=1;window.UpdateLayout();StaTest.Pump();Assert.False(output.IsVisible);
                var speechStatus=Assert.IsType<TextBlock>(window.FindName("SpeechVerificationStatus"));
                Assert.True(speechStatus.IsVisible);Assert.Contains("未验证",speechStatus.Text);
                var speechOutput=Assert.IsType<TextBox>(window.FindName("SpeechTestOutput"));Assert.True(speechOutput.IsVisible);SavePreview(window,"speech-settings.png");
                tabs.SelectedIndex=2;window.UpdateLayout();StaTest.Pump();Assert.False(output.IsVisible);Assert.False(speechStatus.IsVisible);
            }
            finally {window.TryCloseAsync().GetAwaiter().GetResult();StaTest.Pump();}
        });
    }
    [Fact] public async Task ControlSwitchPersistsWithoutChangingTheDefaultOrOtherUiSettings()
    {
        await StaTest.RunAsync(()=>
        {
            using var rig=new Rig();var window=new SettingsWindow(rig.Vm,()=>Task.FromResult(SettingsCloseChoice.Discard));window.Show();StaTest.Pump();
            try
            {
                var tabs=(TabControl)window.FindName("SettingsTabs");var items=tabs.Items.Cast<TabItem>().ToArray();
                var index=Array.FindIndex(items,t=>Equals(t.Header,"控制"));Assert.True(index>=0);Assert.Equal("快捷键",items[index+1].Header);tabs.SelectedIndex=index;
                window.UpdateLayout();StaTest.Pump();SavePreview(window,"control-settings.png");
                var toggle=Assert.IsType<CheckBox>(window.FindName("CaptureProtectionSwitch"));Assert.True(toggle.IsChecked);Assert.False(rig.Vm.HasUnsavedChanges);
                toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,false);StaTest.Pump();
                Assert.True(rig.Vm.HasUnsavedChanges);rig.Vm.SaveAsync(default).GetAwaiter().GetResult();
                var saved=JsonSerializer.SerializeToNode(rig.Store.Value)!;Assert.False(saved["Ui"]!["CaptureProtectionEnabled"]!.GetValue<bool>());Assert.Equal(0.5,rig.Store.Value.Ui.Opacity);
                Assert.False(rig.Vm.HasUnsavedChanges);
            }
            finally {window.TryCloseAsync().GetAwaiter().GetResult();StaTest.Pump();}
        });
    }
    [Fact] public async Task SpeechVerificationTracksOnlySpeechConfigurationEvenWithAnIncompleteAiDraft()
    {
        await StaTest.RunAsync(()=>
        {
            var proof=new ServiceVerification(new byte[32]);var initial=TestSettings.Configured() with { Asr=new() { SecretFingerprint=proof.SecretDigest("asr") } };initial=proof.RecordAsrSuccess(initial,proof.AsrFingerprint(initial));
            using var rig=new Rig(initial);var window=new SettingsWindow(rig.Vm,()=>Task.FromResult(SettingsCloseChoice.Discard));window.Show();
            try
            {
                var tabs=(TabControl)window.FindName("SettingsTabs");tabs.SelectedIndex=1;StaTest.Pump();
                var status=Assert.IsType<TextBlock>(window.FindName("SpeechVerificationStatus"));Assert.Contains("已验证",status.Text);
                rig.Vm.AddAiProfile();StaTest.Pump();Assert.Contains("已验证",status.Text);
                rig.Vm.AsrApiKey="different-key";StaTest.Pump();Assert.Contains("未验证",status.Text);
                rig.Vm.AsrApiKey="asr";StaTest.Pump();Assert.Contains("已验证",status.Text);
                rig.Vm.AsrBaseAddress="invalid address";StaTest.Pump();Assert.Contains("未验证",status.Text);
            }
            finally {window.TryCloseAsync().GetAwaiter().GetResult();StaTest.Pump();}
        });
    }
    [Theory] [InlineData(false,0u)] [InlineData(true,17u)]
    public async Task OverlayEntryShowAndCaptureRespectTheSavedProtectionSwitch(bool enabled,uint affinity)
    {
        await StaTest.RunAsync(()=>
        {
            var settings=WithProtection(enabled);var stub=new StubServices();var api=new AffinityApi();
            var runtime=new SessionCoordinator(stub,stub,stub,stub,stub,()=>settings,new InMemorySessionStore(),new ManualTimeProvider());
            var host=new OverlayHost(runtime,stub,()=>settings,_=>Task.CompletedTask,()=>Task.CompletedTask,()=>Task.CompletedTask,api,new Keys());
            try
            {
                runtime.StartNewAsync(default).GetAwaiter().GetResult();runtime.SetAuxiliaryModeAsync(true,default).GetAwaiter().GetResult();
                var result=host.OpenAsync(default).GetAwaiter().GetResult();Assert.True(result.Success);Assert.Equal(affinity,result.EffectiveAffinity);
                api.Value=enabled?0u:17u;host.Show();StaTest.Pump();Assert.True(host.CurrentWindow!.IsVisible);Assert.Equal(affinity,api.Value);
                Assert.True(runtime.CanExecute(AppCommand.CaptureToDraft));runtime.ExecuteAsync(AppCommand.CaptureToDraft,default).GetAwaiter().GetResult();Assert.Single(runtime.State.Draft.Images);
                host.CurrentWindow.Hide();host.Show();Assert.Equal(affinity,host.CurrentWindow.Protection.EffectiveAffinity);
                host.Close();host.OpenAsync(default).GetAwaiter().GetResult();host.Show();Assert.Equal(affinity,host.CurrentWindow!.Protection.EffectiveAffinity);
            }
            finally {host.Close();runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();}
        });
    }
    private static void SavePreview(Window window,string name)
    {
        var directory=Environment.GetEnvironmentVariable("EXAMHELPER_TEST_UI_OUTPUT");if(string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);window.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file=System.IO.File.Create(System.IO.Path.Combine(directory,name));encoder.Save(file);
    }
    private static AppSettings WithProtection(bool enabled)
    {
        var json=JsonSerializer.SerializeToNode(TestSettings.Configured())!;json["Ui"]!["CaptureProtectionEnabled"]=enabled;return json.Deserialize<AppSettings>()!;
    }
    private sealed class AffinityApi:IWindowProtectionApi
    {
        public uint Value;public bool Set(nint h,uint a,out int e){Value=a;e=0;return true;}public bool Get(nint h,out uint a,out int e){a=Value;e=0;return true;}
    }
    private sealed class Keys:IHotkeyApi
    {
        public bool Register(nint h,int id,uint m,uint k,out int e){e=0;return true;}public bool Unregister(nint h,int id)=>true;
    }
    private sealed class Rig:IDisposable
    {
        private readonly HttpClient _http=new(new FakeHttpHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        public InMemorySettingsStore Store{get;}public SettingsViewModel Vm{get;}
        public Rig(AppSettings? initial=null){var proof=new ServiceVerification(new byte[32]);var secrets=new InMemorySecretStore();Store=new(initial??TestSettings.Configured());Vm=new(Store.Value,"ai","asr",Store,secrets,proof,new(_http,secrets,proof,[1]),new RecordingController(new StubServices(),new ManualTimeProvider()),_=>{});}
        public void Dispose(){Vm.DisposeAsync().AsTask().GetAwaiter().GetResult();_http.Dispose();}
    }
}
