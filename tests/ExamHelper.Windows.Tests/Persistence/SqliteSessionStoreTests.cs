using System.IO;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Persistence;
using Xunit;
namespace ExamHelper.Windows.Tests.Persistence;
public class SqliteSessionStoreTests
{
    [Fact]
    public async Task RoundTripsTextImagesDraftAndPartialAnswer()
    {
        var root = NewRoot();
        try
        {
            var store = new SqliteSessionStore(Path.Combine(root,"history.db"));
            var image = new ImageRef(Guid.NewGuid(),"photo.png",1920,1080);
            var request = new RequestSnapshot { Text = "原始提问",Prompt = "发送时提示",SystemPrompt = "冻结规则",Images = [image],Status = MessageStatus.Failed };
            var session = new SessionSnapshot { Draft = new("未发草稿",[image],5),Requests = [request],PendingRequest = request,
                Messages = [ new() { RequestId = request.RequestId,Role = MessageRole.User,Text = request.Text,Images = [image],Status = MessageStatus.Failed },
                new() { RequestId = request.RequestId,Role = MessageRole.Assistant,Text = "部分回答",Status = MessageStatus.Failed }] };
            await store.SaveAsync(session,default); var loaded = await store.LoadAsync(session.Id,default);
            Assert.NotNull(loaded); Assert.Equal("未发草稿",loaded.Draft.Text); Assert.Equal(image,Assert.Single(loaded.Draft.Images));
            Assert.Equal("部分回答",loaded.Messages[1].Text); Assert.Equal("冻结规则",loaded.PendingRequest!.SystemPrompt);
            Assert.True(Assert.Single(await store.ListAsync(default)).HasSavedDraft);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task InterruptedGenerationBecomesRecoverableFailureWithoutLosingDraft()
    {
        var root = NewRoot();
        try
        {
            var store = new SqliteSessionStore(Path.Combine(root,"history.db"));
            var request = new RequestSnapshot { Text = "旧请求" };
            var session = new SessionSnapshot { Requests = [request],AnswerState = AnswerState.Generating,SpeechState = SpeechState.Recording,
                Draft = new("新的草稿",[],2),Messages = [new() { RequestId = request.RequestId,Role = MessageRole.Assistant,Text = "已收到片段" }] };
            await store.SaveAsync(session,default); await store.RecoverInterruptedAsync(default);
            var loaded = (await store.LoadAsync(session.Id,default))!;
            Assert.Equal(AnswerState.Idle,loaded.AnswerState); Assert.Equal(SpeechState.Idle,loaded.SpeechState);
            Assert.Equal("新的草稿",loaded.Draft.Text); Assert.Equal("已收到片段",loaded.Messages[0].Text);
            Assert.Equal(MessageStatus.Failed,loaded.Messages[0].Status); Assert.NotNull(loaded.PendingRequest);
            Assert.False(loaded.IsAuxiliaryMode); Assert.True(loaded.Generation > session.Generation);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task ReadOnlyDatabaseDoesNotReportSavedAndOldSnapshotRemains()
    {
        var root = NewRoot(); var path = Path.Combine(root,"history.db");
        try
        {
            var store = new SqliteSessionStore(path); var session = new SessionSnapshot();
            await store.SaveAsync(session,default);
            File.SetAttributes(path,FileAttributes.ReadOnly);
            var error = await Assert.ThrowsAsync<ServiceFailure>(() => store.SaveAsync(session with { Draft = new("must not save",[],1) },default));
            Assert.Equal(FailureCategory.Storage,error.Category);
            File.SetAttributes(path,FileAttributes.Normal);
            Assert.False((await store.LoadAsync(session.Id,default))!.Draft.HasContent);
        }
        finally { if(File.Exists(path)) File.SetAttributes(path,FileAttributes.Normal); Directory.Delete(root,true); }
    }
    private static string NewRoot() { var path = Path.Combine(Path.GetTempPath(),"ExamHelper-db-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}