using System.Collections.Immutable;
namespace ExamHelper.Core.Sessions;
public sealed record BatchDeleteResult(ImmutableArray<Guid> DeletedIds,ImmutableDictionary<Guid,string> Failures)
{
    public ImmutableDictionary<Guid,string> CleanupFailures { get; init; }=ImmutableDictionary<Guid,string>.Empty;
    public static BatchDeleteResult Empty { get; }=new([],ImmutableDictionary<Guid,string>.Empty);
}
