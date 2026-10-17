using System.Net;
using ExamHelper.Core.Common;
namespace ExamHelper.Windows.Providers;
public static class ProviderErrorMapper
{
    public static ServiceFailure FromStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => new(FailureCategory.Authentication, "服务密钥无效或已过期。", ((int)status).ToString()),
        HttpStatusCode.Forbidden => new(FailureCategory.Permission, "当前账户没有调用该服务的权限。", ((int)status).ToString()),
        HttpStatusCode.TooManyRequests => new(FailureCategory.RateLimit, "服务限流或余额／配额不足，请稍后手动重试。", ((int)status).ToString()),
        HttpStatusCode.NotFound => new(FailureCategory.InvalidModel, "服务路径或模型不存在，请检查配置。", ((int)status).ToString()),
        HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnprocessableEntity
            => new(FailureCategory.InvalidPayload, "服务不接受此请求，请检查模型能力、请求大小和参数。", ((int)status).ToString()),
        _ => new(FailureCategory.Transport, "服务请求失败，请稍后手动重试。", ((int)status).ToString())
    };
}