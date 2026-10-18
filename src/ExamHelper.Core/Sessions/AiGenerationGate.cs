namespace ExamHelper.Core.Sessions;
public sealed class AiGenerationGate
{
    private readonly object _sync = new();
    private Lease? _current;
    public bool IsBusy { get { lock(_sync) return _current is not null; } }
    public Guid? OwnerId { get { lock(_sync) return _current?.Owner; } }
    public event Action? Changed;
    public IDisposable? TryAcquire(Guid owner)
    {
        Lease lease;
        lock(_sync) { if(_current is not null) return null; _current = lease = new(this,owner); }
        Changed?.Invoke(); return lease;
    }
    private void Release(Lease lease)
    {
        lock(_sync) { if(!ReferenceEquals(_current,lease)) return; _current = null; }
        Changed?.Invoke();
    }
    private sealed class Lease(AiGenerationGate gate,Guid owner) : IDisposable
    {
        private int _released;
        public Guid Owner { get; } = owner;
        public void Dispose() { if(Interlocked.Exchange(ref _released,1) == 0) gate.Release(this); }
    }
}
