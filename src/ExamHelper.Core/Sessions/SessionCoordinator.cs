using System.Collections.Immutable;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Sessions;

public sealed class SessionCoordinator : IAsyncDisposable
{
    private readonly IAiClient _ai;
    private readonly IScreenCapture _capture;
    private readonly IImageStore _images;
    private readonly Func<AppSettings> _settings;
    private readonly ISessionStore _store;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CommandPolicy _policy = new();
    private readonly DraftBuffer _draft = new();
    private readonly SpeechInsertionUndo _undo;
    private readonly RecordingController _recording;
    private readonly SpeechCoordinator _speech;
    private readonly HashSet<Guid> _receivedSpeech = [];
    private readonly CancellationTokenSource _lifetime = new();
    private SessionSnapshot _state = new() { IsAuxiliaryMode = false };
    private CancellationTokenSource? _requestCancellation, _captureCancellation;
    private RequestSnapshot? _active;
    private long _requestEpoch, _captureEpoch, _lastStreamSave;
    private Task _background = Task.CompletedTask;
    private bool _disposed;
    public SessionSnapshot State => _state;
    public string? Error { get; private set; }
    public TimeSpan RecordingElapsed => _recording.Elapsed;
    public event Action<SessionSnapshot>? StateChanged;
    public event Action<AppCommand>? UiCommandRequested;

    public SessionCoordinator(IAiClient ai,IAsrClient asr,IMicrophoneRecorder microphone,IScreenCapture capture,
        IImageStore images,Func<AppSettings> settings,ISessionStore store,TimeProvider clock)
    {
        _ai = ai; _capture = capture; _images = images; _settings = settings; _store = store; _clock = clock;
        _undo = new(_draft,clock); _recording = new(microphone,clock);
        _speech = new(_recording,asr,() => _settings().Asr,() => (_state.Id,_state.SpeechGeneration));
        _speech.TranscriptReady += OnTranscript;
        _speech.StateChanged += OnSpeechStateChanged;
    }

    public async Task StartNewAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(_state.IsAuxiliaryMode) throw new InvalidOperationException("请先返回主页。");
            await StartFreshLockedAsync(ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    private async Task StartFreshLockedAsync(CancellationToken ct)
    {
        var previous = _state; var draft = _draft.Snapshot();
        _undo.Invalidate(); _receivedSpeech.Clear(); _draft.Clear();
        var generation = checked(previous.Generation + 1);
        _state = new() { Generation = generation,SpeechGeneration = generation,UpdatedAt = _clock.GetUtcNow() };
        Error = null; Publish();
        try { await SaveLockedAsync(ct).ConfigureAwait(false); }
        catch { _draft.Restore(draft); _state = previous; Publish(); throw; }
    }
    public async Task ContinueAsync(Guid id,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(_state.IsAuxiliaryMode) throw new InvalidOperationException("请先返回主页。");
            var previous = _state; var previousDraft = _draft.Snapshot();
            var saved = await _store.LoadAsync(id,ct).ConfigureAwait(false) ?? throw new InvalidOperationException("历史对话不存在。");
            var generation = checked(Math.Max(_state.Generation,saved.Generation) + 1);
            _undo.Invalidate(); _receivedSpeech.Clear(); _draft.Restore(saved.Draft);
            _state = RequestRecovery.RecoverOnStartup(saved) with { Generation = generation,SpeechGeneration = generation,IsAuxiliaryMode = true };
            Error = null; Publish();
            try { await SaveLockedAsync(ct).ConfigureAwait(false); }
            catch { _state = previous; _draft.Restore(previousDraft); Publish(); throw; }
        }
        finally { _gate.Release(); }
    }
    public bool CanExecute(AppCommand command) => _policy.CanExecute(command,_state);
    public Task WaitForIdleAsync(CancellationToken ct) => _background.WaitAsync(ct);

