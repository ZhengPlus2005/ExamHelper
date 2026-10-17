namespace ExamHelper.Core.Common;
public enum FailureCategory { Authentication, Permission, RateLimit, InvalidModel, InvalidPayload, Transport, Timeout, Storage }
public sealed class ServiceFailure : Exception
{
    public FailureCategory Category { get; }
    public string? Code { get; }
    public ServiceFailure(FailureCategory category, string message, string? code = null, Exception? inner = null)
        : base(message, inner) { Category = category; Code = code; }
}