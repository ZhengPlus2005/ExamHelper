using System.Collections.Immutable;
namespace ExamHelper.Core.Drafts;
public sealed record ImageRef(Guid Id, string RelativePath, int Width, int Height);
public sealed record DraftSnapshot(string Text, ImmutableArray<ImageRef> Images, long Revision)
{
    public static DraftSnapshot Empty { get; } = new("", [], 0);
    public bool HasContent => Text.Length > 0 || Images.Length > 0;
}
public enum DraftChangeKind { TextAppended, ImageAdded, ImageRemoved, Cleared, Restored, SpeechUndone }
public sealed record DraftChange(DraftChangeKind Kind, long Revision);