    public async Task ExecuteAsync(AppCommand command,CancellationToken ct)
    {
        if(command == AppCommand.ReturnHome) { await ReturnHomeAsync(ct).ConfigureAwait(false); return; }
        if(command == AppCommand.Exit) { await ExitAsync(ct).ConfigureAwait(false); return; }
        CaptureWork? capture = null; (Guid SessionId,long Generation)? speech = null; bool ui = false;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            _state = _state with { Draft = _draft.Snapshot(),SpeechUndoAvailable = _undo.CanUndo };
            var available = _policy.Evaluate(command,_state);
            if(!available.Allowed) throw new InvalidOperationException(available.Reason);
            Error = null;
            switch(command)
            {
                case AppCommand.CaptureAndSend: case AppCommand.CaptureToDraft:
                    _captureCancellation?.Dispose(); _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
                    capture = new(_state.Id,_state.Generation,++_captureEpoch,command == AppCommand.CaptureAndSend,_settings(),_captureCancellation.Token,ct);
                    _state = _state with { CaptureInProgress = true,DirectCaptureInProgress = capture.Direct }; Publish(); break;
                case AppCommand.Send:
                    await StartRequestLockedAsync(NewRequest(_draft.Snapshot(),_settings(),false),false,ct).ConfigureAwait(false); break;
                case AppCommand.RetryRequest:
                    var retry = _state.PendingRequest!;
                    if(retry.RestoredDraftRevision == _draft.Snapshot().Revision) _draft.Clear();
                    await StartRequestLockedAsync(retry with { Generation = _state.Generation,Status = MessageStatus.Pending,RestoredDraftRevision = null },true,ct).ConfigureAwait(false); break;
                case AppCommand.DiscardRequest:
                    if(_state.PendingRequest!.RestoredDraftRevision == _draft.Snapshot().Revision) _draft.Clear();
                    _state = _state with { PendingRequest = null }; Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); break;
                case AppCommand.StopAnswer:
                    StopAnswerLocked(); Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); break;
                case AppCommand.ClearInput:
                    CancelCaptureLocked(); _undo.Invalidate(); _receivedSpeech.Clear();
                    _state = _state with { SpeechGeneration = checked(_state.SpeechGeneration + 1),SpeechState = SpeechState.Idle };
                    await _speech.CancelAsync().ConfigureAwait(false);
                    _draft.Clear(); Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); break;
                case AppCommand.NewConversation:
                    await SaveLockedAsync(ct).ConfigureAwait(false); await StartFreshLockedAsync(ct).ConfigureAwait(false); break;
                case AppCommand.ToggleRecording: case AppCommand.RetrySpeech: case AppCommand.DiscardSpeech:
                    _undo.Invalidate(); speech = (_state.Id,_state.SpeechGeneration);
                    _state = _state with { SpeechState = command == AppCommand.ToggleRecording && _speech.State == SpeechState.Idle ? SpeechState.Recording :
                        command == AppCommand.DiscardSpeech ? SpeechState.Idle : SpeechState.Recognizing };
                    Publish(); break;
                case AppCommand.UndoSpeech:
                    if(_undo.TryUndo()) { Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); } break;
                case AppCommand.ToggleVisibility: case AppCommand.ToggleCollapse: ui = true; break;
            }
        }
        finally { _gate.Release(); }
        if(capture is not null) await CompleteCaptureAsync(capture).ConfigureAwait(false);
        if(speech is { } speechContext)
        {
            try
            {
                if(command == AppCommand.ToggleRecording) await _speech.ToggleAsync(ct,speechContext).ConfigureAwait(false);
                else if(command == AppCommand.RetrySpeech) await _speech.RetryAsync(ct,speechContext).ConfigureAwait(false);
                else await _speech.DiscardAsync(speechContext).ConfigureAwait(false);
            }
            finally { await SynchronizeSpeechAsync().ConfigureAwait(false); }
        }
        if(ui) UiCommandRequested?.Invoke(command);
    }
    public async Task AppendSpeechResultAsync(SpeechTranscript result,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(!_state.IsAuxiliaryMode || result.SessionId != _state.Id || result.Generation != _state.SpeechGeneration || !_receivedSpeech.Add(result.OperationId)) return;
            _undo.Append(result.OperationId,result.Transcript); Publish(); await SaveLockedAsync(ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async Task RemoveImageAsync(Guid id,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(!_state.IsAuxiliaryMode) return;
            _draft.RemoveImage(id); Publish(); await SaveLockedAsync(ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async Task SetUndoHoverAsync(bool hovering)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { if(_disposed) return; if(hovering) _undo.PointerEnter(); else _undo.PointerLeave(); Publish(); }
        finally { _gate.Release(); }
    }
    public async Task RefreshUndoAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { if(!_disposed && _state.SpeechUndoAvailable != _undo.CanUndo) Publish(); }
        finally { _gate.Release(); }
    }

    private RequestSnapshot NewRequest(DraftSnapshot draft,AppSettings settings,bool direct) => new()
    {
        SessionId = _state.Id,Generation = _state.Generation,Text = draft.Text,Images = draft.Images,
        IsDirectCapture = direct,Prompt = direct ? settings.DirectCapturePrompt : "",SystemPrompt = PromptBuilder.BuildSystem(settings)
    };
    private async Task StartRequestLockedAsync(RequestSnapshot request,bool retry,CancellationToken ct)
    {
        _undo.Invalidate();
        if(!retry) _draft.Clear();
        var messages = _state.Messages;
        if(!retry)
            messages = messages.Add(new() { RequestId = request.RequestId,Role = MessageRole.User,Text = ContextBuilder.RequestText(request),Images = request.Images,CreatedAt = _clock.GetUtcNow() })
                .Add(new() { RequestId = request.RequestId,Role = MessageRole.Assistant,CreatedAt = _clock.GetUtcNow() });
        else messages = messages.Select(m => m.RequestId == request.RequestId ? m with { Text = m.Role == MessageRole.Assistant ? "" : m.Text,Status = MessageStatus.Pending } : m).ToImmutableArray();
        _state = _state with { Messages = messages,Requests = _state.Requests.Where(r => r.RequestId != request.RequestId).Append(request).ToImmutableArray(),PendingRequest = null,AnswerState = AnswerState.Generating };
        _active = request; Publish();
        try { await SaveLockedAsync(ct).ConfigureAwait(false); }
        catch { ApplyFailureLocked(request); Publish(); throw; }
        _requestCancellation?.Dispose(); _requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        var epoch = ++_requestEpoch; _lastStreamSave = _clock.GetTimestamp();
        var context = ContextBuilder.Build(_state,request,_settings()); var settings = _settings().Ai;
        _background = RunRequestAsync(request,epoch,context,settings,_requestCancellation.Token);
    }
    private async Task RunRequestAsync(RequestSnapshot request,long epoch,AiRequest context,AiSettings settings,CancellationToken ct)
    {
        try
        {
            bool final = false, complete = false;
            await foreach(var chunk in _ai.StreamAsync(context,settings,ct).ConfigureAwait(false))
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if(!Current(request,epoch)) return;
                    if(chunk.Text.Length > 0)
                        _state = _state with { Messages = _state.Messages.Select(m => m.RequestId == request.RequestId && m.Role == MessageRole.Assistant ? m with { Text = m.Text + chunk.Text } : m).ToImmutableArray() };
                    if(chunk.IsFinal) { final = true; complete = chunk.IsComplete; }
                    Publish();
                    if(_clock.GetElapsedTime(_lastStreamSave) >= TimeSpan.FromMilliseconds(500))
                    { await SaveLockedAsync(CancellationToken.None).ConfigureAwait(false); _lastStreamSave = _clock.GetTimestamp(); }
                }
                finally { _gate.Release(); }
            }
            if(!final || !complete) throw new ServiceFailure(FailureCategory.Transport,"回答未完整结束，请手动重试或丢弃此请求。");
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if(!Current(request,epoch)) return;
                FinishLocked(request,MessageStatus.Complete); Publish(); await SaveLockedAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }
        catch(Exception error)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if(!Current(request,epoch)) return;
                if(error is OperationCanceledException && ct.IsCancellationRequested) FinishLocked(request,MessageStatus.Stopped);
                else { ApplyFailureLocked(request); Error = SafeError(error); }
                Publish(); await TrySaveLockedAsync().ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }
    }
    private bool Current(RequestSnapshot request,long epoch) => !_disposed && _state.IsAuxiliaryMode &&
        request.SessionId == _state.Id && request.Generation == _state.Generation && epoch == _requestEpoch &&
        _active?.RequestId == request.RequestId && _state.AnswerState == AnswerState.Generating;
    private void FinishLocked(RequestSnapshot request,MessageStatus status)
    {
        _state = _state with
        {
            Messages = _state.Messages.Select(m => m.RequestId == request.RequestId ? m with { Status = m.Role == MessageRole.User ? MessageStatus.Complete : status } : m).ToImmutableArray(),
            Requests = _state.Requests.Select(r => r.RequestId == request.RequestId ? r with { Status = status } : r).ToImmutableArray(),
            PendingRequest = null,AnswerState = AnswerState.Idle
        };
        _active = null;
    }
    private void ApplyFailureLocked(RequestSnapshot request)
    {
        _state = _state with
        {
            Messages = _state.Messages.Select(m => m.RequestId == request.RequestId ? m with { Status = MessageStatus.Failed } : m).ToImmutableArray(),
            Requests = _state.Requests.Select(r => r.RequestId == request.RequestId ? r with { Status = MessageStatus.Failed } : r).ToImmutableArray(),
            AnswerState = AnswerState.Idle,Draft = _draft.Snapshot()
        };
        _state = RequestRecovery.ForFailure(_state,request);
        if(_draft.Snapshot() != _state.Draft) _draft.Restore(_state.Draft);
        _active = null;
    }
    private void StopAnswerLocked()
    {
        ++_requestEpoch; _requestCancellation?.Cancel();
        if(_active is { } request) FinishLocked(request,MessageStatus.Stopped);
    }
    private void CancelCaptureLocked()
    {
        ++_captureEpoch; _captureCancellation?.Cancel();
        _state = _state with { CaptureInProgress = false,DirectCaptureInProgress = false };
    }
    private async Task CompleteCaptureAsync(CaptureWork work)
    {
        try
        {
            var image = await _capture.CaptureAsync(work.Token).ConfigureAwait(false);
            var saved = await _images.AddAsync(image,work.Token).ConfigureAwait(false);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if(!CaptureCurrent(work)) return;
                work.Token.ThrowIfCancellationRequested();
                _state = _state with { CaptureInProgress = false,DirectCaptureInProgress = false };
                if(work.Direct) await StartRequestLockedAsync(NewRequest(new("",[saved],_draft.Snapshot().Revision),work.Settings,true),false,work.RequestToken).ConfigureAwait(false);
                else { _draft.AppendImage(saved); Publish(); await SaveLockedAsync(CancellationToken.None).ConfigureAwait(false); }
            }
            finally { _gate.Release(); }
        }
        catch(Exception error)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if(!CaptureCurrent(work)) return;
                _state = _state with { CaptureInProgress = false,DirectCaptureInProgress = false };
                if(error is not OperationCanceledException) Error = SafeError(error);
                Publish(); await TrySaveLockedAsync().ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }
    }
    private bool CaptureCurrent(CaptureWork work) => _state.IsAuxiliaryMode && _state.Id == work.SessionId && _state.Generation == work.Generation && _captureEpoch == work.Epoch;
    public Task ReturnHomeAsync(CancellationToken ct) => LeaveAsync(false,ct);
    public Task ExitAsync(CancellationToken ct) => LeaveAsync(true,ct);
    public Task AbortEntryAsync(CancellationToken ct) => LeaveAsync(true,ct,false);
    private async Task LeaveAsync(bool preserveDraft,CancellationToken ct,bool notifyUi = true)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(!_state.IsAuxiliaryMode) return;
            var before = _draft.Snapshot(); var pendingBefore = _state.PendingRequest;
            StopAnswerLocked(); CancelCaptureLocked(); _undo.Invalidate();
            _state = _state with { Generation = checked(_state.Generation + 1),SpeechGeneration = checked(_state.SpeechGeneration + 1),SpeechState = SpeechState.Idle };
            await _speech.CancelAsync().ConfigureAwait(false);
            if(!preserveDraft) { _draft.Clear(); _state = _state with { PendingRequest = null }; }
            _state = _state with { IsAuxiliaryMode = false }; Publish();
            try { await SaveLockedAsync(ct).ConfigureAwait(false); }
            catch { _draft.Restore(before); _state = _state with { IsAuxiliaryMode = true,PendingRequest = pendingBefore }; Publish(); throw; }
        }
        finally { _gate.Release(); }
        if(notifyUi) UiCommandRequested?.Invoke(preserveDraft ? AppCommand.Exit : AppCommand.ReturnHome);
    }
    private void Publish()
    {
        _state = _state with { Draft = _draft.Snapshot(),SpeechUndoAvailable = !_disposed && _undo.CanUndo,UpdatedAt = _clock.GetUtcNow() };
        StateChanged?.Invoke(_state);
    }
    private async Task SaveLockedAsync(CancellationToken ct)
    {
        try { await _store.SaveAsync(_state,ct).ConfigureAwait(false); }
        catch(Exception error)
        {
            Error = SafeError(error); StateChanged?.Invoke(_state); throw;
        }
    }
    private async Task TrySaveLockedAsync()
    {
        try { await SaveLockedAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
    }
    private void OnTranscript(SpeechTranscript result) => _ = ReceiveTranscriptAsync(result);
    private async Task ReceiveTranscriptAsync(SpeechTranscript result)
    {
        try { await AppendSpeechResultAsync(result,CancellationToken.None).ConfigureAwait(false); } catch { }
    }
    private void OnSpeechStateChanged() => _ = SynchronizeSpeechAsync();
    private async Task SynchronizeSpeechAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(_disposed || !_state.IsAuxiliaryMode) return;
            _state = _state with { SpeechState = _speech.State };
            if(_speech.Error is not null) Error = _speech.Error;
            Publish(); await TrySaveLockedAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    private static string SafeError(Exception error) => error is ServiceFailure ? error.Message : "操作未完成，内容已保留，请检查配置、设备或本地存储。";
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(_disposed) return; _disposed = true; _lifetime.Cancel(); StopAnswerLocked(); CancelCaptureLocked();
            _state = _state with { IsAuxiliaryMode = false }; _undo.Dispose();
            _speech.TranscriptReady -= OnTranscript; _speech.StateChanged -= OnSpeechStateChanged;
        }
        finally { _gate.Release(); }
        await _speech.DisposeAsync().ConfigureAwait(false); await _recording.DisposeAsync().ConfigureAwait(false);
        _requestCancellation?.Dispose(); _captureCancellation?.Dispose(); _lifetime.Dispose();
    }
    private sealed record CaptureWork(Guid SessionId,long Generation,long Epoch,bool Direct,AppSettings Settings,CancellationToken Token,CancellationToken RequestToken);
}