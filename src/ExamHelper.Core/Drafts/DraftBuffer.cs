using System.Collections.Immutable;
namespace ExamHelper.Core.Drafts;
public sealed class DraftBuffer
{
    private DraftSnapshot _value;
    public event Action<DraftChange>? Changed;
    public DraftBuffer(DraftSnapshot? initial = null) => _value = initial ?? DraftSnapshot.Empty;
    public DraftSnapshot Snapshot() => _value;
    public void AppendText(string transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        if (transcript.Length == 0) return;
        Set(_value with { Text = _value.Text + transcript }, DraftChangeKind.TextAppended);
    }
    public void AppendImage(ImageRef image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_value.Images.Any(i => i.Id == image.Id)) throw new ArgumentException("图片已在草稿中。", nameof(image));
        Set(_value with { Images = _value.Images.Add(image) }, DraftChangeKind.ImageAdded);
    }
    public void RemoveImage(Guid id)
    {
        var images = _value.Images.Where(i => i.Id != id).ToImmutableArray();
        if (images.Length != _value.Images.Length) Set(_value with { Images = images }, DraftChangeKind.ImageRemoved);
    }
    public void Clear() => Set(DraftSnapshot.Empty, DraftChangeKind.Cleared);
    public void Restore(DraftSnapshot snapshot) => Set(snapshot, DraftChangeKind.Restored);
    internal bool RemoveSpeechTail(string suffix)
    {
        if (!_value.Text.EndsWith(suffix, StringComparison.Ordinal)) return false;
        Set(_value with { Text = _value.Text[..^suffix.Length] }, DraftChangeKind.SpeechUndone);
        return true;
    }
    private void Set(DraftSnapshot next, DraftChangeKind kind)
    {
        _value = next with { Revision = checked(_value.Revision + 1) };
        Changed?.Invoke(new(kind, _value.Revision));
    }
}