using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExamHelper.App.Controls;
using ExamHelper.App.Home;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Capture;
using ExamHelper.Windows.Images;
using ExamHelper.Windows.Persistence;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class FinalReview04Tests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ChoosingUntestedDefaultChangesRuntimeWithoutChangingInFlightRequest(bool generating)
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ConversationWorkspaceRig();
            var pa=rig.Settings.AiProfiles!.Value[0];var allowed=pa.Models!.Value[0] with { Id=Guid.NewGuid() };
            var blocked=new AiModelProfile { Name="unverified" };
            var pb=pa with { Id=Guid.NewGuid(),Name="B",Models=[blocked,allowed],DefaultModelId=blocked.Id };
            rig.Settings=rig.Settings with { AiProfiles=[pa,pb] };
            var runtime=rig.Workspace.NewAsync(true,default).GetAwaiter().GetResult();
            runtime.UpdateDraftTextAsync("keep text",default).GetAwaiter().GetResult();
            runtime.AppendImagesAsync([new(Guid.NewGuid(),"owned.png",64,32)],default).GetAwaiter().GetResult();
            DeferredAiClient.Attempt? active=null;
            if(generating) { active=rig.Ai.QueueAttempt();runtime.ExecuteAsync(AppCommand.Send,default).GetAwaiter().GetResult();active.Started.Task.GetAwaiter().GetResult(); }
            var before=runtime.State.Draft;var selector=new ModelSelector { AuxiliaryInteraction=true };
            selector.Configure(rig.Settings,runtime.State.Selection,true,rig.Verification);
            selector.SelectionRequested+=s=>runtime.SelectModelAsync(s,default).GetAwaiter().GetResult();
            var window=new Window { Content=selector,Width=480,Height=360 };
            try
            {
                window.Show();StaTest.Pump();selector.OpenHoverMenu(ModelSelectorPart.Profile);window.UpdateLayout();StaTest.Pump();
                var profile=MarkdownViewTests.Nodes(selector).OfType<Button>().Single(b=>b.Tag is AiServiceProfile p && p.Id==pb.Id);
                DwellBehavior.GetCommand(profile)!.Execute(null);
                Assert.Equal(pb.Id,runtime.State.Selection!.ProfileId);
                Assert.Equal(!generating,runtime.CanExecute(AppCommand.Send));Assert.Equal(blocked.Id,runtime.State.Selection.ModelId);
                Assert.False(runtime.CanExecute(AppCommand.CaptureAndSend));
                Assert.Equal(before,runtime.State.Draft);
                selector.Configure(rig.Settings,runtime.State.Selection,true,rig.Verification);
                Assert.False(selector.IsMenuOpen);
                if(active is not null)
                {
                    Assert.Equal(pa.Models.Value[0].Name,active.SettingsUsed!.Model);
                    active.Text("received before failure");active.Fail(new IOException("owned failure"));runtime.WaitForIdleAsync(default).GetAwaiter().GetResult();
                    Assert.Equal("received before failure",runtime.State.Messages[^1].Text);
                    Assert.True(runtime.CanExecute(AppCommand.RetryRequest));
                }
                selector.OpenHoverMenu(ModelSelectorPart.Model);window.UpdateLayout();StaTest.Pump();
                var model=MarkdownViewTests.Nodes(selector).OfType<Button>().Single(b=>b.Tag is ModelChoice m && m.Model.Id==allowed.Id);
                DwellBehavior.GetCommand(model)!.Execute(null);
                Assert.Equal(allowed.Id,runtime.State.Selection.ModelId);
                var next=rig.Ai.QueueAttempt();runtime.ExecuteAsync(generating?AppCommand.RetryRequest:AppCommand.Send,default).GetAwaiter().GetResult();
                next.Started.Task.GetAwaiter().GetResult();Assert.Equal(allowed.Name,next.SettingsUsed!.Model);next.Complete();runtime.WaitForIdleAsync(default).GetAwaiter().GetResult();
            }
            finally { active?.Complete();window.Close();rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task LateImportReleasesOnlyImagesRejectedBeforeRuntimeAcceptance(bool failSave)
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-review-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var registry=new ImageReferenceRegistry();var images=new FileImageStore(Path.Combine(root,"images"),registry);
        var store=new FailingStore(new SqliteSessionStore(Path.Combine(root,"sessions.db"),registry));
        var stub=new StubServices();var settings=TestSettings.Configured();
        await using var workspace=new ConversationWorkspace(store,()=>new(stub,stub,stub,stub,images,()=>settings,store,new ManualTimeProvider()));
        try
        {
            var a=await workspace.NewAsync(false,default);var importer=new DelayedImporter(new ImageImportService(images));
            var controller=new HomeDraftController(()=>workspace.Selected,importer);
            var import=controller.ImportBitmapAsync(ClipboardPaste04Tests.Bitmap(),default);
            var imported=Assert.Single(await importer.Ready.Task);
            var cleaner=new ImageReferenceCleaner(store,images,registry,workspace.LiveImageIds);
            var b=await workspace.NewAsync(false,default);
            if(!failSave) await workspace.DeleteAsync(a.State.Id,default);else store.Fail=true;
            await cleaner.ReconcileAsync(default);Assert.True(registry.Contains(imported.Id));
            importer.Release.TrySetResult();await Assert.ThrowsAnyAsync<Exception>(()=>import);
            await cleaner.ReconcileAsync(default);await cleaner.ReconcileAsync(default);
            if(failSave)
            {
                Assert.Single(a.State.Draft.Images);Assert.True(registry.Contains(imported.Id));Assert.True(File.Exists(Path.Combine(root,"images",imported.RelativePath)));
            }
            else
            {
                Assert.False(registry.Contains(imported.Id));Assert.False(File.Exists(Path.Combine(root,"images",imported.RelativePath)));
                Assert.Null(await store.LoadAsync(a.State.Id,default));
            }
            Assert.Empty(b.State.Draft.Images);
        }
        finally { store.Fail=false;await workspace.DisposeAsync();Directory.Delete(root,true); }
    }
    [Fact]
    public async Task PartialFileImportFailureReleasesItsOwnPendingImages()
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-partial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var registry=new ImageReferenceRegistry();var images=new FileImageStore(Path.Combine(root,"images"),registry);
            var kept=await images.AddAsync(new([1,2,3],1,1),default);
            var good=Path.Combine(root,"good.png");var bad=Path.Combine(root,"bad.png");
            await File.WriteAllBytesAsync(good,ClipboardPaste04Tests.Encode(ClipboardPaste04Tests.Bitmap(),new PngBitmapEncoder()));await File.WriteAllTextAsync(bad,"broken image");
            await Assert.ThrowsAnyAsync<Exception>(()=>new ImageImportService(images).ImportFilesAsync([good,bad],default));
            await images.DeleteUnreferencedAsync(new HashSet<Guid>(),default);
            Assert.Single(Directory.GetFiles(Path.Combine(root,"images"),"*.png"));Assert.True(registry.Contains(kept.Id));
        }
        finally { Directory.Delete(root,true); }
    }
    private sealed class DelayedImporter(IImageImporter inner):IImageImporter
    {
        public TaskCompletionSource<IReadOnlyList<ImageRef>> Ready { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task AbandonAsync(IReadOnlyList<ImageRef> images,CancellationToken ct)=>inner.AbandonAsync(images,ct);
        public Task<IReadOnlyList<ImageRef>> ImportFilesAsync(IEnumerable<string> files,CancellationToken ct)=>inner.ImportFilesAsync(files,ct);
        public async Task<IReadOnlyList<ImageRef>> ImportBitmapAsync(BitmapSource bitmap,CancellationToken ct)
        { var result=await inner.ImportBitmapAsync(bitmap,ct);Ready.TrySetResult(result);await Release.Task;return result; }
    }
    private sealed class FailingStore(ISessionStore inner):ISessionStore
    {
        public bool Fail;
        public Task SaveAsync(SessionSnapshot s,CancellationToken ct)=>Fail?Task.FromException(new IOException("owned save failure")):inner.SaveAsync(s,ct);
        public Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct)=>inner.LoadAsync(id,ct);
        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)=>inner.ListAsync(ct);
        public Task DeleteAsync(Guid id,CancellationToken ct)=>inner.DeleteAsync(id,ct);
    }
}
